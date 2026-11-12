"""Generates the Hubble-style optical tube meshes for KSA.

Run headless:
  blender --background --python make_hubble_tube.py -- <output_dir>

Produces (in <output_dir>, default ./build):
  HubbleTube_MeshAtlas.glb          - subpart meshes (HubbleTubeBody, HubbleTubeDoor,
                                      plus *_VM view meshes the editor raycasts)
  HubbleTube_Prefab_MainTube.glb    - part prefab: subpart instances, _connector*
                                      mount points and _ColPrim_* colliders

Conventions (KSA GlbImport pipeline, see tools/PartBundler and parts/hubble/README.md):
  * Game coordinates are stored directly in the GLB: +X forward, +Z up
    (export_yup=False; verified against Content/Core mesh atlases).
  * Mesh atlas nodes must be at identity with transforms applied to vertices.
  * Connector empties are named _connector1..N; +X faces outward, uniform
    scale is the connector size class (1 ~ 1 m parts).
  * _ColPrim_Cylinder empties: local Y axis = cylinder axis, scale X/Z =
    diameter, scale Y = length. _ColPrim_Box: scale = edge lengths.

Dimensions are full-scale Hubble Space Telescope from NASA references:
13.2 m overall, 4.2 m aft shroud diameter, 3.0 m light shield diameter.
The aperture door outline (flat hinge edge, straight sides, 1.15 m corner
arcs meeting at a soft spade tip, 3.18 x 3.10 m) was measured from NASA's
exploded GLB model (mesh "-ADOOR", inch units).
"""

import math
import sys

import bpy

SEGMENTS = 64

# Texture V bands (also used by make_textures.py - keep in sync)
#   aft shroud   0.000-0.180
#   equipment    0.180-0.330
#   forward      0.330-0.580
#   light shield 0.580-0.880
#   door         0.880-0.940
#   rim/metal    0.940-0.952
#   interior     0.952-0.986
#   mirror       0.986-1.000

# Body lathe profile: (x, radius, v, connect_to_previous)
# The tube is hollow from the aperture back to the dished primary mirror,
# like the real telescope's optical cavity.
BODY_PROFILE = [
    (-1.35, 0.00, 0.999, True),  # primary mirror center (dished aft)
    (-1.00, 1.40, 0.988, True),  # primary mirror edge
    (-1.00, 1.44, 0.984, True),  # mirror mount ring
    (6.60, 1.44, 0.954, True),   # interior baffle wall to the front lip
    (6.60, 1.44, 0.951, False),  # front lip (UV band split)
    (6.60, 1.52, 0.941, True),   # front rim
    (6.60, 1.52, 0.879, False),  # light shield front (UV band split)
    (2.70, 1.52, 0.582, True),   # light shield aft
    (2.55, 1.60, 0.571, True),   # collar to forward shell
    (-0.80, 1.60, 0.331, True),  # forward shell aft
    (-2.30, 2.10, 0.181, True),  # ramp under the equipment collar
    (-6.35, 2.10, 0.030, True),  # aft shroud aft
    (-6.60, 1.75, 0.012, True),  # aft chamfer
    (-6.60, 0.00, 0.000, True),  # aft cap center
]

# Secondary mirror assembly: barrel on the optical axis, aft face is the
# mirror (4.91 m forward of the primary, per the OTA diagram).
SECONDARY_HUB = [
    (4.15, 0.00, 0.944, True),   # front cap center
    (4.15, 0.15, 0.946, True),   # front edge
    (3.75, 0.15, 0.950, True),   # barrel aft
    (3.75, 0.00, 0.995, True),   # aft cap = secondary mirror face
]
SECONDARY_SEGMENTS = 24

# Spider: four vanes holding the secondary hub, spanning hub (r 0.14) to the
# interior wall (r 1.46).
SPIDER_VANES = [
    # (center, size)
    ((3.95, 0.80, 0.0), (0.16, 1.34, 0.05)),
    ((3.95, -0.80, 0.0), (0.16, 1.34, 0.05)),
    ((3.95, 0.0, 0.80), (0.16, 0.05, 1.34)),
    ((3.95, 0.0, -0.80), (0.16, 0.05, 1.34)),
]
SPIDER_UV = (0.15, 0.942, 0.21, 0.950)  # rim/metal band rect

# Faceted SSM equipment section: a 12-sided collar proud of the hull,
# matching the polygonal bay ring on the real telescope.
EQUIPMENT_COLLAR = [
    (-0.78, 1.62, 0.320, True),
    (-0.78, 2.19, 0.305, True),
    (-2.32, 2.19, 0.195, True),
    (-2.32, 1.62, 0.182, True),
]
EQUIPMENT_SEGMENTS = 12

# Aperture door (spade shape, measured from NASA "-ADOOR" mesh):
# p = distance from hinge edge, q = across the door.
DOOR_HALF_WIDTH = 1.55
DOOR_STRAIGHT = 2.03      # straight-sided length from the hinge edge
DOOR_CORNER_RADIUS = 1.15
DOOR_CORNER_OFFSET = 0.40  # corner arc centers at q = +/- this
DOOR_THICKNESS = 0.10
DOOR_V_BAND = (0.882, 0.938)

# Door placement: hinged at the top of the front rim, opened 155 degrees so
# it stands above the aperture tilted slightly forward, like the deployed
# telescope.
DOOR_CLOSED_X = 6.68
DOOR_HINGE = (6.68, 0.0, 1.56)
DOOR_OPEN_ANGLE = math.radians(-155.0)

# Hinge brackets connecting the door's flat edge to the light shield rim.
BRACKET_SIZE = (0.26, 0.12, 0.26)
BRACKET_CENTERS = [(6.60, 0.52, 1.47), (6.60, -0.52, 1.47)]
BRACKET_UV = (0.05, 0.942, 0.11, 0.950)  # rim/metal band rect

# Aperture door animation: closed at time 0 (the part's default/retracted
# state), swinging open to the modeled deployed pose. ~8 s matches the feel
# of the stock solar panel deploy (9.5 s for a larger sweep).
DOOR_ANIM_SECONDS = 8.0
DOOR_ANIM_FPS = 24

CONNECTORS = [
    # (name, location, rotation_euler_xyz, uniform_scale)
    ("_connector1", (6.60, 0.0, 0.0), (0.0, 0.0, 0.0), 4.0),                    # fore axial
    ("_connector2", (-6.60, 0.0, 0.0), (0.0, 0.0, math.pi), 4.0),               # aft axial
    ("_connector3", (1.00, 1.60, 0.0), (0.0, 0.0, math.pi / 2), 1.0),           # solar panel +Y
    ("_connector4", (1.00, -1.60, 0.0), (0.0, 0.0, -math.pi / 2), 1.0),         # solar panel -Y
    ("_connector5", (-0.30, 0.0, 1.60), (0.0, -math.pi / 2, 0.0), 1.0),         # antenna +Z
    ("_connector6", (-0.30, 0.0, -1.60), (0.0, math.pi / 2, 0.0), 1.0),         # antenna -Z
]


def door_direction():
    """World direction of increasing p (hinge to tip) for the open door."""
    c, s = math.cos(DOOR_OPEN_ANGLE), math.sin(DOOR_OPEN_ANGLE)
    # closed p direction is -Z; rotate about Y through the hinge
    return (-s, 0.0, -c)


_DOOR_DIR = door_direction()
_DOOR_LENGTH = DOOR_STRAIGHT + math.sqrt(DOOR_CORNER_RADIUS ** 2 - DOOR_CORNER_OFFSET ** 2)
_DOOR_MID = _DOOR_LENGTH / 2

COLLIDERS = [
    # (name, location, rotation_euler_xyz, scale)
    ("_ColPrim_Cylinder1", (4.575, 0.0, 0.0), (0.0, 0.0, math.pi / 2), (3.04, 4.05, 3.04)),
    ("_ColPrim_Cylinder2", (0.875, 0.0, 0.0), (0.0, 0.0, math.pi / 2), (3.20, 3.35, 3.20)),
    ("_ColPrim_Cylinder3", (-3.70, 0.0, 0.0), (0.0, 0.0, math.pi / 2), (4.38, 5.80, 4.38)),
]

# The door plate collider lives on the door subpart (atlas child node) so it
# follows the open/close animation instead of staying at the open pose.
DOOR_COLLIDER = (
    "_ColPrim_Box1",
    (DOOR_HINGE[0] + _DOOR_MID * _DOOR_DIR[0], 0.0, DOOR_HINGE[2] + _DOOR_MID * _DOOR_DIR[2]),
    (0.0, DOOR_OPEN_ANGLE, 0.0),
    (DOOR_THICKNESS, 2 * DOOR_HALF_WIDTH, _DOOR_LENGTH),
)


def lathe(profile, transform=None, segments=SEGMENTS):
    """Revolves a profile of (x, radius, v, connect) rows around the X axis.

    Returns (verts, faces, face_uvs) where face_uvs holds one (u, v) per
    face corner, in the same order as the face vertex indices.
    """
    verts = []
    rows = []  # per profile row: ("ring", [vert indices]) or ("center", index)
    for x, r, v, _connect in profile:
        if r <= 1e-9:
            idx = len(verts)
            verts.append((x, 0.0, 0.0))
            rows.append(("center", idx, v))
        else:
            indices = []
            for j in range(segments + 1):
                theta = 2.0 * math.pi * j / segments
                indices.append(len(verts))
                verts.append((x, r * math.cos(theta), r * math.sin(theta)))
            rows.append(("ring", indices, v))

    if transform is not None:
        verts = [transform(p) for p in verts]

    faces = []
    face_uvs = []

    def u_of(j):
        return j / segments

    for i in range(len(profile) - 1):
        if not profile[i + 1][3]:
            continue  # UV band split: coincident rings, no faces between
        kind_a, data_a, v_a = rows[i]
        kind_b, data_b, v_b = rows[i + 1]
        if kind_a == "center" and kind_b == "ring":
            for j in range(segments):
                faces.append((data_a, data_b[j], data_b[j + 1]))
                face_uvs.append(((u_of(j) + u_of(j + 1)) / 2, v_a,
                                 u_of(j), v_b, u_of(j + 1), v_b))
        elif kind_a == "ring" and kind_b == "center":
            for j in range(segments):
                faces.append((data_a[j], data_b, data_a[j + 1]))
                face_uvs.append((u_of(j), v_a,
                                 (u_of(j) + u_of(j + 1)) / 2, v_b,
                                 u_of(j + 1), v_a))
        elif kind_a == "ring" and kind_b == "ring":
            for j in range(segments):
                faces.append((data_a[j], data_b[j], data_b[j + 1], data_a[j + 1]))
                face_uvs.append((u_of(j), v_a, u_of(j), v_b,
                                 u_of(j + 1), v_b, u_of(j + 1), v_a))
    return ensure_outward(verts, faces, face_uvs)


def spade_outline(arc_samples=14):
    """Door outline as (p, q) points, counterclockwise from the hinge edge."""
    t_end = math.acos(-DOOR_CORNER_OFFSET / DOOR_CORNER_RADIUS)
    pts = [(0.0, -DOOR_HALF_WIDTH), (DOOR_STRAIGHT, -DOOR_HALF_WIDTH)]
    for i in range(1, arc_samples + 1):  # -q corner arc to the tip
        t = t_end * i / arc_samples
        pts.append((DOOR_STRAIGHT + DOOR_CORNER_RADIUS * math.sin(t),
                    -DOOR_CORNER_OFFSET - DOOR_CORNER_RADIUS * math.cos(t)))
    for i in range(arc_samples - 1, 0, -1):  # +q corner arc back down
        t = t_end * i / arc_samples
        pts.append((DOOR_STRAIGHT + DOOR_CORNER_RADIUS * math.sin(t),
                    DOOR_CORNER_OFFSET + DOOR_CORNER_RADIUS * math.cos(t)))
    pts.append((DOOR_STRAIGHT, DOOR_HALF_WIDTH))
    pts.append((0.0, DOOR_HALF_WIDTH))
    return pts


def door_plate(transform, arc_samples=14):
    """Builds the spade-shaped door plate with front/back faces and side wall."""
    outline = spade_outline(arc_samples)
    n = len(outline)
    v_lo, v_hi = DOOR_V_BAND
    p_max = DOOR_STRAIGHT + DOOR_CORNER_RADIUS + 0.1

    def uv(p, q):
        return (0.5 + q / (2 * DOOR_HALF_WIDTH),
                v_hi - (p / p_max) * (v_hi - v_lo))

    half = DOOR_THICKNESS / 2
    verts = []
    for lx in (half, -half):
        for p, q in outline:
            verts.append(transform((lx, q, p)))
    centroid_p = sum(p for p, _ in outline) / n
    front_c = len(verts)
    verts.append(transform((half, 0.0, centroid_p)))
    back_c = len(verts)
    verts.append(transform((-half, 0.0, centroid_p)))

    faces = []
    face_uvs = []
    c_uv = uv(centroid_p, 0.0)
    for i in range(n):  # front fan (+lx, toward +X when closed)
        j = (i + 1) % n
        faces.append((front_c, i, j))
        face_uvs.append((*c_uv, *uv(*outline[i]), *uv(*outline[j])))
    for i in range(n):  # back fan
        j = (i + 1) % n
        faces.append((back_c, n + j, n + i))
        face_uvs.append((*c_uv, *uv(*outline[j]), *uv(*outline[i])))
    for i in range(n):  # side wall; back edge UVs offset to avoid a
        j = (i + 1) % n  # zero-area (degenerate-tangent) mapping
        faces.append((i, n + i, n + j, j))
        ui, vi = uv(*outline[i])
        uj, vj = uv(*outline[j])
        shift = 0.004
        face_uvs.append((ui, vi, ui, vi - shift, uj, vj - shift, uj, vj))
    return ensure_outward(verts, faces, face_uvs)


def box(center, size, uv_rect):
    """Axis-aligned box; every face maps across uv_rect (u0, v0, u1, v1).

    Degenerate (zero-area) UVs break the shader's tangent basis and cause
    flickering speckle, so each face must span a real texture area.
    """
    cx, cy, cz = center
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    verts = [(cx + sx * hx, cy + sy * hy, cz + sz * hz)
             for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
    faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1),
             (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    u0, v0, u1, v1 = uv_rect
    face_uvs = [(u0, v0, u0, v1, u1, v1, u1, v0)] * 6
    return ensure_outward(verts, faces, face_uvs)


def ensure_outward(verts, faces, face_uvs):
    """Orients faces so their normals point away from the centroid."""
    cx = sum(v[0] for v in verts) / len(verts)
    cy = sum(v[1] for v in verts) / len(verts)
    cz = sum(v[2] for v in verts) / len(verts)
    flux = 0.0
    for face in faces:
        # Newell normal and face center
        nx = ny = nz = 0.0
        fx = fy = fz = 0.0
        for i in range(len(face)):
            x0, y0, z0 = verts[face[i]]
            x1, y1, z1 = verts[face[(i + 1) % len(face)]]
            nx += (y0 - y1) * (z0 + z1)
            ny += (z0 - z1) * (x0 + x1)
            nz += (x0 - x1) * (y0 + y1)
            fx += x0
            fy += y0
            fz += z0
        k = len(face)
        flux += nx * (fx / k - cx) + ny * (fy / k - cy) + nz * (fz / k - cz)
    if flux < 0.0:
        faces = [tuple(reversed(f)) for f in faces]
        face_uvs = [sum(reversed([uvs[i:i + 2] for i in range(0, len(uvs), 2)]), ())
                    for uvs in face_uvs]
    return verts, faces, face_uvs


def merge(*pieces):
    verts = []
    faces = []
    face_uvs = []
    for pv, pf, puv in pieces:
        offset = len(verts)
        verts.extend(pv)
        faces.extend(tuple(i + offset for i in f) for f in pf)
        face_uvs.extend(puv)
    return verts, faces, face_uvs


def make_object(name, geometry):
    verts, faces, face_uvs = geometry
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.validate()

    uv_layer = mesh.uv_layers.new(name="UVMap")
    loop_i = 0
    for face_i, poly in enumerate(mesh.polygons):
        uvs = face_uvs[face_i]
        for corner in range(poly.loop_total):
            uv_layer.data[loop_i].uv = (uvs[corner * 2], uvs[corner * 2 + 1])
            loop_i += 1

    for poly in mesh.polygons:
        poly.use_smooth = True
    # 25 degrees: keeps 64-segment cylinders smooth but makes the 30-degree
    # equipment collar facets (and plate edges) crisp.
    mesh.use_auto_smooth = True
    mesh.auto_smooth_angle = math.radians(25.0)

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def door_transform(p):
    """Door local (thickness, across, from-hinge) -> world, opened at the hinge."""
    lx, ly, lz = p
    wx, wy, wz = lx + DOOR_CLOSED_X, ly, DOOR_HINGE[2] - lz
    hx, _hy, hz = DOOR_HINGE
    dx, dz = wx - hx, wz - hz
    c, s = math.cos(DOOR_OPEN_ANGLE), math.sin(DOOR_OPEN_ANGLE)
    return (hx + dx * c + dz * s, wy, hz - dx * s + dz * c)


def make_empty(name, location, rotation, scale, parent=None):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "ARROWS"
    obj.empty_display_size = 0.5
    obj.parent = parent
    obj.location = location
    obj.rotation_euler = rotation
    obj.scale = scale if isinstance(scale, tuple) else (scale, scale, scale)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def export_glb(filepath, objects, animations=False):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=filepath,
        export_format="GLB",
        use_selection=True,
        export_yup=False,  # KSA stores game coordinates (+X fwd, +Z up) directly
        export_apply=True,
        export_normals=True,
        export_texcoords=True,
        export_materials="NONE",
        export_animations=animations,
    )
    print("EXPORTED:", filepath)


def body_geometry(segments):
    pieces = [
        lathe(BODY_PROFILE, segments=segments),
        lathe(EQUIPMENT_COLLAR, segments=EQUIPMENT_SEGMENTS),
        lathe(SECONDARY_HUB, segments=SECONDARY_SEGMENTS),
        box(BRACKET_CENTERS[0], BRACKET_SIZE, BRACKET_UV),
        box(BRACKET_CENTERS[1], BRACKET_SIZE, BRACKET_UV),
    ]
    pieces += [box(center, size, SPIDER_UV) for center, size in SPIDER_VANES]
    return merge(*pieces)


def export_door_animation(out_dir):
    """Builds the aperture-door open/close animation GLB.

    KSA's KeyframeAnimationData maps named, non-animated leaf nodes to
    subpart instance keys; their animated ancestors act as pivots. Time 0 is
    the retracted (closed) pose, the final keyframe the deployed (open) pose
    matching the geometry baked into the prefab.
    """
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    scene = bpy.context.scene
    scene.render.fps = DOOR_ANIM_FPS
    scene.frame_start = 1
    scene.frame_end = 1 + int(DOOR_ANIM_SECONDS * DOOR_ANIM_FPS)

    pivot = bpy.data.objects.new("DoorPivot", None)
    pivot.location = DOOR_HINGE
    pivot.rotation_mode = "XYZ"
    scene.collection.objects.link(pivot)

    door = bpy.data.objects.new("HubbleTubeDoor1", None)
    door.parent = pivot
    door.location = (-DOOR_HINGE[0], -DOOR_HINGE[1], -DOOR_HINGE[2])
    scene.collection.objects.link(door)

    # Closed pose rotates the open-baked geometry back over the aperture.
    pivot.rotation_euler = (0.0, -DOOR_OPEN_ANGLE, 0.0)
    pivot.keyframe_insert(data_path="rotation_euler", frame=scene.frame_start)
    pivot.rotation_euler = (0.0, 0.0, 0.0)
    pivot.keyframe_insert(data_path="rotation_euler", frame=scene.frame_end)

    export_glb(f"{out_dir}/HubbleTube_Prefab_MainTube_Anim.glb", [pivot, door], animations=True)


def main():
    argv = sys.argv
    out_dir = argv[argv.index("--") + 1] if "--" in argv else "build"

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    body = make_object("HubbleTubeBody", body_geometry(SEGMENTS))
    door = make_object("HubbleTubeDoor", door_plate(door_transform))
    # Door collider as a child of the door subpart so it animates with it
    door_collider = make_empty(*DOOR_COLLIDER[:3], DOOR_COLLIDER[3], parent=door)

    # _VM view meshes: lower-poly watertight copies the editor raycasts for
    # hover, face snapping, and connector targeting. Without them the part
    # cannot be clicked or snapped to (Part.RayCastEgoSubPart bails out).
    body_vm = make_object("HubbleTubeBody_VM", body_geometry(24))
    door_vm = make_object("HubbleTubeDoor_VM", door_plate(door_transform, arc_samples=6))

    export_glb(f"{out_dir}/HubbleTube_MeshAtlas.glb", [body, door, door_collider, body_vm, door_vm])

    # The prefab instances the atlas subparts: node + mesh names carry a
    # trailing number that the bundler strips to find the subpart id.
    for obj in (body, door):
        obj.name += "1"
        obj.data.name = obj.name

    extras = [make_empty(*c[:3], c[3]) for c in CONNECTORS]
    extras += [make_empty(*c[:3], c[3]) for c in COLLIDERS]

    export_glb(f"{out_dir}/HubbleTube_Prefab_MainTube.glb", [body, door] + extras)

    export_door_animation(out_dir)


main()

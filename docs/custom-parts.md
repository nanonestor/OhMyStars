# Making custom KSA parts (field notes)

Everything below was learned building the Hubble tube part (`parts/hubble`,
game version 2026.10.x) by reading the decompiled game code and the shipped
core content. The community wiki's parts guide (old.ksamodding.wiki) predates
the current schema — trust the game's own code and core XML over it.

## The big picture

A part needs four things, all shippable from a mod folder (no core-file edits):

1. **Mesh atlas GLB** (`<Stem>_MeshAtlas.glb`) — one named mesh per subpart
   **plus a `<name>_VM` view mesh per subpart** (see pitfalls).
2. **Prefab GLB** (`<Stem>_Prefab*.glb`) — subpart instances, `_connector*`
   empties (attachment nodes), `_ColPrim_*` collider empties.
3. **Assets XML** — meshes/textures/material/subparts/part. Generate it with
   `tools\PartBundler`, which calls the game's own `KSA.GlbImport` pipeline
   from the installed `KSA.dll`, so the schema always matches the installed
   game. Never hand-maintain this file; regenerate after game updates.
4. **GameData XML** (hand-written) — editor tags, diameters, mass, display
   name, patched onto the part with `Modify="Patch"`, mirroring the core
   `Core*GameData.xml` files.

Register both XML files in `mod.toml` (`assets = [...]`, Assets before
GameData), with meshes under `Meshes\` and textures under `Textures\` in the
mod folder (`Path` attributes resolve relative to the mod directory —
`FileReference.ModPath` → `Mod.GetPath`).

## Non-negotiable requirements (each silently breaks the part if missed)

| Requirement | Failure mode if missed |
|---|---|
| GLB stores game coords directly: +X forward, +Z up; Blender export with `export_yup=False` | part rotated/mirrored in game |
| Atlas nodes at identity, transforms applied to vertices | subpart origins offset; bundler warns |
| `POSITION`, `NORMAL`, `TEXCOORD_0` on every mesh | crash or invisible mesh |
| One primitive (one material) per mesh | only the first primitive renders |
| `<name>_VM` view mesh per subpart in the atlas | part loads and renders but cannot be hovered, selected, face-snapped, or connector-snapped — `Part.RayCastEgoSubPart` returns false without a `MeshViewModule`. The editor raycasts view meshes, not render meshes |
| Colliders | no physics hull; `PartFailure` treats PartModel+MeshView+Collider as the definition of a functional part |
| All three PBR textures in the material | crash or black part |

## Prefab GLB node conventions (from `KSA.GlbImport.PartAssetBundler`)

* **Subpart instances**: node + mesh named `<SubPartId><number>`
  (`HubbleTubeBody1`); the bundler strips the trailing number to find the
  subpart (`InstanceOf`). Mesh data must be present (used for hull colliders).
* **Connectors**: empties named `_connector<N>`; +X points outward (away from
  the part); uniform scale = size class (~diameter in metres; stock 1 m parts
  use scale 1). A connector on the X centerline facing ±X is "axial" and its
  Z axis should align with part Z or attached parts will be rolled (bundler
  warns). Two connectors can snap when their +X axes face each other
  (`dot <= 0` — perpendicular still counts) and both are unconnected.
* **Colliders**: empties named `_ColPrim_Box` / `_Sphere` / `_Capsule` /
  `_Cylinder<N>`; node scale encodes dimensions (Box: edge lengths; Cylinder:
  local Y = axis length, X/Z = diameter, X must equal Z); node
  position/rotation place them. `_ColMesh*` nodes (mesh name = node name,
  single primitive) become convex hulls and are copied into an auto-generated
  `<Stem>_CollisionAtlas.glb`.
* **Gimbals**: child empty named `_pivot*` under a subpart instance.
* **Subpart flags**: child nodes named `_glass`, `_raytrace`,
  `_raytraceShadowProxy` toggle render modes.

## Textures (from `PartInputSet` + `MaterialSet.glsl`)

* Naming: `<Stem>_<anything>Diffuse.png`, `*Normal.png`, `*PBR.png` (also
  supported: `*Emissive.png`, `*Alpha.png`, `*TFI.png`). The stem is the text
  before the first underscore and groups the whole input set.
* Size the texture for the part: stock uses 2048x2048 atlases for 1-3 m
  parts. A 1024 map on a 13 m part reads as blocky "big pixels" up close.
* **Every face needs non-degenerate UVs.** Mapping a face's corners to a
  single texture point (zero UV area) breaks the shader's tangent basis and
  shows up as fast transient flashing/speckle on those faces. Map even plain
  single-color faces across a small rectangle of the texture.
* PBR map channels: **R = ambient occlusion, G = roughness, B = metallic**
  (the wiki's "RoughMetalAo" order is outdated). Flat normal = (128,128,255).
* **Foil needs a crinkle normal map.** The stock probe-core foil
  (`CoreSurfacesB_Subpart_TrussEFoilA`) is glossy (roughness ~35-100,
  metallic 255) with a strong wrinkle normal map that scatters the
  reflection - that combination, not roughness alone, is what reads as foil.
  Normals are standard tangent-space RGB (`rgb*2-1`, G flipped by the shader;
  flat = 128,128,255). Generate from a height field (soft noise + abs-folded
  "crease" ridges), keep it seamless across the U wrap, and bake the same
  height field into the diffuse shading and roughness for coherence.
* **Keep roughness high on plain metal or the part glints harshly.** Decoded
  stock PBR maps measure: CoreCommandB roughness median ~193, CoreStructuralA
  ~148 - even on fully metallic (B=255) surfaces. Roughness below ~120
  without a strong normal map gives mirror-like sun hotspots that no stock
  part exhibits.
* Stock KTX2 textures are BC7 + Zstandard and can be decoded for reference
  with Python (`pip install zstandard texture2ddecoder`): parse the KTX2
  header (level index at byte 80: u64 offset/length/uncompressed per level),
  zstd-decompress level 0, then `texture2ddecoder.decode_bc7(data, w, h)`
  (returns BGRA). Use them for palette/value reference only - do not copy
  game assets into the repo.
* Core content uses `.ktx2`; plain PNG loads fine for mods. PartBundler
  rewrites the generated `.ktx2` references to `.png` automatically.

## GameData patterns (mirror core `Core*GameData.xml`)

```xml
<Part Id="<PartId>" Modify="Patch" DisplayName="Readable Name">
  <EditorTags Modify="Extend"><EditorTag Ref="Structural" /></EditorTags>
  <Diameters Modify="Extend"><Diameter M="4.2" /></Diameters>
  <InertMasses Modify="Extend">
    <CustomMass>
      <Mass Kg="9000" />
      <MassSpecificInertia Ixx="3.2" Iyy="16.1" Izz="16.1" />
    </CustomMass>
  </InertMasses>
</Part>
```

* Editor categories (`CoreEditorTagsGameData.xml`): Booster, Capsules,
  Engines, RCS, Fuel Tanks, Electrical, Coupling, Structural, Panels,
  Landing, Cargo, Lights, Life Support, Interstellar; plus behavior tags
  `Hidden`, `NoFaceSnapping`, `Radial`, `Interstage`. `RootPartWhitelist`
  tags + at least one connector make a part placeable first.
* Connector extras via patch: `<Capabilities>BulkFluid</Capabilities>`
  (fluid crossfeed), `<Flags>Internal</Flags>` — capabilities are not needed
  for plain structural snapping.
* Other modules seen in core GameData: `Tank`, `Battery`, `Habitat`,
  `RadiationShield`, `Light`, `Control`, `DockingPort`, `SolarPanel`,
  `Thruster` — copy shapes from core files and run
  `PartBundler --validate` (deserializes with the game's serializer and
  reports elements/attributes the game would silently ignore).
* Switchable power draw: `<PowerConsumer LightSwitch="true"><Consumed W="..."/>
  </PowerConsumer>` — the game's light parts toggle it by setting the
  `PowerConsumer.LightIsActive` field; the stock update loop handles the
  battery drain, auto-cutoff, and save/restore. `IsSwitchedOn()` reports the
  live state; per-tree totals come from `vehicle.Parts.Batteries` /
  `.PowerConsumers` state lists via `GetState(module)`.
* Part cameras: lease a stock secondary viewport like the docking camera
  does — `ViewportRegistry.TryClaimSecondaryViewport(owner, out vp)`, then
  `SetVisible`, `RequestResize`, `SetCameraMode(CameraMode.Fixed)`,
  `FixedController.Camera.SetFollow(vehicle)`, and point it along a part
  connector with `FixedController.DockingConnector`;
  `GetCamera().SetFieldOfView(deg)` zooms. Release with
  `ViewportRegistry.ReleaseSecondaryViewport(owner)`.
* Deploy/retract animations (solar-panel style): declare
  `<KeyframeAnimation Id="X_Anim" Path="Animations/X_Anim.glb"/>` plus a
  `<KeyframeAnimationModule Key="..." ShowDeployRetract="true">
  <KeyframeAnimation Ref="X_Anim"/></KeyframeAnimationModule>` component.
  The animation GLB (from `KeyframeAnimationData`): animated nodes act as
  pivots; their non-animated, *named* descendant nodes map to subpart
  instance keys (e.g. `HubbleTubeDoor1`) and carry the subpart's local TRS
  under the pivot. Time 0 = retracted (also the spawn default), last
  keyframe = deployed; keyframe times are real seconds (stock solar panel:
  ~9.5 s). Drive from code with `module.TimeGoal = module.Shared.Duration`
  (deploy) or `0` (retract); read progress from the module state's
  `DeploymentState`. Put colliders for animated subparts on the subpart
  (atlas `_Col*` child), not the part, so they move with the animation.

## Workflow (see parts/hubble/README.md for concrete commands)

1. Author geometry procedurally in headless Blender (`blender --background
   --python script.py`) or import/clean an existing model. Blender 3.4 can
   decode Draco-compressed GLBs (e.g. NASA's official models — their CAD
   exports use inch units; the exploded model's mesh names like `-ADOOR`,
   `ADHINGE`, `-HGADISH`, `-SHROUD` are useful for measuring real shapes).
2. Generate textures (any tool; PIL works).
3. `dotnet run --project tools\PartBundler -- <inputDir>` → Assets XML.
4. Hand-write/extend the GameData XML; `PartBundler --validate` both files.
5. `dotnet build src` deploys the mod; check
   `Documents\My Games\Kitten Space Agency\Logs\` for load errors.

## Troubleshooting

* **Part invisible**: missing vertex attributes, multiple primitives, or
  scene node not referencing the mesh.
* **Part loads but can't be clicked/snapped to**: missing `_VM` view meshes
  (the silent killer — nothing is logged).
* **Attached parts roll unexpectedly**: axial connector Z axis not aligned
  with part Z.
* **XML ignored silently**: wrong element/attribute names — the game's
  XmlSerializer skips unknowns without logging; `PartBundler --validate`
  surfaces them.
* **Flickering/speckling faces**: zero-area UVs on those faces (degenerate
  tangent basis); give every face a real UV rectangle.
* **After a game update**: rerun PartBundler (regenerates current-schema
  XML); if GameData breaks, re-check core GameData files for renamed
  elements, and `tools\Refresh-Decompiled.ps1` to refresh the code reference.

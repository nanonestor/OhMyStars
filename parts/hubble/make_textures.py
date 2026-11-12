"""Generates the PBR texture set for the Hubble tube part.

Run:  python make_textures.py [output_dir]

Produces 2048x2048 PNGs (KSA PBR map layout: R=AO, G=roughness, B=metallic,
verified against Content/Core/Shaders/Common/MaterialSet.glsl).

Section looks are matched to the SM4 on-orbit photo (HST-SM4.jpeg.webp):
  * aperture door     - brushed silver, same finish as the aft shroud
  * light shield +    - crinkled silver MLI foil with warm tape seams
    forward shell
  * equipment section - flat gray faceted bays
  * aft shroud        - smooth brushed silver, shinier than the bays
Roughness/metal ranges follow decoded stock textures (CoreStructuralA panel
trim: metal 255, roughness ~160 varying ~90-210) so nothing glints harshly.

Texture V bands must stay in sync with make_hubble_tube.py:
  aft shroud   0.000-0.180
  equipment    0.180-0.330
  forward      0.330-0.580
  light shield 0.580-0.880
  door         0.880-0.940
  rim/metal    0.940-0.952
  interior     0.952-0.986
  mirror       0.986-1.000
U runs around the tube circumference.
"""

import random
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw

SIZE = 2048
random.seed(20261110)  # deterministic layout
_RNG = np.random.default_rng(20261110)


def px(n):
    """Scales a length authored for a 1024 texture to the actual size."""
    return max(1, n * SIZE // 1024)


def band_rows(v_lo, v_hi):
    """Texture rows for a V band (row 0 = V 1.0)."""
    return int(round((1.0 - v_hi) * SIZE)), int(round((1.0 - v_lo) * SIZE))


BANDS = {
    "aft": band_rows(0.000, 0.180),
    "equipment": band_rows(0.180, 0.330),
    "forward": band_rows(0.330, 0.580),
    "shield": band_rows(0.580, 0.880),
    "door": band_rows(0.880, 0.940),
    "rim": band_rows(0.940, 0.952),
    "interior": band_rows(0.952, 0.986),
    "mirror": band_rows(0.986, 1.000),
}

FOIL_BANDS = ("shield", "forward")  # crinkled MLI
GOLD = (176, 152, 96)               # kapton tape / rail accents


def make_streaks():
    """Per-column brushed-metal offsets (-1..1), smooth with sparse scratches."""
    anchors = {}
    step = px(6)
    for x in range(0, SIZE + step, step):
        anchors[x] = random.uniform(-1.0, 1.0)
    out = []
    for x in range(SIZE):
        x0 = (x // step) * step
        t = (x - x0) / step
        a, b = anchors[x0], anchors[x0 + step]
        out.append(a + (b - a) * t)
    for _ in range(SIZE // 32):  # sparse, subtle scratch lines
        x = random.randrange(SIZE)
        out[x] = random.choice((-1.1, 1.1))
    return out


def noise_layer(w, h, cell):
    """Bilinear value noise in [0,1], seamless across the U (x) wrap."""
    gw, gh = max(2, w // cell), max(2, h // cell)
    g = _RNG.random((gh + 1, gw), dtype=np.float32)
    xs = np.linspace(0, gw, w, endpoint=False)
    ys = np.linspace(0, gh, h, endpoint=False)
    x0 = np.floor(xs).astype(int)
    y0 = np.floor(ys).astype(int)
    tx = (xs - x0).astype(np.float32)[None, :]
    ty = (ys - y0).astype(np.float32)[:, None]
    x1 = (x0 + 1) % gw  # wrap horizontally so the tube seam is invisible
    y1 = np.minimum(y0 + 1, gh)
    a = g[np.ix_(y0, x0)]
    b = g[np.ix_(y0, x1)]
    c = g[np.ix_(y1, x0)]
    d = g[np.ix_(y1, x1)]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def foil_height(rows, scale=1.0):
    """Crumpled-foil height field in [0,1]; scale sets the fold size."""
    w, h = SIZE, rows[1] - rows[0]

    def cell(n):
        return max(2, px(int(n * scale)))

    smooth = (0.35 * noise_layer(w, h, cell(20)) +
              0.35 * noise_layer(w, h, cell(8)) +
              0.30 * noise_layer(w, h, cell(3)))
    crease_src = 0.5 * noise_layer(w, h, cell(16)) + 0.5 * noise_layer(w, h, cell(6))
    ridged = 1.0 - np.abs(crease_src * 2.0 - 1.0)  # fold lines at mid contours
    height = 0.55 * smooth + 0.45 * ridged
    height -= height.min()
    height /= height.max() + 1e-9
    return height


def height_to_normal(height):
    """Tangent-space normal map image from a height field (shader: rgb*2-1)."""
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    # scale so steep crinkle slopes approach ~40 degrees, like stock foil
    strength = 0.8 / max(float(np.quantile(np.abs(dx), 0.9)), 1e-6)
    sx, sy = dx * strength, dy * strength
    nz = 1.0 / np.sqrt(sx * sx + sy * sy + 1.0)
    nx, ny = -sx * nz, -sy * nz
    rgb = np.stack([nx * 127.5 + 127.5, ny * 127.5 + 127.5, nz * 127.5 + 127.5], axis=-1)
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")


STREAKS = make_streaks()  # aft shroud + door
# Fine field: diffuse grain + roughness sparkle. Coarse field (~5x larger
# folds): the crinkle normal map.
FOIL_HEIGHT = {band: foil_height(BANDS[band]) for band in FOIL_BANDS}
FOIL_HEIGHT_COARSE = {band: foil_height(BANDS[band], scale=5.0) for band in FOIL_BANDS}


def fill(draw, rows, color):
    draw.rectangle([0, rows[0], SIZE - 1, rows[1] - 1], fill=color)


def hline(draw, y, color, width=None):
    draw.rectangle([0, y, SIZE - 1, y + (width or px(2)) - 1], fill=color)


def vline(draw, x, rows, color, width=None):
    draw.rectangle([x, rows[0], x + (width or px(2)) - 1, rows[1] - 1], fill=color)


def clamp(v):
    return max(0, min(255, int(v)))


def paste_offset(img, rows, base_color, offsets):
    """Fills a band with base_color +/- a centered grayscale offset image."""
    region = Image.new("RGB", offsets.size, base_color)
    region = ImageChops.add(region, offsets.convert("RGB"), scale=1.0, offset=-128)
    img.paste(region, (0, rows[0]))


def soft_mottle(img, rows, sigma):
    """Low-frequency tonal blotches that break up perfect streaks."""
    w, h = SIZE, rows[1] - rows[0]
    noise = Image.effect_noise((max(2, w // 40), max(2, h // 40)), sigma)
    noise = noise.resize((w, h), Image.BILINEAR).convert("RGB")
    region = img.crop((0, rows[0], SIZE, rows[1]))
    region = ImageChops.add(region, noise, scale=1.0, offset=-128)
    img.paste(region, (0, rows[0]))


def grain(img, rows, sigma):
    """Fine gaussian grain over a band."""
    w, h = SIZE, rows[1] - rows[0]
    noise = Image.effect_noise((w, h), sigma).convert("RGB")
    region = img.crop((0, rows[0], SIZE, rows[1]))
    region = ImageChops.add(region, noise, scale=1.0, offset=-128)
    img.paste(region, (0, rows[0]))


def make_diffuse(path):
    img = Image.new("RGB", (SIZE, SIZE))
    draw = ImageDraw.Draw(img)

    # Aft shroud: smooth brushed silver with faint, broken-up streaks
    rows = BANDS["aft"]
    for x in range(SIZE):
        d = STREAKS[x] * 4
        draw.rectangle([x, rows[0], x, rows[1] - 1],
                       fill=(clamp(170 + d), clamp(170 + d), clamp(173 + d)))
    soft_mottle(img, rows, 9)
    for i in range(1, 4):
        hline(draw, rows[0] + (rows[1] - rows[0]) * i // 4, (142, 142, 145))
    for i in range(16):
        vline(draw, i * SIZE // 16, rows, (148, 148, 151))
    for i in range(4):  # vents near the equipment end
        x = i * SIZE // 4 + SIZE // 8
        draw.rectangle([x - px(22), rows[0] + px(8), x + px(22), rows[0] + px(30)],
                       fill=(70, 72, 76))
    grain(img, rows, 4.0)

    # Equipment section: flat gray faceted bays (matte in the photo)
    rows = BANDS["equipment"]
    fill(draw, rows, (150, 150, 153))
    bay_w = SIZE // 12
    for i in range(12):
        x0 = i * bay_w
        tone = random.randint(-10, 12)
        base = (150 + tone, 150 + tone, 153 + tone)
        draw.rectangle([x0 + px(2), rows[0] + px(3), x0 + bay_w - px(3), rows[1] - px(4)],
                       fill=base)
        draw.rectangle([x0 + px(10), rows[0] + px(12), x0 + bay_w - px(11), rows[1] - px(13)],
                       outline=(112 + tone, 112 + tone, 116 + tone), width=px(2))
        draw.rectangle([x0 + px(12), rows[0] + px(14), x0 + bay_w - px(13), rows[0] + px(16)],
                       fill=(182 + tone, 182 + tone, 186 + tone))
        mid = (rows[0] + rows[1]) // 2
        draw.rectangle([x0 + bay_w // 2 - px(9), mid - px(3), x0 + bay_w // 2 + px(9), mid + px(3)],
                       fill=(104, 104, 108))
        for y in range(rows[0] + px(8), rows[1] - px(6), px(11)):
            for rx in (x0 + px(6), x0 + bay_w - px(7)):
                draw.rectangle([rx, y, rx + 1, y + 1], fill=(120, 120, 124))
    hline(draw, rows[0], (110, 110, 114))
    hline(draw, rows[1] - px(2), (110, 110, 114))

    # Light shield + forward shell: crinkled silver MLI foil with gold seams;
    # the crinkle shading is baked from the same height field as the normals
    for band in FOIL_BANDS:
        rows = BANDS[band]
        shading = Image.fromarray(
            np.clip((FOIL_HEIGHT[band] - 0.5) * 24 + 128, 0, 255).astype(np.uint8), "L")
        paste_offset(img, rows, (206, 207, 211), shading)
        seams = 12 if band == "forward" else 8
        for i in range(seams):
            vline(draw, i * SIZE // seams, rows, GOLD, px(2))
        if band == "shield":  # hoop ribs under the foil
            ribs = 10
            for i in range(1, ribs):
                y = rows[0] + (rows[1] - rows[0]) * i // ribs
                hline(draw, y - px(2), (214, 215, 219))
                hline(draw, y, (150, 151, 155))
        else:  # forward shell: foil blanket joint lines
            for i in range(1, 3):
                hline(draw, rows[0] + (rows[1] - rows[0]) * i // 3, GOLD)

    # Aperture door: same brushed silver as the aft shroud, with faint seams
    rows = BANDS["door"]
    for x in range(SIZE):
        d = STREAKS[x] * 4
        draw.rectangle([x, rows[0], x, rows[1] - 1],
                       fill=(clamp(170 + d), clamp(170 + d), clamp(173 + d)))
    soft_mottle(img, rows, 9)
    draw.rectangle([px(8), rows[0] + px(2), SIZE - px(9), rows[1] - px(3)],
                   outline=(142, 142, 145), width=px(3))
    for i in range(1, 4):
        hline(draw, rows[0] + (rows[1] - rows[0]) * i // 4, (150, 150, 153))
    vline(draw, SIZE // 2 - 1, rows, (150, 150, 153))
    grain(img, rows, 4.0)

    # Rim ring, hinge brackets, secondary hub and spider: plain metal gray
    fill(draw, BANDS["rim"], (96, 96, 100))

    # Interior: near-black baffle wall with faint stray-light baffle rings
    rows = BANDS["interior"]
    fill(draw, rows, (22, 22, 24))
    for y in range(rows[0] + px(3), rows[1], px(6)):
        hline(draw, y, (34, 34, 37), 2)

    # Primary/secondary mirror faces: deep blue-gray glass
    rows = BANDS["mirror"]
    fill(draw, rows, (46, 52, 62))
    hline(draw, (rows[0] + rows[1]) // 2, (62, 70, 84), px(3))

    img.save(path)
    print("wrote", path)


def make_normal(path):
    img = Image.new("RGB", (SIZE, SIZE), (128, 128, 255))
    # Crinkle normals are what make the foil read as foil: the surface is
    # glossy but the wrinkles scatter the reflection (like the stock
    # CoreSurfacesB TrussFoil used by the probe cores).
    for band in FOIL_BANDS:
        img.paste(height_to_normal(FOIL_HEIGHT_COARSE[band]), (0, BANDS[band][0]))
    img.save(path)
    print("wrote", path)


def make_pbr(path):
    """R=AO, G=roughness, B=metallic."""
    img = Image.new("RGB", (SIZE, SIZE))
    draw = ImageDraw.Draw(img)

    painted = (255, 205, 35)   # matte painted bays
    metal = (255, 160, 255)    # bare metal fittings
    dark = (235, 230, 5)       # interior baffles: rough, non-metallic
    mirror = (255, 115, 255)   # mirror glass: glossier but small area

    # Aft shroud and door: fully metallic, faint brushed roughness variation
    for key in ("aft", "door"):
        rows = BANDS[key]
        for x in range(SIZE):
            rough = clamp(150 - STREAKS[x] * 18)
            draw.rectangle([x, rows[0], x, rows[1] - 1], fill=(242, rough, 255))

    # Foil: bright silver overall - enough roughness to diffuse light across
    # the surface (pure gloss + metal just mirrors black sky), with the
    # crinkle normals adding the shifting foil shimmer
    for band in FOIL_BANDS:
        rows = BANDS[band]
        rough = np.clip(135 + (FOIL_HEIGHT[band] - 0.5) * 50, 105, 165).astype(np.uint8)
        h, w = rough.shape
        region = np.empty((h, w, 3), dtype=np.uint8)
        region[:, :, 0] = 247
        region[:, :, 1] = rough
        region[:, :, 2] = 235
        img.paste(Image.fromarray(region, "RGB"), (0, rows[0]))

    fill(draw, BANDS["equipment"], painted)
    fill(draw, BANDS["rim"], metal)
    fill(draw, BANDS["interior"], dark)
    fill(draw, BANDS["mirror"], mirror)

    # Slightly rougher, occluded seam lines on the equipment bays
    rows = BANDS["equipment"]
    for i in range(12):
        vline(draw, i * SIZE // 12, rows, (215, 230, 25), px(3))

    img.save(path)
    print("wrote", path)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "build"
    make_diffuse(f"{out}/HubbleTube_TextureAtlas_Diffuse.png")
    make_normal(f"{out}/HubbleTube_TextureAtlas_Normal.png")
    make_pbr(f"{out}/HubbleTube_TextureAtlas_PBR.png")


main()

"""Build an OhMyStars parallax superset star binary.

Output layout (little-endian):
  [game-compatible prefix]  int32 count, then count * 16-byte records
                            (float x, y, z unit direction ecliptic; byte size, r, g, b).
                            Copied verbatim from the reference .bin so the game and
                            any other loader see exactly the original star field.
  [OMSP section]            appended after the prefix; ignored by the game.
      char[4]  "OMSP"
      int32    version (1)
      int32    prefixCount   (== count above)
      int32    extraCount    (stars dimmer than the prefix cut, hidden until brightened)
      (prefixCount + extraCount) records of 20 bytes:
          float px, py, pz   ecliptic position in parsecs, Sun at origin
                             (for stars without a usable distance: unit direction)
          float absmag       absolute visual magnitude
          byte  r, g, b      base linear colour, before the dim-star 0.2 multiplier
          byte  flags        bit0 = has valid distance (can move with parallax)

The prefix record order is reproduced by replaying the original two-stage
PowerShell generator (just_output_raw.ps1 + convert_stars_raw.ps1) and verified
against the reference binary.
"""

from __future__ import annotations

import argparse
import csv
import math
import struct
import sys
from pathlib import Path

OBLIQUITY_DEG = -23.439281
MAG_CUT = 8.795
INVALID_DIST = 100000.0


def ps_round(v: float) -> int:
    # .NET Math.Round default (banker's rounding) == Python round().
    return int(round(v))


def clamp255(v: float) -> int:
    return max(0, min(255, ps_round(v)))


def to_linear(c: int) -> int:
    v = c / 255.0
    lin = v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return max(0, min(255, ps_round(lin * 255)))


def bv_to_linear_rgb(bv: float) -> tuple[int, int, int]:
    temperature = 4600 * (1 / (0.92 * bv + 1.7) + 1 / (0.92 * bv + 0.62))
    t = temperature / 100
    if t <= 66:
        r = 255
        g = clamp255(99.4708025861 * math.log(t) - 161.1195681661)
    else:
        r = clamp255(329.698727446 * math.pow(t - 60, -0.1332047592))
        g = clamp255(288.1221695283 * math.pow(t - 60, -0.0755148492))
    if t >= 66:
        b = 255
    elif t <= 19:
        b = 0
    else:
        b = clamp255(138.5177312231 * math.log(t - 10) - 305.0447927307)
    return to_linear(r), to_linear(g), to_linear(b)


def size_from_mag(mag: float) -> int:
    bright_flux = 10 ** (-0.4 * -1.6)
    dim_flux = 10 ** (-0.4 * 6.8)
    flux = 10 ** (-0.4 * mag)
    norm = max(0.0, min(1.0, (flux - dim_flux) / (bright_flux - dim_flux)))
    return ps_round(10.0 + 245.0 * math.pow(norm, 0.45))


def try_float(s: str | None) -> float | None:
    if s is None:
        return None
    s = s.strip()
    if not s:
        return None
    try:
        return float(s)
    except ValueError:
        return None


def f32(v: float) -> float:
    return struct.unpack("<f", struct.pack("<f", v))[0]


def g15(v: float) -> float:
    # Windows PowerShell stringifies doubles with 15 significant digits.
    return float(f"{v:.15g}")


def stage1(csv_path: Path):
    """Replays just_output_raw.ps1. Yields dicts in output order."""
    a = OBLIQUITY_DEG * math.pi / 180
    ca, sa = math.cos(a), math.sin(a)
    with csv_path.open(newline="", encoding="utf-8") as fh:
        reader = csv.DictReader(fh)
        first = True
        for row in reader:
            if first:  # Select-Object -Skip 1 (Sol)
                first = False
                continue
            ra = try_float(row["ra"])
            dec = try_float(row["dec"])
            mag_str = row["mag"]
            mag = try_float(mag_str)
            bv = try_float(row["ci"])
            if ra is None or dec is None or mag is None or bv is None:
                continue
            ra_rad = ra * math.pi / 12.0
            dec_rad = dec * math.pi / 180.0
            x = math.cos(dec_rad) * math.cos(ra_rad)
            y = math.cos(dec_rad) * math.sin(ra_rad)
            z = math.sin(dec_rad)
            if x == 0 and y == 0 and z == 0:
                continue
            if mag == 0 and bv == 0:
                continue
            yr = y * ca - z * sa
            zr = y * sa + z * ca
            yield {
                "row": row,
                "dir": (x, yr, zr),
                "rgb": bv_to_linear_rgb(bv),
                "mag": float(f"{mag:.15g}"),
            }


def distance_info(row) -> tuple[bool, float, float]:
    dist = try_float(row.get("dist"))
    absmag = try_float(row.get("absmag"))
    valid = dist is not None and 0 < dist < INVALID_DIST and absmag is not None
    return valid, dist or 0.0, absmag if absmag is not None else 0.0


def main() -> int:
    here = Path(__file__).resolve().parent
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--csv", type=Path, default=here.parent / "src" / "athyg_32_reduced_m10.csv")
    ap.add_argument("--reference", type=Path,
                    default=Path(r"C:\Program Files\Kitten Space Agency\Content\Core\briars_binary_dimmer_better_99k_stars.bin"))
    ap.add_argument("--out", type=Path, default=here.parent / "src" / "parallax" / "ohmystars_parallax_99k.bin")
    ap.add_argument("--extra-max-dist", type=float, default=50.0,
                    help="Also include stars dimmer than the prefix cut if within this many parsecs (0 = none). "
                         "They start hidden and only appear when the camera gets close enough.")
    args = ap.parse_args()

    ref = args.reference.read_bytes()
    count = struct.unpack_from("<i", ref, 0)[0]
    prefix_len = 4 + count * 16
    if len(ref) < prefix_len:
        print(f"reference too short: {len(ref)} < {prefix_len}", file=sys.stderr)
        return 1
    prefix = ref[:prefix_len]

    stage1_rows = list(stage1(args.csv))
    kept = []
    dropped = []
    for i, s in enumerate(stage1_rows):
        # convert_stars_raw.ps1: Import-Csv | Select-Object -Skip 1 drops the first stage-1 star.
        if i == 0 or s["mag"] > MAG_CUT:
            dropped.append(s)
            continue
        kept.append(s)

    if len(kept) != count:
        print(f"record count mismatch: replay {len(kept)} vs reference {count}", file=sys.stderr)
        return 1

    max_dir_err = 0.0
    size_mismatch = 0
    rgb_mismatch = 0
    for i, s in enumerate(kept):
        fx, fy, fz, size, r, g, b = struct.unpack_from("<fffBBBB", prefix, 4 + i * 16)
        dx, dy, dz = (f32(g15(v)) for v in s["dir"])
        max_dir_err = max(max_dir_err, abs(fx - dx), abs(fy - dy), abs(fz - dz))
        exp_size = size_from_mag(s["mag"])
        er, eg, eb = s["rgb"]
        if exp_size < 17:
            exp_size = 17
            er, eg, eb = ps_round(er * 0.2), ps_round(eg * 0.2), ps_round(eb * 0.2)
        if exp_size != size:
            size_mismatch += 1
        if (er, eg, eb) != (r, g, b):
            rgb_mismatch += 1

    print(f"prefix stars: {count}")
    print(f"max direction error vs reference: {max_dir_err:.3g}")
    print(f"size mismatches: {size_mismatch}, rgb mismatches: {rgb_mismatch}")
    if max_dir_err > 1e-5 or size_mismatch > count // 1000 or rgb_mismatch > count // 1000:
        print("replay does not match reference; refusing to write", file=sys.stderr)
        return 1

    def record(s, unit_dir) -> bytes:
        valid, dist, absmag = distance_info(s["row"])
        if valid:
            # Exact rendered direction scaled by distance, so nothing jumps when the camera is at the Sun.
            px, py, pz = (c * dist for c in unit_dir)
        else:
            px, py, pz = unit_dir
        r, g, b = s["rgb"]
        return struct.pack("<ffffBBBB", px, py, pz, absmag, r, g, b, 1 if valid else 0)

    body = bytearray()
    valid_count = 0
    for i, s in enumerate(kept):
        fx, fy, fz = struct.unpack_from("<fff", prefix, 4 + i * 16)
        n = math.sqrt(fx * fx + fy * fy + fz * fz)
        body += record(s, (fx / n, fy / n, fz / n))
        valid_count += distance_info(s["row"])[0]

    extras = 0
    if args.extra_max_dist > 0:
        for s in dropped:
            valid, dist, _ = distance_info(s["row"])
            if not valid or dist > args.extra_max_dist:
                continue
            x, y, z = s["dir"]
            n = math.sqrt(x * x + y * y + z * z)
            body += record(s, (x / n, y / n, z / n))
            extras += 1

    header = b"OMSP" + struct.pack("<iii", 1, count, extras)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_bytes(prefix + header + bytes(body))

    print(f"prefix stars with valid distance: {valid_count}")
    print(f"extra (hidden) stars: {extras}")
    print(f"wrote {args.out} ({args.out.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

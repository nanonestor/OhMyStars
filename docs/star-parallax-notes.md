# Star parallax (experimental): developer notes

With this feature on, the background stars move according to the camera's position. They are no longer fixed on a celestial sphere.

The feature is on by default. You can toggle it in two places:
- the main OhMyStars window, under "Show RA/Dec grid";
- the "Parallax (Experimental)" section of the star editor window, which also has the cutoff and hide-radius sliders.

At startup, the selected binaries and slider settings are applied as soon as the star renderer is ready, even when either mod window is closed or collapsed. No off/on toggle is needed. With parallax enabled at startup, disabling it restores the binary selection from before the automatic switch.

## Design rules (agreed — keep these)
- **Static stars.** Star positions do not change over time. Proper motion is not applied.
- **Sun at the origin.** The Sun is the origin of both the catalog and the game world. So `camera.PositionEcl / MetersPerParsec` is the camera's position in parsecs.
- **Real brightness.** Brightness comes from each star's catalog **absolute magnitude** and its distance from the camera. There is **no exaggeration factor**: everything stays at real scale, because the game plans to add interstellar travel.
- **Automatic bin switching.** Turning parallax on switches the active star binaries to the parallax bin and disables the others. Turning it off restores the binaries that were selected before.
- **Camera vs. vessel.**
  - These use the **camera** position: background stars, constellation and asterism lines, star labels, and the star pointer.
  - These use the **vessel** position: the navball marker and "orient to star". The navball only exists while a vessel is being controlled.

## Settings
These are stored in the star editor settings file (`starsedit_settings.ini`), not in the main `settings.ini`.

| Key | Default | Meaning |
|---|---|---|
| `ParallaxEnabled` | 1 | Master toggle |
| `ParallaxCutoffPc` | 500 (range 0–1000; 0 = no limit) | Stars farther than this from the Sun keep their static direction and brightness. This limits the performance cost. |
| `ParallaxHideRadiusPc` | 0.1 (range 0–2) | Catalog stars within this distance of the camera are hidden, so the game's own rendered star body takes over. When the camera is beyond this distance from the Sun, Sol is drawn as a background star. |

The star editor's size and colour sliders (gamma, offset, floor, RGB multipliers) are still applied on top of the parallax values.

## Code map
- **`src/StarParallax.cs`**
  - Reads the OMSP section and maps each game star instance to a parallax record.
  - When the camera moves more than `MoveThresholdPc`, it recomputes each star's direction, size and colour (`Update` → `Apply` → `PushToGpu`).
  - Restores the original instances when the feature is turned off.
  - If an update throws, the feature disables itself.
- **`src/StarsEditWindow.cs`**
  - Parallax UI and settings persistence.
  - Bin switching, using `ParallaxBinaryFileName` and `_preParallaxBinaries`.
  - `Update` applies pending startup settings independently of window visibility and retries if the renderer is not ready or the apply cannot complete.
- **`src/SkyCulturesRenderer.cs`**: the HIP lookups behind lines, labels and the pointer.
  - `hipToDirection` holds static directions and `hipToPositionPc` holds positions in parsecs.
    - `TryGetStarDirection` returns the direction from the camera.
    - `TryGetStarDirectionFrom(observerPc)` returns the direction from any observer, such as the vessel. It falls back to the static direction when parallax is inactive.
  - `hipToGameBodyId` lists catalog stars that also exist as game bodies: Proxima, Alpha Centauri A and B, Barnard's Star, and Tau Ceti.
    - When one of those bodies is loaded, the pointer, labels and navball aim at the body's real position. They use `TryGetGameBodyEgo` from the camera and `TryGetGameBodyVectorFrom` from the vessel.
    - The alignment rotation is applied only to catalog directions, never to game-body directions.
    - **Add entries here if the game adds more real stars.**
  - `_centralStarHip` / `IsCentralStar` refer to Sol. Sol is handled as the system's central body near the Sun, and as a background star once `StarParallax.IsCameraOutsideSolarSystem` is true.
- **Vessel-based users:** `src/NavballMarkerRenderer.cs` and `OhMyStarsWindow.ApplyStarOrientation` are the only code that uses the vessel's position.
- **Star pointer:** drawn in `src/StellariumRenderer.cs` from the camera position. Its default colour is yellow.
- **`src/parallax/ohmystars_parallax_99k.bin`:** the generated data. The csproj packages it into `parallax/`.
- **`tools/make_parallax_bin.py`:** the generator for the bin.

## Parallax bin format
The full spec is in the docstring of `tools/make_parallax_bin.py`. The bin has two parts.

**Game-compatible prefix.** This part is byte-identical to the reference game bin, so the game and other loaders behave as before.
- Starts with an `int32` count.
- Then one 16-byte record per star: a unit direction in ecliptic coordinates, plus size, r, g and b.
- The record order is reproduced by replaying the original PowerShell generator, and the result is verified against the reference bin.

**`OMSP` section.** This comes after the prefix and is ignored by the game. Version 1 uses 20-byte records, each holding:
- the star's ecliptic position in parsecs;
- its absolute magnitude;
- its base linear RGB colour;
- flags. Bit 0 means the star has a valid distance; stars without one never move.

The OMSP section also has "extra" records. These are stars dimmer than the prefix's magnitude cut that lie within `--extra-max-dist` of the Sun (default 50 pc). They start hidden and only appear once the camera gets close enough.

### Regenerating the bin
Run from the repository root:
```
python tools\make_parallax_bin.py
```
- **Defaults:**
  - `--csv`: `src\athyg_32_reduced_m10.csv`
  - `--reference`: `C:\Program Files\Kitten Space Agency\Content\Core\briars_binary_dimmer_better_99k_stars.bin`
  - `--out`: `src\parallax\ohmystars_parallax_99k.bin`
- **CSV coordinates.** `x0, y0, z0` in the CSV are equatorial parsecs:
  - x points toward RA 0h, Dec 0°;
  - y points toward RA 6h, Dec 0°;
  - z points toward Dec +90°.

  They are rotated to ecliptic coordinates using an obliquity of −23.439281°.
- **If the game's reference bin changes**, regenerate the parallax bin. Otherwise the prefix will no longer match the game's star bin.

## Brightness model
- Apparent magnitude = `absmag + 5·log10(d_pc) − 5`.
- `ComputeSizeAndColor` maps that magnitude to a size and colour using the same flux rule as the original generator:
  - flux range from `DimFlux = 10^-2.72` to `BrightFlux = 10^0.64`;
  - size = `10 + 245·norm^0.45`;
  - stars with a size below 17 are dimmed to 0.2× their colour.
- Sol uses an absolute magnitude of 4.85 and a fixed colour (255, 226, 202).

## Known limitations and future ideas
- **Cutoff.** Stars beyond the cutoff stay static. Far from Sol, lines that join a moving star to a static one can look distorted. Raise the cutoff, or set it to 0 for no limit, at some performance cost.
- **Proper motion.** Time-based star positions are intentionally not supported.
- **Other systems.** If the game gains other "central star" systems, the `_centralStarHip` handling and `hipToGameBodyId` will need updating.
- **Changed defaults.** Users who already have saved settings keep their old values. New defaults, such as the yellow pointer, only apply after a reset or a fresh install.
- **Repository cleanup.** `tools/__pycache__` was committed and could be added to `.gitignore`.

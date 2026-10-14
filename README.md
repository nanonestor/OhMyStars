# Oh My Stars
Oh My Stars is a mod for Kitten Space Agency that lets you display constellations in the sky. It includes constellations from dozens of cultures.  
Constellation and Star data are sourced from:  
- Stellarium Project [https://stellarium.org/](https://stellarium.org/)
- AT-HYG catalog 3.2 [https://astronexus.com/projects/at-hyg](https://astronexus.com/projects/at-hyg)
- Constellation Boundaries (Pierre Barbier) - [https://pbarbier.com/constellations/boundaries.html](https://pbarbier.com/constellations/boundaries.html)

## Credits
Oh My Stars is a fork and continuation of the mod StellariumCatalog sanctioned by the author DavidK0 - thank you!


<img width="1932" height="1293" alt="OhMyStars" src="https://github.com/user-attachments/assets/b7452512-7885-41c8-a3ac-d754b64a1633" />

## Getting started

<details>
<summary>How to install</summary>

1. Install [StarMap](https://github.com/StarMapLoader/StarMap/)
   1. Download and unzip [the latest release of StarMap](https://github.com/StarMapLoader/StarMap/releases/latest)
   2. Run the .exe and follow the instructions
2. Install [ModMenu](https://github.com/MrJeranimo/ModMenu/)
   1. Download and unzip [the latest release of ModMenu](https://github.com/MrJeranimo/ModMenu/releases/latest) or obtain it from [spacedock.info](https://spacedock.info/)
   2. Put the contents in `Kitten Space Agency\Mods\`
3. Download and unzip the latest release of Oh My Stars [from GitHub](https://github.com/nanonestor/OhMyStars/releases/latest) or obtain it from [spacedock.info](https://spacedock.info/)
4. Place the contents into `Kitten Space Agency\Mods\`. Your mod folder should look something like this:
```
├── OhMyStars
│   ├── LICENSE
│   ├── ModMenu.Attributes.dll
│   ├── NOTICE.txt
│   ├── OhMyStars.deps.json
│   ├── OhMyStars.dll
│   ├── hyg_v42.csv
│   ├── licenses/
│   ├── lines_in_20.txt
│   ├── mod.toml
│   ├── skycultures/
├── ModMenu
│   ├── LICENSE.txt
│   ├── mod.toml
│   └── ModMenu.dll
```
5. Run KSA through StarMap
</details>


**AI Disclaimer:** This mod was made with the help of AI (but not entirely!).

## 3D stars (game-native parallax)

KSA now places stars in 3D itself. Each record in a star binary carries an inverse distance, so stars shift as the camera moves and their size follows true inverse-square brightness. Oh My Stars follows the game's shifted positions for constellation lines, pointer lines, constellation labels and star names. The navball marker still follows the vessel.

The mod's earlier experimental parallax feature has been removed because the game now does this. How it worked is recorded in [docs/legacy-experimental-parallax.md](docs/legacy-experimental-parallax.md).

## Star binary generator

The star editor can build game star binaries from AT-HYG–style CSV catalogs. The output uses the game's format: a header (star count and magnitude scale) followed by 20-byte records. Each record holds a direction, a size, a colour and an inverse distance in parsecs.

- **Catalog selector** lists the CSV files in the mod folder (install and Documents) that have the required columns: a magnitude column, plus RA/Dec or x/y/z. Picking one fills the Input CSV path. **Rescan** refreshes the list.
- **Analyze** does a dry run of the selected catalog with the current settings. It shows how many star entries would be written, with a breakdown by filter. No files are written.
- **Magnitude Scale** is a numeric entry box with a default of `0.15` (range `0.01` to `5`). The value is written to the binary header for the game's size-vs-distance scaling.
- **Directions** come from the catalog's RA/Dec, rotated into the game's ecliptic frame.
- **Colour** uses the `ci` colour index. Tycho-2 rows (`mag_src = T`) give BT−VT rather than B−V, so they are scaled by 0.85 to approximate B−V.
- **Filters**:
  - Closer than (distance)
  - Farther than (distance)
  - Absolute magnitude below
  - Apparent magnitude below

  Each filter has its own toggle and value. Filters combine inclusively: a star is kept if it passes *any* enabled filter. With no filters enabled, every star is kept.
- **Distance units:** a button switches the distance boxes between light years and parsecs. Values are converted to parsecs, the catalog's unit, before filtering.
- **Add Sol** adds the Sun at 0,0,0. It is skipped if the file already contains a star there.
- **Export current stars** writes every star the game currently has loaded to a new binary, header included. Add Sol works here too. After a Generate or Export finishes, the editor's binaries list refreshes on its own.
- **Output naming:** the Output Name and Export Name boxes take a base name, not a path. Entering `mysky` writes `mysky_stars.bin` (the game binary) and `mysky_stars_IDs.bin` (the identity sister file). All generated and exported files go into the mod's `binaries` folder.
- **Identity file:** a `<name>_stars_IDs.bin` sister file can be written next to each binary. It holds the identity data the mod uses to match stars, such as HIP and catalog IDs, names, magnitudes and colour index. The game ignores this file, and the mod skips it when it looks for star binaries.

### Default binary and non-mod binaries

- On first run the mod generates `binaries\OhMyStars_stars.bin` (plus its identity file `binaries\OhMyStars__stars_IDs.bin`) from `athyg_32_reduced_m10.csv` using magnitude scale `0.15` and selects it as the only active star binary. Existing default binaries from an older generation version are rebuilt automatically on launch; other generated binaries are left unchanged.
- `.bin` files anywhere in each mod folder, including subfolders such as `binaries`, are listed. Identity sister files are skipped.
- Enabling a binary that has no Oh My Stars identity file shows a confirmation. Confirming switches to **vanilla mode**: the mod's star transforms and sky overlays are disabled, and the stars render as the game normally draws them. Selecting only Oh My Stars binaries turns the mod's features back on.

## Build settings defaults

[src/settings.ini](src/settings.ini) is the authoritative settings file for testing and releases.
Every build copies it to the build output and overwrites `settings.ini` in
`%USERPROFILE%\Documents\My Games\Kitten Space Agency\mods\OhMyStars`, including when the
installed settings were changed during testing.

To change the shipped defaults, edit the source settings before building. Close the game
before building or collecting release files so it cannot save testing settings over the defaults.

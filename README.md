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

## Experimental: moving stars (parallax)

When this option is on, stars move as the camera moves. Each star's position comes from the catalog with the Sun at the origin. Brightness is based on absolute magnitude and the real distance from the camera, with no exaggeration. Proper motion is ignored, so the stars themselves never move over time.

The option is **on by default**. You can turn it on or off in the main OhMyStars window, under "Show RA/Dec grid". More settings are in the star editor's "Parallax (Experimental)" section:

- **Moving-star cutoff** (default 500 pc): stars farther than this from the Sun stay fixed in place. Lowering it, or turning the feature off, reduces the performance cost.
- **Hide radius** (default 0.1 pc): background stars this close to the camera are hidden, so the game's own star body takes their place.

Turning the option on automatically switches to the bundled parallax star binary. Turning it off restores your previous binaries.

Constellation lines, labels and the star pointer follow the camera. The navball marker follows the vessel.

Developer notes are in [docs/star-parallax-notes.md](docs/star-parallax-notes.md).

## Build settings defaults

[src/settings.ini](src/settings.ini) is the authoritative settings file for testing and releases.
Every build copies it to the build output and overwrites `settings.ini` in
`%USERPROFILE%\Documents\My Games\Kitten Space Agency\mods\OhMyStars`, including when the
installed settings were changed during testing.

To change the shipped defaults, edit the source settings before building. Close the game
before building or collecting release files so it cannot save testing settings over the defaults.

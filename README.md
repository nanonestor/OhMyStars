# Oh My Stars
Oh My Stars is a mod for Kitten Space Agency that lets you display constellations in the sky. It includes constellations from dozens of cultures.  
Constellation and Star data are sourced from:  
- Stellarium Project [https://stellarium.org/](https://stellarium.org/)
- OMS compiled catalog, based on AT-HYG 4.0 and USNO WDS
  [sources and build notes](catalogs/README.md).
- USNO Sixth Catalog of Orbits of Visual Binary Stars (ORB6), for optional
  Multi orbit curves: [source and attribution](catalogs/README.md#sixth-catalog-of-orbits-of-visual-binary-stars-orb6).
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
2. Optionally install [ModMenu](https://github.com/MrJeranimo/ModMenu/)
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

Without ModMenu loaded, **Oh My Stars** and **Stars Editor** appear directly in
the game's menu bar and open their respective windows. When ModMenu is loaded,
use the existing **Oh My Stars Window** and **Star Editor** entries in its
**Oh My Stars** menu instead; the fallback entries are hidden.
</details>


**AI Disclaimer:** This mod was made with the help of AI (but not entirely!).

## 3D stars (game-native parallax)

KSA now places stars in 3D itself. Each record in a star binary carries an inverse distance, so stars shift as the camera moves and their size follows true inverse-square brightness. Oh My Stars follows the game's shifted positions for constellation lines, pointer lines, constellation labels and star names. The navball marker still follows the vessel.

When enabled, the navball marker label uses this preference order: proper name,
abbreviated Bayer and constellation (for example `Eps-1 Lyr`), HD, Flamsteed and
abbreviated constellation, F. Struve, O. Struve, Hipparcos, another recorded
catalog identifier, then OMS only as a last resort. This does not change window
heading or background-label priorities.

The mod's earlier experimental parallax feature has been removed because the game now does this. How it worked is recorded in [docs/legacy-experimental-parallax.md](docs/legacy-experimental-parallax.md).

## Star editor render tuning

In the **Editor** tab, **Render Tuning** starts with a **Magnitude Scale** numeric text box above **Size Curve Gamma**. It adjusts `brightness.magnitudeScale`, the game's distance-brightness falloff, for all selected star binaries without rewriting their files. Enter a finite value greater than zero; larger values produce a steeper falloff.

The initial value comes from the game's stock star-binary header, not the generator's defaults. **Restore Defaults** keeps the currently selected binaries, resets the other render settings, and reads Magnitude Scale from the last selected binary's header (matching the game's last-binary rule). With no binaries selected, it uses the initial stock value. Magnitude Scale is included in named saved settings and in **Restore Settings**; older settings without it use the stock value. **Export Current Stars** writes the active scale into the exported binary's header.

## Star binary generator

A provisional AT-HYG 4.0 / WDS CSV and its column/source/license documentation
are in [catalogs/](catalogs/README.md). Numeric OMS identities, the multiple-system
model, exclusions, and remaining integration work are recorded in
[the catalog design notes](docs/curated-star-catalog.md). The curated CSV now
supplies **Stars**, **Multi**, sky overlays and the default binary generator.
The default magnitude/colour policy remains unchanged; an unfiltered binary
also includes the catalog's many faint and WDS-only companion candidates.

## Multiple-star systems

Open **Multi** in the Oh My Stars window. Mouse picking defaults on; optionally
enable **Highlight multi star systems** to see the eligible targets.
The **Disable Mouse Clicking** button has a fixed label in **Stars** and **Multi**.
A green dot to its left means mouse clicking is disabled. This toggle starts
false on each launch, so mouse clicking is initially enabled.
With rings off, mouse selection works as in **Stars**, selecting ordinary stars
as well as multiple-system primaries without leaving **Multi**. A star with no
recorded companions shows only its name and RA/Dec, followed by
**The selected star has no recorded companions.** No component list or plot is
shown for that star. With rings on, click inside a primary ring to select that
system; overlapping rings choose the nearest screen center (exact ties use OMS
ID), preserving the ring-only selection mode. Picking ignores ImGui windows and camera drags and stops when its controls
are hidden or another picker starts. Rings are shown only while Multi is visible;
the ring toggle is saved in settings. The **Apparent magnitude cutoff** slider
ranges from 0 to 20 and defaults to **8.9**. Only primaries with a known
catalog apparent magnitude at or brighter than the cutoff get rings and can
be picked. The cutoff is saved in settings; it does not change the star binary
or filter the selected system's component list/plot. Explanatory messages are
kept here rather than in Multi; loading/errors and missing-geometry data remain
visible when relevant.

Below the primary-ring cutoff, **Component apparent magnitude cutoff** ranges
from 0 to 20 (default **11**, saved in settings). Components fainter than this
value move to a separated section at the bottom of the list and default to
unchecked plot checkboxes, regardless of C/U/V/Q classification. Stars exactly at the
cutoff and stars with unknown apparent magnitude remain in the main section.
The primary always remains visible in the information list and plot. A system's
background ring is drawn and selectable only when at least one non-primary
member passes the component cutoff (unknown magnitudes also qualify).
Eligibility is cached and recalculated only when the cutoff changes.
The combined primary/component-filtered marker list is also cached, rebuilding
only when either cutoff changes. Per-frame camera projection visits only
eligible primaries; camera movement and alignment still update their positions.
The shared star selection is retained when its ring disappears. System details
are hidden if no companion passes the component-magnitude/provenance filters,
with a short explanation instead; relaxing those filters restores the details.
The primary-magnitude cutoff still affects rings only, not selected details.
Each component has a checkbox controlling only its plot visibility. Checking
a faint component overrides the cutoff and shows it if geometry is available;
it remains in the separated bottom section. Brighter and unknown-magnitude
components default to checked. Explicit checkbox choices remain in effect
when changing the cutoff; components not manually toggled follow the cutoff.
Selecting a system resets these choices and the plot view; they are not saved.
Checkboxes affect only the plot, not background-ring eligibility.

The **Stars** tab (formerly Information) uses a **Search By:** dropdown with
**All** first (the default), followed by Constellations, Proper and Bayer, plus separate searches for
OMS, AT-HYG, Tycho, Gaia, HYG, Hipparcos, HD, HR, Gliese, Flamsteed,
Herschel, F. Struve, O. Struve, Burnham, BDS and WDS identifiers.
Each selection dropdown has its search field as the first item inside the popup,
not a separate field above it. The **Star** label is to the left of its dropdown.
The **Search By:** label and half-width method dropdown are centered together
within the window. Specific identifier methods show only that type's
identifiers, without appending other star names. **All** searches proper names,
Bayer designations and every recorded catalog ID, showing the first matching
identifier once per star. There is no Cursor search method; background mouse
picking remains available independently of the search method.
**All** lists no results until non-whitespace search text is entered.
Catalog-ID search accepts recorded values or their readable display labels;
exact matches (including individual aliases) appear before partial matches
in both specific-ID and **All** searches. Results are cached until the
method/query changes. Searches run off the drawing thread after a 150 ms typing
pause, cancel superseded queries, and retain at most 201 partial matches while
still checking the whole catalog for exact matches. Unnecessary display-label
formatting is skipped. **All** remains the default and first search method.
The dropdown shows up to
200 matches with an explicit prompt to narrow larger result sets.
Its selected-star heading follows Multi: `Proper name / Flamsteed designation`
on the first line and the full Bayer designation on the second, omitting
unavailable lines. Without these names, both tabs fall back to Burnham / HD,
then F. Struve / O. Struve, then another recorded catalog identifier.
All/catalog searches and the Bayer selector accept unnumbered Bayer names:
`Epsilon Lyrae` matches `Epsilon-1 Lyrae` and `Epsilon-2 Lyrae`. Abbreviated
catalog Bayer aliases such as `Eps` also match numbered designations.
Numbered searches remain specific, and results retain their individual
designations and selectable components rather than merging different stars.
For numbered Bayer designations belonging to a recorded multiple system,
All/catalog Bayer searches and the Bayer selector also offer an unnumbered
system entry, such as `Epsilon Lyrae`, which selects that system's primary.
Numbered entries remain available. Separate systems sharing a designation
retain separate system entries; no alias is inferred for ungrouped stars.
OMS is used only when no other identifier is available. Bayer search entries and
the expandable identifier list also use the full Greek letter and constellation
genitive. An arrow below **Distance (LY)** reveals all recorded identifiers,
including Proper, Bayer and HIP. HIP and Bayer no longer have separate rows
above that arrow.
Identifier values align in a fixed second column sized from all identifier
labels, including labels absent from the selected star. Label width is cached
and only recalculated when the font or font size changes.

Background star name labels use the first available designation: Proper name,
full Bayer/constellation, Flamsteed/constellation, F. Struve, O. Struve,
Burnham, then HD. No name is printed if none is available. Selected/picked-star
name labels use the same cached hierarchy; information-window headings are unchanged.

Background star names, spectral types, asterism names and selected/picked-star
labels share overlap-aware placement. Picker and selected-star labels take
priority over ordinary names, then spectral/asterism text. Labels try nearby
positions inside the viewport; those with no free nearby space are omitted
instead of overlapping. Existing RA/Dec grid labels reserve their text areas.
Placement uses a spatial grid and a bounded number of attempts per label,
and resets each frame as the camera moves.

Stars, Multi, sky overlays and the default binary generator use the compiled
OMS catalog. Stars and Multi share one selected OMS ID, including stars without
HIP or Tycho identifiers. HIP is only an external catalog/sky-culture reference.
Legacy internal Hip-named rendering APIs carry checked OMS IDs.
Standard sky cultures still import positive HIP references; custom cultures
are saved with negative OMS endpoints so no-HIP stars have stable references.
Unavailable legacy endpoints are logged and retained rather than deleted when
editing/saving a custom culture.
If the other tab cannot represent the selection, it shows no selected star
instead of selecting an unrelated default. Changing the search method does
not change the selected star. In Stars, **Multiple Star info** switches to
Multi when the selected star is a known system primary with a companion passing
the current Multi component/provenance filters. At the top of Multi,
the primary-ring checkbox is followed by a row containing a filter-expansion
arrow and a **?** help button. The arrow reveals both apparent-magnitude
cutoff sliders and **Exclude WDS-only entries**; these controls start collapsed
without changing their saved values. **?** opens the separate, closable
Multiple Star Help text window. A separator divides these controls from the names.

Mouse selection defaults to enabled in both Stars and Multi, independently
of the Stars search method. The top-right **Disable Mouse Select** button
has a green **On** indicator; when disabled it becomes **Enable Mouse Select**.
The disable button is an ordinary unpressed action button by default, not an
enabled disable-toggle. Picking is suspended when the main window is closed,
collapsed or showing Settings, and resumes on Stars/Multi if still enabled.
Suspension does not change the user's mouse-selection preference and does not
cancel separate Stars Editor picking sessions.
Multi picking uses the existing cached magnitude/provenance eligibility filters,
even when primary rings are hidden. UI-captured clicks and camera drags do not
select stars. Foreground bodies and vessels block picking using their existing
occlusion spheres, checked at the candidate star position.
Vessel bounds are conservative rather than per-pixel mesh/depth-buffer tests.
Stars mouse picking excludes entries without a finite apparent magnitude,
matching the generator's inability to render them. Such entries remain
searchable and available in Multi; they cannot steal clicks from a bright,
unresolved primary such as Bharani.
In the Stars tab, mouse picking also excludes every non-primary system member,
including C, Q, V and identification-uncertain U companions. Singles and system
primaries remain selectable. Catalog searches still offer component entries;
Multi and Stars Editor picking retain their own eligibility rules.

**Exclude WDS-only entries**, directly below the component cutoff, defaults
to unchecked (WDS-only entries are included) and is saved in settings. When
enabled, it hides entries with `radec_source=WDS`
entirely from the list and plot, including their orbit curves, regardless of
plot checkbox overrides. WDS-only primaries have no selectable rings; other
systems require at least one non-WDS companion passing the magnitude cutoff.
Changing the toggle refreshes the eligibility cache. Hidden stars still
participate internally in geometry calculations, preserving other positions.
Disable the toggle to restore these entries and the conditional asterisk legend.

**Exclude Visual Entries**, directly below WDS-only exclusion in the collapsible
filter list, defaults to unchecked and is saved in settings. When enabled, it
hides only V-type components from component rows, summaries, plots and orbit
overlays, regardless of plot-checkbox overrides. Q entries remain included.
Systems without another eligible companion lose their highlight ring and ring
click target. Hidden V components still participate in layout calculations so
their dependent companions retain their positions. Catalog data is unchanged.
The Multi tab's **?** button opens an information page inside the main mod
window, like the Stars legend. **Back** at the top returns to Multi without
changing the selected system or filters. The page explains companion
classification, source catalogs and their limitations; it is not a separate
floating window. Saved tab settings treat this page as Multi.

At the bottom of the collapsible arrow-button filter list, **Limit highlights to center radius**
optionally restricts highlight rings and their click targets to a circle centered
on the game window. The slider directly beneath it sets the radius from **0% to
50% of screen height** (default 25%). The restriction defaults off, applies only
when highlighting is on, and both settings are saved. A ring is included when
its center is within the radius; at 0%, only an exactly centered system qualifies.

Below the plot, **Plot zoom** (1x to 10x) and east/west and north/south pan sliders explore the
same fixed-size plot box. **Reset plot view** restores 1x and centers on the
primary. The plot fits the visible components before applying zoom; hidden
components do not force a wide scale. Geometry still uses all relationships,
so hiding an intermediate component does not lose its companions' positions.
The reset button follows the three sliders. No information text is displayed
below the plot; unavailable geometry still cannot be plotted. Enabled components
without a usable position are listed inside a reserved band at the top of the
plot, for example **Not Shown - Ca, Cb**. Components deliberately hidden by plot
checkboxes, magnitude limits or WDS-only exclusion are not listed in that notice.
Measured rho/theta links take priority. If a disconnected component has recorded
RA/Dec and the primary does too, the plot uses their spherical angular offset
as an approximate anchor, then follows any measured links to its companions.
RA is interpreted in hours and Dec in degrees, including RA wraparound and
declination scaling. These positions and any companions dependent on them are
listed as **RA/Dec Approx - Ca, Cb** inside the plot. This mixes catalog epochs;
it is a display fallback, not a common-epoch astrometric prediction, and changes
neither catalog data nor physical classifications. Layouts are cached per selection.
If neither a measured chain nor recorded coordinates can locate a component,
it remains in **Not Shown**.
WDS-only companions with neither complete RA/Dec nor a positive rho link to
another component are omitted from the individual component rows and summarized
at the end as **Components not listed becuase contain no ra/dec, or rho values:**
followed by their labels. Incoming, outgoing and group rho links count, so an
unanchored Ca-Cb pair remains listed normally if its internal separation is known.
The primary is never removed, and the WDS-only exclusion setting also applies
to this summary. Catalog records are unchanged.

For supported pairs, blue apparent relative-orbit curves are automatically
overlaid without moving or replacing the measured component points. Orbit
curves follow the companion's plot checkbox and are included in the fitted
extent. Curves are sampled once per selected system, not integrated each
frame; runtime never downloads data. These are complete projected relative
orbits, not a prediction for the game's date or individual barycentric paths.
Recorded WDS positions and fitted ORB6 solutions are independent and may disagree;
a measured point is not snapped onto an orbit to conceal that disagreement.
Only unique, complete grade 1–5 visual solutions matched to explicit
individual component relationships are shown. Group/blended endpoints and
ambiguous solutions are omitted rather than guessed. See the
[ORB6 snapshot and limitations](catalogs/README.md#sixth-catalog-of-orbits-of-visual-binary-stars-orb6).

Each member occupies one line: component label / recorded relationship (such
as `A>B`), `App Mag:` and `Abs Mag:` (two decimal places), rho (arcseconds), then theta
(degrees, both rho and theta to one decimal place), omitting
empty/unknown values. Multiple stored measurements are pipe-separated.
An asterisk before the component label (including the primary) indicates
`radec_source=WDS`: its catalog position is WDS-sourced rather than AT-HYG.
This marker is for the information list only, not a change to plot labels or IDs.
The rho/theta legend sits between separators below the component list and above
the plot. It uses separate short lines, with `* = WDS only sourced entry`
shown only when at least one listed member has a WDS-sourced position.
V members show `(Visual)` instead of the relationship; Q (Questionable) members
show `(Visual ?)`; U members show
`(Relationship Unknown)`.
Only Visual and Questionable members show proper motion: component minus primary, per axis,
in mas/year, and only when both values are available. Primaries and true
companions do not show proper motion. Supported ORB6 companions with exact
individual-pair plot matches, or exact original WDS pair matches to the
measurement-owning representative leaf, are classified C rather than Q/V
during catalog generation (primary and identification-uncertain roles remain
unchanged). Sirius has a scoped curated identity correction: the real
AT-HYG Sirius B entry is category C, component B of Sirius A, replacing
the duplicate WDS-only Ba representation. Optical neighbor Bb remains V.
The exact A-B orbit now matches the individual stars.
Epsilon Lyrae retains Ca/Cb as a distinct tight subsystem. Its real HD 173607
entry is D, not Cb; the duplicate WDS-only D is retired. A is primary and
B, Ca, Cb and D are physical companions. Cb has no recorded magnitude and
remains WDS-only, so viewing it requires disabling WDS-only exclusion.
B-V precedes the final relative-motion column,
formatted as `(rel PM RA: <value> mas/year  rel PM dec: <value> mas/year)`;
missing axes are omitted. Property columns
align across all members using measured text widths; missing values leave
blank cells, and wholly empty columns take no space. RA and Dec appear only once above
the member list, on one line as `RA <value> h / Dec <value> deg`, using the
primary's coordinates. A separator above this line separates it from identifiers.
The system header shows the primary's proper name and Flamsteed designation
on one line, separated by `/` when both exist (for example, `Albireo / 6 Cygni`).
The next line shows its Bayer designation with a full Greek letter name and
Latin constellation genitive (for example, `Iota Cassiopeiae`).
Both Multi heading lines use a font 30% larger than the normal information text.
Missing designations are omitted without dangling separators.
If none of the proper, Flamsteed or Bayer names are available, the header
instead shows `Burnham <id> / HD <id>`. If neither is available, it falls back
to `F. Struve <id> / O. Struve <id>`. Each fallback tier shows just the
available identifiers, without a dangling slash.
When that fallback would show only HD (no Burnham), it also shows F. Struve
if available, otherwise O. Struve: `HD <id> / F. Struve <id>` or
`HD <id> / O. Struve <id>`. This applies to both Stars and Multi headings.
The catalog-heading hierarchy is: Burnham / HD, Burnham alone,
HD / F. Struve, HD / O. Struve, HD alone, F. Struve / O. Struve,
F. Struve alone, O. Struve alone, another recorded catalog identifier,
then OMS as the last resort. Proper, Flamsteed and Bayer names retain their
existing precedence over these catalog fallbacks.
Selected-star headings and expanded identifier lists in Stars and Multi prefix
F. Struve values with Sigma (U+03A3) and O. Struve values with Omicron-Sigma
(U+039F U+03A3), retaining the catalog names. Both symbols are supported by the
bundled JetBrains Mono and 3270 fonts. If the active font lacks either glyph,
the display keeps the original text-only identifiers. Search labels and accepted
search text remain unchanged.
The arrow-only button underneath toggles
an inline list of available identifiers below it as `catalog:id`; missing
identifiers are omitted.
Burnham discovery identifiers (`BU`, displayed as `Burnham 1` for HD 5005) are listed
separately from Burnham Double Stars catalogue numbers (`BDS:455` for that
same system). The optional identifier list uses `F. Struve 2382`,
`O. Struve 183`, `Herschel 2 5` and `HD 5005`; appendix letters and Herschel
classes are retained, and multiple aliases are separated with semicolons.
Multi's expanded identifier list consistently uses `Name: value`, including
`F. Struve: 2382` and `O. Struve: 183`, with one space after each label's colon.
The importer recognizes numbered Greek aliases such as
`eps 1 Lyr`, filling blank Bayer/Flamsteed fields from scoped WDS evidence.
Its expanded 6-arcsecond position search preserves existing tighter-core
matches when available; see the [matching rules](catalogs/README.md).
The plot uses the primary at its center, north up and east left.
It includes C, V, Q and U members with usable theta/rho relationships and uses B-V color
(gray for missing color) and apparent-brightness dot sizes. Missing geometry
is listed as not plotted rather than guessed. Group endpoints such as A or AB
use their primary/leading individual leaf as an approximate location, not a
measured barycenter; chained relationships are supported. This is a plotting
approximation only and does not change catalog measurements.
Plot labels avoid nearby stars and previously placed text, with connector
lines identifying their stars; crowded labels can move farther from their dots.
V entries remain visual-only; U entries are uncertain. Different
observation dates are not a common-epoch orbit model.

Primary rings use curated positions, game observer/parallax/alignment and
occlusion, not a claim that every marker is rendered in the active binary.
Some primaries are below its magnitude cutoff. Ring picking is disabled in
vanilla mode, consistent with existing overlays. The catalog loads in the
background on first opening Multi; errors are shown with a Retry option.

The mod build copies `catalogs/oms_catalog.csv`, its README and source manifest
to `catalogs/` in the build output and deployed mod folder. Keep the generation
assets in this repository: [regeneration instructions](catalogs/README.md#files-and-reproducibility)
and [Generate.ps1](catalogs/Generate.ps1). The source CSV is not duplicated under
`src/`; generation scripts and the OMS registry are not deployed to players.

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

### Identity sister binary format (version 1)

The current sister file is little-endian, with records in exactly the same order as the associated game binary. Its header is the four ASCII bytes `OMSI`, an `Int32` format version (`1`), and an `Int32` record count. Each record contains, in order:

| Field | Binary type | Meaning |
|---|---|---|
| `Hip` | `Int32` | HIP identifier, or `0` when unavailable |
| `CatalogId` | `Int32` | Catalog identifier |
| `DirX`, `DirY`, `DirZ` | Three `Single` values | Unit direction from Sol in the game's ecliptic frame |
| `InverseParsecs` | `Single` | Reciprocal distance in parsecs; matches the game record |
| `Magnitude` | `Single` | Catalog apparent magnitude |
| `AbsoluteMagnitude` | `Single` | Catalog absolute magnitude, or `NaN` when unavailable |
| `ColorIndex` | `Single` | B-V colour index (Tycho BT-VT is converted when generating), or `NaN` when unavailable |
| `Name` | .NET binary string | UTF-8 name prefixed by its byte length encoded as a 7-bit integer |

Each record has 36 fixed bytes followed by the variable-length name. The sister file does **not** store sprite sizes, RGB bytes, spectral types, or generator settings; sizes and RGB live in the game binary, whose header also stores Magnitude Scale. Exported stars retain matched identity metadata where available; unmatched exports use missing-value placeholders. The default binary's sister has the special name `OhMyStars__stars_IDs.bin`; legacy `.ohmystars.bin` sister names are also supported.

### Default binary and non-mod binaries

- On first run the mod generates `binaries\OhMyStars_stars.bin` (plus its identity file `binaries\OhMyStars__stars_IDs.bin`) from `catalogs\oms_catalog.csv` using magnitude scale `0.15` and selects it as the only active star binary. Existing default binaries from an older generation version are rebuilt automatically on launch; other generated binaries are left unchanged. The old reduced AT-HYG CSV is not shipped, and stale copies are removed from build/deployment output. Generated identity records use OMS IDs as their catalog ID; always-include ID matching prefers `oms_id` (falling back to `id` for imported non-OMS CSVs).
- `.bin` files anywhere in each mod folder, including subfolders such as `binaries`, are listed. Identity sister files are skipped.
- Enabling a binary that has no Oh My Stars identity file shows a confirmation. Confirming switches to **vanilla mode**: the mod's star transforms and sky overlays are disabled, and the stars render as the game normally draws them. Selecting only Oh My Stars binaries turns the mod's features back on.

## Building and game source reference

The project targets .NET 10 and the Ahwoo game installation at
`C:\Program Files\Ahwoo\Games\Kitten Space Agency`. Override `KSAGameDir` with
an environment variable, `-p:KSAGameDir="..."`, or the untracked
`src\OhMyStars.csproj.user.props` file for another installation.
The current game uses `Brutal.Numerics.dll` (not `Brutal.Core.Numerics.dll`)
and the split `Brutal.ImGui` assemblies.

Build with `dotnet build .\src\OhMyStars.csproj`. Builds also deploy the mod
to the Documents mods folder described below.

After updating KSA, run `.\tools\Refresh-Decompiled.ps1` to regenerate the
local, gitignored `decompiled` source reference using `ilspycmd`
(`dotnet tool install --global ilspycmd` if missing). Use `-GameDir` to select
another installation. Each selected assembly's source folder is replaced,
and `decompiled\VERSION.txt` records the installed version and source path.
Search the exported C# directly when inspecting game APIs; obsolete snapshot
folders are not needed.

Run `dotnet run --project .\tools\MenuTests\MenuTests.csproj` for the menu
hook/optional-ModMenu regression checks, which use graphics stubs rather than
starting the game. The catalog/layout checks remain available with
`dotnet run --project .\tools\MultiTests\MultiTests.csproj`.

## Build settings defaults

[src/settings.ini](src/settings.ini) is the authoritative settings file for testing and releases.
Every build copies it to the build output and overwrites `settings.ini` in
`%USERPROFILE%\Documents\My Games\Kitten Space Agency\mods\OhMyStars`, including when the
installed settings were changed during testing.

To change the shipped defaults, edit the source settings before building. Close the game
before building or collecting release files so it cannot save testing settings over the defaults.

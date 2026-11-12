# OpenNGC in Oh My Stars

The **Deep Sky** tab uses an unmodified local snapshot of
[OpenNGC](https://github.com/mattiaverga/OpenNGC), by Mattia Verga and the
contributors listed in [AUTHORS](AUTHORS). The files were copied from the
user-supplied OpenNGC directory on 2026-11-02; its upstream commit/release was
not recorded. No online service or external catalog directory is needed at runtime.

## Included data

- [database_files/NGC.csv](database_files/NGC.csv): 13,969 NGC and IC records.
- [database_files/addendum.csv](database_files/addendum.csv): 64 additional
  records, including M40 and M45 (the latter's primary name is `Mel022`).
- [NGC_guide.txt](NGC_guide.txt): original field, type and source-code definitions.
- [outlines/outlines_stellarium.dat](outlines/outlines_stellarium.dat):
  upstream simplified J2000 contour catalog, retained unmodified.
- [outlines/metodology.txt](outlines/metodology.txt): upstream outline-generation
  method and Aladin acknowledgment, retained unmodified.
- [UPSTREAM-README.md](UPSTREAM-README.md): original OpenNGC documentation
  (renamed only), including its data-source acknowledgments.
- [LICENSES/](LICENSES/), [AUTHORS](AUTHORS) and [.reuse/dep5](.reuse/dep5):
  upstream license and attribution material.

The two semicolon-delimited CSVs, guide, authors and license files are byte-for-byte
copies. Oh My Stars does not rewrite, merge or reclassify their records. These
objects remain separate from OMS star identities and the star-binary generator.
Quoted fields, alternate identifiers, both note columns and per-field data-source
codes are preserved. No derived information CSV is needed.

SHA-256 of the bundled source CSVs:

```text
NGC.csv
840FE0C9EE1332E551B2E722A0E92726CD7B157914A3D2177602832AADD3AA9E
addendum.csv
1D8F0914E643ADA325A5A94D88D8FEFAD6A4937A2F77CC34F21483AF22B11983
outlines/outlines_stellarium.dat
5C74C716951C387E5D086CB51A9807BA8C844C71F54E058EC2021B797BF2EF50
outlines/metodology.txt
CB091ED1E2E0BF7FF608A8D56207C24233E21F366F44B6A7AAF7DC274C77141C
```

## Searching and selecting

The first dropdown selects **All object types** or one of the 21 types defined
in the guide, including galaxies, clusters, nebulae and novae. The second selects
**All catalogs**, **NGC**, **IC**, **Messier** or **Other**. Other means the
addendum, so it includes M40/M45 too. NGC/IC membership includes the corresponding
cross-reference columns; Messier membership includes the Messier cross-reference
and primary Messier names.
Messier results are listed numerically from **M1 through M110**, with the
Messier ID first in each dropdown label. Type and keyword filters retain this
order. The order and labels are prepared once, not re-sorted every frame.
The source's `M102` duplicate record has an M101 cross-reference; its primary
M102 identity determines its position in this list, without altering the source.

Search is case-insensitive across common names, primary names and all catalog
identifiers. Spaces and identifier padding are ignored: `NGC 224`, `NGC0224`,
`M31` and `M 031` all find the Andromeda Galaxy. Multiple search words must all
match. An empty query shows every entry passing the two dropdowns. The result
dropdown is scrollable and does not truncate matches.

Selecting a result or clicking a drawn ring shows its names and identifiers,
J2000 position, constellation full name, selected catalog fields, NED notes and
OpenNGC notes at the bottom of the tab. Major/minor axes, parallax, proper motion,
radial velocity, redshift and source-code/reference lines are omitted from this
display; their original values remain available in the bundled CSVs and guide.
Constellation names are independent of the selected sky culture and include
OpenNGC's Serpens Caput/Cauda distinction. Scroll the window to see long information.
Where a positive catalog parallax is available, a cached **Distance** line shows
light years, labeled **from catalog parallax**: `1000 / Pax * 3.26156377695`,
with `Pax` in milliarcseconds. Missing, zero or negative parallax produces no
distance line. These CSVs have no separate distance column; redshift is not
converted into distance. This is a simple inverse-parallax estimate, not a vetted
distance measurement: catalog parallaxes, especially for extended/extragalactic
objects or their associated stars, may not represent the object's true distance.
The value is informational only and does not change overlay positioning.
One **Apparent Magnitude** line shows the integrated V magnitude when available.
Otherwise it falls back to B, then J, H, K, explicitly identifying the nonvisual
band; with none available it shows **Apparent Magnitude: N/A**. The catalog lacks
the object-specific color information/calibration needed to infer V reliably
from those bands, particularly for extended nebulae and galaxies, so no visual
conversion is assumed. Central-star magnitudes are not object magnitudes and
are not substituted or displayed as additional magnitude lines. Original
photometry remains unchanged in the bundled CSVs. This display fallback does
not alter the V/B ring-cutoff policy below.
Selection is independent of Stars/Multi, and a sky pick can select an object
outside the current search filters.

## Sky rings

Expand **Visibility Options** using its arrow button to show the object-type
controls, master toggle and column headings. This section starts collapsed on
each launch; collapsing it does not change visibility settings or stop picking.

Each drawable type has one row with an **on/off checkbox**, an apparent
**magnitude cutoff** slider and a **color swatch** opening the normal color picker.
All type checkboxes start off. Cutoffs start at 12 and span -5 to 25.
Settings are saved with the main window preferences and reset by Restore Defaults.
The **All object types** checkbox above the individual rows enables or disables
every drawable type without changing its magnitude cutoff or color. It is checked
only when every individual type is enabled; from a partial selection, clicking
it enables all types. Individual choices persist through the existing settings.

- Use the catalog checkboxes to limit the rings to NGC, IC, Messier or Other.
  Membership overlaps: with Messier enabled, M31 remains eligible even if NGC is
  disabled. Disabling every catalog hides every ring.
- Smaller magnitude numbers mean brighter objects. An object passes when its
  magnitude is **less than or equal to** the displayed two-decimal cutoff.
  V magnitude is used when available, otherwise B. B is not equivalent to V;
  central-star and infrared magnitudes are not substituted.
- **Include unknown magnitude (no V or B)** starts enabled. Such objects bypass
  only the magnitude cutoff, not the type/catalog controls. Turn this off to
  require a known magnitude. Many nebulae, including the Horsehead, lack V/B.
- **Show enabled rings / outlines even when this tab / window is closed** starts off.
  Enable it for persistent background rings; otherwise they appear only while
  Deep Sky is the open, expanded tab. Search filters do not affect rings.
- A selected object keeps its type color and gets an extra yellow outline and
  a name label. Rings are fixed-size screen markers, not angular-size outlines.
- Positions use the catalog's fixed J2000 RA/Dec, the same coordinate conversion,
  fine alignment and body-occlusion checks as other sky overlays. No distance,
  proper-motion evolution or observer parallax is inferred.
- Duplicate (`Dup`) and nonexistent (`NonEx`) entries remain searchable, but
  have no ring-control rows and never draw rings. Duplicate records retain their master-object cross-references.
  Records without coordinates are likewise searchable only.

Mouse selection starts enabled, sharing **Disable Mouse Clicking** with
Stars/Multi. It is active only while Deep Sky is visible and selects only drawn,
eligible rings, using the nearest ring center within 8 pixels (ties use the
snapshot's record order). ImGui windows and camera drags are ignored. Closing or
collapsing the window, changing tabs, disabling clicking, or starting another
picker stops Deep Sky picking, even when persistent rings remain visible.
As with existing overlays, vanilla star-binary mode disables rendering/picking.

Loading errors appear in the tab and are logged, with a retry button.

## Nebula outlines

Enable **Show nebula outlines** in Deep Sky. Outlines start off, and the toggle
is saved with the other window settings. They are independent of type checkboxes,
the master type checkbox, magnitude cutoffs and unknown-magnitude inclusion:
you can show nebula shapes without turning on their rings. Outlines share the
catalog visibility checkboxes, each object's type color, the display-scope toggle,
fine alignment and vanilla-mode restriction. Search filters do not hide outlines.
Selecting an outlined object makes its contours thicker.

The upstream simplified file is already a useful compact runtime format:
28,879 records describe 513 closed contours across 193 region names. RA is
decimal **hours**, Dec is decimal **degrees**, both J2000. `start` supplies the
object name, `vertex` adds a point, and `end` repeats the starting point to close
the contour. The raw Aladin exports use degrees for RA; those files are not used
at runtime. Each separate start/end sequence is an independent contour. There
are no Bezier control points or spline instructions.

OpenNGC's original outline process used Aladin's DSS2 Color imagery at three
brightness settings, then simplified the drawn contours with a 0.002-degree
tolerance. We render its existing short line segments, rather than inventing
smoothed curves that might overshoot or join disconnected features. All supplied
brightness contours are retained; the Stellarium file does not label their
individual brightness levels. Some shapes encompass other nearby objects, as
the upstream methodology explains; these are illustrative silhouettes, not
precise physical object boundaries.

Exact normalized primary names and unambiguous cross-identifiers link **441
contours to 182 catalog objects**, including `C9` -> `C009` and `M045` -> `Mel022`.
The following 11 region names have no unique drawable counterpart in these two
CSVs and are omitted, with a message in the log only:
`Antlia`, `Cepheus`, `CoronaAustralis`, `Cygnus`, `LBN1032`, `LBN1037`, `LMC`,
`Orion`, `Puppis`, `Scorpion`, `Vela`. We do not invent entries or assign them to
the nearest star/nebula. Objects without supplied contours still use rings.

While Deep Sky mouse picking is active, click anywhere inside a displayed
contour to select its catalog object. **Any clicked object ring takes priority**,
including double-star rings inside a nebula. Without a ring hit, overlapping
contours choose the smallest containing visible footprint; exact ties use
catalog ID. Disconnected contours remain separate, while nested brightness
contours form a selectable union. Clicks behind an occluding body cannot select
the hidden nebula. The same ImGui/drag/picker-ownership protections as ring
selection apply.

Contours crossing viewport edges or the camera plane are clipped before
projection. Only original contour edges are drawn, never synthetic lines along
the screen border. Body-occluded edge segments are omitted (endpoint/midpoint
checks, like constellation boundaries); at very high zoom these conservative
checks can leave small gaps near an occluding body's limb.

## Performance and caching

The CSVs load once in the background. Catalog membership, coordinates, dropdown
labels and search text are prepared once; search results and counts update only
when the query or search dropdowns change. The result dropdown submits only its
visible rows. Selected-object field/identifier/note strings are formatted on first
use and reused.

The eligible ring list is cached until type, displayed magnitude cutoff,
catalog visibility or unknown-magnitude inclusion changes. Steady frames compare
only the small settings snapshot rather than filtering all 14,033 entries.
Color and display-scope changes do not invalidate eligibility. Aligned sky
directions rebuild only when eligibility or fine alignment changes; colors,
slider text and font-dependent label widths are likewise reused until changed.
Replacing/resetting settings is detected without relying on a particular UI
control to invalidate a cache.

Camera/FOV projection and body occlusion still run each frame for eligible
objects so moving the camera or an occluding body cannot leave stale rings.
Mouse targets are rebuilt from those same projected rings only while this tab's
picker is active. No screen-position or occlusion cache assumes a static camera.
Outline source parsing and base-coordinate conversion happen once in the
background. Aligned contour vertices update only when fine alignment changes;
catalog-eligible contour lists update only when catalog visibility changes.
Conservative bounding spheres cull offscreen contours before projecting their
vertices. Frame buffers and click-clipping scratch buffers are reused; polygon
containment/clipping for selection runs only on clicks, after ring hit-testing.
Slider/color changes apply immediately; settings writes are coalesced until
250 ms after editing stops and the mouse is released, or flushed when leaving
the tab/closing the window.

## License and acknowledgments

**The catalog data are CC BY-SA 4.0**, not CC BY 4.0 and not MIT.
OpenNGC's [REUSE declaration](.reuse/dep5) assigns its files to Mattia Verga
(2023), under
[Creative Commons Attribution-ShareAlike 4.0 International](LICENSES/CC-BY-SA-4.0.txt).
Attribution and ShareAlike obligations apply when redistributing/adapting the
database. The snapshot is distributed without data modifications.
The [MIT license text](LICENSES/MIT.txt) is also retained because it is present
upstream; its presence does not relicense these CSVs as MIT. No upstream
Python interface or outline-processing scripts are included. The bundled
outline data and methodology fall under the upstream CC BY-SA 4.0 declaration.
The mod's own code license does not replace third-party data licenses.

OpenNGC credits the following sources (see its original README and the
per-object `Sources` column for details):

- [NASA/IPAC Extragalactic Database (NED)](https://ned.ipac.caltech.edu/):
  This research has made use of the NASA/IPAC Extragalactic Database (NED)
  which is operated by the Jet Propulsion Laboratory, California Institute
  of Technology, under contract with the National Aeronautics and Space
  Administration.
- [HyperLEDA](http://leda.univ-lyon1.fr):
  We acknowledge the usage of the HyperLeda database (http://leda.univ-lyon1.fr).
- [SIMBAD](https://simbad.cds.unistra.fr/simbad/):
  This research has made use of the SIMBAD database, operated at CDS,
  Strasbourg, France.
- [HEASARC](https://heasarc.gsfc.nasa.gov/): OpenNGC used the Messier, MWSC,
  LBN, planetary-nebula, LMC/SMC-cluster and Lynga-cluster tables.
- [Harold Corwin's NGC/IC Positions and Notes](http://haroldcorwin.net/ngcic/index.html).
- Some common names come from internet sources such as Wikipedia.
- Outline methodology: This research has made use of "Aladin sky atlas"
  developed at CDS, Strasbourg Observatory, France.

These are OpenNGC's upstream acknowledgments, not claims that Oh My Stars
independently queried those services or that every underlying source has the
same license. The upstream README and source codes retain their provenance.

## Validation

From the repository root:

```powershell
dotnet run --project tools\DeepSkyTests\DeepSkyTests.csproj
dotnet build src\OhMyStars.csproj --no-restore
```

The checks cover the bundled hashes and record counts, every type/catalog,
names and alternate IDs, M40/M45, coordinates, notes/CSV quoting, magnitude
boundaries, missing magnitudes, overlay eligibility, preference round-tripping
and invalid input. Cache tests cover every eligibility input, settings replacement,
unrelated changes, and zero managed allocations across 10,000 unchanged
eligibility/details reads. Outline tests verify the snapshot, catalog matches,
closed contours, invalid input, viewport/behind-camera clipping, concave shapes,
real M42 containment, RA wrapping, overlap and ring-priority selection, and
zero-allocation repeated picking. In-game visual placement and GUI interaction still require
manual verification.

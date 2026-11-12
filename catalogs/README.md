# Oh My Stars curated catalog (test revision 23, identity-first trial)

## Deep sky catalog

The new **Deep Sky** tab uses a separate, bundled, unmodified
[OpenNGC snapshot](OpenNGC/README.md): 13,969 NGC/IC records plus 64 addendum
records, including M40 and M45. It does not change the curated star catalog
or generated star binaries. Search, per-type ring controls, magnitude policy,
cross-identifiers, source acknowledgments and CC BY-SA 4.0 licensing are
documented in [OpenNGC/README.md](OpenNGC/README.md).

## Curated star catalog

[oms_catalog.csv](oms_catalog.csv) is a modified and supplemented **AT-HYG 4.0
CSV**, with one row per included individual-star candidate. It is not yet a
scientifically reviewed catalog. The mod now uses this CSV for Stars, Multi,
sky overlays and the default binary generator; it no longer ships the old
reduced AT-HYG CSV. Stars and Multi share stable OMS selection IDs.
The default binary is regenerated on upgrade with the existing magnitude and
colour settings, while user-created binaries remain unchanged.

Revision 24 imports the user's full in-game developer review: all 1,236
pending component requests from `dev\system-review-overrides.csv` (validated
against revision 23 with zero identity or type mismatches) now live in
[user-component-reviews.csv](user-component-reviews.csv), which the builder
applies as the highest-priority component role overrides after P, verified by
OMS ID and protected from ORB6 promotion. The import also added 7,873 system
approvals (7,883 total). For unreviewed components, two kinematic rules now
run before the WDS flag and extreme-motion heuristics: a combined
proper-motion difference versus the primary above 75 km/s
(4.740470 x |delta pm| x system distance) classifies V (2,138 components),
and otherwise a most-recent primary-anchored separation under 10 arcseconds
classifies C (29,003 components). The validator re-derives every role with
the same rules. Further manual review is paused until a Gaia DR4-based
source update improves proper motions.

Revision 23 applies the user's component review for Procyon, Sirius, Delta
Cephei, Almaaz, Spica, Polaris, Gliese 667A, Capella, Mizar and HD 98800.
The primary retains P; scoped C/V overrides take precedence over automatic
motion/optical classification and ORB6 promotion. HD 98800 is OMS 160137,
not unrelated OMS 160139, as confirmed by the user. Capella O/P are omitted
at the user's request, with their OMS IDs reserved; their proposed identity
as duplicates of H/L has not been independently established. Other source
fields and retained WDS measurements are unchanged.

### Developer review markers

[definitively-correct-systems.csv](definitively-correct-systems.csv) keeps the
user-declared definitive primary OMS IDs, paired with WDS IDs to detect stale
or incorrect identities. It is review metadata, not an independent scientific
certification. Component corrections themselves belong in the builder's
scoped curated rules and [oms-notes.txt](oms-notes.txt); editing this list alone
does not change C/V classifications.

In **Debug** builds, reviewed primaries have bright-green background highlight
rings when not selected, and selecting one displays green
**Definitively correct** text at the top-right of the Multi tab. Existing ring
and component filters still apply. The currently selected primary's background
ring is yellow, taking precedence over review green. Unselected, unreviewed
rings retain their ordinary light-blue color.

The top of Multi offers **Approve as correct** for an unapproved selected
primary, or **Undo approval** for an approved one. Changes update the green
indicators immediately and persist across restarts. No pending count is shown.
These buttons do not change component types or associations. Save failures
are displayed in Multi and logged, without changing the displayed approval.

Each component row also has a developer dropdown after its component name /
relationship text, followed by a notes checkbox. The dropdown offers **S -
Single**, **P - Primary**, **C - Companion**, **V - Visual**, **Q - Visual ?**,
**U - Relationship Unknown**, and **Remove / unlink**. It initially shows the
catalog's current type, then the saved requested type. Removal requests unlink
the star from this multi system; they do not request deletion of the star's
catalog identity. These are requests for later curation, not immediate changes
to catalog types, displayed relationship text, filtering, plots or membership.
Components lacking position data get review controls below the normal listing
in Debug too; ordinary component filters still apply.

Check the box after the dropdown to show a text field below the component row.
Notes save as you type, on the same CSV row as that component's requested type.
Unchecking clears the saved note. Notes may also be recorded without changing
the type. Selecting the original type and clearing its note cancels that
component's pending request. Save failures are displayed beside the component
and logged; a **Retry save** button retries the failed request. Saved selections
and nonempty notes reload on restart. System approval remains independent: use
**Approve as correct** once the system is correct *with its requested changes*.
Approving or undoing approval never clears component requests.

Runtime edits are stored in `dev\system-review-overrides.csv` under the
**loaded mod's folder**, separately from the shipped list. The CSV columns are
`primary_oms_id,wds_id,name,approved,component_oms_id,component_label,original_type,desired_type,notes`.
System approval rows record the primary OMS ID, WDS ID, name and `approved`
(`true` or `false`), leaving component fields empty. Component rows share the
primary identity, leave `approved` empty, and record the component's stable OMS
ID, current label/type, requested type (or `unlink`) and optional notes.
There is at most one approval row per primary and one request row per
component; later edits replace the corresponding row, not append duplicates.
CSV quoting preserves commas, quotes and multiline notes. Old four-column
approval CSVs still load and are migrated on the next successful save.
Component identity, original label/type and primary WDS mismatches fail
explicitly rather than applying a request to an unexpected star.
Local decisions override the shipped list, including undoing an existing
approval. This developer file is not packaged or overwritten by builds and is
not deleted when switching to Release.

When ready, ask the assistant to **apply my developer CSV / component reviews**,
or **import my system approvals** for approvals alone. For component reviews,
validate each primary OMS/WDS identity, component OMS ID, original label and
original type against the current catalog. Apply the explicit requests to the
builder's scoped curated rules, preserving stable IDs and unaffected source
data; unlink requests remove the system association, not the catalog star.
Check primary/single-role changes for valid resulting system structure rather
than blindly changing one role. Apply pending component requests even when
their system was previously approved; approval does not suppress corrections.
Record each applied catalog correction in [oms-notes.txt](oms-notes.txt), with
a brief explanation such as "User developer review: component B changed from
V to C", including any additional supplied component notes. Regenerate and
validate the catalog and its related artifacts as for other curated changes.
Do not write pending requests into the catalog or notes file during gameplay.

For system approvals, compare the local overrides with the repository list:
validate each OMS/WDS identity, add `true` rows and remove `false` rows in
`catalogs\definitively-correct-systems.csv`, retaining existing unaffected
rows. Report the changes, then clear only the imported local rows after
the source update and refreshed Debug deployment have succeeded, so the new
shipped baseline preserves the decisions after restart. Leave unprocessed rows
and all new edits intact. Later imports read the changes made since that
acknowledgement; no import or acknowledgement happens automatically.
If the mod was loaded from another instance folder, use that instance's
`dev\system-review-overrides.csv`, not an assumed installation path.

**Release** builds compile out the buttons, component review controls,
override loading/saving and both
indicators, do not load or ship the review
list, and remove a stale deployed copy. The source list remains available for
future catalog work. The default star binary version is advanced to remove
the suppressed Capella entries on next launch; user-created binaries are not
rewritten.

Run the focused review regression checks with
`dotnet run --project tools\MultiTests -- catalogs\oms_catalog.csv --reviewed-systems-only`
from the repository root. This checks all ten identities and assignments,
Capella's suppressed entries and reserved IDs, and the unchanged nearby OMS
160139 system, in addition to the synthetic Multi fixtures.

Revision 22 restricts automatic motion-only Q classification to extreme
discrepancies: the companion-minus-primary motion vector magnitude must be
strictly greater than 100 mas/year AND strictly greater than the primary's
total proper-motion magnitude (100%). Equality at either boundary remains C
unless independent evidence says otherwise. Missing vectors do not trigger Q.
This conservative heuristic is not a binding/parallax test. Explicit WDS
optical flags/notes, uncertain-identification rules and curated physical
corrections remain separate and unchanged.

Revision 21 explicitly classifies Almach B / OMS 22096 and C / OMS 369407
as physical C companions of A / OMS 22094. This curated exception supersedes
the supplied WDS A-BC S flag, which previously made B Visual. The
[Gamma Andromedae system description](https://en.wikipedia.org/wiki/Gamma_Andromedae)
describes A with its multiple secondary; the separate ORB6 BC orbit supports
the inner BC pair. The override is scoped to B/C, not D or other S-flagged
systems. Original WDS flags/measurements and AT-HYG fields remain unchanged.

Revision 20 fixes a systemic nested-pair coordinate assumption: a repeated
WDS precise system coordinate on a B-C row is not a direct position of B.
Only the designated system anchor uses that direct coordinate; other leaves
use an incoming individual-pair estimate when available, otherwise unknown
geometry, unless independently identified. Source measurements are unchanged.
For Almach, user-approved HD 12534 / AT-HYG 156597 / OMS 22096 represents B
of the close BC pair, preserving its potentially blended photometry/astrometry.
C remains a separate WDS-only star; no claim of individually resolved B
photometry is made. Duplicate WDS B / OMS 369406 is retired, not recycled.
The default WDS-only filter previously hid all companions; the representative
now makes the system accessible with that filter enabled. This revision initially
preserved WDS's S-flagged A-BC classification; revision 21 supersedes that
classification with a scoped correction. ORB6 BC evidence alone is not evidence for A-BC.
See [oms-notes.txt](oms-notes.txt) for the component IDs and source details.

Revision 19 trials identity-first matching before the positional fallback.
Unique component-scoped HD/HIP references are resolved against the full
AT-HYG input before assigning positions; referenced rows are reserved for
their identified component so another leaf cannot take them positionally.
Unique WDS DM-to-HD cross-references are also considered, but because they
can identify a system rather than a resolved component, require position
agreement within 6 arcseconds and magnitude agreement within 1 magnitude.
Conflicting/shared references fall back to the positional rules and are
recorded in `match-review.csv`. For an unexpanded primary A, unique
name/Bayer/Flamsteed aliases may identify a candidate only with position
agreement within 6 arcseconds and magnitude agreement within 1 magnitude.
These aliases are not used to identify individual leaves of blended groups.
The existing scoped Sirius, Izar and Epsilon Lyrae corrections retain priority.

Once a first endpoint is matched to AT-HYG, its original coordinates anchor
the companion search. The WDS separation/angle and observation epoch are
used with the existing WDS relative-proper-motion correction to estimate
the companion's J2000 position. If the epoch correction cannot be made,
the existing unknown-position rule still applies. Matched companions retain
their own original AT-HYG fields; unmatched entries use WDS-derived
positions or unknown geometry. Nested pairs are anchored only when their
exact first endpoint is already identified, never to an arbitrary blended leaf.
This is a comparison trial, not a claim of improved scientific accuracy.
The pre-trial builder/catalog/registry snapshot was preserved locally for rollback.
The default binary version changes so the trial rebuilds on the next launch;
user-created binaries are not regenerated.

Revision 18 corrects Izar / Epsilon Bootis (WDS 14450+2704): primary A is
AT-HYG 1462701 / OMS 196685 (named Izar, HD 129989), and B is AT-HYG
1462698 / OMS 196683 (HD 129988). The source WDS position is closer to B;
the preferred close-position match previously made B primary and left Izar
single. Both source rows are reused, the duplicate WDS-only B / OMS 357840
is retired with its ID reserved, and C / OMS 357841 remains independently
classified. Original source fields and pair measurements are unchanged.
See [oms-notes.txt](oms-notes.txt) for special-case handling.

Revision 17 corrects Epsilon Lyrae (WDS 18443+3940): AT-HYG 1915959 /
OMS 253708 remains Ca (HD 173608), while AT-HYG 1915960 / OMS 253709 is D
(HD 173607), not the 0.058-arcsecond CHR 77 Cb companion. Cb remains a
separate WDS-only entry with unknown magnitude; no photometry is invented.
The duplicate WDS-only D / OMS 372209 is retired without recycling its ID.
The main A-B and C-D double-double structure is retained alongside the
additional Ca-Cb subsystem. A remains P; B, Ca, Cb and D are curated physical
C companions, not motion-only Q/Visual. Original AT-HYG fields and WDS
relationships/measurements are preserved.

Revision 16 explicitly associates AT-HYG 586689 / OMS 84637 (Sirius B) with
Sirius A / OMS 84638 as category C, component B. Its original AT-HYG fields
are preserved; its historical position lies outside the normal WDS matching
radius. WDS Ba is normalized to B in this system, while optical neighbor Bb
is retained without treating B as an unresolved Ba/Bb subsystem. The duplicate
WDS-only OMS 376056 is retired, not recycled. Original WDS source lines,
measurement values and the registry remain unchanged. This scoped identity
correction also enables the exact A-B orbit curve.

Revision 15 also accepts an exact supported unique ORB6 pair match to the
original WDS source pair owned by a catalog row. This classifies the
measurement-owning representative leaf as C even when a parent endpoint
(for example Sirius B) has been expanded into Ba/Bb. It does not promote the
other subgroup leaves or assign the group orbit to an individual plot marker.
Sirius Ba (the white dwarf Sirius B) therefore remains a physical companion
despite the separate optical Ba/Bb relationship and discrepant source proper
motions. High relative proper motion alone is not proof of an optical pair.

Revision 14 uses the stored ORB6 snapshot as trusted orbital evidence:
companions previously classified Q or V become C when the runtime's supported,
unique individual-pair orbit match produces a plot curve. Primary P and
identification-uncertain U classifications are preserved. Group/blended,
ambiguous, unsupported and unmatched orbits do not change classifications.
This is a catalog-build step, not additional per-frame work. The snapshot is
required in the output directory when regenerating, and its hash is recorded
in the build manifest alongside the other inputs.

Revision 12 adds Q (Questionable): motion-only, tentative or conflicting
optical evidence is no longer presented as definitively Visual. Uncontested
S/U/Y-flagged pairs and explicit optical notes remain V; X dubious doubles
remain excluded. Albireo B (OMS 267899) remains in the Albireo source group
without duplicating its AT-HYG row. The CSV columns are unchanged.
Revision 13 adds a separate `bu` discovery-alias column, recognizes numbered
Greek aliases such as `eps 1 Lyr` as `Eps-1` plus `Lyr`, and expands positional
matching to 6 arcseconds. Candidates within the earlier 2-arcsecond direct /
3-arcsecond derived-position core remain preferred when present, to avoid
replacing established matches with another nearby component.
For expanded-only candidates with both magnitudes known, a difference greater
than 2 magnitudes rejects the positional link (not the star or WDS relationship).
This guard prevents selecting a much fainter neighbor, as tested for Sirius A/B.
Multi plots all component roles with usable geometry; unresolved
group locations are approximated by their primary/leading individual leaf
for display only, without altering catalog measurements.

This build preserves known AT-HYG fields verbatim, supplementing blank identifier
fields only with documented cross-reference evidence. Missing spectral classifications
(blank, `unknown`, `U`, `?`, `.` or `...`) may also be filled from WDS columns 71-79,
with `spect_src=WDS` replacing the old missing-spectrum provenance. Known AT-HYG
spectra and their provenance always win. A single WDS type applies only to the
first pair endpoint; a simple `+` separates first/second component types. Slash
qualifiers are retained, not split. Only exact individual endpoints are used:
group/blended endpoints are not expanded to leaves, and conflicting or complex
parenthesized spectra are left unassigned. WDS-only additions have blank AT-HYG IDs
and no invented Gaia ID, distance, absolute magnitude, color, or spectral type.
Shared Gaia IDs from AT-HYG are retained as supplied, not treated as duplicates
or reported solely for being shared. OMS and AT-HYG row IDs remain unique.
Positional associations and WDS-only identities are provisional: structural
uniqueness checks cannot prove that every physical star has been identified
correctly.

Output counts are recorded in [build-manifest.json](build-manifest.json) and
[validation.json](validation.json), not a completeness claim.
For distribution, check the generated size against GitHub's ordinary 100 MiB
per-file limit; choose Git LFS or release-asset/compressed distribution if
necessary. This builder does not configure LFS or upload data.

## Files and reproducibility

Alpha Centauri (WDS 14396-6050) uses Rigil Kentaurus as primary A
(AT-HYG 1455072 / OMS 195683), Toliman as B (AT-HYG 1455030 / OMS 195677),
and Proxima Centauri as C (AT-HYG 1440825 / OMS 374371).
The historical SCZ 1 Ca-Cb entry is excluded: the supplied WDS notes explicitly
say "Not a binary!" This agrees with the
[three-star system description](https://en.wikipedia.org/wiki/Alpha_Centauri#Physical_properties).
Existing identities and original AT-HYG fields are retained; duplicate WDS-only
B/Ca/Cb identities are retired without recycling IDs. For this system the full
precise WDS separation field is read, preserving AC's 7960.200 arcseconds rather
than dropping its leading digit. The global parser correction is deferred by
user choice because it changes other component associations. The default binary
rebuilds on the next launch.

Iota Cassiopeiae (Huagai, WDS 02291+6724) retains the five physical components
Aa, Ab, B, Ca and Cb. B uses its distinct STF 262 measurements, not the close
CHR 6 Ab data. The BC row's repeated system DM is not assigned to B as an
individual identifier; doing so previously removed B as a duplicate of Aa.
Ca/Cb are curated physical companions despite the historical optical flag.
The separate distant D entry is curated optical/Visual (V), outside the physical
quintuple, following the user's correction. Source measurements, reserved OMS
identities and the two different supplied orbital solutions are preserved;
no outer C orbit is invented. See [oms-notes.txt](oms-notes.txt) for the evidence.

The editable [component identity review](component-identity-review.txt) records
59 other identifier-driven omission events across 58 systems (47 DM-to-HD and
12 HDS-to-HIP links). These remain unresolved, not automatically restored.
Add research and explicit decisions there for later scoped catalog corrections;
the builder does not overwrite or execute that maintained file.

- [oms_catalog.csv](oms_catalog.csv): authoritative star catalog for this draft.
- [identity-registry.csv](identity-registry.csv): persistent `source_key,oms_id`
  assignments. Keep this file when rebuilding; do not recycle IDs. Registry
  entries absent from a later CSV remain reserved.
- [match-review.csv](match-review.csv): remaining actionable identification,
  hierarchy, or contradictory-status issues. Columns are
  `wds_id`, `discoverer`, `components`, `source_line` (1-based in the precise
  WDS input), and `reason`. Routine exclusions, resolved errata, shared Gaia
  IDs, and unavailable geometry do not produce review entries.
- [build-manifest.json](build-manifest.json): input paths and SHA-256 hashes,
  catalog hash, output counts, matching thresholds, and limitations.
- [validation.json](validation.json): results of independent output validation,
  generated by `--validate`, not an assertion of scientific identity certainty.
- [identifier-additions.csv](identifier-additions.csv): audit of filled blank
  catalog identifiers: `source_key`, `column`, `value`, and `evidence`.
- [oms-notes.txt](oms-notes.txt): maintained special-case handling record,
  shipped with the catalog and not overwritten by generation. Each entry is
  one line beginning with comma-separated affected OMS IDs, followed by
  another comma and the note; a blank line separates entries. Include source
  catalog identities/evidence and retired duplicate IDs where relevant.

The zero-dependency .NET 10
[builder](../tools/CatalogBuilder/Program.cs) does not reference game assemblies,
deploy files, change settings, or switch the runtime catalog. From PowerShell:

Keep this entire folder **and** `tools\CatalogBuilder` together in the repository.
The convenience script compiles/runs fixtures, builds in this folder, then
validates without deleting or resetting the identity registry:

```powershell
.\catalogs\Generate.ps1
# Alternate source snapshot, preserving the required subfolder/file names:
.\catalogs\Generate.ps1 -SourceRoot 'D:\path\to\catalogs'
```

Prerequisites: .NET 10 SDK, the full local source snapshot described below,
and the existing `identity-registry.csv`. The sources are not downloaded by the
script. The current default root is `D:\ksa\stars\catalogs` with subfolders
`AT-HYG_40`, `WDS` and `Henry Draper`; exact input names and SHA-256 hashes
are recorded in `build-manifest.json`. Archive the accepted CSV, registry,
manifest, validation and builder revision together **before** replacing inputs.
Never run concurrent builders or reset the registry when updating a source.
Unchanged inputs/code/registry must reproduce the CSV; the registry also
preserves reserved IDs for rows retired by later builds. The manifest is
overwritten on regeneration, so retain old manifests when comparing snapshots.

Equivalent manual commands:

```powershell
dotnet run --project .\tools\CatalogBuilder\CatalogBuilder.csproj -- --self-test
dotnet run --project .\tools\CatalogBuilder\CatalogBuilder.csproj -- 'D:\ksa\stars\catalogs' '.\catalogs'
dotnet run --project .\tools\CatalogBuilder\CatalogBuilder.csproj -- --validate 'D:\ksa\stars\catalogs' '.\catalogs'
```

Do not run concurrent builders against the same output directory. The output
CSV is staged before replacement, but the directory is not a transactional
runtime asset set. Rebuild/validate offline before using any output.

The mod build deploys only `oms_catalog.csv`, this README and the source
manifest under the installed mod's `catalogs` folder. It keeps the builder,
identity registry, review and validation assets here for future regeneration.
No catalog copy under `src` is necessary. Rebuild the mod after regeneration;
restart the game to reload Multi's in-memory catalog.

Runtime model checks (no game assemblies required):

```powershell
dotnet run --project .\tools\MultiTests\MultiTests.csproj -- .\catalogs\oms_catalog.csv
```

This tests CSV parsing, theta/rho layout, group/unknown geometry handling,
nearest-ring selection and the complete generated catalog. In-game checks
still require KSA: open Multi, toggle rings, pick isolated/overlapping
primaries, drag without selecting, click UI without selecting, switch tabs,
inspect a nested system and unknown geometry, and compare markers after
camera motion/alignment changes.

Multi also has a saved 0–20 **component apparent magnitude cutoff** (default
20), independent of the primary-ring cutoff. Fainter C/U/V/Q members remain
listed in a separated bottom section with plot checkboxes unchecked by default.
Checking one overrides the cutoff for plotting, not listing or ring eligibility. Equality and
unknown magnitudes remain visible; the system primary is never filtered.
Systems with no non-primary member passing this cutoff have no background
ring and cannot be selected through the sky picker. Unknown-magnitude members
qualify; plot checkboxes and missing geometry do not affect ring eligibility.
The combined eligible-primary marker list is cached until either apparent
magnitude cutoff changes; only those markers need per-frame camera projection.
The existing selection remains inspectable even when its ring disappears.
Per-component checkboxes hide plot points without hiding catalog information.
The same-size plot supports 1x–10x zoom, east/north pan sliders and a reset
button, all below the plot. Visibility overrides and plot navigation reset on system selection.
These presentation controls do not modify catalog data or relationship layout.
Multi prefixes primary and component information-list entries with `*` when
`radec_source` is exactly `WDS`; plot labels and catalog IDs are unchanged.
The saved, default-on **Exclude WDS-only entries** toggle below the component
magnitude cutoff hides these entries entirely, including plot points and
their orbit curves. It excludes WDS-only primaries and companions from cached
ring eligibility, without deleting their relationships or changing geometry.
Plot checkbox overrides cannot bypass this filter. Disable it to show WDS-only
entries again.
The plot has no information text beneath it; members without usable geometry
cannot be plotted even if checked.

## Encoding and missing values

For a standalone, application-independent guide to the CSV columns, units,
relationships, classification and limitations, see
[OMS catalog reader's guide](oms_catalog_readme.md). The information here
is retained alongside the broader construction and development documentation.
The distributed catalog includes that guide and [attributions.md](attributions.md).
This full README and `build-manifest.json` remain source-only; neither is
needed by the runtime. The separate ORB6 source manifest remains distributed
with its unmodified snapshot.

UTF-8 without BOM; comma-delimited, double-quoted where required, with embedded
double quotes doubled. Read catalog identifiers as strings/exact integers,
especially Gaia's large IDs. Empty scalar cells mean unavailable; never infer
zero. There are no JSON cells. `wds_id` is plain text such as `00050+6631`,
not an array or quoted JSON string. The positive sign is real and is not a
hyphen: it indicates north declination. JSON's former `\u002B` escape meant
`+`; no such encoding is used here.

Alias lists use semicolons. Measurement lists use `|` with aligned entries;
an empty entry means an unknown value for that measurement. Standard CSV
quoting still applies to cells that actually contain commas, quotes or
newlines in retained source fields; it is not added to plain WDS IDs.
All columns are listed below in file order.

## Original AT-HYG columns (retained after `oms_id`)

Coordinate frame/equinox and measurement epoch are different concepts. The
supplied RA/Dec is used in its J2000 frame with **no additional precession**.
Original provenance fields remain unchanged except that filling a missing spectral
classification from WDS sets `spect_src=WDS`. A Gaia distance/ID alone does
not certify that a row's RA/Dec was measured by Gaia.

| Column | Meaning and units |
|---|---|
| `oms_id` | First column. Positive numeric OMS identifier for this individual-star candidate. Stable through the persistent registry, not a component code. |
| `id` | Original AT-HYG 4.0 row ID; blank for a WDS-only star. Not an OMS ID. |
| `tyc` | Tycho-2 identifier, including its hyphenated components. Existing AT-HYG IDs are authoritative; blank identifiers may be filled by the documented cross-reference pass. |
| `gaia` | Gaia DR3 source identifier as supplied by AT-HYG. Preserve every digit. |
| `hyg` | HYG source row identifier; `0` is valid for Sol. |
| `hip` | Hipparcos identifier supplied by AT-HYG; can refer to an unresolved source. |
| `hd` | Henry Draper identifier. Prefer existing AT-HYG HD; fill only when absent and supported by a unique cross-reference. Can refer to an unresolved source. |
| `hr` | Harvard Revised / Yale Bright Star Catalog identifier. |
| `gl` | Gliese/Gliese-Jahreiss designation, preserving suffixes. |
| `bayer` | Bayer designation from AT-HYG. |
| `flam` | Flamsteed number from AT-HYG. |
| `con` | Three-letter constellation abbreviation. |
| `proper` | Proper name supplied by AT-HYG. |
| `ra` | Right ascension in **hours**, in the supplied J2000 frame. WDS-only component positions may be derived; unknown coordinates are blank. |
| `dec` | Declination in **degrees**, in the supplied J2000 frame. |
| `pos_src` | Original source's position provenance, preserved verbatim (for example `T` for Tycho). WDS-only rows use `WDS`. |
| `dist` | Distance from Sol in parsecs; preserved, never copied from a primary to an unknown companion. |
| `x0` | Source equatorial Cartesian X coordinate, parsecs, as supplied. Not a game-space vector. |
| `y0` | Source equatorial Cartesian Y coordinate, parsecs. |
| `z0` | Source equatorial Cartesian Z coordinate, parsecs. |
| `dist_src` | Source provenance for distance/parallax. |
| `mag` | Apparent magnitude in the source's photometric system; do not assume all bands are Johnson V. |
| `absmag` | Source absolute magnitude (apparent magnitude at 10 pc), where available. |
| `ci` | Source color index: typically B-V; Tycho rows (`mag_src=T`) use BT-VT. Not an RGB color. |
| `mag_src` | Original photometric provenance; `WDS` for WDS-only rows. Consult the referenced WDS source line for band/quality flags. |
| `rv` | Radial velocity, km/s, as supplied by AT-HYG. |
| `rv_src` | Radial-velocity provenance. |
| `pmra` | Right-ascension proper motion including cos(dec), milliarcseconds/year, as supplied. |
| `pmdec` | Declination proper motion, milliarcseconds/year. |
| `pm_src` | Proper-motion provenance. |
| `vx` | Source equatorial Cartesian X velocity, parsecs/year, as supplied. |
| `vy` | Source equatorial Cartesian Y velocity, parsecs/year. |
| `vz` | Source equatorial Cartesian Z velocity, parsecs/year. |
| `spect` | Source spectral classification, including qualifiers. |
| `spect_src` | Spectral-classification provenance. |

AT-HYG source tags include `T` (Tycho), `HIP`, `HYG`, `G_R3`/`G_R2` (Gaia
release 3/2), `OTHER`, and `N` (unavailable), depending on the field. Tags are
not translated or homogenized. Sol's special source values are also retained.

## Added OMS and supplemental columns

| Column | Meaning |
|---|---|
| `system_id` | Numeric ID of the WDS group's designated primary/anchor OMS row; blank for ungrouped S/U. Retained for I-flagged U, optical V and questionable Q rows as a source relationship, not proof of physical membership. This draft does not yet merge differently named WDS systems automatically. |
| `component` | `S` = Single; `P` = Primary; `C` = Component; `U` = Unknown identification/classification; `V` = Visual, from uncontested explicit optical notes or WDS S/U/Y flags; `Q` = Questionable physical versus optical relationship, from motion-only differences **greater than 100 mas/year AND greater than 100% of primary motion**, tentative optical evidence or conflicting physical/optical evidence. I-derived U takes precedence over P/C/V/Q. S does not prove physical singleness. Multi displays Q as `(Visual ?)`, otherwise treating it like V. |
| `primary_oms_id` | Designated primary/anchor's OMS ID, including for grouped U rows; self for the anchor and ungrouped S/U. A U anchor retains this reference without asserting primary identification is reliable. |
| `component_label` | Individual-star hierarchy label, such as A, B, Aa, Ab, Aa1 or Aa2. Encodes the structural path: Aa1 means A/Aa/Aa1. Distinct from `component`'s S/P/C/U/V/Q role codes. Blank only for ungrouped S/U. |
| `radec_source` | User-requested OMS code: `GIA` for RA/Dec retained from AT-HYG; `WDS` for WDS-only positions. `GIA` denotes AT-HYG input here, **not a claim of independently verified Gaia astrometry**. Consult `pos_src`. |
| `h` | Semicolon-separated Herschel aliases, preserving list/class and number. On the system-primary row; system/pair-scoped, not automatically individual-component identifiers. |
| `stf` | Semicolon-separated F. Struve aliases, including STFA/STFB where applicable. Same system/pair scope. |
| `stt` | Semicolon-separated O. Struve aliases, including STTA where applicable. Same system/pair scope. |
| `bds` | Semicolon-separated numeric Burnham Double Stars catalog IDs from `wdsidsadsbds.txt`. Stored on the system primary, matched by WDS ID and accepted discovery designation; not automatically individual-component identifiers. Missing IDs and cross-reference rows marked for omission are ignored. |
| `bu` | Semicolon-separated numeric Burnham **discovery designation** aliases from accepted WDS `BU` discoveries, stored on the system primary. This is distinct from BDS numbering: HD 5005 is `BU 1` and `BDS 455`, not BDS 1. Runtime displays these separately as `Burnham 1` and `BDS:455`. |
| `wds_id` | Plain signed WDS coordinate identifier, e.g. `00050+6631`. Retained for grouped U/V; blank for ungrouped S/U. No square brackets, JSON escaping or surrounding JSON quotes. |
| `relationships` | Directional measurement endpoints: `A>B`, `Aa>Ab`, `Aa1>Aa2`, or `AB>CD`. Left is the reference, right is the measured companion/group. Pipe-separated and aligned with theta/rho/year/source-line lists. Group endpoints do not imply a measured barycenter. |
| `theta_deg` | Last WDS position angle, degrees north through east, unprecessed for its observation date. Unknown values are empty entries. |
| `rho_arcsec` | Last WDS separation in arcseconds; unknown values are empty entries. |
| `measurement_year` | Last observation year of each listed theta/rho measurement, not automatically the absolute coordinate epoch. |
| `wds_source_line` | 1-based line in `wds_precise.txt` for each measurement; identifies its flags, magnitudes and discovery designation without embedding the source record in the CSV. |

The CSV has 51 columns. The old `wds_pairs`, `wds_ids`, `hierarchy_path`,
`parent_component`, `relative_to_component`, and `measurement_components`
columns are removed. Structural information uses just `component_label` and
`relationships`, alongside the existing system/role identifiers.
For example `Aa2` derives path A/Aa/Aa2 and parent Aa; `AB>C` describes C
relative to the AB group, not an additional star. `A>C|B>C` means two measures
of C, first relative to A, then B. Reconstruct the reference endpoint by
splitting each relationship at `>`; no separate reference column is needed.

`wds_notes` is also removed to reduce file size. The builder still reads WDS
notes to resolve identities and classifications, but exports no narrative
prose. Consult the original `wdsnewnotes_main.txt` by WDS ID for those notes.
The source manifest records that file's hash.

Every accepted source measurement appears once: on the first individual leaf
of its second endpoint. For example an `AB>CD` measure is stored on C (or its
first descendant), but explicitly names **AB>CD**, not AB>C. This is storage
ownership, not a claim that a group measurement locates an individual leaf.
All measurement columns use the same pipe-list length; blank entries retain
alignment. The WDS source file remains necessary for details intentionally
not duplicated in this simplified catalog.

For WDS-only derived companions, the builder projects last theta/rho from the
WDS subsystem-primary position on the sphere. When both components have WDS
proper motions, it subtracts their differential motion from `measurement_year` to
2000. Without both motions, measures more than five years from 2000 produce
blank derived RA/Dec, **not** a review warning or a missing star. Direct
subsystem-primary coordinates from another accepted pair take precedence over
a derived position. Missing theta/rho becomes blank in measurement columns, not a reason
to remove a companion. Known measurements and dates are retained even when
a J2000 projection is unknown. Future plots must skip unavailable geometry
and label it unknown.

This corrects a linear epoch offset, **not orbital motion**; derived positions
remain provisional and do not overwrite matched AT-HYG positions.
Ordinary WDS proper motions are arcseconds/1000 years, numerically equivalent
to mas/year. Flag P identifies arcseconds/100 years: multiply by **10** for
mas/year on both axes and both components where supplied. Original flags are
available through `wds_source_line`, but all exported proper-motion numbers are normalized; consumers
must not apply the factor again. WDS-only rows also receive these normalized
motions in `pmra`/`pmdec`, with `pm_src=WDS`. AT-HYG motions remain unchanged.

### Hierarchy interpretation

Uppercase letters label the widest components; lowercase letters subdivide
them (Aa/Ab), and numerals subdivide again (Aa1/Aa2). Abbreviated `Aa1,2`
expands to Aa1/Aa2. Commas or hyphens separate endpoints; plain `AB` is A/B.
In an endpoint such as `AB-C`, AB is the group containing A and B, recursively
including their known descendants. `AB-CD` is a relation between two groups.
These groups do **not** become additional star rows.

Only leaves become individual-star rows. A described split of A into Aa/Ab
means no extra WDS-only "A" row. The first leaf down the A branch is designated
the primary; retain its exact label, rather than renaming it A. No combined
group magnitude, proper motion or coordinate is copied onto an individual
leaf. A leaf without a direct measurement remains blank where appropriate.
This hierarchy encodes catalog organization, not proof of gravitational
binding, orbital direction or a mass-weighted barycenter.

### Visual and Questionable classification

The following WDS/motion rules establish the initial classification. A supported
ORB6 individual-pair match that can produce a runtime plot curve then promotes
its Q/V companion to C, overriding optical or discrepant-motion evidence.
This does not override an identification-uncertain U or a primary role.

For each companion, compute
`sqrt((pmra - primary_pmra)^2 + (pmdec - primary_pmdec)^2)` in mas/year.
Values **strictly greater than 100 mas/year AND strictly greater than the
primary's total proper-motion magnitude** give Q when motion is the only
evidence. Equality at either boundary remains C unless separate evidence
establishes V/Q (or an I flag establishes U). Prefer each
available AT-HYG axis; where unavailable, use a direct component-scoped WDS
measurement, normalized to mas/year, preferring the latest observation year
(then source-line order). If either component lacks a complete vector, keep C
rather than invent a motion-based classification. Blended-group motions are not leaf
motions. A motion difference alone does not establish that a pair is unbound;
Q explicitly preserves this uncertainty. Neither V nor Q excludes the star.
Disagreements between WDS and AT-HYG proper motion neither reject matches nor
create review entries or independently cause Q. When both catalogs supply
motion, AT-HYG wins. The threshold compares two stars' authoritative motion vectors,
not two catalogs' measurements of the same star. Retain the WDS relationship
and geometry for future visual-companion display; V is not a physical
membership assertion.

Definite optical wording, including "Optical pair(s), based on study of relative motion"
and "C is optical", classifies
the named relationship's second-endpoint leaves as V even without complete
motion data or an extreme motion difference. For example, `AC and AD` marks
C and D, not A or unrelated companions. Group endpoints expand to their
individual leaves. Without explicit component labels, use the note's
discovery designation and apply to all its supported relationships in that WDS
group. Existing primary rows stay P; optical classification changes companion
leaves only. I-derived U still takes precedence.
Tentative wording such as "probably optical", "possibly optical", or an
uncertain optical/physical status gives Q for the scoped companions, even if
another note or S/U/Y flag calls them optical. Explicit physical/bound or
"not optical" statements conflicting with V evidence also give Q. A physical
statement alone does not change a C companion into Q. These are conservative
text heuristics over the supplied WDS notes, not a new scientific assessment;
they cannot identify every dispute or infer binding from missing evidence.
Explicit optical notes about excluded or absent relationships do not revive
those relationships or quarantine unrelated accepted companions.
Other unresolved existence/status wording excludes the affected WDS group,
as described below.
Resolved optical wording alone no longer quarantines an entire group.
Named components inside longer sentences are scoped individually: "this is a
2+2 hierarchical multiple. C is optical" marks C, without changing B/D or
inventing unresolved subcomponents from the 2+2 description. General optical
wording without named components uses the active discovery designation.

### Explicit component aliases and historical measurements

Statements such as "The A component is the D component of 00567+6043" are
parsed as cross-WDS identity aliases, including reciprocal/transitive links.
When an explicitly equivalent individual endpoint has already been assigned,
the later conflicting relationship is omitted under the first-assignment
policy; no second star is created. The manifest records parsed links and
alias-based omissions. Source notes retain the original labels. An alias
between parent groups does not establish which resolved leaf is identical
to another individual star; no such leaf identity is invented.

Measurement-only commentary stays in the original WDS notes file, not in the
CSV and not as a reason to
quarantine stars: deleted/rejected measures, orbital/spectroscopic solutions,
Hipparcos acceleration or solution disagreements, historical proper-motion
problems, and old plate/spectral measurements do not override AT-HYG fields
or the current precise-last WDS measurement. Eccentricity, periods and orbit
models are not imported as independent catalog fields.
In particular, a historical "spectroscopic" description alone does not
remove, merge, hide, or reclassify an individually listed component, and
does not establish that it is currently unresolved. A visually resolved
component may itself contain a spectroscopic subsystem; the descriptions
are not necessarily contradictory. Retain supported individual entries and
measurements, but do not invent extra stars or subsystem identities from
that narrative alone.
The optional ORB6 orbit overlay uses its separate structured snapshot below,
not orbital values guessed from these narrative WDS notes.

The parser considers the local measurement context and the active discovery
section, including continuation lines. Explicit restored/confirmed status
supersedes earlier doubt. It does **not** dismiss warnings that a companion,
pair, or detection itself may be spurious, a ghost, or unconfirmed merely
because the same paragraph discusses observations.

Historical alternative discovery designations in the errata are not in
themselves unresolved identities: the current WDS records pass the normal
position/component/identifier matching and duplicate checks. Explicit scoped
errata deletions and renames still apply. Acceptance and omission counts are
recorded in the manifest.

### Uncertain WDS identification

I means the object at the precise position may not be the intended WDS pair.
Retain supported I-flagged relationships and classify all their endpoint
leaves as U, including the designated primary when affected. U takes precedence
over the motion/optical V/Q rules. Preserve source relationships, IDs and
measurements for inspection without claiming reliable identification or physical
membership. I by itself no longer generates a review entry. Other independent
identity conflicts and exclusion flags are still handled normally.

### Filling missing catalog IDs

- Existing nonempty AT-HYG identifiers always win, including HD.
- Match WDS primary/subsystem-primary DM to the HD catalog's DM identifier,
  retaining declination sign and selecting BD/CD/CP by the WDS convention.
  Only exact, unique DM matches without supplemental/component ambiguity are
  accepted. Deleted HD records and HD 194648 are excluded.
- Use HDS-to-HIP cross-references only for an explicitly individual first
  endpoint; do not assign a group HIP ID to every resolved child.
- Parse explicit component-scoped `A is HD ...` / `B is HIP ...` notes.
- Search the full AT-HYG catalog by exact Tycho/HIP/HD/HR/Gliese/HYG/name and
  constellation-qualified Bayer/Flamsteed references, including WDS `bf.txt`
  designations when the primary is not an unresolved group. Require a unique
  matching source row. Reuse that full AT-HYG row and its astrometry rather
  than publishing a second WDS-only copy. If that exact source star already
  belongs to another relationship, retain the first assignment and remove
  the later duplicate's relationships and orphan rows. Independent
  magnitude-limited AT-HYG rows remain even if their group is removed.
  Remove identifier-audit entries for discarded rows. Genuine conflicting
  identifiers on an already matched AT-HYG row remain reviewable.
  Fill only blank identifiers and audit every retained addition.
- Herschel/STF/STT aliases use their supplied WDS cross-reference tables and
  discovery designations, preserving class/appendix distinctions.
- The Bayer/Flamsteed `bf.txt` pass resolves a discovery designation only
  when its accepted relationships identify one first endpoint and that
  endpoint is an individual catalog row. It fills blank `bayer`, `flam`
  and `con` cells from consistent scoped aliases. Numbered Greek aliases
  are case-insensitive, normalize spaces/hyphens to AT-HYG form (`eps 1 lyr`
  becomes `Eps-1`, `Lyr`), and take precedence over unnumbered variants.
  Conflicting alias values are not guessed; nonempty source fields are never
  overwritten. A Bayer alias is not by itself used to merge AT-HYG identities:
  the designation may also refer to a blended/unresolved source. Every field
  filled this way is recorded with `bf.txt`, WDS ID, discovery and endpoint
  evidence in `identifier-additions.csv`.
- `BU` discovery aliases are recorded separately in `bu`; they do not
  replace, synthesize, or renumber BDS aliases.
  Multi's optional identifier list expands `stf`, `stt`, and `h` to
  `F. Struve`, `O. Struve`, and `Herschel`, followed by their numbers.
  Appendix letters and Herschel classes remain visible; `hd` displays as
  `HD <number>`. Raw CSV identifiers are unchanged by display formatting.
- BDS aliases use `wdsidsadsbds.txt`, matching both WDS ID and accepted
  discovery designation. Keep multiple IDs as a semicolon-separated list on
  the primary. Ignore missing numbers, rows without a WDS ID, and rows with
  a nonempty Omit field; do not infer BDS numbers from BU discovery numbers.

Missing IDs remain blank when these local sources do not provide an
unambiguous link. No historical HD coordinate nearest-neighbor guess is
used; no HD spectral/photometric objects are added.

## Selection, matching, and limitations

The remaining unsupported/malformed hierarchy records and uncertain-existence
WDS groups are now **excluded**, not queued for review. No stars, measurements,
aliases or notes are imported from excluded groups; unsupported individual
relationships do not prevent other supported relationships in a group.
Independently valid magnitude-limited AT-HYG stars remain unchanged and do not
receive an Unknown classification merely because of an excluded WDS record.
The manifest records excluded hierarchy-pair and uncertain-existence group/pair
counts. A header-only review CSV means no unresolved import issues in this
snapshot, not that excluded records were accepted or scientifically resolved.

1. Start with finite AT-HYG apparent magnitudes `<= 10`, including Sol.
2. Retain separate AT-HYG entries even when their Gaia IDs are shared.
   Do not merge, omit or warn about them solely on that basis.
3. Retain WDS S/U/Y relationships as visual associations: the secondary
   endpoint's individual leaves receive V (the anchor stays P; I-derived U
   takes precedence). These flags indicate nonphysical pairs, not nonexistent
   stars. Continue rejecting X dubious-double relationships. If another discovery designation gives
   the same endpoints with conflicting acceptance, keep that relationship in
   review. Apply the explicit scoped deletions/renames transcribed from
   `wdserror.txt`; historical alternative designations use normal duplicate
   checks rather than a blanket designation-based quarantine.
   Excluded flags are checked before attempting to parse missing coordinates,
   so an excluded record cannot reappear as an invalid-coordinate warning.
4. Seed WDS groups with an A-branch primary endpoint magnitude `<= 10`.
   Where the endpoint is a group, this is its reported group magnitude, not
   a fabricated leaf magnitude. Fainter companions are allowed.
   Recognized nested/group relationships use the leaf model above.
   Unrecognized labels are excluded without rejecting other supported
   relationships. I-flagged endpoint stars are retained as U, not reviewed
   solely for I. P is a unit conversion, not an identity warning.
5. Search **all 2.5M+ AT-HYG rows**, including faint stars, before adding a WDS
   identity. Search within 6 arcseconds **of the WDS primary's position**, or
   6 arcseconds **of the companion's own derived position**, not within that
   distance of the overall primary. There is no system-separation cutoff.
   Select the nearest positional candidate; positions tied within
   0.1 arcseconds of the nearest use the closest source magnitude, with no
   minimum magnitude advantage. Missing magnitudes sort after known values;
   remaining ties choose the lowest AT-HYG ID. These user-approved
   deterministic associations do not generate uncertain-crossmatch reviews,
   but remain provisional scientific identifications.
6. Trust a WDS component's existence when no AT-HYG match is established.
   There is **no** minimum companion separation, within-system proximity
   veto, or 60-arcsecond inter-system/new-star exclusion. Repeated labels in
   the same WDS system reuse the same row. Unknown distances and coordinates
   remain blank; unknown geometry does not remove the star or create review.
   When a candidate AT-HYG star already belongs to another system, retain its
   first assignment and omit the later conflicting relationship. Do not create
   duplicate star rows or renamed B2 aliases for that shared star. Aggregate
   omitted-assignment counts are recorded in the manifest. Unconnected WDS-only
   rows from discarded relationships are removed.
7. Keep known AT-HYG field values unchanged even when WDS differs. Missing
   spectral classifications may be filled from component-specific WDS evidence,
   updating `spect_src` to `WDS`; known classifications always take precedence.
   WDS-versus-AT-HYG proper-motion differences have no matching threshold
   and do not generate review messages. Classify companions as V by scoped
   definite optical/nonphysical evidence, or as Q by tentative/conflicting
   evidence or the extreme absolute-and-relative motion rule.

### Other possible review reasons

- `overlapping_hierarchy_endpoints`: both ends expand to overlapping leaves.
- `identifier_link_conflicting_identity_<label>`: a catalog-ID cross-reference
  contradicts an already matched AT-HYG identity. A WDS-only row definitively
  linking to an assigned source star is instead pruned as a later duplicate.
- `conflicting_excluded_pair_identity`: unresolved contradictory status
  between accepted and excluded structured records for the same endpoints.
- `invalid_source_row`: genuinely malformed input, not routine missing
  geometry or a normally excluded pair.

These are actionable cases, not a listing of routine omissions. Aggregate
exclusion/conversion counts remain available in the manifest.

Unresolved narrative existence/status problems are conservatively excluded at group
scope; this can withhold valid relationships alongside an invalid pair.
Herschel/STF/STT aliases are system/pair-scoped. The source synonym table and
HIP problem table are not automatically interpreted in this draft. There is
no universal exact WDS-to-Gaia crossmatch or handling for every unusual label,
and no scientific guarantee of zero physical duplicates yet.
`wdsnewnotes_main.txt` is narrative notes, **not** the WDSS summary catalog;
W-marked coverage remains incomplete.

Do not point the current generic star generator at this CSV as a production
default: it does not yet honor the `component` classification and would render
secondary components.
Runtime reader/binary integration and the multiple-system plot are separate
pending work. See the [design/handoff notes](../docs/curated-star-catalog.md).

## Sources, references, attribution, and licensing

The manifest identifies the precise local snapshots used; external catalog
websites can change after a snapshot was obtained.

### AT-HYG 4.0 (base star data)

- David Nash, Augmented Tycho-HYG (AT-HYG):
  <https://astronexus.com/projects/at-hyg>.
- Inputs: `athyg_40.csv`, with its supplied `LICENSE` and `ACKNOWLEDGMENTS.md`.
- Licensed **CC BY-SA 4.0**:
  <https://creativecommons.org/licenses/by-sa/4.0/>.
  Repository [license text](../licenses/CC_BY-SA_4.0.txt).
- This draft modifies the selection, adds OMS columns, cross-references and
  WDS information. Source-shared Gaia identifiers are retained.
  AT-HYG-derived material retains its attribution/share-alike obligations.
- Upstream acknowledgments include ESA's Hipparcos/Tycho catalogs (1997,
  ESA SP-1200), Gaia DR3/DPAC, Yale Bright Star Catalog (Hoffleit 1991), and
  Gliese-Jahreiss (1991).
- This work has made use of data from the ESA mission Gaia, processed by the
  Gaia Data Processing and Analysis Consortium (DPAC). Funding for DPAC has
  been provided by national institutions, in particular institutions
  participating in the Gaia Multilateral Agreement.
  <https://www.cosmos.esa.int/gaia>

### Washington Double Star Catalog (USNO)

- Maintained by the U.S. Naval Observatory:
  <https://www.astro.gsu.edu/wds/>.
- Data used: `wds_precise.txt`, `wdsnewnotes_main.txt`, Herschel `h.txt`,
  F. Struve `stf.txt`, O. Struve `stt.txt`, HDS/HIP `hds.txt`, aliases `bf.txt`,
  historical IDS/ADS/BDS cross-references `wdsidsadsbds.txt`, and
  `wdserror.txt`. Main summary-format documentation is `wdsweb_format.txt`;
  the precise-last layout is separately parsed and tested.
- Requested acknowledgment: "This research has made use of the Washington
  Double Star Catalog maintained at the U.S. Naval Observatory."
- The supplied files contain no explicit standalone redistribution license.
  Do **not** infer a license for every contributed measurement or narrative
  merely from government hosting. Confirm applicable redistribution and
  attribution terms before publishing this combined draft. CC BY-SA for
  AT-HYG does not itself relicense third-party source material.
- The actual WDSS summary is linked from the WDS site under
  `Supplement/wdss_summ.txt`; it has not been imported.

### Sixth Catalog of Orbits of Visual Binary Stars (ORB6)

- Rachel A. Matson, Stephen J. Williams, William I. Hartkopf and Brian D. Mason,
  U.S. Naval Observatory; catalog citation: Hartkopf, Mason and Worley (2001),
  *Sixth Catalog of Orbits of Visual Binary Stars*, with current online updates.
- Official catalog: <https://www.astro.gsu.edu/wds/orb6.html>.
  Description, grading and references:
  <https://www.astro.gsu.edu/wds/orb6/orb6text.html>.
  Individual orbit reference codes are retained in the elements file; references
  are available at <https://www.astro.gsu.edu/wds/orb6/wdsref.html>.
- [orb6orbits.txt](orb6orbits.txt): unmodified official fixed-width elements
  snapshot (4,051 records); [orb6format.txt](orb6format.txt): unmodified official
  field/unit documentation. Retrieved 2026-10-19; the homepage reported a
  2026-10-13 catalog update. [orb6-source.json](orb6-source.json) records URLs,
  SHA-256 hashes, byte counts, attribution and the license-status caveat.
  The builder also records the elements hash in its manifest because supported
  orbits affect component classifications. Running `Generate.ps1` does not
  fetch or overwrite the ORB6 snapshot.
- The local WDS summary has O/C orbit flags and narrative notes, not complete
  structured orbital elements. ORB6 supplies those elements. Its five-year
  ephemeris table is not needed to draw the whole apparent orbit and is not
  downloaded. No current-date or game-date orbital propagation is performed.
- Runtime accepts unique visual orbits of grades 1–5, with finite complete
  elements, positive semimajor axis and period, and `0 <= eccentricity < 1`.
  Astrometric grade 9, visibility-only grade 8, incomplete grade 7, unsupported
  designations, and explicit non-2000 equinoxes are omitted. Blank equinoxes
  are used in their supplied orientation without inventing a precession;
  this is a visualization limitation, not certification of frame equivalence.
  Angular semimajor axes are normalized from arcseconds, mas, arcminutes or
  microarcseconds. Period and epoch are checked for presence but do not set
  the displayed star's position.
- Association requires exact WDS ID plus directional component endpoints
  already present in the curated system's measurements and resolved as
  individual rows. Empty ORB6 component designation means AB, as documented
  upstream. Multiple supported solutions for one pair are omitted; no
  nearest-position or name-based matching and no automatic grade tie-breaking
  are used. Group/blended endpoints are not assigned to individual leaves.
- Each accepted ellipse is sampled into 128 line segments in angular
  east/north coordinates once on system selection and reused during zoom/pan.
  It is a projected **relative** orbit centered on its recorded reference
  component, not either star's orbit around a calculated center of mass.
  The companion remains at its existing WDS-measurement-derived plot position;
  orbital curves are not translated/rotated to force a fit to that point.
  Curves follow companion visibility and contribute to the fitted plot extent.
  Curves are shown automatically alongside the measured star markers, with no
  orbit toggle; there is no information text below the plot.
  Missing/corrupt ORB6 input is reported separately with a retry control; the
  curated star information remains available.
- Grade 1 means definitive and grade 5 indeterminate; a drawn curve is not a
  guarantee of an accurate orbit or agreement with the retained observations.
  Published uncertainties are preserved in the raw input, not propagated into
  uncertainty bands. Orbit solution citation codes remain in that input.
- Attribution: "This research has made use of the Washington Double Star
  Catalog maintained at the U.S. Naval Observatory." See also
  [NOTICE.txt](../NOTICE.txt). No explicit standalone redistribution license
  was identified on the inspected catalog and format pages. Confirm terms
  before public distribution; government hosting alone does not license all
  contributed solutions, and AT-HYG's CC BY-SA terms do not relicense ORB6.

To update, replace both raw files from the URLs in the source manifest,
update that manifest's retrieval date/hashes/counts, run MultiTests against
the curated CSV, then build the mod to deploy the snapshot. Keep the accepted
snapshot and manifest together. The game performs no network access.

### Henry Draper Catalogue and Extension (III/135A)

- Cannon, A. J. and Pickering, E. C., Harvard Annals 91-100 (1918-1924);
  ADC corrected/extended machine-readable release (1989).
- CDS/VizieR reference: <https://cdsarc.cds.unistra.fr/viz-bin/cat/III/135A>.
- Inputs: `catalog.dat`, `ReadMe.txt`, and `errata.htx`. Revision 3 uses exact
  unique DM cross-references to fill missing HD identifiers only. Nonempty
  AT-HYG HD values are never replaced. No B1900 position matching, historical
  photometry, or HD supplemental objects are imported.
- Supplied documentation has no explicit standalone license grant.
  Confirm CDS/catalog redistribution conditions before public distribution.
  Historical origin is not a license assertion for every corrected electronic
  record.

This README documents source terms; it is not legal clearance to distribute
all combined material under a single license. Keep source provenance and
resolve the outstanding WDS/HD terms before shipping.

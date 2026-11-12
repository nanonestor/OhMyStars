## Scope and cautions

The catalog combines AT-HYG 4.0 star records with supported individual
components and measurements from the Washington Double Star Catalog (WDS),
plus documented identifier cross-references. The base selection includes
AT-HYG stars with apparent magnitude at most 10; identified companions may
be fainter, and WDS-only components may have unknown magnitude. It is not a
complete census of stars or multiple systems.

A row represents an individual-star candidate, not necessarily an independently
resolved physical star. Some source identifiers or measurements describe
unresolved blends. A recorded system relationship does not prove gravitational
binding. Missing evidence is not evidence of singleness or physical membership.
Curated exceptions are documented by affected OMS IDs in [oms-notes.txt](oms-notes.txt).

User-declared definitive system reviews are kept separately in
[definitively-correct-systems.csv](definitively-correct-systems.csv) in developer
builds. This list enables Debug-only green Multi indicators; it does not add
a CSV column, change classifications by itself, or certify source accuracy.
Release builds omit the list and indicators. The ten-system review preserves
primary P, applies scoped companion C/V corrections, and suppresses Capella
O/P without recycling their IDs. See [oms-notes.txt](oms-notes.txt) for details.

## Column definitions

### OMS ID and original AT-HYG columns

Coordinate frame/equinox and measurement epoch are different concepts. The
supplied RA/Dec is used in its J2000 frame with no additional precession.
Original provenance fields remain unchanged except that filling a missing spectral
classification from WDS sets `spect_src=WDS`. Known AT-HYG spectra are never
replaced. WDS types are assigned only to exact individual pair endpoints: a single
type belongs to the first endpoint, and a simple `+` separates first/second types.
Conflicting, group/blended and complex parenthesized spectra are not inferred.
A Gaia distance/ID alone does
not certify that a row's RA/Dec was measured by Gaia.

| Column | Meaning and units |
|---|---|
| `oms_id` | Positive numeric OMS identifier for this individual-star candidate. Stable through the persistent registry, not a component code. |
| `id` | Original AT-HYG 4.0 row ID; blank for a WDS-only star. Not an OMS ID. |
| `tyc` | Tycho-2 identifier, including its hyphenated components. Existing AT-HYG IDs are authoritative; blank identifiers may be filled by supported cross-references. |
| `gaia` | Gaia DR3 source identifier as supplied by AT-HYG. Preserve every digit. |
| `hyg` | HYG source row identifier; `0` is valid for Sol. |
| `hip` | Hipparcos identifier supplied by AT-HYG; can refer to an unresolved source. |
| `hd` | Henry Draper identifier. Existing AT-HYG HD takes precedence; missing values may be filled by unique cross-references. Can refer to an unresolved source. |
| `hr` | Harvard Revised / Yale Bright Star Catalog identifier. |
| `gl` | Gliese/Gliese-Jahreiss designation, preserving suffixes. |
| `bayer` | Bayer designation from AT-HYG or a supported blank-field addition; numbered aliases use forms such as `Eps-1`. |
| `flam` | Flamsteed number from AT-HYG or a supported blank-field addition. |
| `con` | Three-letter constellation abbreviation. |
| `proper` | Proper name supplied by AT-HYG. |
| `ra` | Right ascension in **hours**, in the supplied J2000 frame. WDS-only component positions may be derived; unknown coordinates are blank. |
| `dec` | Declination in **degrees**, in the supplied J2000 frame. |
| `pos_src` | Original position provenance, preserved verbatim (for example `T` for Tycho). WDS-only rows use `WDS`. |
| `dist` | Distance from Sol in parsecs; preserved, never copied from a primary to an unknown companion. |
| `x0` | Source equatorial Cartesian X coordinate, parsecs, as supplied. |
| `y0` | Source equatorial Cartesian Y coordinate, parsecs. |
| `z0` | Source equatorial Cartesian Z coordinate, parsecs. |
| `dist_src` | Source provenance for distance/parallax. |
| `mag` | Apparent magnitude in the source's photometric system; do not assume all bands are Johnson V. |
| `absmag` | Source absolute magnitude (apparent magnitude at 10 pc), where available. |
| `ci` | Source color index: typically B-V; Tycho rows (`mag_src=T`) use BT-VT. Not an RGB color. |
| `mag_src` | Original photometric provenance; `WDS` for WDS-only rows. Consult the referenced WDS source line for band/quality flags. |
| `rv` | Radial velocity, km/s, as supplied by AT-HYG. |
| `rv_src` | Radial-velocity provenance. |
| `pmra` | Right-ascension proper motion including cos(dec), milliarcseconds/year. |
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

### Added OMS and supplemental columns

| Column | Meaning |
|---|---|
| `system_id` | Numeric OMS ID of the WDS group's designated primary/anchor; blank for ungrouped S/U. Grouped U/V/Q rows retain this source relationship without asserting physical membership. Differently named WDS systems are not automatically merged. |
| `component` | Role: `S` single; `P` primary; `C` component; `U` unknown identification/classification; `V` visual/optical; `Q` questionable physical versus optical relationship. See classification rules below. |
| `primary_oms_id` | Designated primary/anchor's OMS ID, including for grouped U rows; self for the anchor and ungrouped S/U. A U anchor does not assert reliable primary identification. |
| `component_label` | Individual-star hierarchy label, such as A, B, Aa, Ab, Aa1 or Aa2. Distinct from role codes. Blank only for ungrouped S/U. |
| `radec_source` | `GIA` for RA/Dec retained from AT-HYG; `WDS` for WDS-only positions. `GIA` is not a claim of independently verified Gaia astrometry; consult `pos_src`. |
| `h` | Semicolon-separated Herschel aliases, preserving list/class and number. Stored on the system primary; system/pair-scoped, not automatically individual-component identifiers. |
| `stf` | Semicolon-separated F. Struve aliases, including STFA/STFB where applicable. Same system/pair scope. |
| `stt` | Semicolon-separated O. Struve aliases, including STTA where applicable. Same system/pair scope. |
| `bds` | Semicolon-separated numeric Burnham Double Stars catalog IDs from `wdsidsadsbds.txt`, stored on the primary and matched by WDS ID and accepted discovery designation. |
| `bu` | Semicolon-separated numeric Burnham discovery aliases from accepted WDS `BU` discoveries, stored on the primary. Distinct from BDS numbering: HD 5005 is BU 1 and BDS 455, not BDS 1. |
| `wds_id` | Plain signed WDS coordinate identifier, e.g. `00050+6631`. Retained for grouped U/V/Q; blank for ungrouped S/U. |
| `relationships` | Directional endpoints, e.g. `A>B`, `Aa>Ab`, `Aa1>Aa2`, or `AB>CD`. Left is reference; right is measured companion/group. Pipe-separated and aligned with the following measurement lists. |
| `theta_deg` | Last WDS position angle, degrees north through east, unprecessed for its observation date. Unknown values are empty entries. |
| `rho_arcsec` | Last WDS angular separation in arcseconds; unknown values are empty entries. |
| `measurement_year` | Last observation year for each theta/rho measurement, not automatically the absolute coordinate epoch. |
| `wds_source_line` | 1-based line in `wds_precise.txt` for each measurement, locating its original flags, magnitudes and discovery designation. |

## Relationships and hierarchy

Uppercase letters label the widest components; lowercase letters subdivide
them (Aa/Ab), and numerals subdivide again (Aa1/Aa2). `Aa2` describes the path
A/Aa/Aa2 and parent Aa. Abbreviated `Aa1,2` expands to Aa1/Aa2.
Plain `AB` in a pair designation means A/B; in an endpoint such as `AB>C`,
AB is the group containing A and B and their known descendants.
`AB>CD` relates two groups, not four new stars or measured barycenters.

Only leaves become individual-star rows. A split of A into Aa/Ab does not
create an extra WDS-only A row. The first leaf down the A branch is designated
the primary and retains its exact label. Combined-group magnitudes, motions
and coordinates are not automatically assigned to individual leaves.
Hierarchy describes catalog organization, not orbital direction or binding.

Each accepted measurement is stored once, on the first individual leaf of
its second endpoint. An `AB>CD` measurement is stored on C or its first
descendant, but still explicitly names `AB>CD`, not `AB>C`. Storage ownership
does not establish an individually measured leaf position.

For example, `relationships=A>C|B>C`, `theta_deg=90|`,
`rho_arcsec=10|2`, and `measurement_year=2020|2022` describe two measurements
of C. The second separation is known but its position angle is unknown.
Split all measurement cells on `|` **without discarding empty entries**.
Their lengths and ordering must remain aligned, including `wds_source_line`.

Relative geometry requires a usable reference position and both theta and
rho. With theta in radians, east = rho * sin(theta) and north = rho *
cos(theta). A chain of such measurements can locate a component even when
its absolute RA/Dec is blank. A disconnected pair may have known internal
geometry but no known location relative to the system primary.

## Positions, epochs and proper motions

Matched AT-HYG fields are preserved. WDS-only rows do not receive invented
distance, absolute magnitude or Gaia identity, nor a primary's distance.
Empty RA/Dec does not mean the component itself was discarded.

WDS-only derived positions use accepted theta/rho projected on the sphere
from an anchored reference. Repeated WDS precise system coordinates do not
necessarily locate every endpoint of a nested pair. Where both endpoints
have individual WDS proper motions, differential motion is subtracted from
the observation year to 2000. Without both motions, measurements more than
five years from 2000 leave derived RA/Dec blank. Missing theta/rho likewise
prevents derivation but does not remove the component or its known measures.
Supported direct endpoint positions take precedence over derived positions.

This is a linear epoch correction, not an orbital model. Derived coordinates
remain provisional and never replace matched AT-HYG astrometry. Source
positions and relative measures may describe different epochs or blends;
do not interpret their disagreement as proof of separate identities.

Ordinary WDS motions are arcseconds/1000 years, numerically equivalent to
mas/year. WDS flag P denotes arcseconds/100 years: the builder multiplies
these by 10. Exported WDS-only `pmra`/`pmdec` are already normalized to
mas/year with `pm_src=WDS`; **do not multiply them again**.
AT-HYG motions remain unchanged.

## Classification

- **S:** no retained multiple-system association; not proof of physical singleness.
- **P:** designated system primary/anchor.
- **C:** retained companion without the V/Q/U evidence described below, or
  supported physical/orbit evidence or a documented curated correction.
  C is not a guarantee of gravitational binding.
- **U:** identification/classification unknown. WDS I warns that the precise
  object may not be the intended pair; affected endpoint leaves, including
  an anchor, retain U rather than P/C/V/Q.
- **V:** uncontested explicit optical/nonphysical notes or WDS S/U/Y flags.
  The WDS flag U is distinct from this catalog's U role code.
- **Q:** tentative optical evidence, conflicting physical/optical evidence,
  or the extreme motion-only discrepancy below.

The motion-only rule computes the companion-minus-primary vector magnitude:

`delta_mu = sqrt((pmra - primary_pmra)^2 + (pmdec - primary_pmdec)^2)`

It gives Q only when **both** `delta_mu > 100 mas/year` and
`delta_mu > sqrt(primary_pmra^2 + primary_pmdec^2)`. Equality at either
boundary does not trigger Q. Each available AT-HYG axis takes precedence;
missing axes may use the latest exact individual-endpoint WDS motion.
Incomplete vectors do not trigger motion-only Q. Blended-group motions
are not assigned to leaves. This is not a parallax or gravitational-binding
test, and inter-catalog disagreements for the same star do not themselves
cause Q or reject an identity.

Explicit optical statements are scoped to their named relationships or
discovery designation, not automatically the entire system. Tentative
wording such as "probably optical" produces Q; explicit physical evidence
conflicting with optical evidence also gives Q. Curated physical exceptions
are separately documented in [oms-notes.txt](oms-notes.txt).

A supported, unique grade 1-5 ORB6 individual-pair orbit association may
promote its Q/V companion to C, without overriding P or U. An inner
subsystem orbit does not by itself establish an outer system's binding.
ORB6 elements remain in a separate snapshot, not columns in this CSV.
See the [ORB6 source attribution](attributions.md#sixth-catalog-of-orbits-of-visual-binary-stars-orb6)
and supplied [field documentation](orb6format.txt).

## Identity, aliases and source evidence

OMS IDs are stable registry identifiers, not scientific catalog designations.
Retired IDs are reserved and not recycled. Use `oms_id` to distinguish rows;
other catalog identifiers can refer to unresolved sources or whole systems.
In particular, primary-row discovery aliases do not identify every leaf.
Existing nonempty AT-HYG identifiers are preserved; supported unique
cross-references may fill blank fields. Additions are audited separately.

Explicit equivalent endpoints can reconcile cross-WDS identities without
creating duplicate stars. Aliases between parent groups do not establish
which individual leaves are identical. Unsupported, conflicting or dubious
source relationships may be omitted; accepted membership is not exhaustive.

Historical spectroscopic wording alone does not remove, merge or reclassify
an individually recorded component or prove it is unresolved. A resolved
component can contain a spectroscopic subsystem. Narrative notes alone do
not create extra stars or subsystem identities.

Original WDS narrative notes and flags are not embedded in the CSV.
Use `wds_id` to consult `wdsnewnotes_main.txt`, and `wds_source_line` to
locate a measurement in the exact `wds_precise.txt` input snapshot.
Build/source manifests identify input versions and hashes. The full README,
build manifest and further matching, exclusion and audit details remain in
the source repository rather than the distributed catalog files.

## Sources and attribution

The source catalogs retain their own attribution and usage conditions.
Consult the [source attributions and licensing](attributions.md)
section before redistributing source material or derived data. It documents
AT-HYG 4.0, USNO WDS, USNO ORB6 and the Henry Draper cross-reference data.

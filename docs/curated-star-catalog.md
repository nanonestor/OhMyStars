# Curated Oh My Stars catalog: intent and implementation handoff

Recorded 2026-10-18. This is a design/source-inspection and implementation
handoff record. A provisional offline builder and CSV now exist (see below);
binary integration and the multiple-system plot are not implemented.
The shipped catalog and runtime behavior remain unchanged.

## 2026-10-19 classification and Multi heading update

The subsequent identifier update adds `bu` as a separate Burnham discovery
alias column, preserves BDS numbering, and fills blank numbered Greek /
Flamsteed designations from component-scoped `bf.txt` aliases with an audit.
The positional search is now 6 arcseconds for both direct and derived
positions, preferring the original 2/3-arcsecond core when candidates exist
there. This avoids a tested Sirius A/B misassignment from an unrestricted
widened nearest-neighbor search. See the current catalog README and manifest.

Subsequent additions on this date: the separate official ORB6 elements/format
snapshot and provenance manifest are stored under `catalogs`. Multi draws
cached, sampled relative-orbit curves for exact, unique, supported
individual-component matches, without changing the measured star points.
See the [ORB6 source/limitations](../catalogs/README.md#sixth-catalog-of-orbits-of-visual-binary-stars-orb6).
Information-list labels now receive `*` for `radec_source=WDS`, including
the primary; underlying labels, IDs, and plot geometry remain unchanged.

This update supersedes the historical motion-to-V and probably-optical-to-V
decisions below. Q = Questionable now denotes motion-only differences greater
than 10 mas/year, tentative optical notes, and conflicting physical/optical
evidence. Uncontested explicit optical notes and WDS S/U/Y flags remain V;
I-derived U retains precedence. See the current
[classification rules](../catalogs/README.md#visual-and-questionable-classification).
Q retains V's Multi plotting/relative-motion behavior but displays `(Visual ?)`.

Multi's header now uses `Proper name / Flamsteed number + constellation genitive`
on its first line and an expanded Bayer designation plus constellation genitive
on the second. The [designation helper](../src/StarDesignations.cs) contains
all 88 IAU constellation genitives and shares the existing Greek-letter
expansion with the other information view. The CSV identifier fields are
unchanged; this is display formatting, not catalog identifier rewriting.

## First-pass implementation

- [Catalog CSV and column/source/license README](../catalogs/README.md).
- [Offline .NET 10 builder](../tools/CatalogBuilder/Program.cs), requiring no
  game assemblies or external packages.
- [Output validator](../tools/CatalogBuilder/Validation.cs) verifies copied
  AT-HYG fields, identifiers, relationships, roles, coordinate provenance,
  excluded flags, registry links and original WDS measurements.
- This is deliberately provisional. Standard uppercase/lowercase/numeric
  hierarchies now resolve to individual leaves; unusual labels and unresolved
  cross-identifications remain in a review CSV.
  Shared AT-HYG Gaia IDs are accepted without omission or review.
  Unknown geometry remains blank/null, with the component retained.
- The README records the actual implemented subset, heuristics and outstanding
  source/license questions. Remaining design sections below describe the fuller
  intended catalog, not a claim that the first pass completes every requirement.

## Requested outcome

- Replace the AT-HYG 3.2 basis with AT-HYG 4.0.
- Start with AT-HYG stars having apparent magnitude **10 or brighter**
  (`mag <= 10`, including the boundary).
- Enrich this base with eligible Washington Double Star Catalog (WDS) systems
  whose primary is magnitude 10 or brighter. Reuse existing entries, adding
  missing component stars rather than duplicating stars.
- Search the **full** AT-HYG 4.0 input when identifying companions, not just the
  magnitude-limited subset. Existing faint component entries may already be
  the correct matches.
- Keep separate entries for individual component stars, with their available
  magnitudes, coordinates, color/spectral data, identifiers, and provenance.
- Add identifiers for Henry Draper (`HD`), Herschel (`H`), Hipparcos (`HIP`),
  F. Struve (`STF`), and O. Struve (`STT`) wherever supported. Missing aliases
  are normal. These catalog designations do not all identify individual
  resolved stars; some identify pairs or blended sources.
- Preserve AT-HYG RA/Dec for matched stars; use WDS-based coordinates only for
  stars absent from AT-HYG. Preserve the original astrometric source metadata.
- Generate a new default game-format star binary, rendering only single stars
  and the designated primary star of each included multiple system.
- Retain component stars for mod information and plotting, not as additional
  sky sprites.
- Let users select the stock game binary in settings and retain mod identity
  lookup/features through its sister-file path. Preserve existing generator,
  export, saved-selection, constellation, label, pointer, and navball behavior.
- Add a multiple-system information view and ImGui diagram centered on the
  primary, with relative component positions, colors, and brightness-based
  marker sizes.

## Confirmed decisions from the user

1. `oms_id` is the **first column**, numeric, and identifies an individual star.
   Hierarchies/component labels belong in separate columns, not ID suffixes.
2. Single stars, primary stars, and other component stars must be conveniently
   and unambiguously distinguishable.
3. **Do not import WDS pair entries flagged `S`, `U`, `X`, or `Y`.**
   These mark non-physical pairs or dubious doubles in the supplied format
   documentation. Apply this exclusion before magnitude selection, matching,
   hierarchy construction, or WDS enrichment.
4. `W` indicates additional components/measures in WDSS. Import eligible
   supplementary relationships when the supplement and its format are supplied.
   `W` alone is not a reason to reject a pair.
5. The curated catalog is a **CSV in the same vein as AT-HYG**, a modified
   and supplemented version of the source catalog, not a replacement database
   format. It remains the authoritative distributable star catalog; generated
   runtime binaries and optional build/review files are derivatives or aids.
6. AT-HYG RA/Dec takes precedence over WDS RA/Dec, including when matching
   faint components from the full AT-HYG input. Add `radec_source`: `GIA` for
   coordinates retained from AT-HYG and `WDS` for WDS-only coordinates.
   `GIA` is the user's requested OMS provenance code for AT-HYG input, not
   independent verification that the coordinates were measured by Gaia.
   Retain AT-HYG `pos_src` unchanged alongside it.
7. Apply `wdserror.txt` corrections, duplicate resolutions, designation changes,
   and invalid-entry removals where applicable, with an auditable correction
   record. Do not resurrect removed WDS entries through aliases or notes.
8. Ship a catalog README alongside the CSV explaining **every column**, units,
   missing-value conventions, relationship encoding, and source-precedence
   rules, with references/attribution and applicable licenses for every source
   used. Distinguish verified license terms from terms still needing review.
9. **AT-HYG supersedes conflicting WDS values for all existing star fields**,
   not just RA/Dec. WDS supplies relationships and supplemental information.
   Keep conflicting WDS observations separately labeled as observations,
   rather than replacing AT-HYG magnitudes, distance, color, spectral type,
   identifiers, or coordinates. This does not resolve conflicts internal to
   AT-HYG itself.
10. The role column is `component`, with `S` (single), `P` (primary), `C`
    (other component), and `U` (unknown classification). Future runtime code
    treats U like S. Known companions with unknown geometry remain C.
    Do not include `star_role`, `catalog_status`, `render_eligible`,
    `position_method`, or `hd_crossrefs`; HD is represented by its ID column.
11. Preserve source AT-HYG rows sharing a Gaia ID without warnings. Do not
    reject a WDS star because its separation is tiny or hundreds of arcseconds,
    or because it is close to another newly added star/system. Remove the
    within-system 3-arcsecond and inter-system 60-arcsecond rejection guards.
12. WDS P-flag motions are converted from arcseconds/100 years to mas/year
    (multiply by 10); P is not a review issue. Proper-motion mismatches still
    require numerical WDS/AT-HYG values, signed differences, units and tolerance
    in review, rather than a generic ambiguous-letter message.
13. Missing theta, rho or epoch-drift information retains the component and
    leaves unavailable positional values blank/null. Do not emit review rows
    for missing geometry, shared Gaia IDs, or routine S/U/X/Y exclusions.
    Future plots skip unknown geometry and explain it as unknown.
14. Retain nonempty AT-HYG HD and other source IDs. Fill missing identifiers
    only through unambiguous supplied cross-references, including WDS DM to
    HD, HDS to HIP, component-scoped notes, Bayer/Flamsteed/name references,
    and exact links to the full AT-HYG catalog. Audit additions; reuse an
    identified AT-HYG row and its coordinates rather than duplicating it.
15. Remove `wds_pairs`. Replace `wds_ids` with singular plain-text `wds_id`:
    preserve positive/negative declination signs. The former `\u002B` was
    JSON's encoding of `+`, not a hyphen. No JSON brackets/quotes in ID cells.
    Keep relationships and geometry in explicit scalar/list columns.
16. Parse uppercase root components, lowercase subdivisions, numeric
    subdivisions, and comma/hyphen pair endpoints. Expand `Aa1,2` to
    Aa1/Aa2. AB/CD endpoints represent groups, never additional stars.
    Export individual leaves and explicit parent/measurement endpoints.
17. Add V = Visual to **`component`**, not `component_label`. Compare the
    companion and system primary's full proper-motion vectors; magnitude
    strictly **greater than 10 mas/year** gives V. Prefer AT-HYG motions,
    using component-scoped WDS where an axis is unavailable. Exactly 10
    stays C; incomplete vectors cannot establish V. This is a classification
    rule, not proof of optical/nonphysical membership or an exclusion.
18. Add `bds` for Burnham Double Stars catalog numbers from
    `WDS\wdsidsadsbds.txt`. Match WDS ID and accepted discovery designation;
    retain multiple IDs as semicolon-separated numbers on the primary,
    like other pair/system aliases. Ignore missing IDs and marked omissions.
19. Do not reject matches or report WDS-versus-AT-HYG proper-motion
    discrepancies. AT-HYG motion takes precedence wherever available.
    The V threshold compares companion to primary, not WDS to AT-HYG.
    Preserve V relationships for visual display, not physical membership.
20. Narrative-status review reasons must include the triggering source note
    text, matched phrase, and 1-based notes-file line number. Keep the current
    conservative group quarantine explicit; a note may concern another pair.
    Include the full line-numbered note context so continuations and references
    remain readable.
21. Retain I-flagged WDS relationships, marking both endpoint leaves U rather
    than reviewing/excluding them solely for uncertain identification. U takes
    precedence over P/C/V. Preserve group/primary references even on U rows
    as source metadata, not a claim of physical membership.
22. Recognized "Optical pair(s), based on study of relative motion" notes
    classify their named companion endpoints V, independent of measured
    proper-motion differences. Resolve explicit pair labels (including
    multi-pair and group endpoints); unscoped notes require a unique
    relationship for their discovery designation. Do not quarantine a group
    merely for resolved optical wording. Keep other unresolved narrative
    issues in review with their evidence.
23. Also interpret "is probably optical" as V for named companions. Unscoped
    optical statements apply to all supported relationships of the active
    note discovery designation; existing primaries stay P, and I-derived U
    retains precedence.
24. For positional candidates within 0.1 arcsecond of the nearest, choose
    closest magnitude without a minimum advantage; break remaining ties by
    lowest AT-HYG ID. Do not emit uncertain-crossmatch warnings for these.
25. Following clarification, preserve the first AT-HYG system assignment and
    omit conflicting later relationships, rather than merging systems,
    duplicating stars or suffixing the same star's label B2. Remove WDS-only
    orphans of omitted relationships. Retain omission counts in the manifest.
26. Parse explicit "A component is the D component of [WDS ID]" statements
    as identity aliases, including reciprocal/transitive equivalences. Apply
    first-assignment/omit-later policy to equivalent individual endpoints,
    without inferring individual identities from unresolved parent aliases.
27. Interpret general optical statements as V, scoping explicitly named stars
    even within prose such as "2+2 hierarchical multiple. C is optical".
    Preserve primaries P and I-derived U. Descriptive multiplicity counts
    alone do not justify inventing hierarchy leaves or measured positions.
28. Do not quarantine stars for notes explicitly describing an already-deleted
    spurious preliminary measurement; retain that history in the source notes.
    Other spurious object/pair warnings remain reviewable.
29. Measurement/model disagreements, including eccentricity/period discussion,
    rejected measures, HIP solutions, acceleration and historical plate or
    proper-motion problems, are informational unless they challenge stored
    star identity or existence. Keep source notes; do not overwrite AT-HYG.
    Recognize explicitly restored/confirmed pairs. Preserve genuine
    spurious-pair/ghost/detection warnings for review.
30. Historical alternative discovery names do not alone justify quarantine:
    apply current WDS positions and normal duplicate checks plus scoped errata.
31. When a unique exact identifier links a WDS-only row to an AT-HYG star
    already assigned elsewhere, omit the later duplicate relationships and
    remove orphan WDS-only/faint supplemental rows and their audit entries.
    Keep independent magnitude-limited AT-HYG rows and their original fields.
32. Exclude the remaining unsupported/malformed WDS relationships and
    uncertain-existence WDS groups rather than recording them for pending
    review. Do not import their star/relationship data or classify independent
    AT-HYG stars U on their account. Keep other supported relationships and
    independently valid magnitude-limited AT-HYG entries. Track exclusion
    counts in the manifest; a header-only review file does not imply completeness.
33. Simplify exported structure to `component_label` and `relationships`.
    Remove hierarchy_path/parent_component because labels encode the path.
    Replace relative_to_component/measurement_components with directional
    reference>companion endpoints, e.g. A>B or AB>CD, pipe-separated in
    alignment with theta/rho/year/source-line lists. Preserve group semantics.
34. Do not export WDS narrative notes into the CSV. Continue reading the
    original notes for classification/identity processing and hashing that
    source in the manifest; narrative prose remains only in the source files.
35. Initial Multi UI integration: deploy the curated CSV/readme/manifest to the
    mod's catalogs folder. Keep the full generation assets and numeric registry
    in the repository; catalogs/Generate.ps1 wraps offline fixtures/build/validate.
    Multi loads asynchronously, marks only P primaries, picks nearest projected
    ring centers, lists members and plots resolvable individual-star theta/rho
    chains. No invented group barycenters or missing-geometry positions.
    Colors use B-V and dot radii indicate apparent brightness, not stellar size.
    Existing binary defaults and generic generator behavior remain unchanged.
    The initial strict group-reference plot policy is superseded: group endpoints
    now use the primary/leading leaf as an explicitly approximate diagram anchor,
    including Visual and Unknown members with resolvable geometry. Missing
    geometry is still not invented, and stored measurements remain unchanged.
    Future work: curated primary-only binary/sister integration, group-reference
    plotting with measured barycenter constraints, common-epoch propagation, plot navigation
    for wide versus close subsystems, and in-game UX/performance tuning.

Exclusions apply to the WDS relationship: they do not automatically delete a
valid AT-HYG star, or a different accepted relationship in the same WDS group.
If excluded and accepted records conflict about the same pair's identity or
status, quarantine that relationship for review rather than silently choosing
the favorable record.

## Inspected local sources

Source root: `D:\ksa\stars\catalogs`.

| Source | Observed contents / intended use |
|---|---|
| `AT-HYG_40\athyg_40.csv` | Full catalog, about 488 MB. Base stars and full-catalog companion matching. |
| `AT-HYG_40\athyg_40_hyg_ids-not-sure-what-this-is.csv` | Same schema in sampled rows, with populated HYG IDs. Appears to be a HYG-linked subset; do not treat as a separate independent catalog or infer an undocumented role without checking all rows. |
| `AT-HYG_40\STEPS_V4.txt`, `v4_builds.md`, `version-info.md` | Build/multiple-component handling and source conventions. Version 4 explicitly adds components that earlier versions could omit. |
| `AT-HYG_40\LICENSE`, `ACKNOWLEDGMENTS.md` | AT-HYG attribution and CC BY-SA 4.0 terms. |
| `WDS\wds_precise.txt` | User identifies this as "The WDS with precise *last* only." Sampled rows differ from the standard summary in theta/rho layout and precision. Requires its own verified field map. |
| `WDS\wds.summ_con.txt` | Standard-summary-style first/last measurements with a trailing constellation field in sampled rows. Not verified to be WDSS. |
| `WDS\wdsweb_format.txt` | Standard 130-column summary definition, component semantics, quality codes, coordinate caveats. Do not apply its theta/rho offsets blindly to the precise-last variant. |
| `WDS\h.txt` | WDS/discoverer/component to Herschel cross-references; preserve class and number (for example `H 5 32`). |
| `WDS\stf.txt`, `stt.txt` | Struve cross-references; notes can carry additional aliases. |
| `WDS\hds.txt` | HDS-discoverer to HIP cross-references; not a universal WDS-to-HIP mapping. |
| `WDS\bf.txt` | Bayer/Flamsteed, variable-star and proper-name aliases in sampled rows. |
| `WDS\hipprob.txt` | HIP/WDS/discoverer entries with its own code field. Code meanings require documentation; do not assume these codes have the summary notes-field semantics. |
| `WDS\wds_synonyms.txt` | Discoverer synonyms and identification notes, including text about identical entries. Parse cautiously and retain evidence. |
| `WDS\wdsidsadsbds.txt` | Historical IDS/ADS/BDS cross-references and an omit field. |
| `WDS\wdsnewnotes_main.txt` | Newly supplied file headed "USNO Double Star Catalogs: Notes". Narrative identification, component, measurement and literature notes, with continuation lines. Not the structured WDSS catalog. |
| `WDS\wdserror.txt` | Inspected errata: deleted duplicate systems, removed TDS pairs, corrected designations, merged pairs, and Herschel/Struve list and appendix naming rules. Apply relevant corrections before matching. |
| `WDS\wdsnewref.txt`, `methvssep.txt` | Reference/method material; inspect applicable sections before using as matching evidence. |
| `Henry Draper\catalog.dat`, `ReadMe.txt`, `adc_doc.doc`, `errata.htx` | HD/DM aliases and historical spectral/photometric information, including corrections. Compressed copies also exist. |

The suggested supplement `wdsnewnotes_main.txt` is a **notes file**, not a
structured WDSS component/measurement catalog. Its text can support reviewed
component identities and corrections, but mentions of additional stars are not
complete measured catalog rows. A sampled note even refers to a separate WDSS
pair. The structured WDSS file/path remains unlocated. Do not substitute
`wds.summ_con.txt` or turn free-text mentions into invented measurements.

### Astrometric and photometric cautions

- AT-HYG headers include `id,tyc,gaia,hyg,hip,hd,hr,gl,bayer,flam,con,proper`,
  then positions, distances, photometry, velocities and their source fields.
  Retain these original identifiers; `oms_id` does not replace their meaning.
- Sampled rows have `pos_src=T` alongside `dist_src=G_R3`. A Gaia ID or
  Gaia-derived distance does **not** establish Gaia-derived RA/Dec. The build
  notes describe Tycho position input and Gaia distance/velocity enrichment.
  Accordingly `radec_source=GIA` means AT-HYG input by OMS convention, while
  `pos_src` retains the source catalog's more specific provenance.
- Preserve RA units explicitly (AT-HYG `ra` is hours), Dec in degrees,
  coordinate frame/equinox, coordinate epoch, and proper-motion convention.
  Do not equate J2000 equinox with an observation epoch.
- No external Gaia coordinate replacement is required for this build: the
  confirmed policy is to retain AT-HYG coordinates. Keep Gaia IDs as exact
  integers or decimal strings, never floating-point identifiers.
- For WDS-only companions, distinguish a directly supplied subsystem-primary
  position from a position derived using theta/rho and an anchor. Store the
  derivation and epoch separately even though both use `radec_source=WDS`.
  A missing absolute position remains missing; never copy the overall
  primary's coordinates to every component.
- WDS precise coordinates can refer to the **subsystem primary**, not the
  overall system primary. A BC row can be positioned at B, not A.
- WDS theta is unprecessed, for the observation date. Keep theta, rho, and
  measurement epoch together. Last-only measurements are not orbital elements.
- HD positions are equinox B1900, epoch 1900.0 and relatively coarse. Do not
  overwrite modern coordinates or directly nearest-match them as J2000.
- HD has deleted/duplicate records and special magnitude codes representing
  variables, novae, clusters and nebulae. Those codes are not real magnitudes.
- Preserve photometric band/source: WDS B/R/K flags are not Johnson V.
  Tycho `ci` is BT-VT; the existing renderer approximates B-V using 0.85.
- Unknown distance, magnitude, color, or position remains unknown. Do not
  invent a physical distance for an optical/uncertain companion or compute an
  absolute magnitude without defensible distance and photometry.

## Proposed data model

### Primary catalog CSV: one row per individual star

Keep the original AT-HYG column names, units, and order after prepending
`oms_id`. Append the OMS, cross-reference, hierarchy, measurement and provenance
columns after the existing AT-HYG fields. In particular, preserve original
AT-HYG `id`, `hyg`, `hip`, `hd`, `gaia` and other source identifiers; do not
silently reinterpret `id` as `oms_id`. WDS-only stars have no invented AT-HYG
source ID.

Proposed column order:

```text
oms_id,
[all original AT-HYG columns, starting with id and ending with spect_src],
system_id,component,primary_oms_id,component_label,
radec_source,h,stf,stt,bds,wds_id,relationships,[relative measurement columns]
```

This is a schematic header, not a finalized literal CSV schema.
The appended provenance columns include `radec_source` with the confirmed
`GIA`/`WDS` codes. Scalar measurement columns retain endpoints, theta/rho,
epoch and source line; no pair JSON is exported.

- `oms_id`: persistent numeric star ID; never renumber on sorting/filtering.
  Maintain an append-only identity registry with reviewed merges/redirects.
- `system_id`: persistent numeric group ID, separate from WDS coordinate ID.
- `component`: exactly `S`, `P`, `C`, `U`, or `V`, as defined above.
- `primary_oms_id`: self for ungrouped S/U and designated group anchors;
  designated root anchor ID for grouped C/V/U rows. A U anchor remains U.
  This makes grouping/lookup straightforward.
- `component_label`: source-preserving label such as A, B, Aa, Ab, C, or D;
  blank for a single star. Preserve case.
- `relationships`: reference>companion/group endpoints, e.g. Aa>Ab or AB>CD.
  Derive hierarchy navigation from component_label; do not export redundant
  path/parent fields. Neither representation asserts physical binding.

Here S means **no accepted companion in this curated dataset**, not proof that
the star is physically single. Use U for unresolved classification rather than
adding a game-specific status column. Trust WDS-listed components when no
AT-HYG match is established; unavailable geometry remains unavailable.

Retain AT-HYG-compatible physical columns and source IDs, adding distinct
alias/display columns for `hd`, `h`, `hip`, `stf`, `stt`, `bds`, and WDS references.
Reuse the existing `hd` and `hip` columns rather than adding duplicate columns.
Allow multiple aliases without duplicating star rows. Record designation,
target scope (star/group/pair), provenance and match confidence in the CSV;
an internal alias index can help build and query it.

### Hierarchy nodes and pair measurements

Model system/group nodes and relative measurements during construction without
inserting group-only rows. Export endpoints and geometry as explicit columns,
with aligned pipe-separated lists when a row owns multiple measurements.
Each measurement is owned by the first leaf of its second endpoint, but its
full group/individual scope remains explicit. Plain `wds_id` identifies the
system. No `wds_pairs` JSON, duplicate star rows per pair, or group-only stars.
Consult the catalog README for current column definitions and ownership rules.

- Group nodes can represent unresolved AB, CD, or nested subgroups without
  pretending that the blended source is another individual star.
- Internal measurements identify both endpoints (star or group), raw component
  expression, WDS ID, discoverer designation, measurement epoch, theta in
  degrees, rho in arcseconds, flags, source file/row, and acceptance status.
- A system with AB and CD contains four leaf stars plus two group nodes and
  their relationships, not stars called "AB" and "CD" in addition to A-D.
- Parse and retain labels such as `AB`, `AC`, `BC`, `AB-C`, `AB,C`, and
  `Aa,Ab`; unresolved expressions require review, not letter splitting.
- HIP/HD IDs can represent blended groups. Shared HIP/HD is not, by itself,
  evidence that two resolved component rows are duplicates.
- Designate the first leaf down the A hierarchy as the individual primary
  for this test catalog. Never copy a group magnitude, motion or position to
  that leaf, and preserve its actual label (for example Aa1).

## Proposed reproducible merge pipeline

1. Record source versions, hashes, file formats, attribution, and build options.
2. Stream the full AT-HYG input, validate fields, build exact-ID and spatial
   indexes, and select base rows with finite apparent magnitude `<= 10`.
3. Parse WDS variants independently; validate fixed widths, sentinels and flags.
   Apply reviewed errata keyed by WDS ID, discoverer designation and component
   scope; retain the original record and correction evidence. Already-corrected
   input is a no-op, not a reason to apply a rename or deletion twice.
   Exclude X relationships before they affect inclusion or enrichment.
   Retain S/U/Y nonphysical relationships as Visual secondary leaves (primary
   stays P; I-derived U takes precedence). This supersedes the original
   S/U/X/Y exclusion policy: Albireo AB demonstrated that it hid valid visual
   companions even when their individual AT-HYG stars were retained.
   Distinguish a deleted duplicate pair, a nonexistent component, and a deleted
   individual observation: only remove the object that the erratum invalidates.
   Preserve independently valid AT-HYG stars unless evidence invalidates that
   individual star entry. Keep rejected-source identities in a suppression map
   so subsequent synonym/notes processing cannot reintroduce them.
4. Seed eligible systems by primary magnitude `<= 10`; traverse accepted
   component/subsystem relationships under the chosen companion policy.
   Do not accidentally drop a faint BC subsystem because B itself is over 10.
5. Follow W-marked systems into the verified WDSS input, retaining its own
   schema, provenance, quality flags and measurement semantics.
6. Resolve supplied cross-references with component scope. Match against the
   full AT-HYG catalog before creating a new star, including faint companions.
7. Use epoch-aware positional evidence only after explicit IDs/aliases.
   Use pair geometry, separation, photometry and competing candidates to
   validate a match; do not veto matches on WDS/AT-HYG motion differences.
   A single fixed-radius nearest neighbor
   is not sufficient. Conflicts/ambiguous candidates go to a review report.
8. Add verified new individual stars; retain blends as group/source records.
   Preserve matched AT-HYG RA/Dec exactly and set `radec_source=GIA`.
   Only WDS-only stars use WDS-based RA/Dec and `radec_source=WDS`.
   Preserve field-level source provenance; supplement, rather than silently
   overwrite, measurements.
9. Assign/reuse stable OMS IDs, finalize hierarchy/primary assignments, and
   validate uniqueness and relationships before publishing any output.
10. Emit the supplemented AT-HYG-style curated CSV, including its relationship
    and alias information. Keep the identity registry, source manifest, and
    accepted/rejected/ambiguous-match counts and evidence as build aids.
    Runtime-specific indexes/binaries are generated from the curated CSV.
    Build and validate outputs in staging before replacing the active set.

Companion magnitude limits, non-visual WDS magnitude eligibility, structured
WDSS location, and how to handle unflagged pairs without
positive physical evidence still need confirmation.

## Runtime integration surfaces inspected

- [StarBinaryGenerator.cs](../src/StarBinaryGenerator.cs): generation and
  analysis share a reader. Both must apply primary-only eligibility before
  ordinary magnitude/distance filters, including always-include behavior.
  The current magnitude filter is strictly `<`, not inclusive `<=`.
- [StarIdentityFile.cs](../src/StarIdentityFile.cs): version 1 is an ordered,
  one-record-per-rendered-star format with HIP, catalog ID, geometry, magnitude,
  absolute magnitude, color index and name. It does **not** store the full
  catalog, multiple-system relationships, or all advertised catalog aliases.
  Keep v1 reading support; introduce explicit versioning for OMS identities.
- Preserve the ordered sister-file invariant: metadata record i describes
  game record i. Component-only records belong in a separate metadata store,
  not appended to that ordered list.
- [StarsEditWindow.cs](../src/StarsEditWindow.cs): default catalog name,
  generator default input, curated rebuild version, startup selection, binary
  scanning, export, and non-mod confirmation all need consistent changes.
  Existing curated output is `binaries\OhMyStars_stars.bin`, with the special
  sister name `OhMyStars__stars_IDs.bin` and rebuild version `"3"`.
- The existing curated render cutoff is **8.795**, independently of the input
  catalog's magnitude-10 limit. Keep catalog membership separate from rendering
  tuning; do not silently change this visual cutoff.
- The current runtime selects vanilla mode if any selected binary lacks a
  sister file. Automatically supporting the stock binary with full mod
  features is a requested change, not current automatic behavior.
- Stock binary support must preserve stock record order/count and match each
  record conservatively. Keep unknown identities explicitly unmatched.
  Do not rewrite the stock game binary; use a writable, validated sidecar/cache
  if its installation directory is not writable.
- [SkyCulturesRenderer.cs](../src/SkyCulturesRenderer.cs): its catalog path is
  also hard-coded to AT-HYG 3.2. Keep HIP-based constellation endpoints,
  spectral/name caches and component lookups consistent with new identities.
- [StarPicker.cs](../src/StarPicker.cs),
  [OhMyStarsWindow.cs](../src/OhMyStarsWindow.cs), and
  [StellariumRenderer.cs](../src/StellariumRenderer.cs) need lookup/display
  integration; do not let hidden components become selectable sky sprites.
- [OhMyStars.csproj](../src/OhMyStars.csproj): package/copy curated assets and
  metadata. Builds currently deploy files and overwrite installed settings,
  so validation should avoid unintended deployment.
- Update [README.md](../README.md), [NOTICE.txt](../NOTICE.txt), data attribution
  and release contents when new catalog assets actually ship.
- Several runtime files and README were already modified before this work.
  Preserve and integrate those edits rather than reverting them.

## Multiple-system UI behavior

- Show primary/component roles, aliases, hierarchy, individual information,
  data sources, missing values, and accepted pair measurements.
- Center the selected system primary at (0,0). For compatible local offsets,
  theta measured north through east gives east = rho * sin(theta),
  north = rho * cos(theta). Clearly label north/east, angular scale, date,
  and any screen-axis reversal.
- Chain offsets only through resolved endpoints with compatible epochs and
  coordinate orientation. Pair endpoints that are blended group photocenters
  are not automatically barycenters or individual-star positions.
- Prefer consistent-epoch resolved absolute positions when available. Do not
  mix Gaia-epoch coordinates with historical theta/rho without an explicit,
  defensible transformation; label observed/derived data separately.
- Do not invent circular orbits or project orbital motion from one WDS measure.
  Unplaceable components remain visible in the information list with a reason.
- Color from available color index/spectral information with an explicit
  unknown state. Marker radius represents relative apparent brightness, not
  physical stellar radius; document clamping for legibility.

## Verification before switching the default

- Parser fixtures for both WDS variants and WDSS; verify precise theta/rho and
  their epoch against real supplied rows.
- Magnitudes exactly 10 are included; invalid/sentinel magnitudes are not.
- Excluded flag X never creates/enriches WDS relationships; S/U/Y mark Visual
  secondary leaves, and W is not an
  exclusion. Mixed accepted/rejected relationships do not erase valid stars.
- Already-present AT-HYG components, including faint ones, are reused.
  Repeated AB/AC/BC pairs and aliases never create duplicate individual stars.
- Matched stars retain AT-HYG RA/Dec unchanged and have `radec_source=GIA`;
  WDS-only positions have `radec_source=WDS`, with derivations distinguished
  from direct coordinates. Original AT-HYG `pos_src` remains intact.
- Errata renames/merges/deletions are scoped, auditable and idempotent.
  Removed pairs cannot return through synonyms or narrative notes, and a
  removed measure does not delete an otherwise valid star.
- Test Aa/Ab, AB-C and AB-CD structures, group-source aliases, uncertain
  matches, high-proper-motion stars, poles and RA wraparound.
- OMS IDs remain stable on reordered/rebuilt inputs. Numeric Gaia IDs are
  preserved exactly, with repeated source Gaia IDs allowed. All primary/group references resolve and no hierarchy
  cycles or contradictory role assignments are published.
- Fixtures include tiny and wide WDS separations, crowded systems, unknown
  geometry, shared Gaia IDs, P-unit normalization, numeric motion discrepancy
  reports and   the revised plain-text identifier and scalar relationship schema.
- Rendered binary count equals ordered sister count; no secondary sprites;
  component metadata remains complete even when render filters omit a primary.
- Version-1 sisters, stock-binary selection, mixed selections, export,
  saved settings, labels, constellation endpoints, picking, and navball
  behavior retain explicit regression coverage.
- Default regeneration must publish a consistent binary/identity/metadata
  set or leave the previous validated set intact on failure.
- Plot orientation/cardinal theta cases, angular scaling, missing positions,
  blended endpoints and epoch mismatches have deterministic checks.

## Suggested delivery sequence

1. Confirm remaining policies and locate/document WDSS.
2. Implement and validate an offline catalog builder with reviewable match
   evidence and stable IDs. Do not switch runtime defaults yet.
3. Integrate curated loading, versioned identities, primary-only binary
   generation, stock-binary support and packaging.
4. Add component lookup and the system plot, then verify runtime regressions.

## End-of-day handoff: 2026-10-19

This section supersedes earlier planning statements where they conflict.

### Current catalog and accepted policies

- Catalog revision 11, unchanged 50-column schema (manifest schema version 10).
  378,073 rows: 339,255 AT-HYG and 38,818 WDS-only.
  Roles: S 289,332; P 36,996; C 36,644; V 15,059; U 42.
  52,550 stored measurements; match-review.csv is header-only.
- Important policy change: WDS S/U/Y flags indicate nonphysical associations
  and are now INCLUDED as V secondary leaves globally, not excluded.
  X dubious doubles remain excluded. The primary remains P unless I-derived
  uncertainty makes it U. AT-HYG field precedence and stable OMS IDs remain.
- Albireo B revealed the original exclusion issue: WDS 19307+2758,
  STFA 43 AB carries S. It now belongs to Albireo as B / Visual, preserving
  OMS 267899 and its original AT-HYG data without adding a duplicate.
- The independent validator now uses accepted source classification evidence
  even when a particular relationship cannot be exported because another
  endpoint conflicts. A retained star's V/U evidence must not be lost merely
  because that relationship is omitted.
- Keep catalogs/identity-registry.csv permanently; do not recycle IDs.
  catalogs/Generate.ps1 runs builder fixtures, regeneration and validation.
  Keep source snapshots, registry, manifest and builder together for reproduction.

### Multi tab as implemented

- Ring toggle and persistent apparent-magnitude slider, range 0 to 20,
  default 8.9. Inclusive cutoff applies to P primary markers and picking only,
  not to the selected group's member list/plot. Unknown magnitudes get no ring.
  Rings appear only while Multi is visible. Overlaps pick nearest screen center,
  exact ties lowest OMS ID. Existing picker guards UI clicks and camera drags.
- Header: primary proper name if present, Bayer/constellation underneath,
  arrow-only button toggling an inline catalog:id list. No combo popup.
  RA and Dec appear once, on separate lines, then a separator.
- A separated explanatory line defines rho (arcseconds) and theta (degrees,
  north toward east, relative to the recorded relationship's reference).
- Member rows have aligned property columns: label / relationship,
  App Mag (2 decimals), Abs Mag (2 decimals), rho (1 decimal),
  theta (1 decimal), relative proper motion where applicable, then B-V LAST.
  V replaces relationship with "(Visual)"; U with "(Relationship Unknown)".
  Unknown/empty properties are omitted but their column positions stay aligned.
  Only V shows proper motion, component minus primary per axis in mas/year;
  an axis requires both values. No primary/true-companion motion display.
- Plot includes C, V and U wherever geometry resolves. Group endpoints such as
  A or AB use their primary/leading leaf as an approximate location, NOT a
  calculated barycenter. This changes only the diagram, not CSV measurements.
  Truly missing geometry remains unplotted and is listed. North up, east left;
  B-V colors (Tycho conversion retained), gray if unknown, dot size is an
  apparent-brightness proxy. Different epochs are not propagated to one date.
- Labels search positions away from nearby dots and existing label rectangles.
  Crowded labels can move farther away with connector lines. Fixture coverage
  includes close pairs, 14 coincident stars, and plot edges. Very dense plots
  still need in-game inspection; finite plot area can eventually be exhausted.

### Verification and deployment at stopping point

- CatalogBuilder fixtures and full source/structure validation passed after
  the S/U/Y policy change; validation.json is present.
- MultiTests fixtures and full-catalog run passed: 37,017 groups, 88,741
  members, 52,550 measurements, 87,405 plot-ready members. Explicit Albireo
  regression verifies Visual B's original ID and all 14 group members plotted.
- Mod build passed with 0 warnings/errors. Most iterative builds used an
  isolated session deployment, but the final USER-REQUESTED normal build ran:
  dotnet build .\src\OhMyStars.csproj
  It succeeded and executed the normal mod deployment target. The installed
  mod now has the latest DLL, settings and curated catalog assets.
  Normal builds copy repository settings.ini over deployed settings.
- No automated in-game visual/click validation was performed. The user did
  test the UI and Albireo during iteration; latest layout/header/label changes
  still need actual game inspection. Restart the game to reload cached catalog.
- No commits or uploads were made. Worktree contains this work plus earlier
  unrelated edits in window/editor/sky-culture/render/settings files and
  untracked src/wds_precise.txt. Preserve them; do not reset the worktree.

### Next-session priorities

1. Inspect the latest deployed Multi UI in-game: aligned columns, expanded IDs,
   magnitude filtering, overlapping rings, and crowded labels on Albireo and
   nested systems. Tune layout/zoom if needed without inventing catalog data.
2. The rendered star binary/default generator STILL uses the old AT-HYG 3.2
   catalog. The curated CSV currently supplies Multi only. The user expects
   probably an 8.9 cutoff for the future shipped binary; it has NOT been wired
   as the curated default binary yet.
3. Implement primary/single-aware curated binary generation and versioned
   sister/OMS metadata, preserving stock-binary selection and existing features.
   Do not feed every curated companion row to the generic binary generator.
   Plan U handling explicitly (intended to render like singles) and keep V/C
   from becoming duplicate rendered sprites.
4. Structured WDSS input remains unavailable; narrative notes are not WDSS.
   Common-epoch propagation, real group barycenters and wide/close-subsystem
   plot navigation remain future work. Source redistribution/licensing caveats
   in catalogs/README.md still apply.

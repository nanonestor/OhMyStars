# Session handoff - 2026-10-20

## FINAL UPDATE - 23:25 local (supersedes earlier snapshot below)

User is preparing the current build and will commit/push themselves. No commit,
push or release was performed by the assistant. Preserve the large dirty and
untracked worktree, including the user's packaging changes.

### Final verification and important follow-ups

- Latest build/deployment passed with zero warnings/errors. Final packaged
  catalog, reader guide, attributions and OMS notes match source hashes.
- Catalog SHA-256:
  `E051082A3E3522EC829D7788C635C0C422CD8E28167180AB120D5E2780B49FDA`.
- CatalogBuilder self-tests and full independent source validation passed after
  the final catalog regeneration. Isolated MultiTests passed after subsequent
  code changes, including filters, naming, search and plotting regressions.
- IMPORTANT: the real-catalog MultiTests run is NOT entirely green. Geometry
  checks pass across all 37,016 systems, then an old assertion expects
  Albireo B / OMS267899 inside Albireo's system. It is currently an S row.
  This was reported to the user; neither the catalog nor assertion was changed
  to hide it. Investigate identity/matching separately next session.
- Latest help-page and packaging-only changes were build-validated, not
  exercised through an automated in-game UI test.
- Builds copy source settings.ini over installed settings.ini; earlier notes
  below claiming settings were preserved do NOT describe current deployment.
- Game can lock the mod DLL. Ask the user to close it; do not kill the game.
  Copy tasks can retry even with CopyRetryCount=0; verify hashes after builds.

### Compatibility and completed window behavior

- Current game install: `C:\Program Files\Ahwoo\Games\Kitten Space Agency`.
  Updated assembly references include Brutal.Numerics. Ignored decompiled
  sources are refreshable with tools/Refresh-Decompiled.ps1; obsolete inspected
  caches were removed. Game version inspected:
  `2026.10.19.5638+0336992a43d6a74f798ae1ef95981d83c1de6373`.
- When actual ModMenu is not loaded, DrawProgramMenusHook supplies Oh My Stars
  and Stars Editor entries. ModMenu.Attributes alone does not suppress fallback.
- Disable Mouse Clicking is the fixed button label; green dot on its left only
  when disabled. Disable state defaults false on launch and is not persisted.
- With highlights off, Multi uses regular star picking without jumping tabs.
  Singles show heading/RA/Dec and "The selected star has no recorded companions."
- Highlight toggle text is "Highlight multi star systems".
- Stars' Multiple Star info shortcut is flanked by green > and <.
- Multi ? now opens an inline information sub-view with Back at top, containing
  the user's two supplied paragraphs. No separate floating help window.
  MultiHelp persists as parent Multi; selection/filters survive Back.

### Final Multi filters/defaults

- Collapsible arrow list order: primary magnitude cutoff, component cutoff,
  Exclude WDS-only entries, Exclude Visual Entries, Limit highlights to center
  radius, highlight-radius slider.
- Primary cutoff 8.9; component cutoff 11 (range 0-20).
- WDS-only exclusion FALSE; visual exclusion FALSE; highlight rings FALSE.
- Visual exclusion hides V only, not Q. Shared eligibility helpers cover
  component rows, summaries, plots, orbit visibility, rings and ring picking.
  Hidden intermediates still participate in geometry.
- Center-radius restriction defaults FALSE, radius 25%, slider 0-50% of
  viewport height. Uses viewport origin/center, includes boundary, filters
  ring centers and click targets together. Both settings persisted/reset.

### Plot fallback and component-list changes

- Rho/theta geometry is authoritative. Runtime-only coordinate approximation
  anchors disconnected members from primary-relative spherical RA/Dec offsets,
  then follows measured links forward/reverse. RA hours, Dec degrees;
  wraparound and declination scaling tested. Never moves measured positions.
- Layout cached per selection. Approximation opt-in stays OFF for builder/
  scientific classification callers to prevent display fallback changing roles.
- Plot top band lists "Not Shown - Ca, Cb" for enabled members without positions,
  and "RA/Dec Approx - ..." for coordinate anchors and dependent companions.
  Deliberately hidden members do not appear in notices.
- Real catalog fallback adds only 5 plot-ready members. OMS24185 Ca/Cb still
  cannot be placed: neither has coordinates and their pair lacks an A anchor.
- OMS24185 B is the 2022 WDS rho0.083/theta39.6 measurement. Its separate ORB6
  curve disagrees by about 0.036 arcseconds; projection tests pass. Do NOT snap
  recorded observations onto fitted orbits merely for visual agreement.
- WDS-only nonprimary entries lacking complete coordinates AND any positive
  incoming/outgoing/group rho link are replaced in rows by the user's wording:
  "Components not listed becuase contain no ra/dec, or rho values: ...".
  Unanchored Ca/Cb with an internal rho remain normal rows.

### Names and search

- Selected headings retain proper/Flamsteed/Bayer precedence. Catalog fallback:
  Burnham/HD, Burnham, HD/F. Struve, HD/O. Struve, HD, F./O. Struve,
  F. Struve, O. Struve, other catalog identifier, OMS.
- Headings and expanded identifier values add Sigma for F. Struve and
  Omicron-Sigma for O. Struve if the active font supports both. Bundled
  JetBrains Mono and 3270 do; MilSpec33558 does not. Text-only fallback remains.
  Search labels/accepted text are unchanged by symbol decoration.
- Unnumbered Bayer search matches numbered labels (Epsilon Lyrae matches
  Epsilon-1/2 Lyrae); numbered queries retain specificity.
- Additional unnumbered system Bayer result selects the recorded primary.
  Cached per catalog, deduplicated within each system; separate systems retain
  separate aliases. Numbered individual results remain. Wired into All/catalog
  Bayer searches and Bayer selector. ImGui result IDs distinguish aliases.
- Navball label priority: proper, abbreviated Bayer/constellation, HD,
  Flamsteed/abbreviated constellation, F. Struve, O. Struve, HIP, any other
  recorded identifier, OMS last. Window/background priorities unchanged.

### Final catalog state and scientific policy

- Documentation revision22, identity-first trial, 378,699 rows:
  S289931, P36995, C45949, Q1097, V4685, U42.
  All329571 magnitude-limited base stars retained; 338817 AT-HYG rows,
  39882 WDS-only rows; 52583 measurements.
- Motion-only Q requires BOTH delta_mu >100 mas/year AND delta_mu greater
  than primary total motion (100%). Strict boundaries; incomplete vectors do
  not trigger Q. Not a parallax/binding test. 9069 Q entries became C.
- Scoped fixes and rationale are in catalogs/oms-notes.txt: Izar, Sirius,
  Epsilon Lyrae, Almach. Each note one line, IDs comma-separated, blank line
  between entries. Do not invent spectroscopic leaves from old narrative.
- Almach primary OMS22094; representative B OMS22096/HD12534 may be blended BC;
  C OMS369407 retained separately. User approved representative mapping.
  B/C curated physical despite WDS S flag; D unchanged.
- Generic nested-pair fix avoids treating repeated WDS system/A coordinate
  as a nonprimary endpoint's direct position.
- CuratedBinaryVersion remains "8-almach-anchor-fix"; user-generated binaries
  must not be automatically rebuilt.
- 23852 rows lack both RA/Dec (all WDS-only); 23822 are nonprimary companions.
  Of those companions, 23662 have positive rho on their own row.
- Reversible identity-first baseline remains OUTSIDE repository:
  `C:\Users\Kerry\.copilot\session-state\56438f0f-dedc-42e0-ad1f-3bfbbe8bbdf5\files\pre-identity-first`.
  Includes Restore.ps1 and exact pre-trial sources/catalog. Preserves reserved
  IDs on restore. WARNING: restoring also removes subsequent Almach/catalog
  refinements; never run without user request. It is not durable git history.

### Packaged documentation and unresolved licensing

- catalogs/oms_catalog_readme.md explains columns/interpretation, without its
  initial title/intro and Encoding section (removed at user request).
- catalogs/attributions.md carries catalog credits and licensing caveats.
- Full catalogs/README.md and build-manifest.json are SOURCE ONLY, not packaged.
  Build deletes their stale output/deployed copies. Runtime does not need them.
- Packaged catalog content: OMS CSV, reader guide, attributions, oms-notes,
  orb6orbits.txt, orb6format.txt and orb6-source.json. Licenses/NOTICE remain.
- No catalog content/licensing changes made after the user's licensing question.
  AT-HYG is CC BY-SA4.0; acknowledgment included for WDS/ORB6. Explicit
  redistribution terms were not established for WDS/ORB6/HD. This is unresolved
  permission evidence, NOT a finding of known infringement or legal clearance.
- NOTICE.txt still describes the combined CSV broadly as CC BY-SA4.0.
  Recommended follow-up: distinguish confirmed AT-HYG/own-contribution terms
  from unconfirmed third-party terms and seek WDS/ORB6 maintainer confirmation
  before public distribution. User was informed; did not request that edit.

---

## Earlier snapshot (historical; overridden by final update above)

## State at stopping

- User finished for the night; no outstanding requested implementation.
- All changes described below are saved, built and deployed successfully.
- Installed DLL, catalog and manifest were hash-verified after the final build.
- Installed settings were preserved during every deployment.
- No commits made. Large existing dirty/untracked worktree is intentional;
  do not reset or discard it.
- User confirmed the background label collision handling and optimized search
  worked well. Latest Epsilon Lyrae correction still needs in-game inspection.
- Catalog SHA-256:
  `B55DD147C50804312AC11D19B440779B258F28D0A303E15351DD81D56EAF0FDE`
  (source and installed copy verified equal at stopping).

## Search and Stars UI

- OMS is the only shipped catalog and canonical shared selection identity.
  `OhMyStarsWindow.SelectedOmsId` is shared with Multi; legacy Hip-named
  rendering APIs carry OMS IDs internally. Actual HIP is used at external
  sky-culture/game-body boundaries.
- Search By order starts All, then Constellations, Proper, Bayer and individual
  saved catalog identifier types. Cursor is no longer a search method.
- All is still the default; empty/whitespace input shows no results.
- Search text is the first item inside the selection popup.
- Specific catalog searches show only the matching identifier, not an appended
  proper/Bayer name.
- Exact raw/display identifiers and individual semicolon aliases precede partial
  matches, including exact matches found later in the catalog.
- All/catalog searches run asynchronously with 150 ms debounce, cancellation
  of superseded queries and a 201-partial-result cap. UI displays at most 200.
  Errors are logged and displayed with Retry search.
- Measured All latency improved from roughly 400-730 ms to 200-300 ms; moving
  work off the drawing thread removed the main UI stall.
- Search By dropdown is half its former width; label and combo centered together.
  Star label is on the left in every selection mode.
- Stars information header matches Multi: Proper / Flamsteed on line one,
  full Bayer/genitive on line two. Fallback Burnham / HD, then F. Struve /
  O. Struve, then other recorded catalog IDs; OMS last.
- Mouse selection defaults on only while Stars/Multi is visible and usable.
  Button now says Disable Mouse Select / Enable Mouse Select, with green On.
  Stars picker excludes non-primary system members and unknown magnitudes.

## Multi UI

- Top row: Show multiple-system primary rings checkbox.
- Next row: expansion arrow (tooltip Filters) and ? button (tooltip Information).
- Arrow reveals both apparent-magnitude cutoff sliders and Exclude WDS-only
  entries. Starts collapsed; persisted filter values remain effective.
- ? opens the existing Multiple Star Help window, still containing
  `example entry one` pending future user content.
- Separator below controls, above names. Headings enlarged 30%.
- IDs use colon/space formatting. RA and Dec share a line below a separator.
- rho/theta/WDS-only legend is below component rows, above plot.
- No details or Stars shortcut when no companion passes current filters;
  shared selection retained so relaxing filters restores details.
- Default Exclude WDS-only entries remains true. Primary cutoff affects rings,
  not selected details. Component apparent magnitude cutoff is 0-20.

## Background labels

- Added shared collision-aware placement in
  [ScreenLabelLayout.cs](../src/ScreenLabelLayout.cs) and
  [SkyLabelRenderer.cs](../src/SkyLabelRenderer.cs).
- Picker and selected labels take priority, then ordinary star names, then
  spectra/asterism text. Grid text reserves occupied areas.
- Bounded nearby placement, viewport clamping, 64-pixel spatial grid.
  Labels with no nearby space are omitted, not overlapped.
- Deterministic frame reset and crowd/edge tests are in MultiTests.
- Background name hierarchy is separately cached, leaving information headers
  unchanged: Proper only, full Bayer, Flamsteed/genitive, F. Struve,
  O. Struve, Burnham, HD; otherwise no name.
- Same hierarchy for selected/picked background text. Existing active-sky-culture
  scope for ordinary star names was retained.

## Catalog corrections: revisions 15-17

### Confirmed orbital membership

- Builder now accepts exact original WDS pair evidence from unique supported
  ORB6 visual solutions, even where a parent endpoint became a representative
  leaf. Promotes only measurement-owning Q/V row to C; no blanket propagation
  to all subgroup leaves. Preserves P and I-derived U.
- This added 50 C promotions over revision 14: 46 Q and 4 V, including Sirius B,
  Alrescha B, Alsephina B, Alula Australis B. Existing strict plot matching
  was not relaxed.
- Core files: OrbitalClassification.cs, Program.cs, Validation.cs, CatalogTests.cs.

### Sirius: revision 16

- Real Sirius B was a separate S row, not a second P, but appeared independently.
- Explicit association AT40:586689 / OMS84637 -> Sirius system OMS84638,
  category C, label B. Original AT-HYG fields preserved.
- Sirius remains P / A. Normalize WDS Ba endpoint to B for this system only.
- Optical Bb retained as V without making B an unresolved Ba/Bb subsystem.
- Duplicate WDS Ba OMS376056 retired; registry ID never recycled.
- Original source line numbers and numeric measurements preserved.
- Exact A-B orbit now plots. Real B passes default WDS-only exclusion.
- Guard validates source magnitude and proper name; manifest documents override.

### Epsilon Lyrae: revision 17 (latest)

- WDS18443+3940 contains main A-B and C-D pairs PLUS tiny CHR77 Ca-Cb.
- Real HD173607 was wrongly assigned to Cb because its AT-HYG position equals
  the C/Ca position and source Cb magnitude is absent. A duplicate WDS D
  was then generated. User explicitly approved retaining BOTH Ca and Cb.
- Scoped source identity assignments:
  - A OMS253698 remains P, Epsilon-1.
  - B OMS253697 is C.
  - Ca AT40:1915959 / OMS253708 / HD173608 remains C, Epsilon-2.
  - D AT40:1915960 / OMS253709 / HD173607 is C, replacing WDS duplicate D.
  - Cb is distinct WDS-only C, unknown magnitude; no invented photometry.
- WDS D OMS372209 retired; its registry identity remains reserved.
- B/Ca/Cb/D physical override is scoped to this system, after U/P precedence.
  A-B has two supported ORB6 solutions and is still omitted from plotting as
  ambiguous, not taken as evidence of optical membership.
- C-D rho2.43 arcsec and Ca-Cb rho0.058 arcsec remain separate measurements.
- Cb needs Exclude WDS-only entries disabled to appear. D is now visible with
  default exclusion.
- Original AT-HYG fields unchanged, original WDS relationships/values retained.
- Builder source-identity guards check Ca/D HD identities and WDS-only Cb.

## Latest catalog / validation

- Catalog documentation revision17, schema11.
- 377,974 data rows; 37,019 groups; 88,769 members; 52,582 measures;
  87,433 plot-ready members.
- Roles: S289205, P36998, C36940, Q10115, V4674, U42.
- 339,482 AT-HYG rows, 38,492 WDS-only rows.
- All original 329,571 mag<=10 base stars preserved.
- 3,238 unique supported ORB6 solutions, 2,704 exact individual-pair plot curves.
- Stable OMS identity registry intentionally has gaps; never renumber/recycle.
- Latest generation ran full builder fixtures + full independent source validation.
- Latest MultiTests verified real corrected Sirius/Epsilon identities, no duplicate
  rows, correct physical roles, inner orbit and existing regressions.
- Latest normal build: zero warnings/errors, successful deployment.

## Commands and operational cautions

Working directory: `D:\ksa-mods\OhMyStars` (Windows / PowerShell / .NET10).

```powershell
.\catalogs\Generate.ps1
dotnet run --project .\tools\MultiTests\MultiTests.csproj -- .\catalogs\oms_catalog.csv
dotnet run --project .\tools\CatalogBuilder\CatalogBuilder.csproj -- --self-test
dotnet run --project .\tools\MultiTests\MultiTests.csproj -- .\catalogs\oms_catalog.csv --benchmark-search
dotnet build .\src\OhMyStars.csproj --no-restore -t:Compile
```

- Default source root for generation: `D:\ksa\stars\catalogs`.
- Normal `dotnet build .\src\OhMyStars.csproj --no-restore` deploys to
  `C:\Users\Kerry\Documents\My Games\Kitten Space Agency\mods\OhMyStars`.
- Build overwrites installed settings.ini: save its bytes and restore in finally.
- Running game may lock DLL. Do not terminate game; use Compile target and
  report deployment pending. Final deployment this session succeeded.
- Catalog is installed in `catalogs\oms_catalog.csv`, not mod root.
- Default generated binary version remains `4-oms`; corrections here change
  membership/roles but not the preserved real AT-HYG coordinates/IDs.
- Catalog data are provisional. Avoid broad matching-radius increases or
  interpreting all WDS associations as physical. Current famous-star fixes are
  deliberate scoped identity corrections, not general source rewrites.
- No active background commands/agents or temporary diagnostic files remain.

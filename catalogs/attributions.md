# Catalog source attributions and licensing

The OMS catalog combines material from the sources below. Source catalogs
retain their own attribution and usage conditions; a license on one input
does not relicense third-party material from another. The source repository
retains the full construction README and build manifest with input hashes.

## AT-HYG 4.0

- David Nash, Augmented Tycho-HYG (AT-HYG):
  <https://astronexus.com/projects/at-hyg>.
- Base input: `athyg_40.csv`, with its supplied `LICENSE` and
  `ACKNOWLEDGMENTS.md`.
- Licensed **CC BY-SA 4.0**:
  <https://creativecommons.org/licenses/by-sa/4.0/>.
  See the supplied [license text](../licenses/CC_BY-SA_4.0.txt).
- OMS modifies the selection, adds columns, cross-references and WDS
  information. Original nonempty AT-HYG fields are preserved.
  AT-HYG-derived material retains its attribution/share-alike obligations.
- Upstream acknowledgments include ESA's Hipparcos/Tycho catalogs (1997,
  ESA SP-1200), Gaia DR3/DPAC, Yale Bright Star Catalog (Hoffleit 1991),
  and Gliese-Jahreiss (1991).
- This work has made use of data from the ESA mission Gaia, processed by
  the Gaia Data Processing and Analysis Consortium (DPAC). Funding for DPAC
  has been provided by national institutions, in particular institutions
  participating in the Gaia Multilateral Agreement.
  <https://www.cosmos.esa.int/gaia>

## Washington Double Star Catalog

- Maintained by the U.S. Naval Observatory:
  <https://www.astro.gsu.edu/wds/>.
- Inputs include `wds_precise.txt`, `wdsnewnotes_main.txt`, Herschel `h.txt`,
  F. Struve `stf.txt`, O. Struve `stt.txt`, HDS/HIP `hds.txt`, Bayer/Flamsteed
  aliases `bf.txt`, historical cross-references `wdsidsadsbds.txt`, and
  `wdserror.txt`.
- Acknowledgment: "This research has made use of the Washington Double Star
  Catalog maintained at the U.S. Naval Observatory."
- No explicit standalone redistribution license was identified in the
  supplied files. Government hosting alone does not establish redistribution
  rights for every contributed measurement or narrative. Confirm applicable
  redistribution and attribution terms before public distribution.
  AT-HYG's CC BY-SA terms do not themselves relicense WDS material.

## Sixth Catalog of Orbits of Visual Binary Stars (ORB6)

- Rachel A. Matson, Stephen J. Williams, William I. Hartkopf and Brian D.
  Mason, U.S. Naval Observatory.
- Catalog citation: Hartkopf, Mason and Worley (2001), *Sixth Catalog of
  Orbits of Visual Binary Stars*, with current online updates and the
  individual orbit references retained in the elements file.
- Official catalog: <https://www.astro.gsu.edu/wds/orb6.html>.
  Description and grading:
  <https://www.astro.gsu.edu/wds/orb6/orb6text.html>.
  References: <https://www.astro.gsu.edu/wds/orb6/wdsref.html>.
- [orb6orbits.txt](orb6orbits.txt) and [orb6format.txt](orb6format.txt)
  are unmodified official elements and field-documentation snapshots,
  retrieved 2026-10-19. The homepage reported a 2026-10-13 update.
  [orb6-source.json](orb6-source.json) retains URLs, SHA-256 hashes, byte
  counts and source/license-status information.
- OMS derives relative orbit curves and supported component classifications
  from selected records; the original elements remain unchanged.
- Acknowledgment: "This research has made use of the Washington Double Star
  Catalog maintained at the U.S. Naval Observatory."
- No explicit standalone redistribution license was identified on the
  inspected catalog and format pages. Confirm applicable terms before public
  distribution; government hosting alone does not license all contributed
  solutions, and AT-HYG's CC BY-SA terms do not relicense ORB6.

## Henry Draper Catalogue and Extension (III/135A)

- Cannon, A. J. and Pickering, E. C., Harvard Annals 91-100 (1918-1924);
  ADC corrected/extended machine-readable release (1989).
- CDS/VizieR: <https://cdsarc.cds.unistra.fr/viz-bin/cat/III/135A>.
- Inputs: `catalog.dat`, `ReadMe.txt`, and `errata.htx`. Exact unique DM
  cross-references fill supported missing HD identifiers; existing nonempty
  AT-HYG identifiers are not replaced. Historical positions and photometry
  are not imported from this source.
- The supplied documentation contains no explicit standalone license grant.
  Confirm CDS/catalog redistribution conditions before public distribution.
  Historical origin does not establish a license for every corrected
  electronic record.

See also [NOTICE.txt](../NOTICE.txt) for other distributed material.
This document records source terms and outstanding caveats; it is not legal
clearance to distribute all combined material under a single license.

"""Imports in-game developer review overrides into the catalog source data.

Usage:  python tools/import_reviews.py [path-to-system-review-overrides.csv]

Reads the mod's runtime review file (default: the Documents mods install),
validates every row against catalogs/oms_catalog.csv per the contract in
catalogs/README.md (identity, label, original type and primary WDS must all
match), then:
  * merges component type requests into catalogs/user-component-reviews.csv
    (consumed by CatalogBuilder as highest-priority role overrides), and
  * merges system approvals into catalogs/definitively-correct-systems.csv.

The runtime file is NOT cleared here; clear it only after the regenerated
catalog has been validated and deployed (see catalogs/README.md).
"""

import csv
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CATALOG = os.path.join(REPO, "catalogs", "oms_catalog.csv")
REVIEWS_OUT = os.path.join(REPO, "catalogs", "user-component-reviews.csv")
APPROVALS_OUT = os.path.join(REPO, "catalogs", "definitively-correct-systems.csv")
DEFAULT_SOURCE = os.path.expanduser(
    r"~\Documents\My Games\Kitten Space Agency\mods\OhMyStars\dev\system-review-overrides.csv")

VALID_TYPES = {"S", "P", "C", "V", "Q", "U", "unlink"}


def load_catalog():
    stars = {}
    with open(CATALOG, newline="", encoding="utf-8") as f:
        for row in csv.DictReader(f):
            stars[row["oms_id"]] = {
                "wds_id": row["wds_id"],
                "role": row["component"],
                "label": row["component_label"],
                "primary": row["primary_oms_id"],
                "name": row["proper"],
            }
    return stars


def main():
    source = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SOURCE
    if not os.path.exists(source):
        sys.exit(f"Runtime review file not found: {source}")
    stars = load_catalog()

    with open(source, newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    errors = []
    approvals = {}
    components = {}
    for i, r in enumerate(rows, start=2):
        if r["approved"].strip():
            primary = stars.get(r["primary_oms_id"])
            if primary is None:
                errors.append(f"line {i}: unknown primary OMS {r['primary_oms_id']}")
            elif primary["wds_id"] != r["wds_id"]:
                errors.append(f"line {i}: primary {r['primary_oms_id']} WDS mismatch "
                              f"({r['wds_id']} vs catalog {primary['wds_id']})")
            elif primary["role"] != "P":
                errors.append(f"line {i}: OMS {r['primary_oms_id']} is not a system primary "
                              f"(role {primary['role']})")
            else:
                approvals[r["primary_oms_id"]] = (r["approved"] == "true",
                                                  r["wds_id"], r["name"])
        elif r["component_oms_id"].strip():
            comp = stars.get(r["component_oms_id"])
            if comp is None:
                errors.append(f"line {i}: unknown component OMS {r['component_oms_id']}")
                continue
            problems = []
            if comp["wds_id"] != r["wds_id"]:
                problems.append(f"WDS {r['wds_id']} vs catalog {comp['wds_id']}")
            if comp["primary"] != r["primary_oms_id"]:
                problems.append(f"primary {r['primary_oms_id']} vs catalog {comp['primary']}")
            if comp["label"] != r["component_label"]:
                problems.append(f"label {r['component_label']} vs catalog {comp['label']}")
            if comp["role"] != r["original_type"]:
                problems.append(f"original type {r['original_type']} vs catalog {comp['role']}")
            if r["desired_type"] not in VALID_TYPES:
                problems.append(f"unsupported desired type {r['desired_type']}")
            if problems:
                errors.append(f"line {i}: component {r['component_oms_id']}: " + "; ".join(problems))
            else:
                components[r["component_oms_id"]] = r

    if errors:
        print(f"{len(errors)} validation failure(s); nothing was written:")
        for e in errors[:50]:
            print(" ", e)
        if len(errors) > 50:
            print(f"  ... and {len(errors) - 50} more")
        sys.exit(1)

    # Merge component reviews (one row per component; newest import wins).
    existing = {}
    if os.path.exists(REVIEWS_OUT):
        with open(REVIEWS_OUT, newline="", encoding="utf-8") as f:
            for row in csv.DictReader(f):
                existing[row["component_oms_id"]] = row
    for oms, r in components.items():
        existing[oms] = {
            "primary_oms_id": r["primary_oms_id"],
            "wds_id": r["wds_id"],
            "component_oms_id": r["component_oms_id"],
            "component_label": r["component_label"],
            "original_type": r["original_type"],
            "desired_type": r["desired_type"],
            "notes": r["notes"],
        }
    with open(REVIEWS_OUT, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, ["primary_oms_id", "wds_id", "component_oms_id",
                                    "component_label", "original_type", "desired_type", "notes"],
                                lineterminator="\n")
        writer.writeheader()
        for row in sorted(existing.values(), key=lambda r: (int(r["primary_oms_id"]),
                                                            int(r["component_oms_id"]))):
            writer.writerow(row)

    # Merge approvals: add true rows, drop false rows, keep the rest.
    kept = {}
    with open(APPROVALS_OUT, newline="", encoding="utf-8") as f:
        for row in csv.DictReader(f):
            kept[row["primary_oms_id"]] = (row["wds_id"], row["name"])
    added = removed = 0
    for oms, (approved, wds, name) in approvals.items():
        if approved and oms not in kept:
            kept[oms] = (wds, name)
            added += 1
        elif not approved and oms in kept:
            del kept[oms]
            removed += 1
    with open(APPROVALS_OUT, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f, lineterminator="\n")
        writer.writerow(["primary_oms_id", "wds_id", "name"])
        for oms in sorted(kept, key=int):
            writer.writerow([oms, kept[oms][0], kept[oms][1]])

    print(f"Imported {len(components)} component review(s) -> {REVIEWS_OUT}")
    print(f"Approvals: +{added} / -{removed} (total {len(kept)}) -> {APPROVALS_OUT}")
    print("Remember: clear the runtime overrides file only after the regenerated "
          "catalog validates and the Debug deployment succeeds.")


main()

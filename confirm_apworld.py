"""Quick script to confirm the APWorld is set up as expected.
Run this after executing '01_generate.sh' a few times."""
import sys
import warnings
import zlib
import pickle
import shutil
from pathlib import Path

PACKAGE_ROOT = Path(__file__).parent.parent
OUTPUT_DIR = PACKAGE_ROOT / "Archipelago-KSP" / "output"

# Archipelago must be on sys.path for pickle to resolve AP types.
sys.path.insert(0, str(PACKAGE_ROOT / "Archipelago-KSP"))

with warnings.catch_warnings():
    warnings.simplefilter("ignore")
    from worlds.ksp1.Items import ALL_ITEMS, PART_BUNDLE_ITEMS
    from worlds.ksp1.Locations import (
        ALL_LOCATIONS,
        tech_id_to_location_id,
        facility_to_location_id,
        body_to_location_id,
    )
    from worlds.ksp1 import KSPWorld

VALID_ITEM_IDS = {d.id for d in ALL_ITEMS.values()}
EXPECTED_LOC_COUNT = len(ALL_LOCATIONS)  # 95

EXPECTED_SLOT_DATA_KEYS = {
    "tech_id_to_location_id",
    "facility_to_location_id",
    "body_to_location_id",
    "part_bundle_to_tech_id",
    "starting_pod",
    "starting_chute",
    "starting_srb",
    "starting_experiment",
}

VALID_PODS        = set(KSPWorld._STARTING_PODS)
VALID_CHUTES      = set(KSPWorld._STARTING_CHUTES)
VALID_SRBS        = set(KSPWorld._STARTING_SRBS)
VALID_EXPERIMENTS = set(KSPWorld._STARTING_EXPERIMENTS)

# Location ID ranges (inclusive).
TECH_RANGE  = (1970000, 1970061)   # 62 tech nodes
KSC_RANGE   = (1971000, 1971017)   # 18 KSC upgrades
FLAG_RANGE  = (1972000, 1972014)   # 15 flag plants


def load_archipelago(path: Path) -> dict:
    """Deserialize a .archipelago file (1-byte type tag + zlib + pickle)."""
    raw = path.read_bytes()
    # byte 0 is a type tag (0x03); bytes 1..end are a standard zlib stream.
    return pickle.loads(zlib.decompress(raw[1:]))


_failures = 0

# Coverage accumulators: track which starting values have been seen across all zips.
seen_pods        = set()
seen_chutes      = set()
seen_srbs        = set()
seen_experiments = set()


def check(label: str, ok: bool, detail: str = "") -> bool:
    global _failures
    tag = "PASS" if ok else "FAIL"
    suffix = f" -- {detail}" if detail and not ok else ""
    print(f"    [{tag}] {label}{suffix}")
    if not ok:
        _failures += 1
    return ok


def validate_slot(slot_id: int, slot_info, data: dict) -> None:
    sd   = data["slot_data"].get(slot_id, {})
    locs = data["locations"].get(slot_id, {})

    # ---- accumulate coverage ----
    if pod := sd.get("starting_pod"):
        seen_pods.add(pod)
    if chute := sd.get("starting_chute"):
        seen_chutes.add(chute)
    if srb := sd.get("starting_srb"):
        seen_srbs.add(srb)
    if exp := sd.get("starting_experiment"):
        seen_experiments.add(exp)

    # ---- game identity ----
    check("game name", slot_info.game == "Kerbal Space Program",
          f"got {slot_info.game!r}")

    # ---- slot_data keys ----
    missing = EXPECTED_SLOT_DATA_KEYS - set(sd.keys())
    check("slot_data keys complete", not missing, f"missing: {missing}")

    # ---- tech_id_to_location_id ----
    t2l = sd.get("tech_id_to_location_id", {})
    check("tech locations count == 62", len(t2l) == 62, f"got {len(t2l)}")
    bad = {k: v for k, v in t2l.items()
           if not (TECH_RANGE[0] <= v <= TECH_RANGE[1])}
    check("tech location IDs in range", not bad, str(bad))
    check("tech_id_to_location_id matches APWorld", t2l == tech_id_to_location_id,
          str({k: (t2l.get(k), tech_id_to_location_id.get(k))
               for k in set(t2l) ^ set(tech_id_to_location_id)}))

    # ---- facility_to_location_id ----
    f2l = sd.get("facility_to_location_id", {})
    check("facility locations count == 18", len(f2l) == 18, f"got {len(f2l)}")
    bad = {k: v for k, v in f2l.items()
           if not (KSC_RANGE[0] <= v <= KSC_RANGE[1])}
    check("facility location IDs in range", not bad, str(bad))
    check("facility_to_location_id matches APWorld", f2l == facility_to_location_id)

    # ---- body_to_location_id ----
    b2l = sd.get("body_to_location_id", {})
    check("body locations count == 15", len(b2l) == 15, f"got {len(b2l)}")
    bad = {k: v for k, v in b2l.items()
           if not (FLAG_RANGE[0] <= v <= FLAG_RANGE[1])}
    check("body location IDs in range", not bad, str(bad))
    check("body_to_location_id matches APWorld", b2l == body_to_location_id)

    # ---- part_bundle_to_tech_id ----
    pb2t = sd.get("part_bundle_to_tech_id", {})
    check("part_bundle_to_tech_id count == 63", len(pb2t) == 63, f"got {len(pb2t)}")
    # Every tech_id in the map should resolve to a known bundle
    expected_pb2t = {
        name: d.tech_id for name, d in PART_BUNDLE_ITEMS.items() if d.tech_id
    }
    check("part_bundle_to_tech_id matches APWorld", pb2t == expected_pb2t)

    # ---- starting parts ----
    check("starting_pod valid",        sd.get("starting_pod")        in VALID_PODS,
          f"got {sd.get('starting_pod')!r}")
    check("starting_chute valid",      sd.get("starting_chute")      in VALID_CHUTES,
          f"got {sd.get('starting_chute')!r}")
    check("starting_srb valid",        sd.get("starting_srb")        in VALID_SRBS,
          f"got {sd.get('starting_srb')!r}")
    check("starting_experiment valid", sd.get("starting_experiment") in VALID_EXPERIMENTS,
          f"got {sd.get('starting_experiment')!r}")

    # ---- location count ----
    check(f"location count == {EXPECTED_LOC_COUNT}", len(locs) == EXPECTED_LOC_COUNT,
          f"got {len(locs)}")

    # ---- location IDs in valid ranges ----
    def in_valid_range(lid: int) -> bool:
        return (
            (TECH_RANGE[0] <= lid <= TECH_RANGE[1]) or
            (KSC_RANGE[0]  <= lid <= KSC_RANGE[1])  or
            (FLAG_RANGE[0] <= lid <= FLAG_RANGE[1])
        )
    bad_lids = [lid for lid in locs if not in_valid_range(lid)]
    check("all location IDs in valid ranges", not bad_lids,
          f"unexpected: {bad_lids[:10]}")

    # ---- placed item IDs are all known KSP items ----
    bad_iids = [(lid, item_tuple[0]) for lid, item_tuple in locs.items()
                if item_tuple[0] not in VALID_ITEM_IDS]
    check("all placed item IDs valid", not bad_iids,
          f"unexpected (loc_id, item_id): {bad_iids[:5]}")


if __name__ == "__main__":
    zips = sorted(OUTPUT_DIR.glob("*.zip"))
    if not zips:
        print(f"No .zip files found in {OUTPUT_DIR}")
        sys.exit(1)

    for zip_path in zips:
        print(f"\n{zip_path.name}")
        temp_dir = OUTPUT_DIR / "temp_extract"
        try:
            shutil.unpack_archive(str(zip_path), str(temp_dir))
            ap_files = list(temp_dir.glob("*.archipelago"))
            if not ap_files:
                check(".archipelago file present", False, "none found in zip")
                continue
            for ap_path in ap_files:
                data = load_archipelago(ap_path)
                for slot_id, slot_info in data["slot_info"].items():
                    print(f"  Slot {slot_id}: {slot_info.name!r} ({slot_info.game})")
                    validate_slot(slot_id, slot_info, data)
        finally:
            shutil.rmtree(temp_dir, ignore_errors=True)

    # ---- coverage report ----
    coverage = {
        "pod":        (seen_pods,        VALID_PODS),
        "chute":      (seen_chutes,      VALID_CHUTES),
        "srb":        (seen_srbs,        VALID_SRBS),
        "experiment": (seen_experiments, VALID_EXPERIMENTS),
    }
    print("\nStarting-part coverage across all zips:")
    all_covered = True
    for category, (seen, valid) in coverage.items():
        missing = sorted(valid - seen)
        pct = len(seen) / len(valid) * 100
        status = "FULL" if not missing else f"{len(seen)}/{len(valid)}"
        print(f"  {category:12s} [{status:>7}] {pct:5.1f}%  seen: {sorted(seen)}")
        if missing:
            all_covered = False
            print(f"               missing: {missing}")

    print()
    if _failures == 0 and all_covered:
        print("ALL CHECKS PASSED  |  FULL COVERAGE")
    elif _failures == 0:
        print(f"ALL CHECKS PASSED  |  INCOMPLETE COVERAGE (generate more seeds)")
    else:
        print(f"{_failures} FAILURE(S)")
    sys.exit(0 if _failures == 0 else 1)

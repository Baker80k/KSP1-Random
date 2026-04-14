"""ANSI terminal visualization of which items land in which AP progression sphere.
Each block = one item; color = sphere of the location it is placed at (i.e. when
the player receives it). Filler items are aggregated by count, not individually.
Run from repo root: .venv/bin/python KSP1-Random/plot_item_spheres.py"""
import sys
import warnings
import zlib
import pickle
import shutil
from collections import Counter
from pathlib import Path

PACKAGE_ROOT = Path(__file__).parent.parent
OUTPUT_DIR = PACKAGE_ROOT / "Archipelago-KSP" / "output"
sys.path.insert(0, str(PACKAGE_ROOT / "Archipelago-KSP"))

with warnings.catch_warnings():
    warnings.simplefilter("ignore")
    from worlds.ksp1.Items import PART_BUNDLE_ITEMS, SOI_PERMIT_ITEMS, FILLER_ITEMS

# Ordered item IDs per category (definition order = ID order).
BUNDLE_IDS  = [d.id for d in PART_BUNDLE_ITEMS.values()]
PERMIT_IDS  = [d.id for d in SOI_PERMIT_ITEMS.values()]
FILLER_IDS  = {d.id for d in FILLER_ITEMS.values()}

# id -> name reverse lookups
ID_TO_BUNDLE = {d.id: name for name, d in PART_BUNDLE_ITEMS.items()}
ID_TO_PERMIT = {d.id: name for name, d in SOI_PERMIT_ITEMS.items()}
ID_TO_FILLER = {d.id: name for name, d in FILLER_ITEMS.items()}

SPHERE_FG = [
    "\033[92m",  # 0  bright green
    "\033[93m",  # 1  bright yellow
    "\033[91m",  # 2  bright red
    "\033[94m",  # 3  bright blue
    "\033[95m",  # 4  bright magenta
    "\033[96m",  # 5  bright cyan
    "\033[97m",  # 6  white
    "\033[90m",  # 7  dark gray
]
RESET = "\033[0m"
BOLD  = "\033[1m"


def load_archipelago(path: Path) -> dict:
    raw = path.read_bytes()
    return pickle.loads(zlib.decompress(raw[1:]))


def fg(sphere: int) -> str:
    return SPHERE_FG[sphere % len(SPHERE_FG)]


def item_to_sphere_map(data: dict, player: int) -> dict[int, int]:
    """Map item_id -> sphere index via the location it is placed at.
    Filler items repeat; each copy gets the sphere of its own location."""
    loc_spheres: dict[int, int] = {}
    for idx, sphere in enumerate(data["spheres"]):
        for lid in sphere.get(player, set()):
            loc_spheres[lid] = idx

    result: dict[int, int] = {}
    for lid, (item_id, item_player, _flags) in data["locations"][player].items():
        if item_player != player:
            continue  # item belongs to another slot; skip
        sphere = loc_spheres.get(lid, -1)
        result[lid] = (item_id, sphere)
    return result  # loc_id -> (item_id, sphere)


def block_row(label: str, ordered_ids: list[int], item_id_to_sphere: dict[int, int]) -> str:
    """One block per item ID in definition order, colored by sphere."""
    blocks = []
    for iid in ordered_ids:
        s = item_id_to_sphere.get(iid, -1)
        if s < 0:
            blocks.append(f"\033[90m?\033[0m")
        else:
            blocks.append(f"{fg(s)}\u2588{RESET}")
    return f"  {label:<12s} {''.join(blocks)}"


def render(data: dict, zip_name: str) -> None:
    n_spheres = len(data["spheres"])

    for player, slot_info in data["slot_info"].items():
        placement = item_to_sphere_map(data, player)  # loc_id -> (item_id, sphere)

        # Build per-item-id -> sphere (unique items only; filler handled separately)
        bundle_sphere: dict[int, int] = {}
        permit_sphere: dict[int, int] = {}
        # filler: list of (sphere) for each copy
        filler_by_sphere: Counter = Counter()

        for _lid, (item_id, sphere) in placement.items():
            if item_id in {d.id for d in PART_BUNDLE_ITEMS.values()}:
                bundle_sphere[item_id] = sphere
            elif item_id in {d.id for d in SOI_PERMIT_ITEMS.values()}:
                permit_sphere[item_id] = sphere
            elif item_id in FILLER_IDS:
                filler_by_sphere[sphere] += 1

        total_items = len(placement)

        print(f"{BOLD}{zip_name}{RESET}  slot {player}: {slot_info.name!r}")
        print(f"  {n_spheres} sphere(s)   {total_items} items placed")
        print()

        legend_parts = [f"{fg(s)}\u2588 {s}{RESET}" for s in range(n_spheres)]
        print(f"  sphere:  {'  '.join(legend_parts)}")
        print()

        # Block rows for unique-item categories
        print(block_row(f"BUNDLES({len(BUNDLE_IDS)})", BUNDLE_IDS, bundle_sphere))
        print(block_row(f"PERMITS({len(PERMIT_IDS)})", PERMIT_IDS, permit_sphere))
        # Filler: one block per copy, ordered by sphere
        filler_blocks = []
        for s in sorted(filler_by_sphere):
            filler_blocks.append(f"{fg(s)}{'\u2588' * filler_by_sphere[s]}{RESET}")
        filler_total = sum(filler_by_sphere.values())
        print(f"  {'FILLER(' + str(filler_total) + ')':<12s} {''.join(filler_blocks)}")
        print()

        # Per-sphere breakdown
        for s in range(n_spheres):
            bundles_here = [ID_TO_BUNDLE[iid][7:]   # strip "Parts: "
                            for iid in BUNDLE_IDS if bundle_sphere.get(iid) == s]
            permits_here = [ID_TO_PERMIT[iid]
                            for iid in PERMIT_IDS if permit_sphere.get(iid) == s]
            filler_here  = filler_by_sphere.get(s, 0)

            total_here = len(bundles_here) + len(permits_here) + filler_here
            print(f"  {fg(s)}{BOLD}Sphere {s}{RESET}  ({total_here} items)")
            if bundles_here:
                print(f"    Bundles ({len(bundles_here)}): {', '.join(bundles_here)}")
            if permits_here:
                print(f"    Permits ({len(permits_here)}): {', '.join(permits_here)}")
            if filler_here:
                print(f"    Filler  ({filler_here})")
            print()


if __name__ == "__main__":
    zips = sorted(OUTPUT_DIR.glob("*.zip"))
    if not zips:
        print(f"No .zip files found in {OUTPUT_DIR}")
        sys.exit(1)

    for zip_path in zips:
        temp_dir = OUTPUT_DIR / "temp_extract"
        try:
            shutil.unpack_archive(str(zip_path), str(temp_dir))
            for ap_path in sorted(temp_dir.glob("*.archipelago")):
                render(load_archipelago(ap_path), zip_path.name)
        finally:
            shutil.rmtree(temp_dir, ignore_errors=True)

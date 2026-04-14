"""ANSI terminal visualization of AP progression spheres.
Each colored block is one location; color = sphere it becomes reachable in.
Run from repo root: .venv/bin/python KSP1-Random/plot_spheres.py"""
import sys
import warnings
import zlib
import pickle
import shutil
from pathlib import Path

PACKAGE_ROOT = Path(__file__).parent.parent
OUTPUT_DIR = PACKAGE_ROOT / "Archipelago-KSP" / "output"
sys.path.insert(0, str(PACKAGE_ROOT / "Archipelago-KSP"))

with warnings.catch_warnings():
    warnings.simplefilter("ignore")
    from worlds.ksp1.Locations import TECH_LOCATIONS, KSC_UPGRADE_LOCATIONS, FLAG_LOCATIONS

TECH_IDS  = [d.id for d in TECH_LOCATIONS.values()]
KSC_IDS   = [d.id for d in KSC_UPGRADE_LOCATIONS.values()]
FLAG_IDS  = [d.id for d in FLAG_LOCATIONS.values()]

# ANSI colors per sphere index (wraps if > 8 spheres)
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


def loc_to_sphere_map(data: dict, player: int) -> dict[int, int]:
    result = {}
    for idx, sphere in enumerate(data["spheres"]):
        for lid in sphere.get(player, set()):
            result[lid] = idx
    return result


def fg(sphere: int) -> str:
    return SPHERE_FG[sphere % len(SPHERE_FG)]


def block_row(label: str, ids: list[int], l2s: dict[int, int]) -> str:
    blocks = []
    for lid in ids:
        s = l2s.get(lid, -1)
        if s < 0:
            blocks.append(f"\033[90m?\033[0m")
        else:
            blocks.append(f"{fg(s)}\u2588{RESET}")
    return f"  {label:<10s} {''.join(blocks)}"


def render(data: dict, zip_name: str) -> None:
    n_spheres = len(data["spheres"])

    for player, slot_info in data["slot_info"].items():
        l2s = loc_to_sphere_map(data, player)

        print(f"{BOLD}{zip_name}{RESET}  slot {player}: {slot_info.name!r}")
        print(f"  {n_spheres} sphere(s)   {len(l2s)} locations")
        print()

        legend_parts = [f"{fg(s)}\u2588 {s}{RESET}" for s in range(n_spheres)]
        print(f"  sphere:  {'  '.join(legend_parts)}")
        print()

        print(block_row(f"TECH({len(TECH_IDS)})",  TECH_IDS, l2s))
        print(block_row(f"KSC({len(KSC_IDS)})",    KSC_IDS,  l2s))
        print(block_row(f"FLAGS({len(FLAG_IDS)})",  FLAG_IDS, l2s))
        print()

        for s in range(n_spheres):
            in_sphere = {lid for lid, si in l2s.items() if si == s}
            tech_here = [n[6:]  for n, d in TECH_LOCATIONS.items()         if d.id in in_sphere]
            ksc_here  = [n[5:]  for n, d in KSC_UPGRADE_LOCATIONS.items()  if d.id in in_sphere]
            flag_here = [n[6:]  for n, d in FLAG_LOCATIONS.items()          if d.id in in_sphere]

            print(f"  {fg(s)}{BOLD}Sphere {s}{RESET}  ({len(in_sphere)} locations)")
            if tech_here:
                print(f"    Tech  ({len(tech_here)}): {', '.join(tech_here)}")
            if ksc_here:
                print(f"    KSC   ({len(ksc_here)}): {', '.join(ksc_here)}")
            if flag_here:
                print(f"    Flags ({len(flag_here)}): {', '.join(flag_here)}")
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

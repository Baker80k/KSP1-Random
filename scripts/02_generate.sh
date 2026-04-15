#!/usr/bin/env bash
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
"$ROOT/.venv/bin/python" "$ROOT/Archipelago-KSP/Generate.py" --player_files "$ROOT/player_files"
echo "Output in Archipelago-KSP/output/"

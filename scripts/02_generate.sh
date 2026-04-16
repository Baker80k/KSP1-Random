#!/usr/bin/env bash
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

# Windows venvs (Cygwin/MSYS + native Python) use Scripts/, Unix use bin/
if [ -f "$ROOT/.venv/Scripts/python.exe" ]; then
    VENV_PYTHON="$ROOT/.venv/Scripts/python.exe"
else
    VENV_PYTHON="$ROOT/.venv/bin/python"
fi

"$VENV_PYTHON" "$ROOT/Archipelago-KSP/Generate.py" --player_files "$ROOT/player_files"
echo "Output in Archipelago-KSP/output/"

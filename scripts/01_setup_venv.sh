#!/usr/bin/env bash
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

echo "Setting up venv at $ROOT/.venv"
uv venv "$ROOT/.venv"

# Windows venvs (Cygwin/MSYS + native Python) use Scripts/, Unix use bin/
if [ -f "$ROOT/.venv/Scripts/python.exe" ]; then
    VENV_PYTHON="$ROOT/.venv/Scripts/python.exe"
else
    VENV_PYTHON="$ROOT/.venv/bin/python"
fi

uv pip install --python "$VENV_PYTHON" pip setuptools
uv pip install --python "$VENV_PYTHON" -r "$ROOT/Archipelago-KSP/requirements.txt"

ROOT_KSP1="$ROOT/ksp1"
if [ -L "$ROOT_KSP1" ]; then
    echo "Symlink $ROOT_KSP1 already exists, skipping"
elif [ -d "$ROOT_KSP1" ]; then
    echo "Real directory $ROOT_KSP1 exists - not touching it"
else
    echo "Creating symlink $ROOT_KSP1 -> Archipelago-KSP/worlds/ksp1"
    ln -s "Archipelago-KSP/worlds/ksp1" "$ROOT_KSP1"
fi

if [ -f "$ROOT/.venv/Scripts/activate" ]; then
    echo "Done. Activate with: source $ROOT/.venv/Scripts/activate"
else
    echo "Done. Activate with: source $ROOT/.venv/bin/activate"
fi

#!/usr/bin/env bash
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

echo "Setting up venv at $ROOT/.venv"
uv venv "$ROOT/.venv"
uv pip install --python "$ROOT/.venv/bin/python" pip setuptools
uv pip install --python "$ROOT/.venv/bin/python" -r "$ROOT/Archipelago-KSP/requirements.txt"

ROOT_KSP1="$ROOT/ksp1"
if [ -L "$ROOT_KSP1" ]; then
    echo "Symlink $ROOT_KSP1 already exists, skipping"
elif [ -d "$ROOT_KSP1" ]; then
    echo "Real directory $ROOT_KSP1 exists - not touching it"
else
    echo "Creating symlink $ROOT_KSP1 -> Archipelago-KSP/worlds/ksp1"
    ln -s "Archipelago-KSP/worlds/ksp1" "$ROOT_KSP1"
fi

echo "Done. Activate with: source $ROOT/.venv/bin/activate"

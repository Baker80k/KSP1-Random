#!/usr/bin/env bash
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

echo "Setting up venv at $ROOT/.venv"
uv venv "$ROOT/.venv"
uv pip install --python "$ROOT/.venv/bin/python" -r "$ROOT/Archipelago-KSP/requirements.txt"

WORLDS_KSP1="$ROOT/Archipelago-KSP/worlds/ksp1"
if [ -L "$WORLDS_KSP1" ]; then
    echo "Symlink $WORLDS_KSP1 already exists, skipping"
elif [ -d "$WORLDS_KSP1" ]; then
    echo "Replacing real dir $WORLDS_KSP1 with symlink"
    rm -rf "$WORLDS_KSP1"
    ln -s "../../ksp1" "$WORLDS_KSP1"
else
    echo "Creating symlink $WORLDS_KSP1 -> ../../ksp1"
    ln -s "../../ksp1" "$WORLDS_KSP1"
fi

echo "Done. Activate with: source $ROOT/.venv/bin/activate"

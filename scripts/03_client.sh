#!/usr/bin/env bash
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
"$ROOT/.venv/bin/python" "$ROOT/Archipelago-KSP/worlds/ksp1/KSPClient.py"

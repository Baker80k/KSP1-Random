@echo off
setlocal

set "ROOT=%~dp0..\.."
pushd "%ROOT%"

.venv\Scripts\python.exe Archipelago-KSP\Generate.py --player_files player_files
echo Output in Archipelago-KSP\output\

popd
endlocal

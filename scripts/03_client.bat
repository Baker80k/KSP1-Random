@echo off
setlocal

set "ROOT=%~dp0..\.."
pushd "%ROOT%"

.venv\Scripts\python.exe Archipelago-KSP\worlds\ksp1\KSPClient.py

popd
endlocal

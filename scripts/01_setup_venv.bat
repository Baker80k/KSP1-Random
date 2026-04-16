@echo off
setlocal

:: ROOT is two levels up from the scripts\ folder
set "ROOT=%~dp0..\.."
pushd "%ROOT%"

echo Setting up venv at %ROOT%\.venv
uv venv .venv
uv pip install --python .venv\Scripts\python.exe pip setuptools
uv pip install --python .venv\Scripts\python.exe -r Archipelago-KSP\requirements.txt

:: Create junction (no admin required) if ksp1 does not exist
if exist ksp1\ (
    echo Directory ksp1 already exists, skipping
) else (
    echo Creating junction ksp1 -^> Archipelago-KSP\worlds\ksp1
    mklink /J ksp1 Archipelago-KSP\worlds\ksp1
)

popd
echo Done. Activate with: .venv\Scripts\activate.bat
endlocal

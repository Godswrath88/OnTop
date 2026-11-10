@echo off
rem ============================================================================
rem  OnTop — portable single-exe build (no installer, no install step)
rem  Produces portable\out\OnTop.exe: runs from ANY folder, writes config to
rem  %%APPDATA%%\OnTop exactly like the installed version. Self-contained =
rem  .NET 8 is baked in (~150-180 MB; nothing to install, works offline).
rem  See PORTABLE.md for the trade-offs vs the installer.
rem ============================================================================
pushd "%~dp0"
dotnet publish ..\OnTop\OnTop.csproj -c Release -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o out
if errorlevel 1 (echo. & echo BUILD FAILED & popd & exit /b 1)
echo.
echo Portable build ready: %~dp0out\OnTop.exe
popd

#!/usr/bin/env bash
# Builds the release zip dist/CSGTA-<version>.zip:
#   gta/CSGTA.asi, gta/CSGTA/...      -> GTA V Enhanced folder (loaded by Ultimate ASI Loader)
#   tools/CsSoundImporter.exe         -> %LOCALAPPDATA%\CSGTA\bin (Melty runs it before the first start)
set -euo pipefail
cd "$(dirname "$0")"
VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props)
rm -rf dist && mkdir -p dist

./asi/build.sh test
dotnet test tests/CSGTA.Tests -c Release --nologo
./asi/build.sh
dotnet publish src/CsSoundImporter -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist/importer --nologo

OUT=dist/stage
mkdir -p "$OUT/gta/CSGTA" "$OUT/tools"
cp dist/asi/CSGTA.asi "$OUT/gta/"
cp packaging/CSGTA.ini packaging/THIRD-PARTY-NOTICES.txt "$OUT/gta/CSGTA/"
cp README.md "$OUT/gta/CSGTA/README.md"
cp dist/importer/CsSoundImporter.exe "$OUT/tools/"

(cd "$OUT" && find . -exec touch -d '2026-01-01 00:00:00' {} + && zip -X -r -9 "../CSGTA-$VERSION.zip" gta tools >/dev/null)
echo "dist/CSGTA-$VERSION.zip"

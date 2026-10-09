#!/usr/bin/env bash
# Builds the release zip: dist/CSGTA-<version>.zip, laid out relative to the GTA V folder.
set -euo pipefail
cd "$(dirname "$0")"
VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props)
OUT=dist/stage
rm -rf dist && mkdir -p "$OUT/scripts/CSGTA"

dotnet test tests/CSGTA.Tests -c Release --nologo
dotnet build src/CSGTA -c Release --nologo
dotnet publish src/CsSoundImporter -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist/importer --nologo

cp src/CSGTA/bin/Release/net48/CSGTA.dll "$OUT/scripts/"
cp src/CSGTA/bin/Release/net48/NAudio.dll "$OUT/scripts/"
cp packaging/CSGTA.ini "$OUT/scripts/"
cp dist/importer/CsSoundImporter.exe "$OUT/scripts/CSGTA/"
cp packaging/THIRD-PARTY-NOTICES.txt "$OUT/scripts/CSGTA/"
cp README.md "$OUT/scripts/CSGTA/README.md"

(cd "$OUT" && find . -exec touch -d '2026-01-01 00:00:00' {} + && zip -X -r -9 "../CSGTA-$VERSION.zip" scripts >/dev/null)
echo "dist/CSGTA-$VERSION.zip"

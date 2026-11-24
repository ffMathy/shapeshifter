#!/usr/bin/env bash
# Regenerates the Microsoft Store listing import folders and ZIPs: renders the screenshots and logos from the real UI
# (the screenshot generator's --store mode), then fills Partner Center's listing CSV from text/<language>.json.
#
#   packaging/store/listing/make-listing.sh [out-dir] [-- build_listing.py options, e.g. --columns-from export.csv]
#
# Needs the .NET SDK and Python 3. Render on Windows, where Segoe UI and fonts for every script are installed (the
# Store listing workflow does); elsewhere the Windows look falls back to Inter and other scripts to the Noto fonts
# (Debian/Ubuntu: fonts-noto-core fonts-noto-cjk fonts-noto-ui-core fonts-dejavu-core). Set
# SHAPESHIFTER_STORE_LANGUAGES=en,de to render only some languages.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../../.." && pwd)"
out="$(realpath -m "${1:-$repo/artifacts/store-listing}")"
shift || true
[[ "${1:-}" == "--" ]] && shift

images="$out/rendered"
rm -rf "${images:?}"
mkdir -p "$images"

dotnet run --project "$repo/tools/Shapeshifter.ScreenshotGenerator" -c Release -- \
  --store "$images" ${SHAPESHIFTER_STORE_LANGUAGES:+--languages "$SHAPESHIFTER_STORE_LANGUAGES"}

"$(command -v python3 || command -v python)" "$here/build_listing.py" --images "$images" --out "$out" ${SHAPESHIFTER_STORE_LANGUAGES:+--languages "$SHAPESHIFTER_STORE_LANGUAGES"} "$@"

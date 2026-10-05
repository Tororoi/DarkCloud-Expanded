#!/bin/bash
# Instruction listing (addr, bytes, mnemonic) of ONE function from the main ELF or dun.bin, via the Ghidra project
# decompile.sh builds. Output: /tmp/listing_<img>_<name>.txt
#
#   ./listing.sh main "CharaChangeKey__Fv"
#   ./listing.sh dun  "SwordDmgCheck1__Ffi"
set -e
BREW="$(brew --prefix 2>/dev/null || echo /usr/local)"
export JAVA_HOME="$BREW/opt/openjdk@21"
HEADLESS="$(echo "$BREW"/Cellar/ghidra/*/libexec/support/analyzeHeadless | cut -d' ' -f1)"
PROJ=/tmp/ghidraproj
HERE="$(cd "$(dirname "$0")" && pwd)"
IMG="${1:-main}"; NAME="${2:?function name}"
if [ "$IMG" = "main" ]; then PNAME=SCUS_971.11; else PNAME=dun.bin; fi
[ -e "$PROJ/dc_${IMG}.gpr" ] || { echo "run decompile.sh $IMG <fn> once first (imports the image)"; exit 1; }
OUT="/tmp/listing_${IMG}_${NAME}.txt"
"$HEADLESS" "$PROJ" "dc_${IMG}" -process "$PNAME" -noanalysis -scriptPath "$HERE" \
  -postScript DumpListing.java "$NAME" "$OUT" 2>&1 | grep -E "DumpListing|ERROR" || true
echo "-> $OUT"

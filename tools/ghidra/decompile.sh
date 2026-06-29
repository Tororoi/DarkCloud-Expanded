#!/bin/bash
# Decompile PS2 EE functions from Dark Cloud's SCUS_971.11 (main) or dun.bin (overlay)
# using Ghidra headless + the EmotionEngine (r5900) extension. Output: /tmp/decomp_<img>.txt
#
#   ./decompile.sh main "ToanKey_Play__Fv,Set__14CCollisionDataFPfiiffiiii"
#   ./decompile.sh dun  "SwordDmgCheck1__Ffi"
#
# First run does a full import+analysis (~20s); set REIMPORT=1 to force re-import.
set -e
export JAVA_HOME=/usr/local/opt/openjdk@21
HEADLESS=/usr/local/Cellar/ghidra/12.1.2/libexec/support/analyzeHeadless
PROJ=/tmp/ghidraproj
HERE="$(cd "$(dirname "$0")" && pwd)"
DC=/Users/thomascantwell/ROMs/dc_extracted
IMG="${1:-main}"; NAMES="${2:?function name list (comma-separated)}"
mkdir -p "$PROJ"
if [ "$IMG" = "main" ]; then
  PNAME=SCUS_971.11; FILE="$DC/SCUS_971.11"; IMPORT=(-import "$FILE" -processor "r5900:LE:32:default")
  PRE=()
else
  PNAME=dun.bin; FILE="$DC/dun.bin"
  IMPORT=(-import "$FILE" -processor "r5900:LE:32:default" -loader BinaryLoader -loader-baseAddr 0x1DABC80)
  PRE=(-preScript ApplySymbols.java)   # name the overlay funcs from symbols.txt
fi
OUT="/tmp/decomp_${IMG}.txt"
if [ -d "$PROJ/${PNAME%.*}*"  ] || [ -n "$REIMPORT" ] || [ ! -e "$PROJ/dc_${IMG}.gpr" ]; then
  "$HEADLESS" "$PROJ" "dc_${IMG}" "${IMPORT[@]}" -scriptPath "$HERE" "${PRE[@]}" \
    -postScript DumpDecomp.java "$NAMES" "$OUT" 2>&1 | grep -E "DumpDecomp|ERROR|Import succeeded" || true
else
  "$HEADLESS" "$PROJ" "dc_${IMG}" -process "$PNAME" -noanalysis -scriptPath "$HERE" \
    -postScript DumpDecomp.java "$NAMES" "$OUT" 2>&1 | grep -E "DumpDecomp|ERROR" || true
fi
echo "-> $OUT"

#!/usr/bin/env bash
# Reproduce the full final-report benchmark suite on this machine.
# Usage: tools/run-final-suite.sh   (from the project root; needs JDK 11+ on PATH — gradle fetches its own JDK 21)
# Produces results/final-hit-lf{0.5,0.75,0.9,0.95}.json and results/final-get.json,
# then: python3 tools/final_report.py
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p results

echo "== correctness gate =="
./gradlew -q smokeTest

echo "== hit-only load-factor sweep (table geometry fixed at ~2^27 entries; size = lf * 2^27 minus margin) =="
for CFG in "0.5:67000000" "0.75:100600000" "0.9:120700000" "0.95:127400000"; do
  LF="${CFG%%:*}"; SZ="${CFG##*:}"
  echo "-- loadFactor=$LF (size=$SZ) --"
  ./gradlew -q run --args="getHit -p size=$SZ -p lookups=1000000 -p presize=true -p loadFactor=$LF \
    -p keyDist=random,pulsed -f 1 -wi 3 -i 5 -r 500ms -w 500ms \
    -rf json -rff results/final-hit-lf$LF.json" | grep -E "^NullableLong" || true
done

echo "== miss-path appendix run (lf 0.5) =="
./gradlew -q run --args="getMiss -p size=67000000 -p lookups=1000000 -p presize=true -p loadFactor=0.5 \
  -p keyDist=random,pulsed -f 1 -wi 3 -i 5 -r 500ms -w 500ms \
  -rf json -rff results/final-get.json" | grep -E "^NullableLong" || true

echo "== assembling report =="
python3 tools/final_report.py
echo "done: results/final-report.html"

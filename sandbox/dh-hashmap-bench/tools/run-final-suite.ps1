# Reproduce the full final-report benchmark suite on native Windows.
# Usage (from the project root, ideally on an otherwise-idle box):
#   powershell -ExecutionPolicy Bypass -File tools\run-final-suite.ps1
# Produces results\final-hit-lf*.json and results\final-get.json.
# Copy results\*.json back and regenerate the report with: python3 tools/final_report.py
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
New-Item -ItemType Directory -Force -Path results | Out-Null

Write-Host "== correctness gate =="
.\gradlew.bat -q smokeTest
if ($LASTEXITCODE -ne 0) { throw "smokeTest failed" }

$configs = @(
    @{ lf = "0.5";  size = "67000000"  },
    @{ lf = "0.75"; size = "100600000" },
    @{ lf = "0.9";  size = "120700000" },
    @{ lf = "0.95"; size = "127400000" }
)
foreach ($c in $configs) {
    Write-Host "-- loadFactor=$($c.lf) (size=$($c.size)) --"
    .\gradlew.bat -q run --args="getHit -p size=$($c.size) -p lookups=1000000 -p presize=true -p loadFactor=$($c.lf) -p keyDist=random,pulsed -f 1 -wi 3 -i 5 -r 500ms -w 500ms -rf json -rff results/final-hit-lf$($c.lf).json"
    if ($LASTEXITCODE -ne 0) { throw "benchmark failed at loadFactor=$($c.lf)" }
}

Write-Host "== miss-path appendix run (lf 0.5) =="
.\gradlew.bat -q run --args="getMiss -p size=67000000 -p lookups=1000000 -p presize=true -p loadFactor=0.5 -p keyDist=random,pulsed -f 1 -wi 3 -i 5 -r 500ms -w 500ms -rf json -rff results/final-get.json"
if ($LASTEXITCODE -ne 0) { throw "miss run failed" }

Write-Host "done: copy results\*.json back and run tools/final_report.py"

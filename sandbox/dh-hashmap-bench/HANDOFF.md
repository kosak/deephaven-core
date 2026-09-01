# HANDOFF — read this before touching anything

Context for anyone (human or Claude) opening this project fresh. This is a benchmarking sandbox for
deephaven-core's `HashMapLockFreeK1V1/K2V2/K4V4` (the redirection-index hash maps). The campaign's full
story and results are in `results/final-report.html` (regenerate: `python3 tools/final_report.py`).

## What is established fact (don't re-litigate without new data)
- The `NullableLongLongMap` interface here is BATCH-ONLY (arrays + oldValues out-params, ~4K chunks).
  Batching killed megamorphic dispatch (measured: 33% penalty per-element, ~0% batched).
- Three experimental families, all differentially verified against java.util.HashMap:
  - `HMLFnomod*`: division-free probing (weak fold + Lemire fastmod via per-batch `numBucketsReciprocal`).
    ~20-30% faster lookups, keeps the sequential/pulsed-key locality superpower.
  - `HMLFamac*`: + AMAC windowed batch get. Null result at load factor 0.5; wins ~10-15% at 0.9+.
  - `HMLFamacK4V4MS`: 64-byte-aligned native MemorySegment (JDK 22+; Arena.ofAuto for GC-managed,
    reader-safe segments; long-indexed so no size cap). Supersedes a deleted ByteBuffer variant
    (HMLFamacK4V4BB, ~15% lookup win at small scale but int-indexed = 2GB/table cap = 1/8 the reach of a
    long[]; archived numbers in results/bb-*.json, code in git history). Do not reintroduce ByteBuffer.
- The deliberately weak probe1 hash is LOAD-BEARING: sequential/pulsed keys -> adjacent buckets ->
  3-13x faster than fastutil on the redirection-index workload. Do not "fix" it with a strong mixer.
- fastutil comparison: wins uniform-random lookups at occupancy <= 0.75; structurally unusable for the
  lock-free contract (backward-shift deletion moves entries under concurrent readers).

## Invariants
- Toolchain is JDK 25 (auto-provisioned via foojay resolver) — required by `HMLFamacK4V4MS`
  (java.lang.foreign; Arena.ofAuto() = GC-managed segments, which is what makes lock-free rehash safe;
  64-byte aligned AND long-indexed, so no 2GB cap like the ByteBuffer variant).
- Lookup patterns: `lookupPattern=sorted|shuffled|window`. `window` (a dense contiguous run) flatters pulsed
  tables enormously and is NOT the realistic redirection-index read; suite v2 measures sorted+shuffled samples.
- `./gradlew smokeTest` (or `gradlew.bat` on Windows) is the correctness gate. Run it after ANY change
  to hash code. It has caught two real bugs already.
- The originals (`HashMapLockFree*`) stay byte-faithful to deephaven-core except the batch wrappers.
  New ideas get NEW files/families.
- fastutil sizes tables to powers of two: benchmark sizes must be `loadFactor * 2^k` or its true
  occupancy silently diverges (see the `loadFactor` javadoc in the benchmark).
- Benchmarks live in `src/main/java/bench/NullableLongLongMapBench.java`; results JSONs in `results/`.

## If you are here to reproduce the final suite on this machine
Run `tools/run-final-suite.sh` (Linux/WSL) or `tools\run-final-suite.ps1` (native Windows), ~60-75 min,
needs ~8GB free RAM and an otherwise idle box. Then send `results/*.json` back to the originating
session/machine, which merges them into an environment-comparison section of the report.
Nothing else needs to be modified for that task. Resist refactoring; that is not why you are here.

## History
Built 2026-08-31 in a session with Corey Kosak, starting from "benchmark HashMapLockFreeKnVn" and ending
at the load-factor/pulsed-key study. Prior context that doesn't fit here: the session transcript on the
WSL2 machine, and the memory file `dh-hashmap-bench-sandbox.md` in that machine's Claude project memory.

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

## Design backlog (discussed and endorsed, deliberately NOT yet implemented)
- **In-array header** (colleague's idea, refined): steal a header block at the front of kvs/segment:
  immutable-after-publish words (fastmod magic, numBuckets, noEntryValue, format/impl discriminator),
  then padding, then writer-mutable counters (size, nonEmptySlots), then padding, then buckets. Header
  travels atomically with the array snapshot -> per-ELEMENT fastmod becomes race-free and division-free
  (one L1 load). PADDING MATH (heap arrays have arbitrary 8B-granular alignment — do NOT assume slot 0
  starts a cache line): two words are guaranteed on different lines only if >= 64 bytes (8 slots) apart;
  7 spacer slots is the proven minimum. Intel's adjacent-line prefetcher pulls line PAIRS, so use 16
  spacer slots (128B), same as the JDK's @Contended default. Data start at a multiple of 8 slots keeps
  MS-variant buckets 64-aligned. Total theft ~48-64 slots: negligible.
- **Related pre-existing bug to fix during engine migration**: HashMapBase's size/nonEmptySlots fields
  share a cache line with the keysAndValues reference -> every put invalidates the line concurrent
  readers use to fetch the array. Separate them (padding or field reordering).
- **Tiered redirection index**: run heap NOMOD_K2V2-style at comfortable occupancy; one-way promotion
  (a rehash whose destination is a different class) to AMAC_K4V4 / AMAC_K4V4_MS at ~0.75 occupancy or
  the array-length ceiling. Swap happens in the wrapper (volatile baseline field) during commitUpdates;
  readers keep old snapshot; precedent = HashMap treeification. MS also relieves heap pressure and the
  ~912M-entry heap cap. Never migrate back.
- **Open bet (Cinnabon-denominated)**: for JDK 22+, is MS-backed better than heap for ALL shapes/load
  factors? Settle with a substrate-only A/B: MS-backed nomod-K2V2 (no AMAC) vs heap twin. Expected:
  MS wins big tables, heap wins cache-hot small tables (per-access segment overhead) and write paths.
- **Engine migration survey (done, no code)**: only 5 files touch the map. RowRedirection's fillChunk
  defaults already loop per-element get() -> override with batch + gather/scatter for the updates->
  baseline overlay. SortOperation hands out reverseLookup::get as LongUnaryOperator (per-element leak;
  serve via 1-element batches). Production defaults contradict findings: hashBucketWidth=1, lf 0.5.
- **Static kernels over self-describing arrays** (Corey, endorsed): with the full header (type word,
  capacity, magic, counters), the KnVn class hierarchy is redundant at the storage layer — replace
  instance methods with static kernels that read the header type word once per batch and jump to a
  width-specialized loop (same cost model that killed the megamorphism worry). Benefits: the tiered
  swap becomes "publish an array with a different type word" (readers never know); the array/segment
  becomes a serialization format (disk/mmap/shared memory) for free. NON-NEGOTIABLE residue: one thin
  facade must still own the concurrency contract (the volatile publication field, single-writer
  discipline, old-array liveness) — cf. ConcurrentHashMap's static tabAt kernels + one owning class.
  In production the facade can simply be WritableRowRedirectionLockFree holding volatile long[]
  directly. Kernels should validate a header magic number on entry.
- **Kernel-singleton refinement** (Corey): instead of raw static kernels, stateless singleton objects
  (flat two-level hierarchy: one abstract Kernel + one final per width; all data passed as long[] self)
  -> recovers full JIT devirtualization/inlining (exact-typed receiver) while keeping self-describing
  arrays. TRAP: the facade's (kernel, table) pair must change atomically under tiered migration — the
  magic-constant race reincarnated at dispatch level. Fix: publish an immutable pair via one volatile
  ref (preferred) or re-derive kernel from header type word per batch; either way the kernel should
  assert the header type word once per batch as a tripwire.

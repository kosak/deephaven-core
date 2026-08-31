package bench;

import io.deephaven.util.datastructures.hash.HMLFamacK1V1;
import io.deephaven.util.datastructures.hash.HMLFamacK2V2;
import io.deephaven.util.datastructures.hash.HMLFamacK4V4;
import io.deephaven.util.datastructures.hash.HMLFamacK4V4BB;
import io.deephaven.util.datastructures.hash.HMLFnomodK1V1;
import io.deephaven.util.datastructures.hash.HMLFnomodK2V2;
import io.deephaven.util.datastructures.hash.HMLFnomodK4V4;
import io.deephaven.util.datastructures.hash.HashMapLockFreeK1V1;
import io.deephaven.util.datastructures.hash.HashMapLockFreeK2V2;
import io.deephaven.util.datastructures.hash.HashMapLockFreeK4V4;
import io.deephaven.util.datastructures.hash.NullableLongLongMap;
import it.unimi.dsi.fastutil.longs.Long2LongOpenHashMap;
import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Fork;
import org.openjdk.jmh.annotations.Level;
import org.openjdk.jmh.annotations.Measurement;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Param;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.Setup;
import org.openjdk.jmh.annotations.State;
import org.openjdk.jmh.annotations.Warmup;
import org.openjdk.jmh.infra.Blackhole;

import java.util.Arrays;
import java.util.concurrent.TimeUnit;
import java.util.random.RandomGenerator;
import java.util.random.RandomGeneratorFactory;

import static io.deephaven.util.QueryConstants.NULL_LONG;

/**
 * Starter benchmarks for the HashMapLockFreeKnVn family, with fastutil's Long2LongOpenHashMap as a baseline.
 *
 * Each benchmark invocation sweeps the whole key set, so scores are "time to do {size} operations". Divide by
 * {size} for per-op cost, or just compare impls at the same size.
 *
 * Examples:
 *   ./gradlew run --args="get"
 *   ./gradlew run --args="fill -p impl=K1V1,K4V4 -p size=4000000 -prof gc"
 */
@State(Scope.Thread)
@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.MILLISECONDS)
@Warmup(iterations = 3, time = 1)
@Measurement(iterations = 5, time = 1)
@Fork(1)
public class NullableLongLongMapBench {
    @FunctionalInterface
    public interface MapFactory {
        /** desiredEntries is entry-slot capacity (the DH maps' desiredInitialCapacity convention). */
        NullableLongLongMap create(int desiredEntries, float loadFactor);
    }

    public enum Impl {
        // The DH constructors' third argument is the noEntryValue; -1 is their default.
        K1V1((cap, lf) -> new HashMapLockFreeK1V1(cap, lf, -1)),
        K2V2((cap, lf) -> new HashMapLockFreeK2V2(cap, lf, -1)),
        K4V4((cap, lf) -> new HashMapLockFreeK4V4(cap, lf, -1)),
        NOMOD_K1V1((cap, lf) -> new HMLFnomodK1V1(cap, lf, -1)),
        NOMOD_K2V2((cap, lf) -> new HMLFnomodK2V2(cap, lf, -1)),
        NOMOD_K4V4((cap, lf) -> new HMLFnomodK4V4(cap, lf, -1)),
        AMAC_K1V1((cap, lf) -> new HMLFamacK1V1(cap, lf, -1)),
        AMAC_K2V2((cap, lf) -> new HMLFamacK2V2(cap, lf, -1)),
        AMAC_K4V4((cap, lf) -> new HMLFamacK4V4(cap, lf, -1)),
        AMAC_K4V4_BB((cap, lf) -> new HMLFamacK4V4BB(cap, lf, -1)),
        FASTUTIL(FastutilAdapter::new);

        final MapFactory factory;

        Impl(MapFactory factory) {
            this.factory = factory;
        }
    }

    @Param({"K1V1", "K2V2", "K4V4", "NOMOD_K1V1", "NOMOD_K2V2", "NOMOD_K4V4", "AMAC_K1V1", "AMAC_K2V2", "AMAC_K4V4", "AMAC_K4V4_BB", "FASTUTIL"})
    public Impl impl;

    @Param({"1000000"})
    public int size;

    /** Batch size: keys are fed to the map interface in chunks of this many elements. */
    @Param({"4096"})
    public int chunkSize;

    /**
     * "random": uniform random longs. "sequential": a contiguous block of small keys — the case the original weak
     * probe1 hash is deliberately cache-friendly for (adjacent keys land in adjacent buckets); the nomod variants
     * scatter these, so always compare both distributions before believing an improvement.
     */
    @Param({"random"})
    public String keyDist;

    /** When true, maps are created at full capacity so fill measures pure insertion, not rehashing. */
    @Param({"false"})
    public boolean presize;

    /**
     * Table load factor. With presize=true the filled map sits at ~this occupancy, so lookup benchmarks measure a
     * table that is genuinely this full. Note fastutil rounds its table to a power of two: pick size = loadFactor *
     * 2^k (e.g. 1887436 = 0.9 * 2^21) or its true occupancy will silently differ from the requested load factor.
     */
    @Param({"0.5"})
    public float loadFactor;

    /**
     * When true, setup runs the measured sweep loop with ALL impls first, so the map.get() call site inside
     * sweep() has a megamorphic type profile before C2 compiles it. When false (JMH's default forking already
     * isolates each impl in its own JVM), the call site is monomorphic. A/B this to price megamorphic dispatch.
     */
    @Param({"false"})
    public boolean pollute;

    private long[] keys;
    private long[] missingKeys;
    private long[][] keyChunks;
    private long[][] valueChunks;
    private long[][] missChunks;
    private long[] scratch;
    private NullableLongLongMap filledMap;

    @Setup(Level.Trial)
    public void setupTrial() {
        // L64X128MixRandom: fixed seed, statistically solid, and (unlike Random) cheap enough to not matter
        final RandomGenerator rng = RandomGeneratorFactory.of("L64X128MixRandom").create(20260831);
        if ("sequential".equals(keyDist)) {
            keys = new long[size];
            missingKeys = new long[size];
            final long start = 1_000_000;
            for (int i = 0; i < size; ++i) {
                keys[i] = start + i;
                missingKeys[i] = start + size + i;
            }
        } else {
            keys = distinctKeys(rng, size);
            missingKeys = distinctKeys(rng, size); // 128-bit-ish state space: overlap with 'keys' is negligible
        }
        keyChunks = chunk(keys, chunkSize);
        missChunks = chunk(missingKeys, chunkSize);
        valueChunks = new long[keyChunks.length][];
        for (int c = 0, base = 0; c < keyChunks.length; base += keyChunks[c].length, ++c) {
            valueChunks[c] = new long[keyChunks[c].length];
            for (int j = 0; j < valueChunks[c].length; ++j) {
                valueChunks[c][j] = base + j;
            }
        }
        scratch = new long[chunkSize];
        filledMap = createMap();
        fill(filledMap);
        if (pollute) {
            final long[][] pollutionChunks = chunk(distinctKeys(rng, 65536), chunkSize);
            final long[] pollutionScratch = new long[chunkSize];
            for (final Impl other : Impl.values()) {
                final NullableLongLongMap m = other.factory.create(65536 * 2, 0.5f);
                for (final long[] c : pollutionChunks) {
                    m.put(c, c, pollutionScratch);
                }
                long sum = 0;
                for (int rep = 0; rep < 50; ++rep) {
                    sweep(m, pollutionChunks, pollutionScratch, null);
                    sum += pollutionScratch[0];
                }
                if (sum == 12345678901L) {
                    throw new IllegalStateException("unreachable; defeats dead-code elimination");
                }
            }
        }
    }

    private static long[][] chunk(long[] src, int chunkSize) {
        final int n = (src.length + chunkSize - 1) / chunkSize;
        final long[][] result = new long[n][];
        for (int c = 0; c < n; ++c) {
            final int from = c * chunkSize;
            result[c] = Arrays.copyOfRange(src, from, Math.min(from + chunkSize, src.length));
        }
        return result;
    }

    /** The one shared call site for map.get(): 'pollute' poisons this site's type profile through all impls. */
    private static void sweep(NullableLongLongMap map, long[][] chunks, long[] scratch, Blackhole bh) {
        for (final long[] c : chunks) {
            map.get(c, scratch);
            if (bh != null) {
                bh.consume(scratch);
            }
        }
    }

    private static long[] distinctKeys(RandomGenerator rng, int n) {
        final long[] result = new long[n];
        for (int i = 0; i < n; ++i) {
            long k;
            do {
                k = rng.nextLong();
                // NULL_LONG is the null key; NULL_LONG+1 and +2 are reserved sentinels (see HashMapBase)
            } while (k >= NULL_LONG && k <= NULL_LONG + 2);
            result[i] = k;
        }
        return result;
    }

    private NullableLongLongMap createMap() {
        return impl.factory.create(presize ? (int) (size / loadFactor) + 1 : 16, loadFactor);
    }

    private void fill(NullableLongLongMap map) {
        for (int c = 0; c < keyChunks.length; ++c) {
            map.put(keyChunks[c], valueChunks[c], scratch);
        }
    }

    /** Insert {size} distinct keys into a fresh map (includes rehash cost unless presize=true). */
    @Benchmark
    public NullableLongLongMap fill() {
        final NullableLongLongMap map = createMap();
        fill(map);
        return map;
    }

    /** Look up every present key once, in insertion order. */
    @Benchmark
    public void getHit(Blackhole bh) {
        sweep(filledMap, keyChunks, scratch, bh);
    }

    /** Look up {size} absent keys. */
    @Benchmark
    public void getMiss(Blackhole bh) {
        sweep(filledMap, missChunks, scratch, bh);
    }

    /** Remove every key, then re-insert all of them (exercises deleted-slot handling). */
    @Benchmark
    public void removeThenReinsert() {
        final NullableLongLongMap map = filledMap;
        for (final long[] c : keyChunks) {
            map.remove(c, scratch);
        }
        fill(map);
    }

    /** fastutil baseline behind the same interface (only what the benchmarks call is implemented). */
    private static final class FastutilAdapter implements NullableLongLongMap {
        private final Long2LongOpenHashMap map;

        FastutilAdapter(int desiredEntries, float loadFactor) {
            // fastutil's first argument is expected element count, not slot capacity; convert from ours.
            map = new Long2LongOpenHashMap(Math.max(16, (int) (desiredEntries * loadFactor)), loadFactor);
            map.defaultReturnValue(NULL_LONG);
        }

        @Override
        public void put(long[] keys, long[] values, long[] oldValues) {
            for (int ii = 0; ii < keys.length; ++ii) {
                oldValues[ii] = map.put(keys[ii], values[ii]);
            }
        }

        @Override
        public void putIfAbsent(long[] keys, long[] values, long[] oldValues) {
            for (int ii = 0; ii < keys.length; ++ii) {
                oldValues[ii] = map.putIfAbsent(keys[ii], values[ii]);
            }
        }

        @Override
        public void get(long[] keys, long[] result) {
            for (int ii = 0; ii < keys.length; ++ii) {
                result[ii] = map.get(keys[ii]);
            }
        }

        @Override
        public void remove(long[] keys, long[] oldValues) {
            for (int ii = 0; ii < keys.length; ++ii) {
                oldValues[ii] = map.remove(keys[ii]);
            }
        }

        @Override
        public int size() {
            return map.size();
        }

        @Override
        public boolean isEmpty() {
            return map.isEmpty();
        }

        @Override
        public long defaultReturnValue() {
            return map.defaultReturnValue();
        }

        @Override
        public int capacity() {
            throw new UnsupportedOperationException();
        }

        @Override
        public void clear() {
            map.clear();
        }

        @Override
        public void resetToNull() {
            throw new UnsupportedOperationException();
        }

        @Override
        public void forEach(it.unimi.dsi.fastutil.longs.LongLongBiConsumer consumer) {
            throw new UnsupportedOperationException();
        }
    }
}

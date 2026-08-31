package bench;

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

import java.util.concurrent.TimeUnit;
import java.util.function.IntFunction;
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
    public enum Impl {
        K1V1(HashMapLockFreeK1V1::new),
        K2V2(HashMapLockFreeK2V2::new),
        K4V4(HashMapLockFreeK4V4::new),
        FASTUTIL(FastutilAdapter::new);

        final IntFunction<NullableLongLongMap> factory;

        Impl(IntFunction<NullableLongLongMap> factory) {
            this.factory = factory;
        }
    }

    @Param({"K1V1", "K2V2", "K4V4", "FASTUTIL"})
    public Impl impl;

    @Param({"1000000"})
    public int size;

    /** When true, maps are created at full capacity so fill measures pure insertion, not rehashing. */
    @Param({"false"})
    public boolean presize;

    /**
     * When true, setup runs the measured sweep loop with ALL impls first, so the map.get() call site inside
     * sweep() has a megamorphic type profile before C2 compiles it. When false (JMH's default forking already
     * isolates each impl in its own JVM), the call site is monomorphic. A/B this to price megamorphic dispatch.
     */
    @Param({"false"})
    public boolean pollute;

    private long[] keys;
    private long[] missingKeys;
    private NullableLongLongMap filledMap;

    @Setup(Level.Trial)
    public void setupTrial() {
        // L64X128MixRandom: fixed seed, statistically solid, and (unlike Random) cheap enough to not matter
        final RandomGenerator rng = RandomGeneratorFactory.of("L64X128MixRandom").create(20260831);
        keys = distinctKeys(rng, size);
        missingKeys = distinctKeys(rng, size); // 128-bit-ish state space: overlap with 'keys' is negligible
        filledMap = createMap();
        fill(filledMap);
        if (pollute) {
            final long[] pollutionKeys = distinctKeys(rng, 65536);
            for (final Impl other : Impl.values()) {
                final NullableLongLongMap m = other.factory.apply(65536 * 2);
                for (int i = 0; i < pollutionKeys.length; ++i) {
                    m.put(pollutionKeys[i], i);
                }
                long sum = 0;
                for (int rep = 0; rep < 50; ++rep) {
                    sum += sweep(m, pollutionKeys);
                }
                if (sum == 12345678901L) {
                    throw new IllegalStateException("unreachable; defeats dead-code elimination");
                }
            }
        }
    }

    private static long sweep(NullableLongLongMap map, long[] keys) {
        long sum = 0;
        for (final long k : keys) {
            sum += map.get(k);
        }
        return sum;
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
        return impl.factory.apply(presize ? size * 2 : 16);
    }

    private void fill(NullableLongLongMap map) {
        final long[] lk = keys;
        for (int i = 0; i < lk.length; ++i) {
            map.put(lk[i], i);
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
    public long getHit() {
        return sweep(filledMap, keys);
    }

    /** Look up {size} absent keys. */
    @Benchmark
    public long getMiss() {
        return sweep(filledMap, missingKeys);
    }

    /** Remove every key, then re-insert all of them (exercises deleted-slot handling). */
    @Benchmark
    public void removeThenReinsert(Blackhole bh) {
        final NullableLongLongMap map = filledMap;
        for (final long k : keys) {
            bh.consume(map.remove(k));
        }
        fill(map);
    }

    /** fastutil baseline behind the same interface (only what the benchmarks call is implemented). */
    private static final class FastutilAdapter implements NullableLongLongMap {
        private final Long2LongOpenHashMap map;

        FastutilAdapter(int initialCapacity) {
            map = new Long2LongOpenHashMap(initialCapacity);
            map.defaultReturnValue(NULL_LONG);
        }

        @Override
        public long put(long key, long value) {
            return map.put(key, value);
        }

        @Override
        public long putIfAbsent(long key, long value) {
            return map.putIfAbsent(key, value);
        }

        @Override
        public long get(long key) {
            return map.get(key);
        }

        @Override
        public long remove(long key) {
            return map.remove(key);
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

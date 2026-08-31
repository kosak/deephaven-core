package bench;

import io.deephaven.util.datastructures.hash.*;

import java.util.HashMap;
import java.util.Map;
import java.util.SplittableRandom;
import java.util.function.Supplier;

/**
 * Differential test: every impl must behave identically to java.util.HashMap under a randomized workload that forces
 * many rehashes (tiny initial capacity), heavy deletion (tombstones), reinsertion, and sequential as well as random
 * keys. Exits nonzero on the first mismatch.
 */
public final class SmokeTest {
    public static void main(String[] args) {
        final Map<String, Supplier<NullableLongLongMap>> impls = Map.of(
                "HashMapLockFreeK1V1", HashMapLockFreeK1V1::new,
                "HashMapLockFreeK2V2", HashMapLockFreeK2V2::new,
                "HashMapLockFreeK4V4", HashMapLockFreeK4V4::new,
                "HMLFnomodK1V1", HMLFnomodK1V1::new,
                "HMLFnomodK2V2", HMLFnomodK2V2::new,
                "HMLFnomodK4V4", HMLFnomodK4V4::new);
        for (final Map.Entry<String, Supplier<NullableLongLongMap>> e : impls.entrySet()) {
            for (final boolean sequential : new boolean[] {false, true}) {
                run(e.getKey(), e.getValue().get(), sequential);
            }
        }
        System.out.println("SmokeTest: all impls agree with java.util.HashMap");
    }

    private static void run(String name, NullableLongLongMap m, boolean sequential) {
        final String ctx = name + (sequential ? " (sequential keys)" : " (random keys)");
        final long noEntry = m.defaultReturnValue();
        final SplittableRandom rng = new SplittableRandom(987654321L);
        final HashMap<Long, Long> ref = new HashMap<>();
        final int keySpace = 200_000;
        for (int op = 0; op < 2_000_000; ++op) {
            final long key = sequential ? rng.nextInt(keySpace) : mixToSpace(rng.nextInt(keySpace));
            final int action = rng.nextInt(10);
            final long expected;
            final long actual;
            if (action < 4) {
                final long value = rng.nextLong(1, Long.MAX_VALUE);
                expected = unbox(ref.put(key, value), noEntry);
                actual = m.put(key, value);
            } else if (action < 5) {
                final long value = rng.nextLong(1, Long.MAX_VALUE);
                expected = unbox(ref.putIfAbsent(key, value), noEntry);
                actual = m.putIfAbsent(key, value);
            } else if (action < 8) {
                expected = unbox(ref.get(key), noEntry);
                actual = m.get(key);
            } else {
                expected = unbox(ref.remove(key), noEntry);
                actual = m.remove(key);
            }
            check(ctx, "op " + op, expected, actual);
            if ((op & 0xfffff) == 0xfffff) {
                check(ctx, "size", ref.size(), m.size());
            }
        }
        check(ctx, "final size", ref.size(), m.size());
        final long[] count = new long[1];
        m.forEach((k, v) -> {
            check(ctx, "forEach key " + k, unbox(ref.get(k), noEntry), v);
            ++count[0];
        });
        check(ctx, "forEach count", ref.size(), count[0]);
        System.out.println("  ok: " + ctx + " (final size " + ref.size() + ", capacity " + m.capacity() + ")");
    }

    /** Spread the small int across long-space so 'random' mode exercises full-width keys. */
    private static long mixToSpace(int x) {
        long k = x * 0x9e3779b97f4a7c15L;
        k ^= k >>> 31;
        return k;
    }

    private static long unbox(Long v, long noEntry) {
        return v == null ? noEntry : v;
    }

    private static void check(String ctx, String what, long expected, long actual) {
        if (expected != actual) {
            System.err.println("MISMATCH [" + ctx + "] " + what + ": expected " + expected + ", got " + actual);
            System.exit(1);
        }
    }
}

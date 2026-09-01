package bench;

import io.deephaven.util.datastructures.hash.*;

import java.util.HashMap;
import java.util.Map;
import java.util.SplittableRandom;
import java.util.function.Supplier;

/**
 * Differential test of the batch interface: every impl must behave identically to java.util.HashMap under randomized
 * batches (1..4096 elements, duplicates included) of put/putIfAbsent/get/remove, forcing many rehashes (tiny initial
 * capacity), heavy deletion (tombstones), reinsertion, and sequential as well as random keys. Since batch remove has
 * no output, each remove is followed by a get over the same keys. Exits nonzero on the first mismatch.
 */
public final class SmokeTest {
    private static final int MAX_BATCH = 4096;

    public static void main(String[] args) {
        final Map<String, Supplier<NullableLongLongMap>> impls = Map.ofEntries(
                Map.entry("HashMapLockFreeK1V1", (Supplier<NullableLongLongMap>) HashMapLockFreeK1V1::new),
                Map.entry("HashMapLockFreeK2V2", HashMapLockFreeK2V2::new),
                Map.entry("HashMapLockFreeK4V4", HashMapLockFreeK4V4::new),
                Map.entry("HMLFnomodK1V1", HMLFnomodK1V1::new),
                Map.entry("HMLFnomodK2V2", HMLFnomodK2V2::new),
                Map.entry("HMLFnomodK4V4", HMLFnomodK4V4::new),
                Map.entry("HMLFamacK1V1", HMLFamacK1V1::new),
                Map.entry("HMLFamacK2V2", HMLFamacK2V2::new),
                Map.entry("HMLFamacK4V4", HMLFamacK4V4::new),
                Map.entry("HMLFamacK4V4MS", HMLFamacK4V4MS::new));
        for (final Map.Entry<String, Supplier<NullableLongLongMap>> e : impls.entrySet()) {
            for (final boolean sequential : new boolean[] {false, true}) {
                run(e.getKey(), e.getValue().get(), sequential);
            }
        }
        System.out.println("SmokeTest: all impls agree with java.util.HashMap (batch interface)");
    }

    private static void run(String name, NullableLongLongMap m, boolean sequential) {
        final String ctx = name + (sequential ? " (sequential keys)" : " (random keys)");
        final long noEntry = m.defaultReturnValue();
        final SplittableRandom rng = new SplittableRandom(987654321L);
        final HashMap<Long, Long> ref = new HashMap<>();
        final int keySpace = 200_000;
        final long[] keys = new long[MAX_BATCH];
        final long[] values = new long[MAX_BATCH];
        final long[] actual = new long[MAX_BATCH];
        final long[] expected = new long[MAX_BATCH];
        long totalOps = 0;
        for (int round = 0; round < 1000; ++round) {
            final int batch = rng.nextInt(1, MAX_BATCH + 1);
            totalOps += batch;
            for (int i = 0; i < batch; ++i) {
                final int raw = rng.nextInt(keySpace);
                keys[i] = sequential ? raw : mixToSpace(raw);
            }
            final int action = rng.nextInt(10);
            if (action < 4) {
                fillRandomValues(rng, values, batch);
                for (int i = 0; i < batch; ++i) {
                    expected[i] = unbox(ref.put(keys[i], values[i]), noEntry);
                }
                m.put(subarray(keys, batch), subarray(values, batch), actual);
                checkArrays(ctx, "put round " + round, expected, actual, batch);
            } else if (action < 5) {
                fillRandomValues(rng, values, batch);
                for (int i = 0; i < batch; ++i) {
                    expected[i] = unbox(ref.putIfAbsent(keys[i], values[i]), noEntry);
                }
                m.putIfAbsent(subarray(keys, batch), subarray(values, batch), actual);
                checkArrays(ctx, "putIfAbsent round " + round, expected, actual, batch);
            } else if (action < 8) {
                for (int i = 0; i < batch; ++i) {
                    expected[i] = unbox(ref.get(keys[i]), noEntry);
                }
                m.get(subarray(keys, batch), actual);
                checkArrays(ctx, "get round " + round, expected, actual, batch);
            } else {
                for (int i = 0; i < batch; ++i) {
                    expected[i] = unbox(ref.remove(keys[i]), noEntry);
                }
                final long[] batchKeys = subarray(keys, batch);
                m.remove(batchKeys, actual);
                checkArrays(ctx, "remove round " + round, expected, actual, batch);
                for (int i = 0; i < batch; ++i) {
                    expected[i] = noEntry;
                }
                m.get(batchKeys, actual);
                checkArrays(ctx, "get-after-remove round " + round, expected, actual, batch);
            }
            if ((round & 0xff) == 0xff) {
                check(ctx, "size at round " + round, ref.size(), m.size());
            }
        }
        check(ctx, "final size", ref.size(), m.size());
        final long[] count = new long[1];
        m.forEach((k, v) -> {
            check(ctx, "forEach key " + k, unbox(ref.get(k), noEntry), v);
            ++count[0];
        });
        check(ctx, "forEach count", ref.size(), count[0]);
        System.out.println("  ok: " + ctx + " (" + totalOps + " ops, final size " + ref.size()
                + ", capacity " + m.capacity() + ")");
    }

    private static void fillRandomValues(SplittableRandom rng, long[] values, int batch) {
        for (int i = 0; i < batch; ++i) {
            values[i] = rng.nextLong(1, Long.MAX_VALUE);
        }
    }

    /** Exact-size copies so impls can't get away with ignoring keys.length. */
    private static long[] subarray(long[] src, int n) {
        final long[] result = new long[n];
        System.arraycopy(src, 0, result, 0, n);
        return result;
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

    private static void checkArrays(String ctx, String what, long[] expected, long[] actual, int n) {
        for (int i = 0; i < n; ++i) {
            check(ctx, what + " index " + i, expected[i], actual[i]);
        }
    }

    private static void check(String ctx, String what, long expected, long actual) {
        if (expected != actual) {
            System.err.println("MISMATCH [" + ctx + "] " + what + ": expected " + expected + ", got " + actual);
            System.exit(1);
        }
    }
}

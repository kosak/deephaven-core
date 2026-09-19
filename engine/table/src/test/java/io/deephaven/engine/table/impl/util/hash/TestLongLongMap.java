//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.engine.table.impl.util.hash;

import io.deephaven.util.mutable.MutableInt;
import io.deephaven.chunk.LongChunk;
import io.deephaven.chunk.WritableLongChunk;
import io.deephaven.chunk.attributes.Any;
import it.unimi.dsi.fastutil.longs.Long2LongOpenHashMap;
import it.unimi.dsi.fastutil.longs.LongLongBiConsumer;
import junit.framework.TestCase;
import org.junit.Test;
import org.junit.runner.RunWith;
import org.junit.runners.Parameterized;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.function.BiFunction;
import java.util.function.LongUnaryOperator;

@RunWith(Parameterized.class)
public class TestLongLongMap {
    private static final Factory referenceFactory = new Factory("fastutil", TestLongLongMap::newReferenceMap);

    private static NullableLongLongMap newReferenceMap(final int initialCapacity, final float loadFactor) {
        return new TestNullableLongLongMap(initialCapacity, loadFactor);
    }

    @Parameterized.Parameters(name = "map={0}, cap={1}, load={2}")
    public static Iterable<Object[]> data() {
        List<Object[]> result = new ArrayList<>();
        final Factory[] factories = {
                referenceFactory,
                new Factory("K1V1", HashMapLockFreeK1V1::new),
                new Factory("K2V2", HashMapLockFreeK2V2::new),
                new Factory("K4V4", HashMapLockFreeK4V4::new)
        };
        final int[] initialCapacities = {10, 1000, 1000000};
        final float[] loadFactors = {0.5f, 0.75f, 0.9f};
        for (Factory factory : factories) {
            for (int ic : initialCapacities) {
                for (float lf : loadFactors) {
                    result.add(new Object[] {factory, ic, lf});
                }
            }
        }
        return result;
    }

    private final Factory factory;
    private final int initialCapacity;
    private final float loadFactor;

    public TestLongLongMap(Factory factory, int initialCapacity, float loadFactor) {
        this.factory = factory;
        this.initialCapacity = initialCapacity;
        this.loadFactor = loadFactor;
    }

    @Test
    public void zeroKey() {
        NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        map.put(0, 12345);
        final NullableLongLongMap.ScalarAccess scalarAccess = new NullableLongLongMap.ScalarAccess();
        scalarAccess.reset(map);
        TestCase.assertEquals(scalarAccess.get(0), 12345);
        TestCase.assertEquals(map.size(), 1);
    }

    @Test
    public void badKeys() {
        // The reference fastutil implementation doesn't have key limitations
        if (factory == referenceFactory) {
            return;
        }
        NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        try {
            map.put(HashMapBase.SPECIAL_KEY_FOR_DELETED_SLOT, 12345);
            TestCase.fail("SPECIAL_KEY_FOR_DELETED_SLOT should not be accepted");
        } catch (io.deephaven.base.verify.AssertionFailure e) {
            // do nothing
        }
        try {
            map.put(HashMapBase.REDIRECTED_KEY_FOR_EMPTY_SLOT, 12345);
            TestCase.fail("REDIRECTED_KEY_FOR_EMPTY_SLOT should not be accepted");
        } catch (io.deephaven.base.verify.AssertionFailure e) {
            // do nothing
        }
    }

    @Test
    public void nullMapReturnsNoEntry() {
        // The reference fastutil implementation doesn't have resetToNull
        if (factory == referenceFactory) {
            return;
        }
        NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        final long noEntryValue = map.defaultReturnValue();
        map.put(0, 1);
        map.put(2, 3);
        map.resetToNull();
        // The hoisted ScalarAccess pattern: allocate and reset a cursor once, outside the loop; gets inside the
        // loop are then cheap. (Reset again after mutating the map. Code whose enclosing method is itself invoked
        // per-element has no loop to hoist over — stash the cursor in a ThreadLocal instead; see
        // WritableRowRedirectionLockFree for that form.)
        final NullableLongLongMap.ScalarAccess scalarAccess = new NullableLongLongMap.ScalarAccess();
        scalarAccess.reset(map);
        for (int ii = 0; ii < 4; ++ii) {
            TestCase.assertEquals(scalarAccess.get(ii), noEntryValue);
        }
    }

    @Test
    public void prevValuesOnInsert() {
        final long beginKey = 100;
        final long endKey = 200;
        final long beginValue = 5000;
        final long endValue = 5010;
        NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        final long noEntryValue = map.defaultReturnValue();
        for (long valueBase = beginValue; valueBase < endValue; ++valueBase) {
            for (long key = beginKey; key < endKey; ++key) {
                final long expectedPrevious = valueBase == beginValue ? noEntryValue : key + valueBase - 1;
                final long actualPrevious = map.put(key, key + valueBase);
                TestCase.assertEquals(expectedPrevious, actualPrevious);
            }
        }
    }

    @Test
    public void prevValuesOnRemove() {
        final long beginKey = 100;
        final long endKey = 200;
        NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        final long noEntryValue = map.defaultReturnValue();
        for (long key = beginKey; key < endKey; ++key) {
            final long previous = map.put(key, key - 10000);
            TestCase.assertEquals(previous, noEntryValue);
        }
        for (long key = beginKey; key < endKey; ++key) {
            final long expectedPrevious = key - 10000;
            final long actualPrevious = map.remove(key);
            TestCase.assertEquals(expectedPrevious, actualPrevious);
        }
    }

    @Test
    public void putIfAbsent() {
        final long beginKey = 100;
        final long endKey = 200;
        NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        final long noEntryValue = map.defaultReturnValue();
        for (long key = beginKey; key < endKey; key += 2) {
            final long previous = map.put(key, key + 5000);
            TestCase.assertEquals(previous, noEntryValue);
        }
        for (long key = beginKey; key < endKey; ++key) {
            final long expectedPrevious = (key % 2) == 0 ? key + 5000 : noEntryValue;
            final long actualPrevious = map.putIfAbsent(key, key + 10000);
            TestCase.assertEquals(expectedPrevious, actualPrevious);
        }
        final NullableLongLongMap.ScalarAccess scalarAccess = new NullableLongLongMap.ScalarAccess();
        scalarAccess.reset(map);
        for (long key = beginKey; key < endKey; ++key) {
            final long expectedValue = (key % 2) == 0 ? key + 5000 : key + 10000;
            final long actualValue = scalarAccess.get(key);
            TestCase.assertEquals(expectedValue, actualValue);
        }
    }

    @Test
    public void clear() {
        final int numIterations = 10;
        final int sizeAtWhichToClear = 10000;
        NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        for (int iteration = 0; iteration < numIterations; ++iteration) {
            TestCase.assertEquals(map.size(), 0);
            for (long ii = 0; ii < sizeAtWhichToClear; ++ii) {
                map.put(ii, ii + 1);
            }
            TestCase.assertEquals(map.size(), sizeAtWhichToClear);
            map.clear();
        }
    }

    @Test
    public void setToNull() {
        // The reference fastutil implementation doesn't have resetToNull
        if (factory == referenceFactory) {
            return;
        }
        final int numIterations = 10;
        final int sizeAtWhichToClear = 10000;
        NullableLongLongMap map = (NullableLongLongMap) factory.create(initialCapacity, loadFactor);
        for (int iteration = 0; iteration < numIterations; ++iteration) {
            TestCase.assertEquals(map.size(), 0);
            for (long ii = 0; ii < sizeAtWhichToClear; ++ii) {
                map.put(ii, ii + 1);
            }
            TestCase.assertEquals(map.size(), sizeAtWhichToClear);
            map.resetToNull();
        }
    }

    @Test
    public void zeroComesBackThroughKeys() {
        NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        final long specialKey = HashMapBase.SPECIAL_KEY_FOR_EMPTY_SLOT;
        map.put(specialKey, 12345);
        final long[] keys = ((NullableLongLongMapTestAccessors) map).keyArray();
        TestCase.assertEquals(1, keys.length);
        TestCase.assertEquals(specialKey, keys[0]);
    }

    @Test
    public void testKeysAndValues() {
        Map<Long, Long> reference = new HashMap<>(initialCapacity, loadFactor);
        NullableLongLongMapTestAccessors test =
                (NullableLongLongMapTestAccessors) factory.create(initialCapacity, loadFactor);
        Random rng = new Random(1283712890);
        populate(rng, 1000000, 10000, 0.75, reference, test);

        final long[] expectedKeys = new long[reference.size()];
        final long[] expectedValues = new long[reference.size()];
        int nextIndex = 0;
        for (Map.Entry<Long, Long> entry : reference.entrySet()) {
            expectedKeys[nextIndex] = entry.getKey();
            expectedValues[nextIndex] = entry.getValue();
            ++nextIndex;
        }
        TestCase.assertEquals(nextIndex, reference.size());
        TestCase.assertEquals(reference.size(), test.size());

        final long[] actualKeys = test.keyArray();
        final long[] actualValues = test.valueArray();
        TestCase.assertEquals(expectedKeys.length, actualKeys.length);
        TestCase.assertEquals(expectedValues.length, actualValues.length);

        Arrays.sort(expectedKeys);
        Arrays.sort(expectedValues);
        Arrays.sort(actualKeys);
        Arrays.sort(actualValues);

        TestCase.assertTrue(Arrays.equals(expectedKeys, actualKeys));
        TestCase.assertTrue(Arrays.equals(expectedValues, actualValues));

        if (test instanceof HashMapBase) {
            // Also exercise the caller-provided-space overloads.
            final long[] keySpace = new long[reference.size()];
            final long[] valueSpace = new long[reference.size()];
            TestCase.assertSame(keySpace, test.keyArray(keySpace));
            TestCase.assertSame(valueSpace, test.valueArray(valueSpace));
            Arrays.sort(keySpace);
            Arrays.sort(valueSpace);
            TestCase.assertTrue(Arrays.equals(expectedKeys, keySpace));
            TestCase.assertTrue(Arrays.equals(expectedValues, valueSpace));
        }
    }

    @Test
    public void do100KInserts() {
        NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        final long beginKey = -50000;
        final long endKey = 50000;
        final long size = endKey - beginKey;
        final long noEntryValue = map.defaultReturnValue();
        for (long key = beginKey; key < endKey; ++key) {
            map.put(key, key + 1000000);
        }
        TestCase.assertEquals(map.size(), size);
        // One reset serves all three read loops: the map is not mutated between them.
        final NullableLongLongMap.ScalarAccess scalarAccess = new NullableLongLongMap.ScalarAccess();
        scalarAccess.reset(map);
        // These lookups should fail
        for (long key = beginKey - size; key < beginKey; ++key) {
            final long result = scalarAccess.get(key);
            TestCase.assertEquals(result, noEntryValue);
        }
        // These lookups should succeed
        for (long key = beginKey; key < endKey; ++key) {
            final long result = scalarAccess.get(key);
            TestCase.assertEquals(result, key + 1000000);
        }
        // These lookups should fail
        for (long key = endKey; key < endKey + size; ++key) {
            final long result = scalarAccess.get(key);
            TestCase.assertEquals(result, noEntryValue);
        }
    }

    @Test
    public void do100KInsertsThen50KRemoves() {
        NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        final long beginKey = 0;
        final long endKey = 100000;
        final long size = endKey - beginKey;
        final long noEntryValue = map.defaultReturnValue();
        for (long key = beginKey; key < endKey; ++key) {
            map.put(key, key + 1000000);
        }
        for (long key = beginKey; key < endKey; key += 2) {
            map.remove(key);
        }
        TestCase.assertEquals(map.size(), size / 2);
        final NullableLongLongMap.ScalarAccess scalarAccess = new NullableLongLongMap.ScalarAccess();
        scalarAccess.reset(map);
        for (long key = beginKey; key < endKey; ++key) {
            final long expectedResult = (key % 2) == 0 ? noEntryValue : key + 1000000;
            final long actualResult = scalarAccess.get(key);
            TestCase.assertEquals(expectedResult, actualResult);
        }
    }

    @Test
    public void chunkedGetHitsAndMisses() {
        final NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        final long noEntryValue = map.defaultReturnValue();
        final long beginKey = -50000;
        final long endKey = 50000;
        final long size = endKey - beginKey;
        final int totalProbes = (int) (3 * size);
        final long[] probes = new long[totalProbes];
        final long probeBegin = beginKey - size;
        for (int ii = 0; ii < totalProbes; ++ii) {
            probes[ii] = probeBegin + ii;
        }

        // A chunked get on a never-populated map yields noEntryValue everywhere.
        checkChunkedGet(map, probes, 4096, key -> noEntryValue);

        for (long key = beginKey; key < endKey; ++key) {
            map.put(key, key + 1000000);
        }

        // Probe a range three times as wide as the occupied keyspace — misses below, hits, misses above — through
        // the chunked entry point, with chunk sizes covering the degenerate, the odd, the typical (with a partial
        // tail), and everything-in-one-chunk.
        for (final int chunkSize : new int[] {1, 7, 4096, totalProbes}) {
            checkChunkedGet(map, probes, chunkSize,
                    key -> key >= beginKey && key < endKey ? key + 1000000 : noEntryValue);
        }

        // An empty keys chunk yields an empty result (the result-size contract).
        final WritableLongChunk<Any> emptyResult = WritableLongChunk.writableChunkWrap(new long[1]);
        map.get(LongChunk.chunkWrap(new long[0]), emptyResult);
        TestCase.assertEquals(0, emptyResult.size());
    }

    @Test
    public void chunkedGetAfterRemoves() {
        final NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        final long noEntryValue = map.defaultReturnValue();
        final long endKey = 100000;
        for (long key = 0; key < endKey; ++key) {
            map.put(key, key + 1000000);
        }
        for (long key = 0; key < endKey; key += 2) {
            map.remove(key);
        }
        // Even keys are tombstoned; the chunked path must probe past the tombstones exactly as a scalar get would.
        final long[] probes = new long[(int) endKey];
        for (int ii = 0; ii < probes.length; ++ii) {
            probes[ii] = ii;
        }
        for (final int chunkSize : new int[] {1000, 4096}) {
            checkChunkedGet(map, probes, chunkSize, key -> (key % 2) == 0 ? noEntryValue : key + 1000000);
        }
    }

    /**
     * Feed {@code probes} through the chunked get in slices of at most {@code chunkSize}, checking every result and the
     * result-size contract on each call.
     */
    private static void checkChunkedGet(final NullableLongLongMap map, final long[] probes, final int chunkSize,
            final LongUnaryOperator expected) {
        final WritableLongChunk<Any> resultChunk = WritableLongChunk.writableChunkWrap(new long[chunkSize]);
        for (int begin = 0; begin < probes.length; begin += chunkSize) {
            final int thisSize = Math.min(chunkSize, probes.length - begin);
            map.get(LongChunk.chunkWrap(probes, begin, thisSize), resultChunk);
            TestCase.assertEquals(thisSize, resultChunk.size());
            for (int ii = 0; ii < thisSize; ++ii) {
                TestCase.assertEquals(expected.applyAsLong(probes[begin + ii]), resultChunk.get(ii));
            }
        }
    }

    @Test
    public void do1MRandomOperationsLotsOfCollisions() {
        // Standard of correctness: java.util.HashMap
        Map<Long, Long> reference = new HashMap<>(initialCapacity, loadFactor);
        NullableLongLongMap test = factory.create(initialCapacity, loadFactor);
        Random rng = new Random(12345);
        populate(rng, 1000000, 10000, 0.75, reference, test);

        TestCase.assertEquals(reference.size(), test.size());

        Entries masterEntries = Entries.create(reference);
        Entries targetEntries = Entries.create(test);

        TestCase.assertTrue(masterEntries.destructivelyEquals(targetEntries));
    }

    @Test
    public void mapStaysSmall() {
        // no way to ask the reference fastutil map for its capacity
        if (factory == referenceFactory) {
            return;
        }
        final int size = 1000;
        final int iterations = 1000000;
        final long randomMod = 1000000000; // 1 billion
        // Use this interface because we want to access 'capacity'
        NullableLongLongMap map = (NullableLongLongMap) factory.create(initialCapacity, loadFactor);
        Random insertStream = new Random(67890);
        Random deleteStream = new Random(67890);

        for (int ii = 0; ii < size; ++ii) {
            final long key = insertStream.nextLong() % randomMod;
            final long value = key + 12;
            map.put(key, value);
        }

        for (int ii = 0; ii < iterations; ++ii) {
            final long deleteKey = deleteStream.nextLong() % randomMod;
            map.remove(deleteKey);

            final long key = insertStream.nextLong() % randomMod;
            final long value = key + 12;
            map.put(key, value);
        }

        // Rationale:
        // 1. Start with the larger of (the target size or the initial capacity)
        // 2. Scale by the inverse of the load factor
        // 3. Scale by 2 (you might have gotten unlucky and gotten just to the threshold and then doubled)
        // 4. Fudge by scaling by 2 (you might have gotten unlucky and had just enough deleted items sitting in slots
        final int expectedCapacityLimit = 2 * (int) (Math.max(size, initialCapacity) / loadFactor);
        final int fudgedLimit = expectedCapacityLimit * 2;
        final int actualCapacity = map.capacity();
        if (actualCapacity > fudgedLimit) {
            String message = String.format("actualCapacity (%d) <= fudgedLimit (%d)", actualCapacity, fudgedLimit);
            TestCase.assertTrue(message, actualCapacity <= fudgedLimit);
        }
    }

    @Test
    public void resetToNullRetainingCapacityRemembersCapacity() {
        // The reference fastutil implementation doesn't have resetToNullRetainingCapacity
        if (factory == referenceFactory) {
            return;
        }
        final int size = 1000;
        final NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        final long noEntryValue = map.defaultReturnValue();

        // Resetting a never-allocated map is a no-op.
        map.resetToNullRetainingCapacity();
        TestCase.assertEquals(0, map.capacity());
        final NullableLongLongMap.ScalarAccess scalarAccess = new NullableLongLongMap.ScalarAccess();
        scalarAccess.reset(map);
        TestCase.assertEquals(noEntryValue, scalarAccess.get(0));

        for (int ii = 0; ii < size; ++ii) {
            map.put(ii * 7, ii);
        }
        final int filledCapacity = map.capacity();
        map.resetToNullRetainingCapacity();

        // The array is released, so the map holds no storage while it sits empty.
        TestCase.assertEquals(0, map.size());
        TestCase.assertTrue(map.isEmpty());
        TestCase.assertEquals(0, map.capacity());
        // The puts above invalidated the binding (the writer footnote in the ScalarAccess contract): reset again.
        scalarAccess.reset(map);
        for (int ii = 0; ii < size; ++ii) {
            TestCase.assertEquals(noEntryValue, scalarAccess.get(ii * 7));
        }

        // The remembered capacity is restored by the next allocation, so refilling to the same size never rehashes.
        map.put(0, 1);
        TestCase.assertEquals(filledCapacity, map.capacity());
        for (int ii = 1; ii < size; ++ii) {
            map.put(ii * 7, ii + 1);
        }
        TestCase.assertEquals(filledCapacity, map.capacity());
        // Mutated again: reset again.
        scalarAccess.reset(map);
        for (int ii = 1; ii < size; ++ii) {
            TestCase.assertEquals(ii + 1, scalarAccess.get(ii * 7));
        }
    }

    @Test
    public void iteratorFromEmptyAndNullMap() {
        NullableLongLongMap map = factory.create(initialCapacity, loadFactor);
        map.put(0, 1);
        map.put(2, 3);
        map.clear();
        emptyMapHelper(map);
        if (factory == referenceFactory) {
            return;
        }
        NullableLongLongMap nullableMap = map;
        nullableMap.resetToNull();
        emptyMapHelper(map);
    }

    private void emptyMapHelper(NullableLongLongMap map) {
        final MutableInt count = new MutableInt();
        map.forEach((key, value) -> {
            count.increment();
        });
        TestCase.assertEquals(0, count.get());
    }

    static class Factory {
        private final String name;
        private BiFunction<Integer, Float, NullableLongLongMap> constructor;

        Factory(String name, BiFunction<Integer, Float, NullableLongLongMap> constructor) {
            this.name = name;
            this.constructor = constructor;
        }

        @Override
        public String toString() {
            return name;
        }

        public NullableLongLongMap create(int initialCapacity, float loadFactor) {
            return constructor.apply(initialCapacity, loadFactor);
        }
    }

    static class Entries {
        public static Entries create(Map<Long, Long> map) {
            int size = map.size();
            final long[] keys = new long[size];
            final long[] values = new long[size];
            int nextIndex = 0;
            for (Map.Entry<Long, Long> entry : map.entrySet()) {
                keys[nextIndex] = entry.getKey();
                values[nextIndex] = entry.getValue();
                ++nextIndex;
            }
            TestCase.assertEquals(nextIndex, size);
            return new Entries(keys, values);
        }

        public static Entries create(NullableLongLongMap map) {
            int size = map.size();
            final long[] keys = new long[size];
            final long[] values = new long[size];
            final MutableInt nextIndex = new MutableInt();
            map.forEach((key, value) -> {
                keys[nextIndex.get()] = key;
                values[nextIndex.getAndIncrement()] = value;
            });
            TestCase.assertEquals(size, nextIndex.get());
            return new Entries(keys, values);
        }

        private final long[] keys;
        private final long[] values;

        Entries(long[] keys, long[] values) {
            TestCase.assertEquals(keys.length, values.length);
            this.keys = keys;
            this.values = values;
        }

        boolean destructivelyEquals(Entries other) {
            Arrays.sort(keys);
            Arrays.sort(values);
            Arrays.sort(other.keys);
            Arrays.sort(other.values);
            final boolean keysEqual = Arrays.equals(keys, other.keys);
            final boolean valuesEqual = Arrays.equals(values, other.values);
            return keysEqual && valuesEqual;
        }
    }

    private static void populate(Random rng, int numIterations, long randomRange, double putProbability,
            Map<Long, Long> reference, NullableLongLongMap test) {
        for (int ii = 0; ii < numIterations; ++ii) {
            final long nextKey = Math.abs(rng.nextLong()) % randomRange;
            final long nextValue = ii;

            if (rng.nextDouble() < putProbability) {
                reference.put(nextKey, nextValue);
                test.put(nextKey, nextValue);
            } else {
                reference.remove(nextKey);
                test.remove(nextKey);
            }
        }
    }

    private static class TestNullableLongLongMap implements NullableLongLongMapTestAccessors {
        final Long2LongOpenHashMap map;

        public TestNullableLongLongMap(int initialCapacity, float loadFactor) {
            map = new Long2LongOpenHashMap(initialCapacity, loadFactor);
            map.defaultReturnValue(-1);
        }

        @Override
        public void resetToNull() {
            throw new UnsupportedOperationException();
        }

        @Override
        public void resetToNullRetainingCapacity() {
            throw new UnsupportedOperationException();
        }

        @Override
        public int capacity() {
            throw new UnsupportedOperationException();
        }

        @Override
        public long[] keyArray() {
            return map.keySet().toLongArray();
        }

        @Override
        public long[] keyArray(long[] space) {
            return map.keySet().toArray(space);
        }

        @Override
        public long[] valueArray() {
            return map.values().toLongArray();
        }

        @Override
        public long[] valueArray(long[] space) {
            return map.values().toArray(space);
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
        public long put(long key, long value) {
            return map.put(key, value);
        }

        @Override
        public long putIfAbsent(long key, long value) {
            return map.putIfAbsent(key, value);
        }

        @Override
        public void get(LongChunk<? extends Any> keys, WritableLongChunk<? extends Any> result) {
            final int size = keys.size();
            for (int ii = 0; ii < size; ++ii) {
                result.set(ii, map.get(keys.get(ii)));
            }
            result.setSize(size);
        }

        @Override
        public long remove(long key) {
            return map.remove(key);
        }

        @Override
        public void clear() {
            map.clear();
        }

        @Override
        public void forEach(LongLongBiConsumer consumer) {
            map.forEach(consumer);
        }
    }
}

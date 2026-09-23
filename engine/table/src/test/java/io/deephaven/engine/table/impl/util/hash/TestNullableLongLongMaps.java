//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.engine.table.impl.util.hash;

import junit.framework.TestCase;
import org.junit.Test;

import java.util.HashMap;
import java.util.Map;

public class TestNullableLongLongMaps {
    // On either side of NullableLongLongMaps.AMAC_LOAD_FACTOR_FLOOR.
    private static final double DENSE = 0.9;
    private static final double SPARSE = 0.5;
    private static final long NO_ENTRY_VALUE = -7;

    @Test
    public void upgradePreservesEverything() {
        final NullableLongLongMap map = HashMapLockFreeK2V2.of(16, DENSE, NO_ENTRY_VALUE);
        final Map<Long, Long> reference = new HashMap<>();
        final NullableLongLongMap.ScalarAccess cursor = new NullableLongLongMap.ScalarAccess();
        cursor.reset(map);
        for (long key = 0; key < 10_000; ++key) {
            cursor.put(key, key + 1_000_000);
            reference.put(key, key + 1_000_000);
        }
        for (long key = 0; key < 10_000; key += 3) {
            cursor.remove(key);
            reference.remove(key);
        }
        final NullableLongLongMap upgraded = NullableLongLongMaps.maybeUpgrade(map, DENSE, 1);
        TestCase.assertTrue(upgraded instanceof HashMapLockFreeK4V4);
        TestCase.assertEquals(NO_ENTRY_VALUE, upgraded.defaultReturnValue());
        TestCase.assertEquals(reference.size(), upgraded.size());
        cursor.reset(upgraded);
        for (long key = 0; key < 10_000; ++key) {
            TestCase.assertEquals((long) reference.getOrDefault(key, NO_ENTRY_VALUE), cursor.get(key));
        }
    }

    @Test
    public void belowThresholdReturnsTheSameMap() {
        final NullableLongLongMap map = HashMapLockFreeK1V1.of(16, DENSE, NO_ENTRY_VALUE);
        final NullableLongLongMap.ScalarAccess cursor = new NullableLongLongMap.ScalarAccess();
        cursor.reset(map);
        for (long key = 0; key < 100; ++key) {
            cursor.put(key, key);
        }
        TestCase.assertSame(map, NullableLongLongMaps.maybeUpgrade(map, DENSE, 1000));
    }

    @Test
    public void sparseLoadFactorReturnsTheSameMap() {
        final NullableLongLongMap map = HashMapLockFreeK1V1.of(16, SPARSE, NO_ENTRY_VALUE);
        final NullableLongLongMap.ScalarAccess cursor = new NullableLongLongMap.ScalarAccess();
        cursor.reset(map);
        for (long key = 0; key < 100; ++key) {
            cursor.put(key, key);
        }
        TestCase.assertSame(map, NullableLongLongMaps.maybeUpgrade(map, SPARSE, 1));
    }

    @Test
    public void ceilingTriggerOverridesSparseLoadFactor() {
        final NullableLongLongMap map = HashMapLockFreeK1V1.of(16, SPARSE, NO_ENTRY_VALUE);
        final NullableLongLongMap.ScalarAccess cursor = new NullableLongLongMap.ScalarAccess();
        cursor.reset(map);
        for (long key = 0; key < 100; ++key) {
            cursor.put(key, key + 1);
        }
        final NullableLongLongMap upgraded = NullableLongLongMaps.maybeUpgrade(map, SPARSE, 1000, 50);
        TestCase.assertTrue(upgraded instanceof HashMapLockFreeK4V4);
        TestCase.assertEquals(100, upgraded.size());
        cursor.reset(upgraded);
        for (long key = 0; key < 100; ++key) {
            TestCase.assertEquals(key + 1, cursor.get(key));
        }
    }

    @Test
    public void alreadyWideReturnsTheSameMap() {
        for (final NullableLongLongMap map : new NullableLongLongMap[] {
                HashMapLockFreeK4V4.of(16, DENSE, NO_ENTRY_VALUE),
                HashMapLockFreeK4V4WithAMAC.of(16, DENSE, NO_ENTRY_VALUE)}) {
            final NullableLongLongMap.ScalarAccess cursor = new NullableLongLongMap.ScalarAccess();
            cursor.reset(map);
            for (long key = 0; key < 100; ++key) {
                cursor.put(key, key);
            }
            TestCase.assertSame(map, NullableLongLongMaps.maybeUpgrade(map, DENSE, 1));
        }
    }

    @Test
    public void wantWindowedReadsGatesOnFootprint() {
        final int threshold = NullableLongLongMaps.DEFAULT_AMAC_THRESHOLD_ENTRIES;
        // At and above the footprint threshold: windowed, however full the map happens to be (occupancy is not an
        // input — it sawtooths with rehash and turned out to be second-order; see the javadoc).
        TestCase.assertTrue(NullableLongLongMaps.wantWindowedReads(threshold));
        TestCase.assertTrue(NullableLongLongMaps.wantWindowedReads(Integer.MAX_VALUE));
        // Below it (cache-resident): serial.
        TestCase.assertFalse(NullableLongLongMaps.wantWindowedReads(threshold - 1));
        TestCase.assertFalse(NullableLongLongMaps.wantWindowedReads(0));
    }
}

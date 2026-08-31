//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.util.datastructures.hash;

import it.unimi.dsi.fastutil.longs.LongLongBiConsumer;

public final class HMLFamacK2V2 extends HMLFamacK2V2Base implements NullableLongLongMapTestAccessors {
    private volatile long[] keysAndValues;

    public HMLFamacK2V2() {
        this(DEFAULT_INITIAL_CAPACITY, DEFAULT_LOAD_FACTOR, DEFAULT_NO_ENTRY_VALUE);
    }

    public HMLFamacK2V2(int desiredInitialCapacity) {
        this(desiredInitialCapacity, DEFAULT_LOAD_FACTOR, DEFAULT_NO_ENTRY_VALUE);
    }

    HMLFamacK2V2(int desiredInitialCapacity, float loadFactor) {
        this(desiredInitialCapacity, loadFactor, DEFAULT_NO_ENTRY_VALUE);
    }

    public HMLFamacK2V2(int desiredInitialCapacity, float loadFactor, long noEntryValue) {
        super(desiredInitialCapacity, loadFactor, noEntryValue);
        this.keysAndValues = null;
    }

    @Override
    protected void setKeysAndValues(long[] keysAndValues) {
        this.keysAndValues = keysAndValues;
    }

    @Override
    public void put(long[] keys, long[] values, long[] oldValues) {
        putBatch(keys, values, oldValues, false);
    }

    @Override
    public void putIfAbsent(long[] keys, long[] values, long[] oldValues) {
        putBatch(keys, values, oldValues, true);
    }

    private void putBatch(long[] keys, long[] values, long[] oldValues, boolean insertOnly) {
        long[] kvs = keysAndValues;
        long numBucketsReciprocal = kvs == null ? 0 : reciprocalFor(kvs);
        for (int ii = 0; ii < keys.length; ++ii) {
            oldValues[ii] = putImpl(kvs, numBucketsReciprocal, keys[ii], values[ii], insertOnly);
            // putImpl may have allocated or rehashed; if so, pick up the new array and recompute the reciprocal.
            final long[] cur = keysAndValues;
            if (cur != kvs) {
                kvs = cur;
                numBucketsReciprocal = reciprocalFor(kvs);
            }
        }
    }

    @Override
    public void get(long[] keys, long[] result) {
        // Lock-free reader: one volatile read and one division amortized over the whole batch, then AMAC windowing.
        final long[] kvs = keysAndValues;
        if (kvs == null) {
            java.util.Arrays.fill(result, 0, keys.length, defaultReturnValue());
            return;
        }
        if (keys.length == 0) {
            return;
        }
        getBatchImpl(kvs, reciprocalFor(kvs), keys, result);
    }

    @Override
    public void remove(long[] keys, long[] oldValues) {
        // Single-writer contract and remove never reallocates, so one snapshot suffices.
        final long[] kvs = keysAndValues;
        final long numBucketsReciprocal = kvs == null ? 0 : reciprocalFor(kvs);
        for (int ii = 0; ii < keys.length; ++ii) {
            oldValues[ii] = removeImpl(kvs, numBucketsReciprocal, keys[ii]);
        }
    }

    public int capacity() {
        return capacityImpl(keysAndValues);
    }

    @Override
    public void clear() {
        clearImpl(keysAndValues);
    }

    public void resetToNull() {
        resetToNullImpl();
        keysAndValues = null;
    }

    @Override
    public long[] keyArray() {
        return keysOrValuesImpl(keysAndValues, null, false);
    }

    @Override
    public long[] keyArray(long[] space) {
        return keysOrValuesImpl(keysAndValues, space, false);
    }

    @Override
    public long[] valueArray() {
        return keysOrValuesImpl(keysAndValues, null, true);
    }

    @Override
    public long[] valueArray(long[] space) {
        return keysOrValuesImpl(keysAndValues, space, true);
    }

    @Override
    public void forEach(LongLongBiConsumer consumer) {
        forEachImpl(keysAndValues, consumer);
    }
}

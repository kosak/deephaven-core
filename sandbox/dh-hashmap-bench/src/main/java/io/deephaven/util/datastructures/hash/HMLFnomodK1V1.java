//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.util.datastructures.hash;

import it.unimi.dsi.fastutil.longs.LongLongBiConsumer;

public final class HMLFnomodK1V1 extends HMLFnomodK1V1Base implements NullableLongLongMapTestAccessors {
    private volatile long[] keysAndValues;

    public HMLFnomodK1V1() {
        this(DEFAULT_INITIAL_CAPACITY, DEFAULT_LOAD_FACTOR, DEFAULT_NO_ENTRY_VALUE);
    }

    public HMLFnomodK1V1(int desiredInitialCapacity) {
        this(desiredInitialCapacity, DEFAULT_LOAD_FACTOR, DEFAULT_NO_ENTRY_VALUE);
    }

    HMLFnomodK1V1(int desiredInitialCapacity, float loadFactor) {
        this(desiredInitialCapacity, loadFactor, DEFAULT_NO_ENTRY_VALUE);
    }

    public HMLFnomodK1V1(int desiredInitialCapacity, float loadFactor, long noEntryValue) {
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
        long magic = kvs == null ? 0 : magicFor(kvs);
        for (int ii = 0; ii < keys.length; ++ii) {
            oldValues[ii] = putImpl(kvs, magic, keys[ii], values[ii], insertOnly);
            // putImpl may have allocated or rehashed; if so, pick up the new array and recompute the magic constant.
            final long[] cur = keysAndValues;
            if (cur != kvs) {
                kvs = cur;
                magic = magicFor(kvs);
            }
        }
    }

    @Override
    public void get(long[] keys, long[] result) {
        // Lock-free reader: one volatile read and one division amortized over the whole batch.
        final long[] kvs = keysAndValues;
        final long magic = kvs == null ? 0 : magicFor(kvs);
        for (int ii = 0; ii < keys.length; ++ii) {
            result[ii] = getImpl(kvs, magic, keys[ii]);
        }
    }

    @Override
    public void remove(long[] keys) {
        // Single-writer contract and remove never reallocates, so one snapshot suffices.
        final long[] kvs = keysAndValues;
        final long magic = kvs == null ? 0 : magicFor(kvs);
        for (int ii = 0; ii < keys.length; ++ii) {
            removeImpl(kvs, magic, keys[ii]);
        }
    }

    public int capacity() {
        return capacityImpl(keysAndValues);
    }

    @Override
    public void clear() {
        clearImpl(keysAndValues);
    }

    @Override
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

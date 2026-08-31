//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.util.datastructures.hash;

import it.unimi.dsi.fastutil.longs.LongLongBiConsumer;

public final class HashMapLockFreeK2V2 extends HashMapK2V2 implements NullableLongLongMapTestAccessors {
    private volatile long[] keysAndValues;

    public HashMapLockFreeK2V2() {
        this(DEFAULT_INITIAL_CAPACITY, DEFAULT_LOAD_FACTOR, DEFAULT_NO_ENTRY_VALUE);
    }

    public HashMapLockFreeK2V2(int desiredInitialCapacity) {
        this(desiredInitialCapacity, DEFAULT_LOAD_FACTOR, DEFAULT_NO_ENTRY_VALUE);
    }

    HashMapLockFreeK2V2(int desiredInitialCapacity, float loadFactor) {
        this(desiredInitialCapacity, loadFactor, DEFAULT_NO_ENTRY_VALUE);
    }

    public HashMapLockFreeK2V2(int desiredInitialCapacity, float loadFactor, long noEntryValue) {
        super(desiredInitialCapacity, loadFactor, noEntryValue);
        this.keysAndValues = null;
    }

    @Override
    protected void setKeysAndValues(long[] keysAndValues) {
        this.keysAndValues = keysAndValues;
    }

    @Override
    public void put(long[] keys, long[] values, long[] oldValues) {
        for (int ii = 0; ii < keys.length; ++ii) {
            oldValues[ii] = putImpl(keysAndValues, keys[ii], values[ii], false);
        }
    }

    @Override
    public void putIfAbsent(long[] keys, long[] values, long[] oldValues) {
        for (int ii = 0; ii < keys.length; ++ii) {
            oldValues[ii] = putImpl(keysAndValues, keys[ii], values[ii], true);
        }
    }

    @Override
    public void get(long[] keys, long[] result) {
        for (int ii = 0; ii < keys.length; ++ii) {
            result[ii] = getImpl(keysAndValues, keys[ii]);
        }
    }

    @Override
    public void remove(long[] keys) {
        for (int ii = 0; ii < keys.length; ++ii) {
            removeImpl(keysAndValues, keys[ii]);
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

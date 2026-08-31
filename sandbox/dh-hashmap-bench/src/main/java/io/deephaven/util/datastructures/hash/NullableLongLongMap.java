//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.util.datastructures.hash;

import it.unimi.dsi.fastutil.longs.LongLongBiConsumer;

/**
 * The interface we use for our Long2LongMaps that are the basis for a hashed redirection index.
 */
public interface NullableLongLongMap {
    void resetToNull();

    int capacity();

    /**
     * @return the size of this map
     */
    int size();

    /**
     * @return true if this map is empty (i.e. size is zero)
     */
    boolean isEmpty();

    /**
     * @return the value returned from {@link #get(long)} when no value is present in the map.
     */
    long defaultReturnValue();

    /**
     * For each i in [0, keys.length): add a mapping from keys[i] to values[i], storing the previous value of keys[i]
     * (or {@link #defaultReturnValue()} if there was no mapping) in oldValues[i]. Elements are processed in index
     * order. values and oldValues must have at least keys.length elements.
     *
     * @param keys the keys to add
     * @param values the values to add
     * @param oldValues output: the previous value of each key (or {@link #defaultReturnValue()})
     */
    void put(long[] keys, long[] values, long[] oldValues);

    /**
     * For each i in [0, keys.length): add a mapping from keys[i] to values[i] if one does not already exist, storing
     * the previous value of keys[i] (or {@link #defaultReturnValue()} if there was no mapping) in oldValues[i].
     * Elements are processed in index order. values and oldValues must have at least keys.length elements.
     *
     * @param keys the keys to add
     * @param values the values to add
     * @param oldValues output: the previous value of each key (or {@link #defaultReturnValue()})
     */
    void putIfAbsent(long[] keys, long[] values, long[] oldValues);

    /**
     * For each i in [0, keys.length): store the value associated with keys[i] (or {@link #defaultReturnValue()} if no
     * mapping exists) in result[i]. result must have at least keys.length elements.
     *
     * @param keys the keys to look up
     * @param result output: the value of each key (or {@link #defaultReturnValue()})
     */
    void get(long[] keys, long[] result);

    /**
     * For each i in [0, keys.length): remove the mapping for keys[i] if one exists, storing the removed value (or
     * {@link #defaultReturnValue()} if there was no mapping) in oldValues[i]. Elements are processed in index order.
     * oldValues must have at least keys.length elements.
     *
     * @param keys the keys to remove
     * @param oldValues output: the removed value of each key (or {@link #defaultReturnValue()})
     */
    void remove(long[] keys, long[] oldValues);

    void clear();

    void forEach(LongLongBiConsumer consumer);
}

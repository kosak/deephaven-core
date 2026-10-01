//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.engine.table.impl.util.hash;

import io.deephaven.chunk.LongChunk;
import io.deephaven.chunk.WritableLongChunk;
import io.deephaven.chunk.attributes.Any;
import it.unimi.dsi.fastutil.longs.LongLongBiConsumer;

/**
 * The interface we use for our Long2LongMaps that are the basis for a hashed redirection index.
 */
public interface NullableLongLongMap {
    /**
     * Empty the map and release its backing array. The next put allocates a new array at the initial capacity.
     */
    void resetToNull();

    /**
     * Empty the map and release its backing array, as {@link #resetToNull()} does, but remember the capacity the map
     * had reached so that the next allocation is made at that size instead of regrowing from the initial capacity
     * through successive rehashes. Like {@link #resetToNull()} and unlike {@link #clear()}, this never writes into the
     * array a concurrent reader might be probing, so it is safe to call while unsynchronized readers are active.
     */
    void resetToNullRetainingCapacity();

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
     * @return the value returned from {@link #get(LongChunk, WritableLongChunk)} when no value is present in the map.
     */
    long defaultReturnValue();

    /**
     * Add a mapping from key to value. Return the old value of key.
     * 
     * @param key the key to add
     * @param value the value to add
     * @return the old value of key (or {@link #defaultReturnValue()} if there was no mapping)
     */
    long put(long key, long value);

    /**
     * Add a mapping from key to value, if one does not already exist. Return the old value of key (or
     * {@link #defaultReturnValue()}) if one does not exist.
     * 
     * @param key the key to add
     * @param value the value to add
     * @return the old value of key (or {@link #defaultReturnValue()} if there was no mapping)
     */
    long putIfAbsent(long key, long value);

    /**
     * Gets the value associated with each element of {@code keys}, writing it to the corresponding element of
     * {@code result}. Keys with no mapping yield {@link #defaultReturnValue()}. On return, the size of {@code result}
     * is set to {@code keys.size()}; its capacity must be at least that large.
     *
     * @param keys the keys to get
     * @param result output: the value of each key (or {@link #defaultReturnValue()})
     */
    void get(LongChunk<? extends Any> keys, WritableLongChunk<? extends Any> result);

    /**
     * Remove a mapping for a key. Return the removed value of key (or {@link #defaultReturnValue()}) if one does not
     * exist.
     * 
     * @param key the key to add
     * @return the removed value of (or {@link #defaultReturnValue()} if there was no mapping)
     */
    long remove(long key);

    /**
     * Empty the map in place, retaining its backing array and capacity. Not safe in the presence of concurrent readers.
     */
    void clear();

    void forEach(LongLongBiConsumer consumer);

    final class ScalarAccessHolder {
        private final ScalarAccess scalarAccess = new ScalarAccess(null);

        /**
         * Gets the cached {@link ScalarAccess} and sets its map. {@link get} calls do not nest,
         * so an inner {@link get} call on the same ScalarAccessHolder wipes out the state of an outer call.
         * @param map The map to reset the {@link ScalarAccess} to.
         * @return The cached {@link ScalarAccess}
         */
        public ScalarAccess get(NullableLongLongMap map) {
            scalarAccess.reset(map);
            return scalarAccess;
        }
    }

    /**
     * A reusable cursor for scalar access to a {@link NullableLongLongMap}, for callers whose shape is genuinely
     * per-element. {@link #reset} binds the cursor to a map and performs (and, in future map implementations, caches)
     * whatever per-batch setup the map's chunked operations need, so that {@link #get} calls are cheap: callers with a
     * loop should reset once outside the loop.
     *
     * <p>
     * Contract: an instance may be used by only one thread at a time. It is valid from the time of {@link #reset}, with
     * the same semantics as any other read of these maps: a concurrent writer will not make it crash, but readers under
     * a clock discipline must discard their work if the clock tells them to. One footnote for writers reading their own
     * map: a mutation invalidates that thread's own bindings to the mutated map — reset again before the next scalar
     * read.
     *
     * <p>
     * Keep one cursor per map you are working with (rather than ping-ponging one cursor between maps): future
     * implementations memoize per-map state keyed on the map's backing storage, and rebinding churns that cache.
     */
    final class ScalarAccess implements AutoCloseable {
        private NullableLongLongMap map;
        private final WritableLongChunk<Any> keyChunk = WritableLongChunk.writableChunkWrap(new long[1]);
        private final WritableLongChunk<Any> valueChunk = WritableLongChunk.writableChunkWrap(new long[1]);

        public ScalarAccess(final NullableLongLongMap map) {
            this.map = map;
        }

        public void reset(final NullableLongLongMap map) {
            this.map = map;
        }

        /**
         * Drops the binding made by {@link #reset}, keeping the cursor's scratch for the next one. A cursor that lives
         * longer than the maps it reads, a thread-local one in particular, releases after each use so that it never
         * keeps a map, and the array behind it, reachable after the map's owner has let it go.
         */
        public void close() {
            this.map = null;
        }

        /**
         * Gets the value associated with key, exactly as {@link NullableLongLongMap#get} would. Returns the bound map's
         * {@link NullableLongLongMap#defaultReturnValue()} if no mapping exists.
         */
        public long get(final long key) {
            keyChunk.set(0, key);
            map.get(keyChunk, valueChunk);
            return valueChunk.get(0);
        }
    }
}

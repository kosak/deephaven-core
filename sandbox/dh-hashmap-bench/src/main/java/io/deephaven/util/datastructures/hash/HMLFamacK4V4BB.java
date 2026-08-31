//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.util.datastructures.hash;

import io.deephaven.hash.PrimeFinder;
import it.unimi.dsi.fastutil.longs.LongLongBiConsumer;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.util.Arrays;

import static io.deephaven.util.datastructures.hash.HMLFamacBase.DEFAULT_INITIAL_CAPACITY;
import static io.deephaven.util.datastructures.hash.HMLFamacBase.DEFAULT_LOAD_FACTOR;
import static io.deephaven.util.datastructures.hash.HMLFamacBase.DEFAULT_NO_ENTRY_VALUE;
import static io.deephaven.util.datastructures.hash.HMLFamacBase.GET_WINDOW;
import static io.deephaven.util.datastructures.hash.HMLFamacBase.REDIRECTED_KEY_FOR_EMPTY_SLOT;
import static io.deephaven.util.datastructures.hash.HMLFamacBase.SIZE_LIMIT4;
import static io.deephaven.util.datastructures.hash.HMLFamacBase.SPECIAL_KEY_FOR_DELETED_SLOT;
import static io.deephaven.util.datastructures.hash.HMLFamacBase.SPECIAL_KEY_FOR_EMPTY_SLOT;
import static io.deephaven.util.datastructures.hash.HMLFamacBase.fixKey;
import static io.deephaven.util.datastructures.hash.HMLFamacBase.probe1;
import static io.deephaven.util.datastructures.hash.HMLFamacBase.probe2;
import static io.deephaven.util.datastructures.hash.HMLFamacBase.reciprocalFor;

/**
 * Experimental K4V4 variant of the HMLFamac family backed by a direct ByteBuffer whose storage is aligned to 64
 * bytes, so every bucket (4 entries x 16 bytes) occupies exactly one cache line. A heap long[] cannot make that
 * promise: its data starts at arrayBase + 16 with 8-byte object alignment, so most K4V4 buckets straddle two lines
 * and every probe pays double the miss traffic. Alignment also simplifies the AMAC window: stashing the first key
 * warms the whole bucket, so no second stash is needed.
 *
 * <p>
 * Probing, sentinels, tombstones, growth heuristics, and the batch/AMAC structure mirror HMLFamacK4V4 exactly; only
 * the storage substrate and addressing (bytes instead of long indices) differ. SPECIAL_KEY_FOR_EMPTY_SLOT is zero,
 * so a freshly allocated direct buffer is already all-empty.
 */
public final class HMLFamacK4V4BB implements NullableLongLongMap {
    private static final int ENTRIES_PER_BUCKET = 4;
    private static final int ENTRY_BYTES = 16;
    private static final int BUCKET_BYTES = ENTRIES_PER_BUCKET * ENTRY_BYTES;
    private static final float NEARLY_FULL_LOAD_FACTOR = 0.9f;

    private final int desiredInitialCapacity;
    private final float loadFactor;
    private final long noEntryValue;

    private int size;
    private int nonEmptySlots;
    private int rehashThreshold;
    private volatile ByteBuffer buf;

    public HMLFamacK4V4BB() {
        this(DEFAULT_INITIAL_CAPACITY, DEFAULT_LOAD_FACTOR, DEFAULT_NO_ENTRY_VALUE);
    }

    public HMLFamacK4V4BB(int desiredInitialCapacity) {
        this(desiredInitialCapacity, DEFAULT_LOAD_FACTOR, DEFAULT_NO_ENTRY_VALUE);
    }

    public HMLFamacK4V4BB(int desiredInitialCapacity, float loadFactor, long noEntryValue) {
        this.desiredInitialCapacity = desiredInitialCapacity;
        this.loadFactor = loadFactor;
        this.noEntryValue = noEntryValue;
        this.buf = null;
        this.rehashThreshold = 0;
    }

    private static long reciprocalForBuf(ByteBuffer b) {
        return reciprocalFor(b.limit() / BUCKET_BYTES);
    }

    /** Allocates a zeroed, 64-byte-aligned direct buffer holding numBuckets buckets and sets the rehash threshold. */
    private ByteBuffer newBuffer(int numBuckets, float loadFactorToUse) {
        final long byteLength = (long) numBuckets * BUCKET_BYTES;
        if (byteLength > Integer.MAX_VALUE - BUCKET_BYTES) {
            throw new UnsupportedOperationException("Table too large: " + numBuckets + " buckets");
        }
        final ByteBuffer raw = ByteBuffer.allocateDirect((int) byteLength + BUCKET_BYTES);
        final ByteBuffer aligned = raw.alignedSlice(BUCKET_BYTES).order(ByteOrder.nativeOrder());
        if (aligned.alignmentOffset(0, BUCKET_BYTES) != 0 || aligned.capacity() < byteLength) {
            throw new IllegalStateException("alignedSlice failed to produce a 64-byte-aligned region");
        }
        aligned.limit((int) byteLength);
        rehashThreshold = (int) ((long) numBuckets * ENTRIES_PER_BUCKET * loadFactorToUse);
        return aligned;
    }

    private ByteBuffer allocateInitialBuffer() {
        final int desiredNumBuckets =
                (desiredInitialCapacity + ENTRIES_PER_BUCKET - 1) / ENTRIES_PER_BUCKET;
        final ByteBuffer result = newBuffer(PrimeFinder.nextPrime(Math.max(1, desiredNumBuckets)), loadFactor);
        buf = result;
        return result;
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
        ByteBuffer b = buf;
        long numBucketsReciprocal = b == null ? 0 : reciprocalForBuf(b);
        for (int ii = 0; ii < keys.length; ++ii) {
            oldValues[ii] = putOne(b, numBucketsReciprocal, keys[ii], values[ii], insertOnly);
            // putOne may have allocated or rehashed; if so, pick up the new buffer and recompute the reciprocal.
            final ByteBuffer cur = buf;
            if (cur != b) {
                b = cur;
                numBucketsReciprocal = reciprocalForBuf(b);
            }
        }
    }

    private long putOne(ByteBuffer b, long numBucketsReciprocal, long key, long value, boolean insertOnly) {
        if (b == null) {
            b = allocateInitialBuffer();
            numBucketsReciprocal = reciprocalForBuf(b);
        }
        return putOneNoTranslate(b, numBucketsReciprocal, fixKey(key), value, insertOnly);
    }

    private long putOneNoTranslate(ByteBuffer b, long numBucketsReciprocal, long key, long value,
            boolean insertOnly) {
        int location = locationFor(b, key, numBucketsReciprocal);
        if (location >= 0) {
            final long oldValue = b.getLong(location + 8);
            if (!insertOnly) {
                b.putLong(location + 8, value);
            }
            return oldValue;
        }

        location = -location - 1;
        ++size;
        if (size >= SIZE_LIMIT4) {
            throw new UnsupportedOperationException(
                    String.format("The Hashtable has exceeded its maximum capacity of %d elements", SIZE_LIMIT4));
        }
        if (b.getLong(location) == SPECIAL_KEY_FOR_EMPTY_SLOT) {
            ++nonEmptySlots;
        }
        b.putLong(location, key);
        b.putLong(location + 8, value);

        if (nonEmptySlots >= rehashThreshold) {
            // Same heuristic as HMLFamacBase.rehash: mostly-live tables grow, tombstone-heavy tables rehash in place.
            final boolean wantResize = size >= nonEmptySlots * 2 / 3;
            rehash(b, wantResize);
        }
        return noEntryValue;
    }

    private void rehash(ByteBuffer oldBuf, boolean wantResize) {
        final int oldNumBuckets = oldBuf.limit() / BUCKET_BYTES;
        final int newNumBuckets;
        final float loadFactorToUse;
        if (wantResize) {
            final int maxBuckets = (Integer.MAX_VALUE - BUCKET_BYTES) / BUCKET_BYTES;
            newNumBuckets = Math.min(PrimeFinder.nextPrime(oldNumBuckets * 2), maxBuckets);
            loadFactorToUse = newNumBuckets < maxBuckets ? loadFactor : NEARLY_FULL_LOAD_FACTOR;
        } else {
            newNumBuckets = oldNumBuckets;
            loadFactorToUse = loadFactor;
        }
        size = 0;
        nonEmptySlots = 0;
        final ByteBuffer newBuf = newBuffer(newNumBuckets, loadFactorToUse);
        final long newReciprocal = reciprocalForBuf(newBuf);
        for (int off = 0; off < oldBuf.limit(); off += ENTRY_BYTES) {
            final long oldKey = oldBuf.getLong(off);
            if (oldKey == SPECIAL_KEY_FOR_EMPTY_SLOT || oldKey == SPECIAL_KEY_FOR_DELETED_SLOT) {
                continue;
            }
            putOneNoTranslate(newBuf, newReciprocal, oldKey, oldBuf.getLong(off + 8), true);
        }
        buf = newBuf;
    }

    @Override
    public void get(long[] keys, long[] result) {
        final ByteBuffer b = buf;
        if (b == null) {
            Arrays.fill(result, 0, keys.length, noEntryValue);
            return;
        }
        if (keys.length == 0) {
            return;
        }
        getBatch(b, reciprocalForBuf(b), keys, result);
    }

    /**
     * AMAC window over an aligned buffer: because a bucket is exactly one cache line, stashing the first key warms
     * the whole bucket — entries 1-3 are guaranteed L1 hits on the next visit, with no second stash.
     */
    private void getBatch(ByteBuffer kvs, long numBucketsReciprocal, long[] keys, long[] result) {
        final int n = keys.length;
        final int byteLength = kvs.limit();
        final int numBuckets = byteLength / BUCKET_BYTES;
        final long noEntry = noEntryValue;
        final int window = Math.min(GET_WINDOW, n);
        final int[] jobSlot = new int[window];
        final long[] jobKey = new long[window];
        final int[] jobProbe = new int[window];
        final int[] jobProbeStart = new int[window];
        final int[] jobOffset = new int[window];
        final long[] stashedKey0 = new long[window];
        int next = 0;
        int active = 0;
        for (int w = 0; w < window; ++w) {
            final long target = fixKey(keys[next]);
            final int probe = probe1(target, numBuckets, numBucketsReciprocal) * BUCKET_BYTES;
            jobSlot[w] = next;
            jobKey[w] = target;
            jobProbe[w] = probe;
            jobProbeStart[w] = probe;
            jobOffset[w] = 0;
            stashedKey0[w] = kvs.getLong(probe);
            ++next;
            ++active;
        }
        int w = -1;
        while (active > 0) {
            w = w + 1 == window ? 0 : w + 1;
            final int slot = jobSlot[w];
            if (slot < 0) {
                continue;
            }
            final long target = jobKey[w];
            final int probe = jobProbe[w];
            final long k0 = stashedKey0[w];
            // The stash warmed this line; the remaining entries of the (aligned) bucket are L1 hits.
            final long k1 = kvs.getLong(probe + 16);
            final long k2 = kvs.getLong(probe + 32);
            final long k3 = kvs.getLong(probe + 48);
            final long value;
            if (k0 == target) {
                value = kvs.getLong(probe + 8);
            } else if (k1 == target) {
                value = kvs.getLong(probe + 24);
            } else if (k2 == target) {
                value = kvs.getLong(probe + 40);
            } else if (k3 == target) {
                value = kvs.getLong(probe + 56);
            } else if (k0 == SPECIAL_KEY_FOR_EMPTY_SLOT || k1 == SPECIAL_KEY_FOR_EMPTY_SLOT
                    || k2 == SPECIAL_KEY_FOR_EMPTY_SLOT || k3 == SPECIAL_KEY_FOR_EMPTY_SLOT) {
                value = noEntry;
            } else {
                // No match, no empty slot: advance to the next bucket and yield this job's turn.
                int offset = jobOffset[w];
                if (offset == 0) {
                    offset = (1 + probe2(target, numBuckets - 2)) * BUCKET_BYTES;
                    jobOffset[w] = offset;
                }
                final long advanced = (long) probe + offset;
                final int nextProbe = (int) (advanced >= byteLength ? advanced - byteLength : advanced);
                if (nextProbe == jobProbeStart[w]) {
                    throw new IllegalStateException("Wrapped around? Impossible.");
                }
                jobProbe[w] = nextProbe;
                stashedKey0[w] = kvs.getLong(nextProbe);
                continue;
            }
            result[slot] = value;
            if (next < n) {
                final long newTarget = fixKey(keys[next]);
                final int newProbe = probe1(newTarget, numBuckets, numBucketsReciprocal) * BUCKET_BYTES;
                jobSlot[w] = next;
                jobKey[w] = newTarget;
                jobProbe[w] = newProbe;
                jobProbeStart[w] = newProbe;
                jobOffset[w] = 0;
                stashedKey0[w] = kvs.getLong(newProbe);
                ++next;
            } else {
                jobSlot[w] = -1;
                --active;
            }
        }
    }

    @Override
    public void remove(long[] keys, long[] oldValues) {
        // Single-writer contract and remove never reallocates, so one snapshot suffices.
        final ByteBuffer b = buf;
        final long numBucketsReciprocal = b == null ? 0 : reciprocalForBuf(b);
        for (int ii = 0; ii < keys.length; ++ii) {
            oldValues[ii] = removeOne(b, numBucketsReciprocal, keys[ii]);
        }
    }

    private long removeOne(ByteBuffer b, long numBucketsReciprocal, long key) {
        if (b == null) {
            return noEntryValue;
        }
        final int location = locationFor(b, fixKey(key), numBucketsReciprocal);
        if (location < 0) {
            return noEntryValue;
        }
        --size;
        b.putLong(location, SPECIAL_KEY_FOR_DELETED_SLOT);
        return b.getLong(location + 8);
    }

    private static int locationFor(ByteBuffer b, long target, long numBucketsReciprocal) {
        final int byteLength = b.limit();
        final int numBuckets = byteLength / BUCKET_BYTES;
        final int probeStart = probe1(target, numBuckets, numBucketsReciprocal) * BUCKET_BYTES;
        int probe = probeStart;
        int offset = 0;
        int priorDeletedSlot = -1;
        while (true) {
            for (int j = 0; j < BUCKET_BYTES; j += ENTRY_BYTES) {
                final long cKey = b.getLong(probe + j);
                if (cKey == target) {
                    return probe + j;
                }
                if (cKey == SPECIAL_KEY_FOR_EMPTY_SLOT) {
                    return -(priorDeletedSlot != -1 ? priorDeletedSlot : probe + j) - 1;
                }
                if (priorDeletedSlot == -1 && cKey == SPECIAL_KEY_FOR_DELETED_SLOT) {
                    priorDeletedSlot = probe + j;
                }
            }
            if (offset == 0) {
                offset = (1 + probe2(target, numBuckets - 2)) * BUCKET_BYTES;
            }
            final long advanced = (long) probe + offset;
            probe = (int) (advanced >= byteLength ? advanced - byteLength : advanced);
            if (probe == probeStart) {
                throw new IllegalStateException("Wrapped around? Impossible.");
            }
        }
    }

    @Override
    public void resetToNull() {
        size = 0;
        nonEmptySlots = 0;
        rehashThreshold = 0;
        buf = null;
    }

    @Override
    public int capacity() {
        final ByteBuffer b = buf;
        return b == null ? 0 : b.limit() / ENTRY_BYTES;
    }

    @Override
    public int size() {
        return size;
    }

    @Override
    public boolean isEmpty() {
        return size == 0;
    }

    @Override
    public long defaultReturnValue() {
        return noEntryValue;
    }

    @Override
    public void clear() {
        final ByteBuffer b = buf;
        if (b == null) {
            return;
        }
        size = 0;
        nonEmptySlots = 0;
        for (int off = 0; off < b.limit(); off += 8) {
            b.putLong(off, SPECIAL_KEY_FOR_EMPTY_SLOT);
        }
    }

    @Override
    public void forEach(LongLongBiConsumer consumer) {
        final ByteBuffer b = buf;
        if (b == null) {
            return;
        }
        for (int off = 0; off < b.limit(); off += ENTRY_BYTES) {
            final long rawKey = b.getLong(off);
            if (rawKey == SPECIAL_KEY_FOR_EMPTY_SLOT || rawKey == SPECIAL_KEY_FOR_DELETED_SLOT) {
                continue;
            }
            // Undo fixKey's storage translation: key 0 is stored as the REDIRECTED sentinel.
            final long key = rawKey == REDIRECTED_KEY_FOR_EMPTY_SLOT ? SPECIAL_KEY_FOR_EMPTY_SLOT : rawKey;
            consumer.accept(key, b.getLong(off + 8));
        }
    }
}

//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.util.datastructures.hash;

import io.deephaven.hash.PrimeFinder;
import it.unimi.dsi.fastutil.longs.LongLongBiConsumer;

import java.lang.foreign.Arena;
import java.lang.foreign.MemorySegment;
import java.util.Arrays;

import static java.lang.foreign.ValueLayout.JAVA_LONG;
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
 * K4V4 on a 64-byte-aligned native MemorySegment (java.lang.foreign, JDK 22+). Same algorithm as HMLFamacK4V4BB but
 * with two structural upgrades over ByteBuffer: segments are long-indexed, so a table can exceed 2GB; and storage
 * comes from {@link Arena#ofAuto()}, which is GC-managed — an old segment stays valid exactly as long as any reader
 * still references its snapshot, giving the same lock-free rehash semantics the heap long[] design gets for free.
 * (A shared arena's deterministic close() is deliberately NOT used: closing while a concurrent reader holds an old
 * snapshot would throw mid-probe. Deterministic reclamation would need epoch-based schemes; out of scope here.)
 */
public final class HMLFamacK4V4MS implements NullableLongLongMap {
    private static final int ENTRIES_PER_BUCKET = 4;
    private static final long ENTRY_BYTES = 16;
    private static final long BUCKET_BYTES = ENTRIES_PER_BUCKET * ENTRY_BYTES;
    private static final float NEARLY_FULL_LOAD_FACTOR = 0.9f;

    private final int desiredInitialCapacity;
    private final float loadFactor;
    private final long noEntryValue;

    private int size;
    private int nonEmptySlots;
    private int rehashThreshold;
    private volatile MemorySegment seg;

    public HMLFamacK4V4MS() {
        this(DEFAULT_INITIAL_CAPACITY, DEFAULT_LOAD_FACTOR, DEFAULT_NO_ENTRY_VALUE);
    }

    public HMLFamacK4V4MS(int desiredInitialCapacity) {
        this(desiredInitialCapacity, DEFAULT_LOAD_FACTOR, DEFAULT_NO_ENTRY_VALUE);
    }

    public HMLFamacK4V4MS(int desiredInitialCapacity, float loadFactor, long noEntryValue) {
        this.desiredInitialCapacity = desiredInitialCapacity;
        this.loadFactor = loadFactor;
        this.noEntryValue = noEntryValue;
        this.seg = null;
        this.rehashThreshold = 0;
    }

    private static int numBucketsOf(MemorySegment s) {
        return (int) (s.byteSize() / BUCKET_BYTES);
    }

    private static long reciprocalForSeg(MemorySegment s) {
        return reciprocalFor(numBucketsOf(s));
    }

    /** Allocates a zeroed, 64-byte-aligned, GC-managed native segment and sets the rehash threshold. */
    private MemorySegment newSegment(int numBuckets, float loadFactorToUse) {
        final MemorySegment result = Arena.ofAuto().allocate((long) numBuckets * BUCKET_BYTES, 64);
        rehashThreshold = (int) ((long) numBuckets * ENTRIES_PER_BUCKET * loadFactorToUse);
        return result;
    }

    private MemorySegment allocateInitialSegment() {
        final int desiredNumBuckets =
                (desiredInitialCapacity + ENTRIES_PER_BUCKET - 1) / ENTRIES_PER_BUCKET;
        final MemorySegment result = newSegment(PrimeFinder.nextPrime(Math.max(1, desiredNumBuckets)), loadFactor);
        seg = result;
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
        MemorySegment s = seg;
        long numBucketsReciprocal = s == null ? 0 : reciprocalForSeg(s);
        for (int ii = 0; ii < keys.length; ++ii) {
            oldValues[ii] = putOne(s, numBucketsReciprocal, keys[ii], values[ii], insertOnly);
            final MemorySegment cur = seg;
            if (cur != s) {
                s = cur;
                numBucketsReciprocal = reciprocalForSeg(s);
            }
        }
    }

    private long putOne(MemorySegment s, long numBucketsReciprocal, long key, long value, boolean insertOnly) {
        if (s == null) {
            s = allocateInitialSegment();
            numBucketsReciprocal = reciprocalForSeg(s);
        }
        return putOneNoTranslate(s, numBucketsReciprocal, fixKey(key), value, insertOnly);
    }

    private long putOneNoTranslate(MemorySegment s, long numBucketsReciprocal, long key, long value,
            boolean insertOnly) {
        long location = locationFor(s, key, numBucketsReciprocal);
        if (location >= 0) {
            final long oldValue = s.get(JAVA_LONG, location + 8);
            if (!insertOnly) {
                s.set(JAVA_LONG, location + 8, value);
            }
            return oldValue;
        }

        location = -location - 1;
        ++size;
        if (size >= SIZE_LIMIT4) {
            throw new UnsupportedOperationException(
                    String.format("The Hashtable has exceeded its maximum capacity of %d elements", SIZE_LIMIT4));
        }
        if (s.get(JAVA_LONG, location) == SPECIAL_KEY_FOR_EMPTY_SLOT) {
            ++nonEmptySlots;
        }
        s.set(JAVA_LONG, location, key);
        s.set(JAVA_LONG, location + 8, value);

        if (nonEmptySlots >= rehashThreshold) {
            final boolean wantResize = size >= nonEmptySlots * 2 / 3;
            rehash(s, wantResize);
        }
        return noEntryValue;
    }

    private void rehash(MemorySegment oldSeg, boolean wantResize) {
        final int oldNumBuckets = numBucketsOf(oldSeg);
        final int newNumBuckets;
        final float loadFactorToUse;
        if (wantResize) {
            final int maxBuckets = SIZE_LIMIT4 / ENTRIES_PER_BUCKET;
            newNumBuckets = Math.min(PrimeFinder.nextPrime(oldNumBuckets * 2), maxBuckets);
            loadFactorToUse = newNumBuckets < maxBuckets ? loadFactor : NEARLY_FULL_LOAD_FACTOR;
        } else {
            newNumBuckets = oldNumBuckets;
            loadFactorToUse = loadFactor;
        }
        size = 0;
        nonEmptySlots = 0;
        final MemorySegment newSeg = newSegment(newNumBuckets, loadFactorToUse);
        final long newReciprocal = reciprocalForSeg(newSeg);
        final long oldBytes = oldSeg.byteSize();
        for (long off = 0; off < oldBytes; off += ENTRY_BYTES) {
            final long oldKey = oldSeg.get(JAVA_LONG, off);
            if (oldKey == SPECIAL_KEY_FOR_EMPTY_SLOT || oldKey == SPECIAL_KEY_FOR_DELETED_SLOT) {
                continue;
            }
            putOneNoTranslate(newSeg, newReciprocal, oldKey, oldSeg.get(JAVA_LONG, off + 8), true);
        }
        seg = newSeg;
    }

    @Override
    public void get(long[] keys, long[] result) {
        final MemorySegment s = seg;
        if (s == null) {
            Arrays.fill(result, 0, keys.length, noEntryValue);
            return;
        }
        if (keys.length == 0) {
            return;
        }
        getBatch(s, reciprocalForSeg(s), keys, result);
    }

    /** AMAC window over an aligned segment: one stash warms the whole single-cache-line bucket. */
    private void getBatch(MemorySegment kvs, long numBucketsReciprocal, long[] keys, long[] result) {
        final int n = keys.length;
        final long byteLength = kvs.byteSize();
        final int numBuckets = (int) (byteLength / BUCKET_BYTES);
        final long noEntry = noEntryValue;
        final int window = Math.min(GET_WINDOW, n);
        final int[] jobSlot = new int[window];
        final long[] jobKey = new long[window];
        final long[] jobProbe = new long[window];
        final long[] jobProbeStart = new long[window];
        final long[] jobOffset = new long[window];
        final long[] stashedKey0 = new long[window];
        int next = 0;
        int active = 0;
        for (int w = 0; w < window; ++w) {
            final long target = fixKey(keys[next]);
            final long probe = probe1(target, numBuckets, numBucketsReciprocal) * BUCKET_BYTES;
            jobSlot[w] = next;
            jobKey[w] = target;
            jobProbe[w] = probe;
            jobProbeStart[w] = probe;
            jobOffset[w] = 0;
            stashedKey0[w] = kvs.get(JAVA_LONG, probe);
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
            final long probe = jobProbe[w];
            final long k0 = stashedKey0[w];
            final long k1 = kvs.get(JAVA_LONG, probe + 16);
            final long k2 = kvs.get(JAVA_LONG, probe + 32);
            final long k3 = kvs.get(JAVA_LONG, probe + 48);
            final long value;
            if (k0 == target) {
                value = kvs.get(JAVA_LONG, probe + 8);
            } else if (k1 == target) {
                value = kvs.get(JAVA_LONG, probe + 24);
            } else if (k2 == target) {
                value = kvs.get(JAVA_LONG, probe + 40);
            } else if (k3 == target) {
                value = kvs.get(JAVA_LONG, probe + 56);
            } else if (k0 == SPECIAL_KEY_FOR_EMPTY_SLOT || k1 == SPECIAL_KEY_FOR_EMPTY_SLOT
                    || k2 == SPECIAL_KEY_FOR_EMPTY_SLOT || k3 == SPECIAL_KEY_FOR_EMPTY_SLOT) {
                value = noEntry;
            } else {
                long offset = jobOffset[w];
                if (offset == 0) {
                    offset = (1 + probe2(target, numBuckets - 2)) * BUCKET_BYTES;
                    jobOffset[w] = offset;
                }
                long nextProbe = probe + offset;
                if (nextProbe >= byteLength) {
                    nextProbe -= byteLength;
                }
                if (nextProbe == jobProbeStart[w]) {
                    throw new IllegalStateException("Wrapped around? Impossible.");
                }
                jobProbe[w] = nextProbe;
                stashedKey0[w] = kvs.get(JAVA_LONG, nextProbe);
                continue;
            }
            result[slot] = value;
            if (next < n) {
                final long newTarget = fixKey(keys[next]);
                final long newProbe = probe1(newTarget, numBuckets, numBucketsReciprocal) * BUCKET_BYTES;
                jobSlot[w] = next;
                jobKey[w] = newTarget;
                jobProbe[w] = newProbe;
                jobProbeStart[w] = newProbe;
                jobOffset[w] = 0;
                stashedKey0[w] = kvs.get(JAVA_LONG, newProbe);
                ++next;
            } else {
                jobSlot[w] = -1;
                --active;
            }
        }
    }

    @Override
    public void remove(long[] keys, long[] oldValues) {
        final MemorySegment s = seg;
        final long numBucketsReciprocal = s == null ? 0 : reciprocalForSeg(s);
        for (int ii = 0; ii < keys.length; ++ii) {
            oldValues[ii] = removeOne(s, numBucketsReciprocal, keys[ii]);
        }
    }

    private long removeOne(MemorySegment s, long numBucketsReciprocal, long key) {
        if (s == null) {
            return noEntryValue;
        }
        final long location = locationFor(s, fixKey(key), numBucketsReciprocal);
        if (location < 0) {
            return noEntryValue;
        }
        --size;
        s.set(JAVA_LONG, location, SPECIAL_KEY_FOR_DELETED_SLOT);
        return s.get(JAVA_LONG, location + 8);
    }

    private static long locationFor(MemorySegment s, long target, long numBucketsReciprocal) {
        final long byteLength = s.byteSize();
        final int numBuckets = (int) (byteLength / BUCKET_BYTES);
        final long probeStart = probe1(target, numBuckets, numBucketsReciprocal) * BUCKET_BYTES;
        long probe = probeStart;
        long offset = 0;
        long priorDeletedSlot = -1;
        while (true) {
            for (long j = 0; j < BUCKET_BYTES; j += ENTRY_BYTES) {
                final long cKey = s.get(JAVA_LONG, probe + j);
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
            probe += offset;
            if (probe >= byteLength) {
                probe -= byteLength;
            }
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
        seg = null; // GC-managed arena: the old segment is reclaimed once no reader references it
    }

    @Override
    public int capacity() {
        final MemorySegment s = seg;
        return s == null ? 0 : (int) (s.byteSize() / ENTRY_BYTES);
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
        final MemorySegment s = seg;
        if (s == null) {
            return;
        }
        size = 0;
        nonEmptySlots = 0;
        s.fill((byte) 0); // SPECIAL_KEY_FOR_EMPTY_SLOT is zero
    }

    @Override
    public void forEach(LongLongBiConsumer consumer) {
        final MemorySegment s = seg;
        if (s == null) {
            return;
        }
        final long byteLength = s.byteSize();
        for (long off = 0; off < byteLength; off += ENTRY_BYTES) {
            final long rawKey = s.get(JAVA_LONG, off);
            if (rawKey == SPECIAL_KEY_FOR_EMPTY_SLOT || rawKey == SPECIAL_KEY_FOR_DELETED_SLOT) {
                continue;
            }
            final long key = rawKey == REDIRECTED_KEY_FOR_EMPTY_SLOT ? SPECIAL_KEY_FOR_EMPTY_SLOT : rawKey;
            consumer.accept(key, s.get(JAVA_LONG, off + 8));
        }
    }
}

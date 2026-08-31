//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.util.datastructures.hash;

import io.deephaven.base.verify.Assert;
import io.deephaven.hash.PrimeFinder;
import it.unimi.dsi.fastutil.longs.LongLongBiConsumer;

import java.util.Arrays;
import java.util.HashSet;

import static io.deephaven.util.QueryConstants.NULL_LONG;

/**
 * Experimental variant of HashMapBase ("HMLFnomod" = HashMapLockFree, no modulo): identical except that probe1 and
 * probe2 use mix + multiply-high range reduction instead of a 64-bit modulo by the prime bucket count.
 */
public abstract class HMLFnomodBase implements NullableLongLongMap {
    static final int DEFAULT_INITIAL_CAPACITY = 10;
    static final long DEFAULT_NO_ENTRY_VALUE = -1;
    static final float DEFAULT_LOAD_FACTOR = 0.5f;

    // There are three "special keys" removed from the range of valid keys that are used to represent various slot
    // states:
    // 1. SPECIAL_KEY_FOR_EMPTY_SLOT is used to represent a slot that has never been used.
    // 2. SPECIAL_KEY_FOR_DELETED_SLOT is used to represent a slot that was once in use, but the key that was formerly
    // present there has been deleted.
    // 3. NULL_LONG is used to represent the null key.
    //
    // These values must all be distinct.
    //
    // For the sake of efficiency, we define SPECIAL_KEY_FOR_EMPTY_SLOT to be 0. This means that our arrays are ready to
    // go after being allocated, and we don't need to fill them with a special value. However, we are aware that our
    // callers may wish to use 0 as a key. To support this, we remap key=0 on get, put, remove, and iteration operations
    // to our special value called REDIRECTED_KEY_FOR_EMPTY_SLOT.
    static final long SPECIAL_KEY_FOR_EMPTY_SLOT = 0;
    static final long REDIRECTED_KEY_FOR_EMPTY_SLOT = NULL_LONG + 1;
    static final long SPECIAL_KEY_FOR_DELETED_SLOT = NULL_LONG + 2;

    /**
     * This is the load factor we use as the hashtable nears its maximum size, in order to try to keep functioning
     * (albeit with reduced performance) rather than failing.
     */
    private static final float NEARLY_FULL_LOAD_FACTOR = 0.9f;

    /**
     * This is the fraction of the maximum possible size at which we just give up and throw an exception. It is kept
     * slightly smaller than the NEARLY_FULL_LOAD_FACTOR (otherwise, we might end up in a situation where every put
     * caused a rehash). Additionally, for some reason K2V2 is much less tolerant of getting full than the other two (it
     * gets very slow as it approaches the max). For this reason, until we figure it out, we maintain individual size
     * factors for each KnVn.
     */
    private static final float SIZE_LIMIT_FACTOR1 = 0.85f;
    private static final float SIZE_LIMIT_FACTOR2 = 0.75f;
    private static final float SIZE_LIMIT_FACTOR4 = 0.85f;
    /**
     * This is the size at which we just give up and throw an exception rather than do a new put. It is number of
     * entries (aka number of longs / 2) * SIZE_LIMIT_FACTORn.
     */
    static final int SIZE_LIMIT1 = (int) (Integer.MAX_VALUE / 2 * SIZE_LIMIT_FACTOR1);
    static final int SIZE_LIMIT2 = (int) (Integer.MAX_VALUE / 2 * SIZE_LIMIT_FACTOR2);
    static final int SIZE_LIMIT4 = (int) (Integer.MAX_VALUE / 2 * SIZE_LIMIT_FACTOR4);

    static {
        // All the "SPECIAL_" values need to be unique. This is one way to check this easily.
        HashSet<Long> hs = new HashSet<>();
        hs.add(NULL_LONG);
        hs.add(SPECIAL_KEY_FOR_EMPTY_SLOT);
        hs.add(REDIRECTED_KEY_FOR_EMPTY_SLOT);
        hs.add(SPECIAL_KEY_FOR_DELETED_SLOT);
        Assert.eq(hs.size(), "hs.size()", 4, "4");
    }

    private final int desiredInitialCapacity;
    private final float loadFactor;
    private final long noEntryValue;
    // There are three kinds of slots: empty, holding a value, and deleted (formerly holding a value).
    // 'size' is the number of slots holding a value.
    int size;
    // 'nonEmptySlots' is the number of slots either holding a value or deleted. It is an invariant that
    // nonEmptySlots >= size.
    int nonEmptySlots;
    // The threshold (generally loadFactor * capacity) at which a rehash is triggered. This happens when nonEmptySlots
    // meets or exceeds rehashThreshold. There is a decision to make about whether to rehash at the same capacity or a
    // larger capacity. The heuristic we use is that if size >= (2/3) * nonEmptySlots we rehash to a larger capacity.
    int rehashThreshold;
    // In various places in the code, we will be dealing with three kinds of units:
    // - How many buckets in the array (this is always a prime number)
    // - How many entries in the array (at 4 entries per bucket, this is numBuckets * 4)
    // - How many longs in the array (at 2 longs per entry (key and value), this is numEntries * 2)
    // The actual array of longs (with length (numBuckets * 4 * 2)) is stored in our child.

    HMLFnomodBase(int desiredInitialCapacity, float loadFactor, long noEntryValue) {
        this.desiredInitialCapacity = desiredInitialCapacity;
        this.loadFactor = loadFactor;
        this.noEntryValue = noEntryValue;
        this.size = 0;
        this.nonEmptySlots = 0;
        this.rehashThreshold = 0;
    }

    static long fixKey(long key) {
        Assert.neq(key, "key", REDIRECTED_KEY_FOR_EMPTY_SLOT, "REDIRECTED_KEY_FOR_EMPTY_SLOT");
        Assert.neq(key, "key", SPECIAL_KEY_FOR_DELETED_SLOT, "SPECIAL_KEY_FOR_DELETED_SLOT");
        return key == SPECIAL_KEY_FOR_EMPTY_SLOT ? REDIRECTED_KEY_FOR_EMPTY_SLOT : key;
    }

    long[] allocateKeysAndValuesArray(int entriesPerBucket) {
        // DesiredInitialCapacity is in units of 'entries'.
        // Ceiling(desiredInitialCapacity / entriesPerBucket)
        final int desiredNumBuckets = (desiredInitialCapacity + entriesPerBucket - 1) / entriesPerBucket;
        // Because we want the number of buckets to be prime
        final int proposedBucketCapacity = PrimeFinder.nextPrime(desiredNumBuckets);
        final int maxBucketCapacity = getMaxBucketCapacity(entriesPerBucket);
        final int newBucketCapacity = Math.min(proposedBucketCapacity, maxBucketCapacity);
        Assert.leq((long) newBucketCapacity * entriesPerBucket * 2, "(long)newBucketCapacity * entriesPerBucket * 2",
                Integer.MAX_VALUE, "Integer.MAX_VALUE");
        final int entryCapacity = newBucketCapacity * entriesPerBucket;
        final int longCapacity = entryCapacity * 2;
        rehashThreshold = (int) (entryCapacity * loadFactor);
        final long[] keysAndValues = new long[longCapacity];
        setKeysAndValues(keysAndValues);
        return keysAndValues;
    }

    void rehash(long[] oldKeysAndValues, boolean wantResize, int entriesPerBucket) {
        final int oldNumLongs = oldKeysAndValues.length;

        final int newNumLongs;
        if (wantResize) {
            final int oldBucketCapacity = oldNumLongs / (entriesPerBucket * 2);
            final int proposedBucketCapacity = PrimeFinder.nextPrime(oldBucketCapacity * 2);
            final int maxBucketCapacity = getMaxBucketCapacity(entriesPerBucket);
            final int newBucketCapacity = Math.min(proposedBucketCapacity, maxBucketCapacity);
            Assert.leq((long) newBucketCapacity * entriesPerBucket * 2,
                    "(long)newBucketCapacity * entriesPerBucket * 2",
                    Integer.MAX_VALUE, "Integer.MAX_VALUE");
            final int newEntryCapacity = newBucketCapacity * entriesPerBucket;
            newNumLongs = newEntryCapacity * 2;

            // If we reach the max bucket capacity, then force the rehash threshold to a high number like 90%.
            final float loadFactorToUse = newBucketCapacity < maxBucketCapacity ? loadFactor : NEARLY_FULL_LOAD_FACTOR;
            rehashThreshold = (int) (newEntryCapacity * loadFactorToUse);
        } else {
            newNumLongs = oldNumLongs;
        }
        size = 0;
        nonEmptySlots = 0;
        long[] newKvs = new long[newNumLongs];
        final long newMagic = fastmodMagic(newNumLongs / (entriesPerBucket * 2));

        // Copy the keys and values over.
        for (int ii = 0; ii < oldKeysAndValues.length; ii += 2) {
            final long oldKey = oldKeysAndValues[ii];
            if (oldKey == SPECIAL_KEY_FOR_EMPTY_SLOT || oldKey == SPECIAL_KEY_FOR_DELETED_SLOT) {
                continue;
            }
            final long oldValue = oldKeysAndValues[ii + 1];
            putImplNoTranslate(newKvs, newMagic, oldKey, oldValue, true);
        }
        setKeysAndValues(newKvs);
    }

    void checkSize(int sizeLimit) {
        // If the size reaches the max allowed value, then throw an exception.
        if (size >= sizeLimit) {
            throw new UnsupportedOperationException(
                    String.format("The Hashtable has exceeded its maximum capacity of %d elements", sizeLimit));
        }
    }

    protected abstract long putImplNoTranslate(long[] kvs, long magic, long key, long value, boolean insertOnly);

    protected abstract void setKeysAndValues(long[] keysAndValues);

    @Override
    public final int size() {
        return size;
    }

    @Override
    public final boolean isEmpty() {
        return size == 0;
    }

    final int capacityImpl(long[] keysAndValues) {
        return keysAndValues == null ? 0 : keysAndValues.length / 2;
    }

    final void clearImpl(long[] keysAndValues) {
        size = 0;
        nonEmptySlots = 0;
        // We leave rehashThreshold alone because the array size (and therefore the hashtable capacity) isn't changing.
        Arrays.fill(keysAndValues, SPECIAL_KEY_FOR_EMPTY_SLOT);
    }

    final void resetToNullImpl() {
        size = 0;
        nonEmptySlots = 0;
        rehashThreshold = 0;
    }

    @Override
    public final long defaultReturnValue() {
        return noEntryValue;
    }

    /**
     * @param kv Our keys and values array
     * @param space The array to populate (if {@code space} is not null and {@code space.length} >=
     *        {@link HMLFnomodBase#size()}, otherwise an array of length {@link HMLFnomodBase#size()} will be allocated.
     * @param wantValues false to return keys; true to return values
     * @return The passed-in or newly-allocated array of (keys or values).
     */
    final long[] keysOrValuesImpl(final long[] kv, final long[] space, final boolean wantValues) {
        final int sz = size;
        final long[] result = space != null && space.length >= sz ? space : new long[sz];
        int nextIndex = 0;
        // In a single-threaded case, we would not need the 'nextIndex < sz' part of the conjunction. But in the
        // unsynchronized concurrent case, we might encounter more keys than would fit in the array (because
        // the set of keys grew concurrently after we checked the size). To avoid an index
        // range exception, we do the 'nextIndex < sz' test here.
        for (int ii = 0; ii < kv.length && nextIndex < sz; ii += 2) {
            final long key = kv[ii];
            if (key == SPECIAL_KEY_FOR_EMPTY_SLOT || key == SPECIAL_KEY_FOR_DELETED_SLOT) {
                continue;
            }
            final long resultEntry;
            if (wantValues) {
                resultEntry = kv[ii + 1];
            } else {
                resultEntry = key == REDIRECTED_KEY_FOR_EMPTY_SLOT ? SPECIAL_KEY_FOR_EMPTY_SLOT : key;
            }
            result[nextIndex++] = resultEntry;
        }
        return result;
    }

    final void forEachImpl(final long[] kv, LongLongBiConsumer consumer) {
        if (kv == null) {
            return;
        }
        for (int nextIndex = findOccupiedSlot(kv, 0); nextIndex < kv.length; nextIndex =
                findOccupiedSlot(kv, nextIndex + 2)) {
            final long rawKey = kv[nextIndex];
            final long key = rawKey == REDIRECTED_KEY_FOR_EMPTY_SLOT ? SPECIAL_KEY_FOR_EMPTY_SLOT : rawKey;
            final long value = kv[nextIndex + 1];
            consumer.accept(key, value);
        }
    }

    /**
     * Find next occupied slot starting at {@code beginSlot}.
     *
     * @param beginSlot The inclusive position from where to start looking.
     * @return The slot containing the next occupied key, or keysAndValues.length if none.
     */
    private int findOccupiedSlot(long[] keysAndValues, int beginSlot) {
        while (beginSlot < keysAndValues.length) {
            final long key = keysAndValues[beginSlot];
            if (key != SPECIAL_KEY_FOR_EMPTY_SLOT && key != SPECIAL_KEY_FOR_DELETED_SLOT) {
                break;
            }
            beginSlot += 2;
        }
        return beginSlot;
    }

    // Run this at class load time to confirm that the values returned by getMaxBucketCapacity aren't too large.
    // (It would be nice to also confirm that they are prime, but there's no easy way to do that)
    static {
        final int longsPerEntry = 2;
        for (int entriesPerBucket : new int[] {1, 2, 4}) {
            final long mbc = getMaxBucketCapacity(entriesPerBucket);
            // Assert.isPrime(mbc);
            Assert.leq(mbc * entriesPerBucket * longsPerEntry, "mbc * entriesPerBucket * longsPerEntry",
                    Integer.MAX_VALUE, "Integer.MAX_VALUE");
        }
    }

    /**
     * @param entriesPerBucket Number of entries per bucket
     * @return The largest prime p such that p * entriesPerBucket * 2 <= Integer.MAX_VALUE
     */
    private static int getMaxBucketCapacity(int entriesPerBucket) {
        switch (entriesPerBucket) {
            case 1:
                return 1073741789;
            case 2:
                return 536870909;
            case 4:
                return 268435399;
            default:
                throw new UnsupportedOperationException("Unexpected entriesPerBucket " + entriesPerBucket);
        }
    }

    /**
     * Computes Stafford variant 13 of 64bit mix function.
     *
     * <p>
     * See David Stafford's <a href="http://zimbry.blogspot.com/2011/09/better-bit-mixing-improving-on.html">Mix13
     * variant</a> / java.util.SplittableRandom#mix64(long).
     */
    static long mix64(long key) {
        key ^= (key >>> 30);
        key *= 0xbf58476d1ce4e5b9L;
        key ^= (key >>> 27);
        key *= 0x94d049bb133111ebL;
        key ^= (key >>> 31);
        return key;
    }

    /**
     * Computes the murmur3 fmix64 finalizer — a second mixer, independent of {@link #mix64}, so that probe2's step
     * remains an independent hash function for double hashing.
     */
    static long mix64b(long key) {
        key ^= (key >>> 33);
        key *= 0xff51afd7ed558ccdL;
        key ^= (key >>> 33);
        key *= 0xc4ceb9fe1a85ec53L;
        key ^= (key >>> 33);
        return key;
    }

    /**
     * Computes the magic constant for Lemire's exact "fastmod" remainder: for 32-bit unsigned x and divisor d,
     * x % d == unsignedMultiplyHigh(magic * x, d) where magic = ceil(2^64 / d). The batch API makes this affordable
     * without stored per-array state: each batch call snapshots the keysAndValues array once, derives the bucket
     * count from its length, and pays for this one division across the whole batch.
     */
    static long fastmodMagic(int d) {
        return Long.divideUnsigned(-1L, d) + 1;
    }

    /**
     * Experimental (HMLFnomod, batch edition): same deliberately weak hash as the original probe1 — a 32-bit fold
     * that sends sequentially indexed keys to adjacent, distinct buckets (cache-friendly) — but the prime modulo is
     * computed exactly via fastmod with the caller-supplied precomputed magic constant instead of a division.
     */
    static int probe1(long key, int range, long magic) {
        final long fold32 = (key ^ (key >>> 32)) & 0xffffffffL;
        return (int) Math.unsignedMultiplyHigh(magic * fold32, range);
    }

    /**
     * Second probe (double-hash step): full mix then multiply-high "fastrange" reduction — well-mixed input makes
     * plain scaling into [0, range) as good as a remainder, and it needs no per-divisor constant at all.
     */
    static int probe2(long key, int range) {
        return (int) Math.unsignedMultiplyHigh(mix64b(key), range);
    }
}

//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.util.datastructures.hash;

public abstract class HMLFamacK4V4Base extends HMLFamacBase {
    HMLFamacK4V4Base(int desiredInitialCapacity, float loadFactor, long noEntryValue) {
        super(desiredInitialCapacity, loadFactor, noEntryValue);
    }

    static long reciprocalFor(long[] kvs) {
        return reciprocalFor(kvs.length / (4 * 2));
    }

    final long putImpl(long[] kvs, long numBucketsReciprocal, long key, long value, boolean insertOnly) {
        if (kvs == null) {
            kvs = allocateKeysAndValuesArray(4);
            numBucketsReciprocal = reciprocalFor(kvs);
        }
        final long fixedKey = fixKey(key);
        return putImplNoTranslate(kvs, numBucketsReciprocal, fixedKey, value, insertOnly);
    }

    protected final long putImplNoTranslate(long[] kvs, long numBucketsReciprocal, long key, long value, boolean insertOnly) {
        // To minimize possible painful effects of nonsynchronized access to our array, we get the reference once.
        int location = getLocationFor(kvs, key, numBucketsReciprocal);
        if (location >= 0) {
            // Item found, so replace it (unless 'insertOnly' is set).
            final long oldValue = kvs[location + 1];
            if (!insertOnly) {
                kvs[location + 1] = value;
            }
            return oldValue;
        }

        // Item not found, so insert it.
        location = -location - 1;
        ++size;
        checkSize(SIZE_LIMIT4);
        // The slot is either empty or removed. If we're about to consume an empty slot, then update our counter.
        if (kvs[location] == SPECIAL_KEY_FOR_EMPTY_SLOT) {
            ++nonEmptySlots;
        }
        kvs[location] = key;
        kvs[location + 1] = value;

        // Did we run out of empty slots?
        if (nonEmptySlots >= rehashThreshold) {
            // This means we're low on empty slots. We might be low on empty slots because we've done a lot of
            // deletions of previous items (in this case 'size' could be small), or because we've done a lot of
            // insertions (in this case 'size' would be close to 'nonEmptySlots'). In the former case we would rather
            // rehash to the same size. In the latter case we would like to grow the hash table. The heuristic we use to
            // make this decision is if size exceeds 2/3 of the nonEmptySlots.
            boolean wantResize = size >= nonEmptySlots * 2 / 3;
            rehash(kvs, wantResize, 4);
        }

        return defaultReturnValue();
    }

    final long getImpl(long[] kvs, long numBucketsReciprocal, long key) {
        if (kvs == null) {
            return defaultReturnValue();
        }
        key = fixKey(key);
        // To minimize possible painful effects of nonsynchronized access to our array, we get the reference once.
        final int location = getLocationFor(kvs, key, numBucketsReciprocal);
        if (location < 0) {
            return defaultReturnValue();
        }
        return kvs[location + 1];
    }

    final long removeImpl(long[] kvs, long numBucketsReciprocal, long key) {
        if (kvs == null) {
            return defaultReturnValue();
        }
        key = fixKey(key);
        // To minimize possible painful effects of nonsynchronized access to our array, we get the reference once.
        final int location = getLocationFor(kvs, key, numBucketsReciprocal);
        if (location < 0) {
            return defaultReturnValue();
        }
        --size;
        kvs[location] = SPECIAL_KEY_FOR_DELETED_SLOT;
        return kvs[location + 1];
    }

    private static int getLocationFor(long[] kvs, long target, long numBucketsReciprocal) {
        // In units of longs
        final int length = kvs.length;
        // In units of buckets
        final int numBuckets = length / (4 * 2);

        // In units of longs again
        final int probeStart = probe1(target, numBuckets, numBucketsReciprocal) * (4 * 2);
        int probe = probeStart;
        // Step between buckets, in units of longs; computed lazily on the first collision.
        int offset = 0;
        // Deleted slots must be searched through, but the first one is remembered: if the key turns out to be absent,
        // the insertion point we report is that tombstone rather than the terminating empty slot.
        int priorDeletedSlot = -1;
        while (true) {
            // Scan the bucket's entries in order. An empty slot both terminates the search and (by the insertion
            // invariant) guarantees no later entry in this bucket is live.
            for (int j = 0; j < 4 * 2; j += 2) {
                final long cKey = kvs[probe + j];
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
                offset = (1 + probe2(target, numBuckets - 2)) * (4 * 2);
            }
            // offset < length and probe < length, so one conditional subtraction replaces the modulo.
            final long advanced = (long) probe + offset;
            probe = (int) (advanced >= length ? advanced - length : advanced);
            if (probe == probeStart) {
                throw new IllegalStateException("Wrapped around? Impossible.");
            }
        }
    }
    /**
     * Batch get via a rolling window of {@link HMLFamacBase#GET_WINDOW} in-flight lookups (AMAC style). Each job's
     * turn ends by loading the first and last keys of its next bucket into the stash; the values are
     * consumed one full window-rotation later, by which time the cache lines have typically arrived — so up to a
     * window's worth of misses are serviced concurrently instead of serially. Tombstones need no bookkeeping here:
     * a lookup just probes past them, and only insertion cares where they are.
     */
    final void getBatchImpl(long[] kvs, long numBucketsReciprocal, long[] keys, long[] result) {
        final int n = keys.length;
        final int length = kvs.length;
        final int numBuckets = length / (4 * 2);
        final long noEntry = defaultReturnValue();
        final int window = Math.min(GET_WINDOW, n);
        final int[] jobSlot = new int[window];
        final long[] jobKey = new long[window];
        final int[] jobProbe = new int[window];
        final int[] jobProbeStart = new int[window];
        final int[] jobOffset = new int[window];
        final long[] stashedKey0 = new long[window];
        final long[] stashedKeyLast = new long[window];
        int next = 0;
        int active = 0;
        for (int w = 0; w < window; ++w) {
            final long target = fixKey(keys[next]);
            final int probe = probe1(target, numBuckets, numBucketsReciprocal) * (4 * 2);
            jobSlot[w] = next;
            jobKey[w] = target;
            jobProbe[w] = probe;
            jobProbeStart[w] = probe;
            jobOffset[w] = 0;
            stashedKey0[w] = kvs[probe];
            stashedKeyLast[w] = kvs[probe + 6];
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
                final long k3 = stashedKeyLast[w];
                // Entries 1 and 2 sit between entry 0 and entry 3, so their cache lines were warmed by the stashes.
                final long k1 = kvs[probe + 2];
                final long k2 = kvs[probe + 4];
                final long value;
                if (k0 == target) {
                    value = kvs[probe + 1];
                } else if (k1 == target) {
                    value = kvs[probe + 3];
                } else if (k2 == target) {
                    value = kvs[probe + 5];
                } else if (k3 == target) {
                    value = kvs[probe + 7];
                } else if (k0 == SPECIAL_KEY_FOR_EMPTY_SLOT || k1 == SPECIAL_KEY_FOR_EMPTY_SLOT
                        || k2 == SPECIAL_KEY_FOR_EMPTY_SLOT || k3 == SPECIAL_KEY_FOR_EMPTY_SLOT) {
                    value = noEntry;
                } else {
                // No match, no empty slot: advance to the next bucket and yield this job's turn.
                int offset = jobOffset[w];
                if (offset == 0) {
                    offset = (1 + probe2(target, numBuckets - 2)) * (4 * 2);
                    jobOffset[w] = offset;
                }
                final long advanced = (long) probe + offset;
                final int nextProbe = (int) (advanced >= length ? advanced - length : advanced);
                if (nextProbe == jobProbeStart[w]) {
                    throw new IllegalStateException("Wrapped around? Impossible.");
                }
                jobProbe[w] = nextProbe;
                stashedKey0[w] = kvs[nextProbe];
                stashedKeyLast[w] = kvs[nextProbe + 6];
                continue;
            }
            result[slot] = value;
            if (next < n) {
                final long newTarget = fixKey(keys[next]);
                final int newProbe = probe1(newTarget, numBuckets, numBucketsReciprocal) * (4 * 2);
                jobSlot[w] = next;
                jobKey[w] = newTarget;
                jobProbe[w] = newProbe;
                jobProbeStart[w] = newProbe;
                jobOffset[w] = 0;
                stashedKey0[w] = kvs[newProbe];
                stashedKeyLast[w] = kvs[newProbe + 6];
                ++next;
            } else {
                jobSlot[w] = -1;
                --active;
            }
        }
    }
}

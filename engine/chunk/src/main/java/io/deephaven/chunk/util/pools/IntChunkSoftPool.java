//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
// ****** AUTO-GENERATED CLASS - DO NOT EDIT MANUALLY
// ****** Edit CharChunkSoftPool and run "./gradlew replicateSourcesAndChunks" to regenerate
//
// @formatter:off
package io.deephaven.chunk.util.pools;

import io.deephaven.util.type.ArrayTypeUtils;
import io.deephaven.chunk.attributes.Any;
import io.deephaven.chunk.*;
import io.deephaven.util.datastructures.SegmentedSoftPool;
import org.jetbrains.annotations.NotNull;

import static io.deephaven.chunk.util.pools.ChunkPoolConstants.*;

/**
 * {@link ChunkPool} implementation for chunks of ints.
 */
@SuppressWarnings("rawtypes")
public final class IntChunkSoftPool implements IntChunkPool {

    private final WritableIntChunk<Any> EMPTY = WritableIntChunk.writableChunkWrap(ArrayTypeUtils.EMPTY_INT_ARRAY);

    /**
     * Sub-pools by power-of-two sizes for {@link WritableIntChunk}s.
     */
    private final SegmentedSoftPool<WritableIntChunk>[] writableIntChunks;

    /**
     * Sub-pool of {@link ResettableIntChunk}s.
     */
    private final SegmentedSoftPool<ResettableIntChunk> resettableIntChunks;

    /**
     * Sub-pool of {@link ResettableWritableIntChunk}s.
     */
    private final SegmentedSoftPool<ResettableWritableIntChunk> resettableWritableIntChunks;

    IntChunkSoftPool() {
        // noinspection unchecked
        writableIntChunks = new SegmentedSoftPool[NUM_POOLED_CHUNK_CAPACITIES];
        for (int pcci = 0; pcci < NUM_POOLED_CHUNK_CAPACITIES; ++pcci) {
            final int chunkLog2Capacity = pcci + SMALLEST_POOLED_CHUNK_LOG2_CAPACITY;
            final int chunkCapacity = 1 << chunkLog2Capacity;
            writableIntChunks[pcci] = new SegmentedSoftPool<>(
                    SUB_POOL_SEGMENT_CAPACITY,
                    () -> ChunkPoolInstrumentation
                            .getAndRecord(() -> WritableIntChunk.makeWritableChunkForPool(chunkCapacity)),
                    (final WritableIntChunk chunk) -> chunk.setSize(chunkCapacity));
        }
        resettableIntChunks = new SegmentedSoftPool<>(
                SUB_POOL_SEGMENT_CAPACITY,
                () -> ChunkPoolInstrumentation.getAndRecord(ResettableIntChunk::makeResettableChunkForPool),
                ResettableIntChunk::clear);
        resettableWritableIntChunks = new SegmentedSoftPool<>(
                SUB_POOL_SEGMENT_CAPACITY,
                () -> ChunkPoolInstrumentation.getAndRecord(ResettableWritableIntChunk::makeResettableChunkForPool),
                ResettableWritableIntChunk::clear);
    }

    @Override
    public ChunkPool asChunkPool() {
        return new ChunkPool() {
            @Override
            public <ATTR extends Any> WritableChunk<ATTR> takeWritableChunk(final int capacity) {
                return takeWritableIntChunk(capacity);
            }

            @Override
            public <ATTR extends Any> void giveWritableChunk(@NotNull final WritableChunk<ATTR> writableChunk) {
                giveWritableIntChunk(writableChunk.asWritableIntChunk());
            }

            @Override
            public <ATTR extends Any> ResettableReadOnlyChunk<ATTR> takeResettableChunk() {
                return takeResettableIntChunk();
            }

            @Override
            public <ATTR extends Any> void giveResettableChunk(
                    @NotNull final ResettableReadOnlyChunk<ATTR> resettableChunk) {
                giveResettableIntChunk(resettableChunk.asResettableIntChunk());
            }

            @Override
            public <ATTR extends Any> ResettableWritableChunk<ATTR> takeResettableWritableChunk() {
                return takeResettableWritableIntChunk();
            }

            @Override
            public <ATTR extends Any> void giveResettableWritableChunk(
                    @NotNull final ResettableWritableChunk<ATTR> resettableWritableChunk) {
                giveResettableWritableIntChunk(resettableWritableChunk.asResettableWritableIntChunk());
            }
        };
    }

    @Override
    public <ATTR extends Any> WritableIntChunk<ATTR> takeWritableIntChunk(final int capacity) {
        if (capacity == 0) {
            // noinspection unchecked
            return (WritableIntChunk<ATTR>) EMPTY;
        }
        final int poolIndexForTake = getPoolIndexForTake(checkCapacityBounds(capacity));
        if (poolIndexForTake >= 0) {
            // noinspection resource
            final WritableIntChunk result = writableIntChunks[poolIndexForTake].take();
            result.setSize(capacity);
            // noinspection unchecked
            return ChunkPoolReleaseTracking.onTake(result);
        }
        // noinspection unchecked
        return ChunkPoolReleaseTracking.onTake(WritableIntChunk.makeWritableChunkForPool(capacity));
    }

    @Override
    public void giveWritableIntChunk(@NotNull final WritableIntChunk<?> writableIntChunk) {
        if (writableIntChunk == EMPTY || writableIntChunk.isAlias(EMPTY)) {
            return;
        }
        ChunkPoolReleaseTracking.onGive(writableIntChunk);
        final int capacity = writableIntChunk.capacity();
        final int poolIndexForGive = getPoolIndexForGive(checkCapacityBounds(capacity));
        if (poolIndexForGive >= 0) {
            writableIntChunks[poolIndexForGive].give(writableIntChunk);
        }
    }

    @Override
    public <ATTR extends Any> ResettableIntChunk<ATTR> takeResettableIntChunk() {
        // noinspection unchecked
        return ChunkPoolReleaseTracking.onTake(resettableIntChunks.take());
    }

    @Override
    public void giveResettableIntChunk(@NotNull final ResettableIntChunk resettableIntChunk) {
        resettableIntChunks.give(ChunkPoolReleaseTracking.onGive(resettableIntChunk));
    }

    @Override
    public <ATTR extends Any> ResettableWritableIntChunk<ATTR> takeResettableWritableIntChunk() {
        // noinspection unchecked
        return ChunkPoolReleaseTracking.onTake(resettableWritableIntChunks.take());
    }

    @Override
    public void giveResettableWritableIntChunk(
            @NotNull final ResettableWritableIntChunk resettableWritableIntChunk) {
        resettableWritableIntChunks.give(ChunkPoolReleaseTracking.onGive(resettableWritableIntChunk));
    }
}

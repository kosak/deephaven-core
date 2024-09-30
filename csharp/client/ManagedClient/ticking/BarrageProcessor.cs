using Google.FlatBuffers;
using io.deephaven.barrage.flatbuf;

namespace Deephaven.ManagedClient;

interface IChunkProcessor {
  TickingUpdate? ProcessNextChunk(IColumnSource[] sources, int[] begins, int[] ends, byte[] metadata);
}

public class BarrageProcessor {
  private IChunkProcessor _currentProcessor;

  public BarrageProcessor() {
    _currentProcessor = new AwaitingMetadata();
  }

  public const UInt32 DeephavenMagicNumber = 0x6E687064U;

  public static byte[] CreateSubscriptionRequest(byte[] ticketBytes, int size) {
    throw new NotImplementedException("NIY");
  }

  public TickingUpdate? ProcessNextChunk(IColumnSource[] sources, int[] sizes, byte[] metadata) {
    var begins = new int[sizes.Length];
    return _currentProcessor.ProcessNextChunk(sources, begins, sizes, metadata);
  }
}

class AwaitingMetadata : IChunkProcessor {
  public TickingUpdate? ProcessNextChunk(IColumnSource[] sources, int[] begins, int[] ends, byte[] metadata) {
    if (metadata == null) {
      throw new Exception("Metadata was required here, but none was received");
    }

    var bmw = BarrageMessageWrapper.GetRootAsBarrageMessageWrapper(new ByteBuffer(metadata));

    if (bmw.Magic != BarrageProcessor.DeephavenMagicNumber) {
      throw new Exception($"Expected magic number {BarrageProcessor.DeephavenMagicNumber:x}, got {bmw.Magic:x}");
    }

    if (bmw.MsgType != BarrageMessageType.BarrageUpdateMetadata) {
      throw new Exception($"Expected Barrage Message Type {BarrageMessageType.BarrageUpdateMetadata}, got {bmw.MsgType}");
    }

    // var payloadRawSbytes = bmw.GetMsgPayloadArray();
    // var payloadRawBytes = new byte[payloadRawSbytes.Length];
    // Array.Copy(payloadRawSbytes, payloadRawBytes, payloadRawSbytes.Length);
    var bytes = bmw.GetMsgPayloadBytes()!.ToArray<byte>();
    var bmd = BarrageUpdateMetadata.GetRootAsBarrageUpdateMetadata(new ByteBuffer(bytes));

    var removedRowsBytes = bmd.GetRemovedRowsBytes();
    var shiftDataBytes = bmd.GetShiftDataBytes();
    var addedRowsBytes = bmd.GetAddedRowsBytes();
    if (removedRowsBytes == null || shiftDataBytes == null || addedRowsBytes == null) {
      throw new Exception("Programming error: These data structures should not be null");
    }

    var diRemoved = new DataInput(removedRowsBytes);
    var diThreeShiftIndices = new DataInput(shiftDataBytes);
    var diAdded = new DataInput(addedRowsBytes);

    var removedRows = IndexDecoder.ReadExternalCompressedDelta(diRemoved);
    var shiftStartIndex = IndexDecoder.ReadExternalCompressedDelta(diThreeShiftIndices);
    var shiftEndIndex = IndexDecoder.ReadExternalCompressedDelta(diThreeShiftIndices);
    var shiftDestIndex = IndexDecoder.ReadExternalCompressedDelta(diThreeShiftIndices);
    var addedRows = IndexDecoder.ReadExternalCompressedDelta(diAdded);

    var perColumnModifies = new List<RowSequence>();
    for (var i = 0; i != bmd.ModColumnNodesLength; ++i) {
      var mcns = bmd.ModColumnNodes(i);
      if (!mcns.HasValue) {
        throw new Exception($"Programming error: ModColumnNodes[{i}] should not be empty");
      }

      var modifiedRowsBytes = mcns.Value.GetModifiedRowsBytes();
      if (modifiedRowsBytes == null) {
        throw new Exception($"Programming error: modifiedRowsBytes[{i}] should not be null");
      }
      var diModified = new DataInput(modifiedRowsBytes);
      var modRows = IndexDecoder.ReadExternalCompressedDelta(diModified);
      perColumnModifies.Add(modRows);
    }

    // Correct order to process Barrage info is:
    // 1. removes
    // 2. shifts
    // 3. adds
    // 4. modifies
    // We have not called with add or modify data yet, but we can do removes and shifts now
    // (steps 1 and 2).
    var (prev, removedRowsIndexSpace, afterRemoves) = ProcessRemoves(removedRows);
    tableState.ApplyShifts(shiftStartIndex, shiftEndIndex, shiftDestIndex);

    var addedRowsIndexSpace = tableState.AddKeys(addedRows);

    var nextState = new AwaitingAdds(perColumnModifies.ToArray(), prev, removedRowsIndexSpace, afterRemoves, addedRowsIndexSpace);
    return nextState.ProcessNextChunk(sources, begins, ends, Array.Empty<byte>());
  }

  (ClientTable, RowSequence, ClientTable) ProcessRemoves(RowSequence removedRows) {
    var prev = tableState.Snapshot();
    // The reason we special-case "empty" is because when the tables are unchanged, we prefer
    // to indicate this via pointer equality (e.g. beforeRemoves == afterRemoves).
    RowSequence removedRowsIndexSpace;
    ClientTable afterRemoves;
    if (removedRows.Empty) {
      removedRowsIndexSpace = RowSequence.CreateEmpty();
      afterRemoves = prev;
    } else {
      removedRowsIndexSpace = tableState.Erase(removedRows);
      afterRemoves = tableState.Snapshot();
    }

    return (prev, removedRowsIndexSpace, afterRemoves);
  }
}

class AwaitingAdds(
  RowSequence[] perColumnModifies,
  ClientTable prev,
  RowSequence removedRowsIndexSpace,
  ClientTable afterRemoves,
  RowSequence addedRowsIndexSpace) : IChunkProcessor {

  bool _firstTime = true;
  private RowSequence _addedRowsRemaining;

  public TickingUpdate? ProcessNextChunk(IColumnSource[] sources, int[] begins, int[] ends, byte[] metadata) {
    if (_firstTime) {
      _firstTime = false;

      if (addedRowsIndexSpace.Empty) {
        _addedRowsRemaining = RowSequence.CreateEmpty();

        var afterAdds = afterRemoves;

        var nextState = new AwaitingModifies(afterAdds);
        return nextState.ProcessNextChunk(sources, begins, ends, metadata);
      }

      if (owner->awaitingMetadata_.num_cols_ == 0) {
        throw new Exception("AddedRows is not empty but numCols == 0");
      }

      // Working copy that is consumed in the iterations of the loop.
      _addedRowsRemaining = addedRowsIndexSpace;
    }

    auto & begins = *beginsp;
    AssertAllSame(sources.size(), begins.size(), ends.size());
    var numSources = sources.size();

    if (_addedRowsRemaining.Empty) {
      throw new Exception("Programming error: addedRowsRemaining is empty");
    }

    if (begins.Equals(ends)) {
      // Need more data from caller.
      return (null, this);
    }

    var chunkSize = ends[0] - begins[0];
    for (var i = 1; i != numSources; ++i) {
      var thisSize = ends[i] - begins[i];
      if (thisSize != chunkSize) {
        throw new Exception($"Chunks have inconsistent sizes: {thisSize} vs {chunkSize}");
      }
    }

    if (_addedRowsRemaining.Size < chunkSize) {
      throw new Exception($"There is excess data in the chunk. Expected {_addedRowsRemaining.Size}, have {chunkSize}");
    }

    var indexRowsThisTime = _addedRowsRemaining.Take(chunkSize);
    _addedRowsRemaining = _addedRowsRemaining.Drop(chunkSize);
    tableState.AddData(sources, begins, ends, indexRowsThisTime);

    // To indicate to the caller that we've consumed the data here (so it can't e.g. be passed on to modify)
    Array.Copy(ends, begins, ends.Length);

    if (!_addedRowsRemaining.Empty) {
      // Need more data from caller.
      return (null, this);
    }

    // No more data remaining. Add phase is done.
    var after_adds = tableState.Snapshot();

    owner->state_ = State::kAwaitingModifies;
    owner->awaitingModifies_.Init(std::move(after_adds));
    return owner->awaitingModifies_.ProcessNextChunk(owner, sources, beginsp, ends, nullptr, 0);
  }
}
using Apache.Arrow;
using Google.FlatBuffers;
using io.deephaven.barrage.flatbuf;
using Array = System.Array;

namespace Deephaven.ManagedClient;

interface IChunkProcessor {
  (TickingUpdate?, IChunkProcessor) ProcessNextChunk(IColumnSource[] sources, int[] begins, int[] ends, byte[] metadata);
}

public class BarrageProcessor {
  private readonly TableState _tableState = new();
  private IChunkProcessor _currentProcessor;

  public BarrageProcessor(int numCols) {
    _currentProcessor = new AwaitingMetadata(numCols, _tableState);
  }

  public const UInt32 DeephavenMagicNumber = 0x6E687064U;

  public static byte[] CreateSubscriptionRequest(byte[] ticketBytes, int size) {
    throw new NotImplementedException("NIY");
  }

  public TickingUpdate? ProcessNextChunk(IColumnSource[] sources, int[] sizes, byte[]? metadata) {
    var begins = new int[sizes.Length];
    var (update, next) = _currentProcessor.ProcessNextChunk(sources, begins, sizes, metadata);
    _currentProcessor = next;
    return update;
  }
}

class AwaitingMetadata(int numCols, TableState tableState) : IChunkProcessor {
  public (TickingUpdate?, IChunkProcessor) ProcessNextChunk(IColumnSource[] sources, int[] begins, int[] ends,
    byte[] metadata) {
    if (metadata == null) {
      throw new Exception("Metadata was required here, but none was received");
    }

    var bmw = BarrageMessageWrapper.GetRootAsBarrageMessageWrapper(new ByteBuffer(metadata));

    if (bmw.Magic != BarrageProcessor.DeephavenMagicNumber) {
      throw new Exception($"Expected magic number {BarrageProcessor.DeephavenMagicNumber:x}, got {bmw.Magic:x}");
    }

    if (bmw.MsgType != BarrageMessageType.BarrageUpdateMetadata) {
      throw new Exception(
        $"Expected Barrage Message Type {BarrageMessageType.BarrageUpdateMetadata}, got {bmw.MsgType}");
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

    var nextState = new AwaitingAdds(numCols, tableState, perColumnModifies.ToArray(), prev, removedRowsIndexSpace, afterRemoves,
      addedRowsIndexSpace);
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
  int numCols,
  TableState tableState,
  RowSequence[] perColumnModifies,
  ClientTable prev,
  RowSequence removedRowsIndexSpace,
  ClientTable afterRemoves,
  RowSequence addedRowsIndexSpace) : IChunkProcessor {

  bool _firstTime = true;
  private RowSequence _addedRowsRemaining = RowSequence.CreateEmpty();

  public (TickingUpdate?, IChunkProcessor) ProcessNextChunk(IColumnSource[] sources, int[] begins, int[] ends, byte[] metadata) {
    if (_firstTime) {
      _firstTime = false;

      if (addedRowsIndexSpace.Empty) {
        _addedRowsRemaining = RowSequence.CreateEmpty();

        var afterAdds = afterRemoves;
        var nextState = new AwaitingModifies(numCols, tableState, prev, removedRowsIndexSpace, afterRemoves,
          addedRowsIndexSpace, afterAdds, perColumnModifies);
        return nextState.ProcessNextChunk(sources, begins, ends, metadata);
      }

      if (numCols == 0) {
        throw new Exception("AddedRows is not empty but numCols == 0");
      }

      // Working copy that is consumed in the iterations of the loop.
      _addedRowsRemaining = addedRowsIndexSpace;
    }

    Checks.AssertAllSame(sources.Length, begins.Length, ends.Length);
    var numSources = sources.Length;

    if (_addedRowsRemaining.Empty) {
      throw new Exception("Programming error: addedRowsRemaining is empty");
    }

    if (begins.Equals(ends)) {
      // Need more data from caller.
      return (null, this);
    }

    var chunkSize = (UInt64)(ends[0] - begins[0]);
    for (var i = 1; i != numSources; ++i) {
      var thisSize = (UInt64)(ends[i] - begins[i]);
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
    {
      var afterAdds = tableState.Snapshot();
      var nextState = new AwaitingModifies(numCols, tableState, prev, removedRowsIndexSpace, afterRemoves,
        addedRowsIndexSpace, afterAdds, perColumnModifies);
      return nextState.ProcessNextChunk(sources, begins, ends, metadata);
    }
  }
}

class AwaitingModifies(
  int numCols,
  TableState tableState,
  ClientTable prev,
  RowSequence removedRowsIndexSpace,
  ClientTable afterRemoves,
  RowSequence addedRowsIndexSpace,
  ClientTable afterAdds,
  RowSequence[] perColumnModifies) : IChunkProcessor {
  private bool _firstTime = true;
  private RowSequence[] _modifiedRowsRemaining = [];
  private RowSequence[] _modifiedRowsIndexSpace = [];

  public (TickingUpdate?, IChunkProcessor) ProcessNextChunk(IColumnSource[] sources, int[] begins, int[] ends, byte[] metadata) {
    if (_firstTime) {
      _firstTime = false;

      if (Checks.AllEmpty(perColumnModifies)) {
        var afterModifies = afterAdds;
        var nextState = new BuildingResult(numCols, tableState, prev, removedRowsIndexSpace, afterRemoves,
          addedRowsIndexSpace, afterAdds, _modifiedRowsIndexSpace, afterModifies);
        return nextState.ProcessNextChunk(sources, begins, ends, metadata);
      }

      _modifiedRowsIndexSpace = new RowSequence[numCols];
      _modifiedRowsRemaining = new RowSequence[numCols];
      for (var i = 0; i < numCols; ++i) {
        var rs = tableState.ConvertKeysToIndices(perColumnModifies[i]);
        _modifiedRowsIndexSpace[i] = rs;
        _modifiedRowsRemaining[i] = rs;
      }
    }

    if (Checks.AllEmpty(_modifiedRowsRemaining)) {
      throw new Exception("Impossible: modifiedRowsRemaining is empty");
    }

    if (begins.Equals(ends)) {
      // Need more data from caller.
      return (null, this);
    }

    var numSources = sources.Length;
    if (numSources > _modifiedRowsRemaining.Length) {
      throw new Exception($"Number of sources ({numSources}) greater than expected ({_modifiedRowsRemaining.Length})");
    }

    for (var i = 0; i < numSources; ++i) {
      var numRowsRemaining = _modifiedRowsRemaining[i].Size;
      var numRowsAvailable = (UInt64)(ends[i] - begins[i]);

      if (numRowsAvailable > numRowsRemaining) {
        throw new Exception($"col {i}: numRowsAvailable ({numRowsAvailable}) > numRowsRemaining ({numRowsRemaining})");
      }

      if (numRowsAvailable == 0) {
        // Nothing available for this column. Advance to next column.
        continue;
      }

      var mr = _modifiedRowsRemaining[i];
      var rowsAvailable = mr.Take(numRowsAvailable);
      _modifiedRowsRemaining[i] = mr.Drop(numRowsAvailable);

      tableState.ModifyData(i, sources[i], begins[i], ends[i], rowsAvailable);
      begins[i] = ends[i];
    }

    foreach (var mr in _modifiedRowsRemaining) {
      if (!mr.Empty) {
        // At least one of our colums is hungry for more data that we don't have.
        return (null, this);
      }
    }

    {
      var afterModifies = tableState.Snapshot();
      var nextState = new BuildingResult(numCols, tableState, prev, removedRowsIndexSpace, afterRemoves,
        addedRowsIndexSpace, afterAdds, _modifiedRowsIndexSpace, afterModifies);
      return nextState.ProcessNextChunk(sources, begins, ends, metadata);
    }
  }
}

class BuildingResult(
  int numCols,
  TableState tableState,
  ClientTable prev,
  RowSequence removedRowsIndexSpace,
  ClientTable afterRemoves,
  RowSequence addedRowsIndexSpace,
  ClientTable afterAdds,
  RowSequence[] modifiedRowsIndexSpace,
  ClientTable afterModifies) : IChunkProcessor {

  public (TickingUpdate?, IChunkProcessor) ProcessNextChunk(IColumnSource[] sources, int[] begins, int[] ends, byte[] metadata) {
    if (!begins.Equals(ends)) {
      throw new Exception(
        $"Barrage logic is done processing but there is leftover caller-provided data. begins = [{string.Join(",", begins)}]. ends=[{string.Join(",", ends)}]");
    }

    var result = new TickingUpdate(prev,
      removedRowsIndexSpace, afterRemoves,
      addedRowsIndexSpace, afterAdds,
      modifiedRowsIndexSpace, afterModifies);
    var nextState = new AwaitingMetadata(numCols, tableState);
    return (result, nextState);
  }
}

static class Checks {
  public static bool AllEmpty(RowSequence[] rowSequences) {
    return rowSequences.All(rs => rs.Empty);
  }

  public static void AssertAllSame(int val0, int val1, int val2) {
    if (val0 != val1 || val0 != val2) {
      throw new Exception($"Sizes differ: {val0} vs {val1} vs {val2}");
    }
  }
}

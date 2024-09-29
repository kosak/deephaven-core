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

    var nextState = new AwaitingAdds(perColumnModifies, prev, removedRowsIndexSpace, afterRemoves, addedRowsIndexSpace);
    return nextState.ProcessNextChunk(sources, begins, ends, Array.Empty<byte>());
  }

  (ClientTable, RowSequence, ClientTable) ProcessRemoves(RowSequence removedRows) {
    auto prev = table_state_.Snapshot();
    // The reason we special-case "empty" is because when the tables are unchanged, we prefer
    // to indicate this via pointer equality (e.g. beforeRemoves == afterRemoves).
    std::shared_ptr<RowSequence> removed_rows_index_space;
    std::shared_ptr<ClientTable> after_removes;
    if (removed_rows.Empty()) {
      removed_rows_index_space = RowSequence::CreateEmpty();
      after_removes = prev;
    } else {
      removed_rows_index_space = table_state_.Erase(removed_rows);
      after_removes = table_state_.Snapshot();
    }
    return { std::move(prev), std::move(removed_rows_index_space), std::move(after_removes)};
  }
}
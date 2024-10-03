// <auto-generated>
//  automatically generated by the FlatBuffers compiler, do not modify
// </auto-generated>

namespace io.deephaven.barrage.flatbuf
{

using global::System;
using global::System.Collections.Generic;
using global::Google.FlatBuffers;

/// A data header describing the shared memory layout of a "record" or "row"
/// batch for a ticking barrage table.
public struct BarrageUpdateMetadata : IFlatbufferObject
{
  private Table __p;
  public ByteBuffer ByteBuffer { get { return __p.bb; } }
  public static void ValidateVersion() { FlatBufferConstants.FLATBUFFERS_23_5_26(); }
  public static BarrageUpdateMetadata GetRootAsBarrageUpdateMetadata(ByteBuffer _bb) { return GetRootAsBarrageUpdateMetadata(_bb, new BarrageUpdateMetadata()); }
  public static BarrageUpdateMetadata GetRootAsBarrageUpdateMetadata(ByteBuffer _bb, BarrageUpdateMetadata obj) { return (obj.__assign(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public void __init(int _i, ByteBuffer _bb) { __p = new Table(_i, _bb); }
  public BarrageUpdateMetadata __assign(int _i, ByteBuffer _bb) { __init(_i, _bb); return this; }

  /// This batch is generated from an upstream table that ticks independently of the stream. If
  /// multiple events are coalesced into one update, the server may communicate that here for
  /// informational purposes.
  public long FirstSeq { get { int o = __p.__offset(4); return o != 0 ? __p.bb.GetLong(o + __p.bb_pos) : (long)0; } }
  public long LastSeq { get { int o = __p.__offset(6); return o != 0 ? __p.bb.GetLong(o + __p.bb_pos) : (long)0; } }
  /// Indicates if this message was sent due to upstream ticks or due to a subscription change.
  public bool IsSnapshot { get { int o = __p.__offset(8); return o != 0 ? 0!=__p.bb.Get(o + __p.bb_pos) : (bool)false; } }
  /// If this is a snapshot and the subscription is a viewport, then the effectively subscribed viewport
  /// will be included in the payload. It is an encoded and compressed RowSet.
  public sbyte EffectiveViewport(int j) { int o = __p.__offset(10); return o != 0 ? __p.bb.GetSbyte(__p.__vector(o) + j * 1) : (sbyte)0; }
  public int EffectiveViewportLength { get { int o = __p.__offset(10); return o != 0 ? __p.__vector_len(o) : 0; } }
#if ENABLE_SPAN_T
  public Span<sbyte> GetEffectiveViewportBytes() { return __p.__vector_as_span<sbyte>(10, 1); }
#else
  public ArraySegment<byte>? GetEffectiveViewportBytes() { return __p.__vector_as_arraysegment(10); }
#endif
  public sbyte[] GetEffectiveViewportArray() { return __p.__vector_as_array<sbyte>(10); }
  /// When this is set the viewport RowSet will be inverted against the length of the table. That is to say
  /// every position value is converted from `i` to `n - i - 1` if the table has `n` rows.
  public bool EffectiveReverseViewport { get { int o = __p.__offset(12); return o != 0 ? 0!=__p.bb.Get(o + __p.bb_pos) : (bool)false; } }
  /// If this is a snapshot, then the effectively subscribed column set will be included in the payload.
  public sbyte EffectiveColumnSet(int j) { int o = __p.__offset(14); return o != 0 ? __p.bb.GetSbyte(__p.__vector(o) + j * 1) : (sbyte)0; }
  public int EffectiveColumnSetLength { get { int o = __p.__offset(14); return o != 0 ? __p.__vector_len(o) : 0; } }
#if ENABLE_SPAN_T
  public Span<sbyte> GetEffectiveColumnSetBytes() { return __p.__vector_as_span<sbyte>(14, 1); }
#else
  public ArraySegment<byte>? GetEffectiveColumnSetBytes() { return __p.__vector_as_arraysegment(14); }
#endif
  public sbyte[] GetEffectiveColumnSetArray() { return __p.__vector_as_array<sbyte>(14); }
  /// This is an encoded and compressed RowSet that was added in this update.
  public sbyte AddedRows(int j) { int o = __p.__offset(16); return o != 0 ? __p.bb.GetSbyte(__p.__vector(o) + j * 1) : (sbyte)0; }
  public int AddedRowsLength { get { int o = __p.__offset(16); return o != 0 ? __p.__vector_len(o) : 0; } }
#if ENABLE_SPAN_T
  public Span<sbyte> GetAddedRowsBytes() { return __p.__vector_as_span<sbyte>(16, 1); }
#else
  public ArraySegment<byte>? GetAddedRowsBytes() { return __p.__vector_as_arraysegment(16); }
#endif
  public sbyte[] GetAddedRowsArray() { return __p.__vector_as_array<sbyte>(16); }
  /// This is an encoded and compressed RowSet that was removed in this update.
  public sbyte RemovedRows(int j) { int o = __p.__offset(18); return o != 0 ? __p.bb.GetSbyte(__p.__vector(o) + j * 1) : (sbyte)0; }
  public int RemovedRowsLength { get { int o = __p.__offset(18); return o != 0 ? __p.__vector_len(o) : 0; } }
#if ENABLE_SPAN_T
  public Span<sbyte> GetRemovedRowsBytes() { return __p.__vector_as_span<sbyte>(18, 1); }
#else
  public ArraySegment<byte>? GetRemovedRowsBytes() { return __p.__vector_as_arraysegment(18); }
#endif
  public sbyte[] GetRemovedRowsArray() { return __p.__vector_as_array<sbyte>(18); }
  /// This is an encoded and compressed RowSetShiftData describing how the keyspace of unmodified rows changed.
  public sbyte ShiftData(int j) { int o = __p.__offset(20); return o != 0 ? __p.bb.GetSbyte(__p.__vector(o) + j * 1) : (sbyte)0; }
  public int ShiftDataLength { get { int o = __p.__offset(20); return o != 0 ? __p.__vector_len(o) : 0; } }
#if ENABLE_SPAN_T
  public Span<sbyte> GetShiftDataBytes() { return __p.__vector_as_span<sbyte>(20, 1); }
#else
  public ArraySegment<byte>? GetShiftDataBytes() { return __p.__vector_as_arraysegment(20); }
#endif
  public sbyte[] GetShiftDataArray() { return __p.__vector_as_array<sbyte>(20); }
  /// This is an encoded and compressed RowSet that was included with this update.
  /// (the server may include rows not in addedRows if this is a viewport subscription to refresh
  ///  unmodified rows that were scoped into view)
  public sbyte AddedRowsIncluded(int j) { int o = __p.__offset(22); return o != 0 ? __p.bb.GetSbyte(__p.__vector(o) + j * 1) : (sbyte)0; }
  public int AddedRowsIncludedLength { get { int o = __p.__offset(22); return o != 0 ? __p.__vector_len(o) : 0; } }
#if ENABLE_SPAN_T
  public Span<sbyte> GetAddedRowsIncludedBytes() { return __p.__vector_as_span<sbyte>(22, 1); }
#else
  public ArraySegment<byte>? GetAddedRowsIncludedBytes() { return __p.__vector_as_arraysegment(22); }
#endif
  public sbyte[] GetAddedRowsIncludedArray() { return __p.__vector_as_array<sbyte>(22); }
  /// The list of modified column data are in the same order as the field nodes on the schema.
  public io.deephaven.barrage.flatbuf.BarrageModColumnMetadata? ModColumnNodes(int j) { int o = __p.__offset(24); return o != 0 ? (io.deephaven.barrage.flatbuf.BarrageModColumnMetadata?)(new io.deephaven.barrage.flatbuf.BarrageModColumnMetadata()).__assign(__p.__indirect(__p.__vector(o) + j * 4), __p.bb) : null; }
  public int ModColumnNodesLength { get { int o = __p.__offset(24); return o != 0 ? __p.__vector_len(o) : 0; } }

  public static Offset<io.deephaven.barrage.flatbuf.BarrageUpdateMetadata> CreateBarrageUpdateMetadata(FlatBufferBuilder builder,
      long first_seq = 0,
      long last_seq = 0,
      bool is_snapshot = false,
      VectorOffset effective_viewportOffset = default(VectorOffset),
      bool effective_reverse_viewport = false,
      VectorOffset effective_column_setOffset = default(VectorOffset),
      VectorOffset added_rowsOffset = default(VectorOffset),
      VectorOffset removed_rowsOffset = default(VectorOffset),
      VectorOffset shift_dataOffset = default(VectorOffset),
      VectorOffset added_rows_includedOffset = default(VectorOffset),
      VectorOffset mod_column_nodesOffset = default(VectorOffset)) {
    builder.StartTable(11);
    BarrageUpdateMetadata.AddLastSeq(builder, last_seq);
    BarrageUpdateMetadata.AddFirstSeq(builder, first_seq);
    BarrageUpdateMetadata.AddModColumnNodes(builder, mod_column_nodesOffset);
    BarrageUpdateMetadata.AddAddedRowsIncluded(builder, added_rows_includedOffset);
    BarrageUpdateMetadata.AddShiftData(builder, shift_dataOffset);
    BarrageUpdateMetadata.AddRemovedRows(builder, removed_rowsOffset);
    BarrageUpdateMetadata.AddAddedRows(builder, added_rowsOffset);
    BarrageUpdateMetadata.AddEffectiveColumnSet(builder, effective_column_setOffset);
    BarrageUpdateMetadata.AddEffectiveViewport(builder, effective_viewportOffset);
    BarrageUpdateMetadata.AddEffectiveReverseViewport(builder, effective_reverse_viewport);
    BarrageUpdateMetadata.AddIsSnapshot(builder, is_snapshot);
    return BarrageUpdateMetadata.EndBarrageUpdateMetadata(builder);
  }

  public static void StartBarrageUpdateMetadata(FlatBufferBuilder builder) { builder.StartTable(11); }
  public static void AddFirstSeq(FlatBufferBuilder builder, long firstSeq) { builder.AddLong(0, firstSeq, 0); }
  public static void AddLastSeq(FlatBufferBuilder builder, long lastSeq) { builder.AddLong(1, lastSeq, 0); }
  public static void AddIsSnapshot(FlatBufferBuilder builder, bool isSnapshot) { builder.AddBool(2, isSnapshot, false); }
  public static void AddEffectiveViewport(FlatBufferBuilder builder, VectorOffset effectiveViewportOffset) { builder.AddOffset(3, effectiveViewportOffset.Value, 0); }
  public static VectorOffset CreateEffectiveViewportVector(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); for (int i = data.Length - 1; i >= 0; i--) builder.AddSbyte(data[i]); return builder.EndVector(); }
  public static VectorOffset CreateEffectiveViewportVectorBlock(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateEffectiveViewportVectorBlock(FlatBufferBuilder builder, ArraySegment<sbyte> data) { builder.StartVector(1, data.Count, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateEffectiveViewportVectorBlock(FlatBufferBuilder builder, IntPtr dataPtr, int sizeInBytes) { builder.StartVector(1, sizeInBytes, 1); builder.Add<sbyte>(dataPtr, sizeInBytes); return builder.EndVector(); }
  public static void StartEffectiveViewportVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(1, numElems, 1); }
  public static void AddEffectiveReverseViewport(FlatBufferBuilder builder, bool effectiveReverseViewport) { builder.AddBool(4, effectiveReverseViewport, false); }
  public static void AddEffectiveColumnSet(FlatBufferBuilder builder, VectorOffset effectiveColumnSetOffset) { builder.AddOffset(5, effectiveColumnSetOffset.Value, 0); }
  public static VectorOffset CreateEffectiveColumnSetVector(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); for (int i = data.Length - 1; i >= 0; i--) builder.AddSbyte(data[i]); return builder.EndVector(); }
  public static VectorOffset CreateEffectiveColumnSetVectorBlock(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateEffectiveColumnSetVectorBlock(FlatBufferBuilder builder, ArraySegment<sbyte> data) { builder.StartVector(1, data.Count, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateEffectiveColumnSetVectorBlock(FlatBufferBuilder builder, IntPtr dataPtr, int sizeInBytes) { builder.StartVector(1, sizeInBytes, 1); builder.Add<sbyte>(dataPtr, sizeInBytes); return builder.EndVector(); }
  public static void StartEffectiveColumnSetVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(1, numElems, 1); }
  public static void AddAddedRows(FlatBufferBuilder builder, VectorOffset addedRowsOffset) { builder.AddOffset(6, addedRowsOffset.Value, 0); }
  public static VectorOffset CreateAddedRowsVector(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); for (int i = data.Length - 1; i >= 0; i--) builder.AddSbyte(data[i]); return builder.EndVector(); }
  public static VectorOffset CreateAddedRowsVectorBlock(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateAddedRowsVectorBlock(FlatBufferBuilder builder, ArraySegment<sbyte> data) { builder.StartVector(1, data.Count, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateAddedRowsVectorBlock(FlatBufferBuilder builder, IntPtr dataPtr, int sizeInBytes) { builder.StartVector(1, sizeInBytes, 1); builder.Add<sbyte>(dataPtr, sizeInBytes); return builder.EndVector(); }
  public static void StartAddedRowsVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(1, numElems, 1); }
  public static void AddRemovedRows(FlatBufferBuilder builder, VectorOffset removedRowsOffset) { builder.AddOffset(7, removedRowsOffset.Value, 0); }
  public static VectorOffset CreateRemovedRowsVector(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); for (int i = data.Length - 1; i >= 0; i--) builder.AddSbyte(data[i]); return builder.EndVector(); }
  public static VectorOffset CreateRemovedRowsVectorBlock(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateRemovedRowsVectorBlock(FlatBufferBuilder builder, ArraySegment<sbyte> data) { builder.StartVector(1, data.Count, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateRemovedRowsVectorBlock(FlatBufferBuilder builder, IntPtr dataPtr, int sizeInBytes) { builder.StartVector(1, sizeInBytes, 1); builder.Add<sbyte>(dataPtr, sizeInBytes); return builder.EndVector(); }
  public static void StartRemovedRowsVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(1, numElems, 1); }
  public static void AddShiftData(FlatBufferBuilder builder, VectorOffset shiftDataOffset) { builder.AddOffset(8, shiftDataOffset.Value, 0); }
  public static VectorOffset CreateShiftDataVector(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); for (int i = data.Length - 1; i >= 0; i--) builder.AddSbyte(data[i]); return builder.EndVector(); }
  public static VectorOffset CreateShiftDataVectorBlock(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateShiftDataVectorBlock(FlatBufferBuilder builder, ArraySegment<sbyte> data) { builder.StartVector(1, data.Count, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateShiftDataVectorBlock(FlatBufferBuilder builder, IntPtr dataPtr, int sizeInBytes) { builder.StartVector(1, sizeInBytes, 1); builder.Add<sbyte>(dataPtr, sizeInBytes); return builder.EndVector(); }
  public static void StartShiftDataVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(1, numElems, 1); }
  public static void AddAddedRowsIncluded(FlatBufferBuilder builder, VectorOffset addedRowsIncludedOffset) { builder.AddOffset(9, addedRowsIncludedOffset.Value, 0); }
  public static VectorOffset CreateAddedRowsIncludedVector(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); for (int i = data.Length - 1; i >= 0; i--) builder.AddSbyte(data[i]); return builder.EndVector(); }
  public static VectorOffset CreateAddedRowsIncludedVectorBlock(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateAddedRowsIncludedVectorBlock(FlatBufferBuilder builder, ArraySegment<sbyte> data) { builder.StartVector(1, data.Count, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateAddedRowsIncludedVectorBlock(FlatBufferBuilder builder, IntPtr dataPtr, int sizeInBytes) { builder.StartVector(1, sizeInBytes, 1); builder.Add<sbyte>(dataPtr, sizeInBytes); return builder.EndVector(); }
  public static void StartAddedRowsIncludedVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(1, numElems, 1); }
  public static void AddModColumnNodes(FlatBufferBuilder builder, VectorOffset modColumnNodesOffset) { builder.AddOffset(10, modColumnNodesOffset.Value, 0); }
  public static VectorOffset CreateModColumnNodesVector(FlatBufferBuilder builder, Offset<io.deephaven.barrage.flatbuf.BarrageModColumnMetadata>[] data) { builder.StartVector(4, data.Length, 4); for (int i = data.Length - 1; i >= 0; i--) builder.AddOffset(data[i].Value); return builder.EndVector(); }
  public static VectorOffset CreateModColumnNodesVectorBlock(FlatBufferBuilder builder, Offset<io.deephaven.barrage.flatbuf.BarrageModColumnMetadata>[] data) { builder.StartVector(4, data.Length, 4); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateModColumnNodesVectorBlock(FlatBufferBuilder builder, ArraySegment<Offset<io.deephaven.barrage.flatbuf.BarrageModColumnMetadata>> data) { builder.StartVector(4, data.Count, 4); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateModColumnNodesVectorBlock(FlatBufferBuilder builder, IntPtr dataPtr, int sizeInBytes) { builder.StartVector(1, sizeInBytes, 1); builder.Add<Offset<io.deephaven.barrage.flatbuf.BarrageModColumnMetadata>>(dataPtr, sizeInBytes); return builder.EndVector(); }
  public static void StartModColumnNodesVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(4, numElems, 4); }
  public static Offset<io.deephaven.barrage.flatbuf.BarrageUpdateMetadata> EndBarrageUpdateMetadata(FlatBufferBuilder builder) {
    int o = builder.EndTable();
    return new Offset<io.deephaven.barrage.flatbuf.BarrageUpdateMetadata>(o);
  }
}


static public class BarrageUpdateMetadataVerify
{
  static public bool Verify(Google.FlatBuffers.Verifier verifier, uint tablePos)
  {
    return verifier.VerifyTableStart(tablePos)
      && verifier.VerifyField(tablePos, 4 /*FirstSeq*/, 8 /*long*/, 8, false)
      && verifier.VerifyField(tablePos, 6 /*LastSeq*/, 8 /*long*/, 8, false)
      && verifier.VerifyField(tablePos, 8 /*IsSnapshot*/, 1 /*bool*/, 1, false)
      && verifier.VerifyVectorOfData(tablePos, 10 /*EffectiveViewport*/, 1 /*sbyte*/, false)
      && verifier.VerifyField(tablePos, 12 /*EffectiveReverseViewport*/, 1 /*bool*/, 1, false)
      && verifier.VerifyVectorOfData(tablePos, 14 /*EffectiveColumnSet*/, 1 /*sbyte*/, false)
      && verifier.VerifyVectorOfData(tablePos, 16 /*AddedRows*/, 1 /*sbyte*/, false)
      && verifier.VerifyVectorOfData(tablePos, 18 /*RemovedRows*/, 1 /*sbyte*/, false)
      && verifier.VerifyVectorOfData(tablePos, 20 /*ShiftData*/, 1 /*sbyte*/, false)
      && verifier.VerifyVectorOfData(tablePos, 22 /*AddedRowsIncluded*/, 1 /*sbyte*/, false)
      && verifier.VerifyVectorOfTables(tablePos, 24 /*ModColumnNodes*/, io.deephaven.barrage.flatbuf.BarrageModColumnMetadataVerify.Verify, false)
      && verifier.VerifyTableEnd(tablePos);
  }
}

}
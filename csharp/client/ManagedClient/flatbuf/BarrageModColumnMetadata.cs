// <auto-generated>
//  automatically generated by the FlatBuffers compiler, do not modify
// </auto-generated>

namespace io.deephaven.barrage.flatbuf
{

using global::System;
using global::System.Collections.Generic;
using global::Google.FlatBuffers;

/// Holds all of the rowset data structures for the column being modified.
public struct BarrageModColumnMetadata : IFlatbufferObject
{
  private Table __p;
  public ByteBuffer ByteBuffer { get { return __p.bb; } }
  public static void ValidateVersion() { FlatBufferConstants.FLATBUFFERS_23_5_26(); }
  public static BarrageModColumnMetadata GetRootAsBarrageModColumnMetadata(ByteBuffer _bb) { return GetRootAsBarrageModColumnMetadata(_bb, new BarrageModColumnMetadata()); }
  public static BarrageModColumnMetadata GetRootAsBarrageModColumnMetadata(ByteBuffer _bb, BarrageModColumnMetadata obj) { return (obj.__assign(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public void __init(int _i, ByteBuffer _bb) { __p = new Table(_i, _bb); }
  public BarrageModColumnMetadata __assign(int _i, ByteBuffer _bb) { __init(_i, _bb); return this; }

  /// This is an encoded and compressed RowSet for this column (within the viewport) that were modified.
  /// There is no notification for modifications outside of the viewport.
  public sbyte ModifiedRows(int j) { int o = __p.__offset(4); return o != 0 ? __p.bb.GetSbyte(__p.__vector(o) + j * 1) : (sbyte)0; }
  public int ModifiedRowsLength { get { int o = __p.__offset(4); return o != 0 ? __p.__vector_len(o) : 0; } }
#if ENABLE_SPAN_T
  public Span<sbyte> GetModifiedRowsBytes() { return __p.__vector_as_span<sbyte>(4, 1); }
#else
  public ArraySegment<byte>? GetModifiedRowsBytes() { return __p.__vector_as_arraysegment(4); }
#endif
  public sbyte[] GetModifiedRowsArray() { return __p.__vector_as_array<sbyte>(4); }

  public static Offset<io.deephaven.barrage.flatbuf.BarrageModColumnMetadata> CreateBarrageModColumnMetadata(FlatBufferBuilder builder,
      VectorOffset modified_rowsOffset = default(VectorOffset)) {
    builder.StartTable(1);
    BarrageModColumnMetadata.AddModifiedRows(builder, modified_rowsOffset);
    return BarrageModColumnMetadata.EndBarrageModColumnMetadata(builder);
  }

  public static void StartBarrageModColumnMetadata(FlatBufferBuilder builder) { builder.StartTable(1); }
  public static void AddModifiedRows(FlatBufferBuilder builder, VectorOffset modifiedRowsOffset) { builder.AddOffset(0, modifiedRowsOffset.Value, 0); }
  public static VectorOffset CreateModifiedRowsVector(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); for (int i = data.Length - 1; i >= 0; i--) builder.AddSbyte(data[i]); return builder.EndVector(); }
  public static VectorOffset CreateModifiedRowsVectorBlock(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateModifiedRowsVectorBlock(FlatBufferBuilder builder, ArraySegment<sbyte> data) { builder.StartVector(1, data.Count, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreateModifiedRowsVectorBlock(FlatBufferBuilder builder, IntPtr dataPtr, int sizeInBytes) { builder.StartVector(1, sizeInBytes, 1); builder.Add<sbyte>(dataPtr, sizeInBytes); return builder.EndVector(); }
  public static void StartModifiedRowsVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(1, numElems, 1); }
  public static Offset<io.deephaven.barrage.flatbuf.BarrageModColumnMetadata> EndBarrageModColumnMetadata(FlatBufferBuilder builder) {
    int o = builder.EndTable();
    return new Offset<io.deephaven.barrage.flatbuf.BarrageModColumnMetadata>(o);
  }
}


static public class BarrageModColumnMetadataVerify
{
  static public bool Verify(Google.FlatBuffers.Verifier verifier, uint tablePos)
  {
    return verifier.VerifyTableStart(tablePos)
      && verifier.VerifyVectorOfData(tablePos, 4 /*ModifiedRows*/, 1 /*sbyte*/, false)
      && verifier.VerifyTableEnd(tablePos);
  }
}

}
// <auto-generated>
//  automatically generated by the FlatBuffers compiler, do not modify
// </auto-generated>

namespace io.deephaven.barrage.flatbuf
{

using global::System;
using global::System.Collections.Generic;
using global::Google.FlatBuffers;

/// Establish a new session.
public struct NewSessionRequest : IFlatbufferObject
{
  private Table __p;
  public ByteBuffer ByteBuffer { get { return __p.bb; } }
  public static void ValidateVersion() { FlatBufferConstants.FLATBUFFERS_23_5_26(); }
  public static NewSessionRequest GetRootAsNewSessionRequest(ByteBuffer _bb) { return GetRootAsNewSessionRequest(_bb, new NewSessionRequest()); }
  public static NewSessionRequest GetRootAsNewSessionRequest(ByteBuffer _bb, NewSessionRequest obj) { return (obj.__assign(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public void __init(int _i, ByteBuffer _bb) { __p = new Table(_i, _bb); }
  public NewSessionRequest __assign(int _i, ByteBuffer _bb) { __init(_i, _bb); return this; }

  /// A nested protocol version (gets delegated to handshake)
  public uint ProtocolVersion { get { int o = __p.__offset(4); return o != 0 ? __p.bb.GetUint(o + __p.bb_pos) : (uint)0; } }
  /// Arbitrary auth/handshake info.
  public sbyte Payload(int j) { int o = __p.__offset(6); return o != 0 ? __p.bb.GetSbyte(__p.__vector(o) + j * 1) : (sbyte)0; }
  public int PayloadLength { get { int o = __p.__offset(6); return o != 0 ? __p.__vector_len(o) : 0; } }
#if ENABLE_SPAN_T
  public Span<sbyte> GetPayloadBytes() { return __p.__vector_as_span<sbyte>(6, 1); }
#else
  public ArraySegment<byte>? GetPayloadBytes() { return __p.__vector_as_arraysegment(6); }
#endif
  public sbyte[] GetPayloadArray() { return __p.__vector_as_array<sbyte>(6); }

  public static Offset<io.deephaven.barrage.flatbuf.NewSessionRequest> CreateNewSessionRequest(FlatBufferBuilder builder,
      uint protocol_version = 0,
      VectorOffset payloadOffset = default(VectorOffset)) {
    builder.StartTable(2);
    NewSessionRequest.AddPayload(builder, payloadOffset);
    NewSessionRequest.AddProtocolVersion(builder, protocol_version);
    return NewSessionRequest.EndNewSessionRequest(builder);
  }

  public static void StartNewSessionRequest(FlatBufferBuilder builder) { builder.StartTable(2); }
  public static void AddProtocolVersion(FlatBufferBuilder builder, uint protocolVersion) { builder.AddUint(0, protocolVersion, 0); }
  public static void AddPayload(FlatBufferBuilder builder, VectorOffset payloadOffset) { builder.AddOffset(1, payloadOffset.Value, 0); }
  public static VectorOffset CreatePayloadVector(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); for (int i = data.Length - 1; i >= 0; i--) builder.AddSbyte(data[i]); return builder.EndVector(); }
  public static VectorOffset CreatePayloadVectorBlock(FlatBufferBuilder builder, sbyte[] data) { builder.StartVector(1, data.Length, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreatePayloadVectorBlock(FlatBufferBuilder builder, ArraySegment<sbyte> data) { builder.StartVector(1, data.Count, 1); builder.Add(data); return builder.EndVector(); }
  public static VectorOffset CreatePayloadVectorBlock(FlatBufferBuilder builder, IntPtr dataPtr, int sizeInBytes) { builder.StartVector(1, sizeInBytes, 1); builder.Add<sbyte>(dataPtr, sizeInBytes); return builder.EndVector(); }
  public static void StartPayloadVector(FlatBufferBuilder builder, int numElems) { builder.StartVector(1, numElems, 1); }
  public static Offset<io.deephaven.barrage.flatbuf.NewSessionRequest> EndNewSessionRequest(FlatBufferBuilder builder) {
    int o = builder.EndTable();
    return new Offset<io.deephaven.barrage.flatbuf.NewSessionRequest>(o);
  }
}


static public class NewSessionRequestVerify
{
  static public bool Verify(Google.FlatBuffers.Verifier verifier, uint tablePos)
  {
    return verifier.VerifyTableStart(tablePos)
      && verifier.VerifyField(tablePos, 4 /*ProtocolVersion*/, 4 /*uint*/, 4, false)
      && verifier.VerifyVectorOfData(tablePos, 6 /*Payload*/, 1 /*sbyte*/, false)
      && verifier.VerifyTableEnd(tablePos);
  }
}

}
// <auto-generated>
//  automatically generated by the FlatBuffers compiler, do not modify
// </auto-generated>

namespace io.deephaven.barrage.flatbuf
{

public enum BarrageMessageType : sbyte
{
  /// A barrage message wrapper might send a None message type
  /// if the msg_payload is empty.
  None = 0,
  /// for session management (not-yet-used)
  NewSessionRequest = 1,
  RefreshSessionRequest = 2,
  SessionInfoResponse = 3,
  /// for subscription parsing/management (aka DoPut, DoExchange)
  BarrageSerializationOptions = 4,
  BarrageSubscriptionRequest = 5,
  BarrageUpdateMetadata = 6,
  BarrageSnapshotRequest = 7,
  BarragePublicationRequest = 8,
};


}

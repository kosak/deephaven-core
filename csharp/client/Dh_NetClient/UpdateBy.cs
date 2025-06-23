using Io.Deephaven.Proto.Backplane.Grpc;

namespace Deephaven.Dh_NetClient;

public class UpdateByOperation {
  public readonly UpdateByRequest.Types.UpdateByOperation UpdateByProto;

  public UpdateByOperation(UpdateByRequest.Types.UpdateByOperation updateByProto) {
    UpdateByProto = updateByProto;
  }
}

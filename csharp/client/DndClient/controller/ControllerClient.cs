using Deephaven.ManagedClient;
using Grpc.Net.Client;

namespace DeephavenEnterprise.DndClient;

public class ControllerClient : IDisposable {
  public static ControllerClient Connect(string descriptiveName, string target, ClientOptions? options = null) {
    var channel = ClientUtil.CreateChannel("AuthClient::Connect: ClientOptions", target, options);
    return new ControllerClient(descriptiveName, channel);
  }

  private readonly string _descriptiveName;
  private readonly GrpcChannel _channel;

  public ControllerClient(string descriptiveName, GrpcChannel channel) {
    _descriptiveName = descriptiveName;
    _channel = channel;
  }

  public void Dispose() {
    throw new NotImplementedException();
  }
}

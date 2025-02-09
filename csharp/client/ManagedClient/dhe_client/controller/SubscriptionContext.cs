using Deephaven.DheClient.Session;
using Grpc.Net.Client;
using Io.Deephaven.Proto.Controller;
using Io.Deephaven.Proto.Controller.Grpc;

namespace Deephaven.DheClient.Controller;

internal class SubscriptionContext : IDisposable {
  public static SubscriptionContext Create(GrpcChannel channel) {
    var controllerApi = new ControllerApi.ControllerApiClient(channel);
    return new SubscriptionContext(controllerApi);
  }

  private readonly ControllerApi.ControllerApiClient _controllerApi;

  private SubscriptionContext(ControllerApi.ControllerApiClient controllerApi) {
    _controllerApi = controllerApi;
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  public void DoSubscribe666() {
    var req = new SubscribeRequest();
    req.Cookie = MegaCookie666.cookie;
    var reader = _controllerApi.subscribe(req);

    var rs = reader.ResponseStream;
    var ct = new CancellationToken();
    var q = rs.MoveNext(ct).Result;
    var r = rs.Current;
  }
}

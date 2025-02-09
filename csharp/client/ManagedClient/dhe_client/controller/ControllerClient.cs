using Deephaven.DheClient.Auth;
using Deephaven.ManagedClient;
using Grpc.Core;
using Io.Deephaven.Proto.Auth;
using Io.Deephaven.Proto.Controller;
using Io.Deephaven.Proto.Controller.Grpc;

namespace Deephaven.DheClient.Controller;

public class ControllerClient {
  public const string ControllerServiceName = "PersistentQueryController";

  public static ControllerClient Connect(string descriptiveName, string target,
    ClientOptions options) {
    var channel = GrpcUtil.CreateChannel(target, options);
    // where does this go: arguments and also credentials
    // grpc::ChannelArguments channel_args;
    // for (const auto &opt : options.IntOptions()) {
    //   channel_args.SetInt(opt.first, opt.second);
    // }
    // for (const auto &opt : options.StringOptions()) {
    //   channel_args.SetString(opt.first, opt.second);
    // }

    var controllerApi = new ControllerApi.ControllerApiClient(channel);
    var co = new CallOptions();
    var req = new PingRequest();
    _ = controllerApi.ping(req, co);

    var clientId = ClientUtil.MakeClientId(descriptiveName, Guid.NewGuid().ToString());

    return new ControllerClient(clientId, controllerApi);
  }

  private readonly ClientId _clientId;
  private readonly ControllerApi.ControllerApiClient _controllerApi;

  public ControllerClient(ClientId clientId, ControllerApi.ControllerApiClient controllerApi) {
    _clientId = clientId;
    _controllerApi = controllerApi;
  }

  public bool Authenticate(AuthToken authToken) {
    var req = new AuthenticationRequest();
    req.ClientId = _clientId;
    req.Token = AuthUtil.AuthTokenToProto(authToken);
    req.GetConfiguration = true;
    var resp = _controllerApi.authenticate(req);
    return resp.Authenticated;
  }
}

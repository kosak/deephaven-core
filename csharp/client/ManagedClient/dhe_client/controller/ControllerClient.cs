using Deephaven.DheClient.Auth;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Auth;
using Io.Deephaven.Proto.Controller;
using Io.Deephaven.Proto.Controller.Grpc;

namespace Deephaven.DheClient.Controller;

public class ControllerClient : IDisposable {
  private const string ControllerServiceName = "PersistentQueryController";

  public static ControllerClient Create(string descriptiveName, string target,
    ClientOptions options, AuthClient authClient) {

    // Get an auth token for me from the AuthClient
    var authToken = authClient.CreateToken(ControllerServiceName);

    var channel = GrpcUtil.CreateChannel(target, options);
    // where does this go: arguments and also credentials
    // grpc::ChannelArguments channel_args;
    // for (const auto &opt : options.IntOptions()) {
    //   channel_args.SetInt(opt.first, opt.second);
    // }
    // for (const auto &opt : options.StringOptions()) {
    //   channel_args.SetString(opt.first, opt.second);
    // }

    // Create the stub
    var controllerApi = new ControllerApi.ControllerApiClient(channel);

    // Authenticate to the controller
    var clientId = ClientUtil.MakeClientId(descriptiveName, Guid.NewGuid().ToString());

    var authReq = new AuthenticationRequest {
      ClientId = clientId,
      Token = AuthUtil.AuthTokenToProto(authToken),
      GetConfiguration = true
    };
    var authResp = controllerApi.authenticate(authReq);
    if (!authResp.Authenticated) {
      throw new Exception("Failed to authenticate to the Controller");
    }

    var authCookie = authResp.Cookie.ToByteArray();
    var subscriptionContext = SubscriptionContext.Create(channel, authCookie);
    return new ControllerClient(clientId, controllerApi, subscriptionContext);
  }

  private readonly ClientId _clientId;
  private readonly ControllerApi.ControllerApiClient _controllerApi;
  private readonly SubscriptionContext _subscriptionContext;

  private ControllerClient(ClientId clientId, ControllerApi.ControllerApiClient controllerApi,
    SubscriptionContext subscriptionContext) {
    _clientId = clientId;
    _controllerApi = controllerApi;
    _subscriptionContext = subscriptionContext;
  }

  public void Dispose() {
    throw new NotImplementedException();
  }
}

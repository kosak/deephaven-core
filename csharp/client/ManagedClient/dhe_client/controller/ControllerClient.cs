using Deephaven.DheClient.Auth;
using Deephaven.ManagedClient;
using Google.Protobuf;
using Io.Deephaven.Proto.Auth;
using Io.Deephaven.Proto.Controller;
using Io.Deephaven.Proto.Controller.Grpc;

namespace Deephaven.DheClient.Controller;

public class ControllerClient : IDisposable {
  private const string ControllerServiceName = "PersistentQueryController";
  private static readonly TimeSpan HeartbeatPeriod = TimeSpan.FromSeconds(10);

  public static ControllerClient Connect(string descriptiveName, string target,
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
    var clientId = ClientUtil.MakeClientId(descriptiveName, Guid.NewGuid().ToByteArray());

    var authReq = new AuthenticationRequest {
      ClientId = clientId,
      Token = AuthUtil.ProtoFromAuthToken(authToken),
      GetConfiguration = true
    };
    var authResp = controllerApi.authenticate(authReq);
    if (!authResp.Authenticated) {
      throw new Exception("Failed to authenticate to the Controller");
    }

    var authCookie = authResp.Cookie.ToByteArray();
    var subscriptionContext = SubscriptionContext.Create(controllerApi, authCookie);
    var cancellation = new CancellationToken();

    var result = new ControllerClient(clientId, controllerApi, cancellation,
      subscriptionContext, authCookie);
    Task.Run(result.Heartbeat, cancellation).Forget();
    return result;
  }

  private readonly ClientId _clientId;
  private readonly ControllerApi.ControllerApiClient _controllerApi;
  private readonly CancellationToken _cancellation;
  private readonly SubscriptionContext _subscriptionContext;
  private readonly byte[] _authCookie;

  private ControllerClient(ClientId clientId, ControllerApi.ControllerApiClient controllerApi,
    CancellationToken cancellation, SubscriptionContext subscriptionContext,
    byte[] authCookie) {
    _clientId = clientId;
    _controllerApi = controllerApi;
    _cancellation = cancellation;
    _subscriptionContext = subscriptionContext;
    _authCookie = authCookie;
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  public Subscription Subscribe() {
    return new Subscription(_subscriptionContext);
  }

  /// <summary>
  /// Test if a given status implies a running query.
  /// If not running and not terminal, then the query is in the initialization process.
  /// </summary>
  /// <param name="status">The status</param>
  /// <returns>true if the status represents a running query</returns>
  public static bool IsRunning(PersistentQueryStatusEnum status) {
    return status == PersistentQueryStatusEnum.PqsRunning;
  }

  /// <summary>
  /// Test if a given status implies a terminal (not running) query.
  /// If not running and not terminal, then the query is in the initialization process.
  /// </summary>
  /// <param name="status">The status</param>
  /// <returns>true if the status represents a terminal query</returns>
  public static bool IsTerminal(PersistentQueryStatusEnum status) {
    return status == PersistentQueryStatusEnum.PqsError ||
      status == PersistentQueryStatusEnum.PqsDisconnected ||
      status == PersistentQueryStatusEnum.PqsStopped ||
      status == PersistentQueryStatusEnum.PqsFailed ||
      status == PersistentQueryStatusEnum.PqsCompleted;
  }

  private async Task Heartbeat() {
    await Task.Delay(HeartbeatPeriod, _cancellation);
    Console.WriteLine("heartbeat sent a ping");
    var req = new PingRequest {
      Cookie = ByteString.CopyFrom(_authCookie)
    };
    _ = _controllerApi.ping(req);
    Task.Run(Heartbeat, _cancellation).Forget();
  }
}

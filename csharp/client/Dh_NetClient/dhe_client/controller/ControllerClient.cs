using Deephaven.DheClient.Auth;
using Deephaven.ManagedClient;
using Google.Protobuf;
using Grpc.Net.Client;
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

    var result = new ControllerClient(clientId, channel, controllerApi,
      subscriptionContext, authCookie);
    return result;
  }

  private readonly ClientId _clientId;
  private readonly GrpcChannel _channel;
  private readonly ControllerApi.ControllerApiClient _controllerApi;
  private readonly SubscriptionContext _subscriptionContext;
  private readonly Subscription _sharedSubscription;

  /// <summary>
  /// These fields are all protected by a synchronization object
  /// </summary>
  private struct SyncedFields {
    public readonly object SyncRoot = new();
    public readonly byte[] AuthCookie;
    public readonly Timer Keepalive;
    public bool Cancelled = false;

    public SyncedFields(byte[] authCookie, Timer keepalive) {
      AuthCookie = authCookie;
      Keepalive = keepalive;
    }
  }

  private SyncedFields _synced;

  private ControllerClient(ClientId clientId, GrpcChannel channel,
    ControllerApi.ControllerApiClient controllerApi,
    SubscriptionContext subscriptionContext, byte[] authCookie) {
    _clientId = clientId;
    _channel = channel;
    _controllerApi = controllerApi;
    _subscriptionContext = subscriptionContext;
    _sharedSubscription = new(_subscriptionContext);
    var keepalive = new Timer(Heartbeat);
    _synced = new SyncedFields(authCookie, keepalive);
    keepalive.Change(HeartbeatPeriod, HeartbeatPeriod);
  }

  public void Dispose() {
    lock (_synced.SyncRoot) {
      if (_synced.Cancelled) {
        return;
      }
      _synced.Cancelled = true;
      _synced.Keepalive.Dispose();
    }
    _subscriptionContext.Dispose();
    _channel.Dispose();
  }

  public Subscription Subscribe() => _sharedSubscription;

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

  private void Heartbeat(object? unused) {
    PingRequest req;
    lock (_synced.SyncRoot) {
      if (_synced.Cancelled) {
        return;
      }
      req = new PingRequest {
        Cookie = ByteString.CopyFrom(_synced.AuthCookie)
      };
    }
    // TODO(kosak): catch exception here
    _ = _controllerApi.ping(req);
  }
}

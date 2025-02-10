using System.Diagnostics;
using Deephaven.DheClient.Auth;
using Deephaven.DheClient.Session;
using Deephaven.ManagedClient;
using Grpc.Core;
using Io.Deephaven.Proto.Auth;
using Io.Deephaven.Proto.Controller;
using Io.Deephaven.Proto.Controller.Grpc;

namespace Deephaven.DheClient.Controller;

public class ControllerClient : IDisposable {
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


    var clientId = ClientUtil.MakeClientId(descriptiveName, Guid.NewGuid().ToString());

    var controllerApi = new ControllerApi.ControllerApiClient(channel);
    var co = new CallOptions();
    var req = new PingRequest();
    _ = controllerApi.ping(req, co);

    var subscriptionContext = SubscriptionContext.Create(channel);

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

  public bool Authenticate(AuthToken authToken) {
    var req = new AuthenticationRequest();
    req.ClientId = _clientId;
    req.Token = AuthUtil.AuthTokenToProto(authToken);
    req.GetConfiguration = true;
    var resp = _controllerApi.authenticate(req);
    MegaCookie666.moarCookie = resp.Cookie;
    return resp.Authenticated;
  }

  public void Superpain666() {
    _subscriptionContext.Superpain666();
    return;

    var req = new SubscribeRequest();
    req.Cookie = MegaCookie666.moarCookie;
    var reader = _controllerApi.subscribe(req);

    var rs = reader.ResponseStream;
    var ct = new CancellationToken();

    Task.Run(() => SuperZamboniHate(rs, ct), ct).Forget();

    // while (true) {
    //   var t = rs.MoveNext(ct);
    //   var res = t.Result;
    //   if (!res) {
    //     Debug.WriteLine("I am sad, continuing");
    //     continue;
    //   }
    //   var r = rs.Current;
    //   Debug.WriteLine(r.Event);
    // }

    // _subscriptionContext.DoSubscribe666();
  }

  private static async void SuperZamboniHate(
    IAsyncStreamReader<SubscribeResponse> rs,
    CancellationToken ct) {
    var vvv = await rs.MoveNext(ct);
    if (!vvv) {
      Debug.WriteLine("I am sad, continuing");
    } else {
      var r = rs.Current;
      Debug.WriteLine(r.Event);
    }

    Task.Run(() => SuperZamboniHate(rs, ct), ct).Forget();
  }
}

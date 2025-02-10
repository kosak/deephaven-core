using Deephaven.ManagedClient;
using Google.Protobuf;
using Io.Deephaven.Proto.Auth;
using Io.Deephaven.Proto.Auth.Grpc;

namespace Deephaven.DheClient.Auth;

public class AuthClient {
  public static AuthClient Connect(string descriptiveName, Credentials credentials,
    string target, ClientOptions options) {
    var channel = GrpcUtil.CreateChannel(target, options);
    // where does this go: arguments and also credentials
    // grpc::ChannelArguments channel_args;
    // for (const auto &opt : options.IntOptions()) {
    //   channel_args.SetInt(opt.first, opt.second);
    // }
    // for (const auto &opt : options.StringOptions()) {
    //   channel_args.SetString(opt.first, opt.second);
    // }

    var authApi = new AuthApi.AuthApiClient(channel);
    var uuid = System.Guid.NewGuid().ToByteArray();
    var clientId = ClientUtil.MakeClientId(descriptiveName, uuid);
    var (authCookie, deadline) = Authenticate(clientId, authApi, credentials);
    var ct = new CancellationToken();
    var result = new AuthClient(clientId, authApi, ct, authCookie);
    Task.Run(() => result.RefreshCookie(ct, deadline), ct).Forget();
    return result;
  }

  private static (byte[], DateTimeOffset) Authenticate(ClientId clientId, AuthApi.AuthApiClient authApi,
    Credentials credentials) {
    // TODO(kosak): more credential types here
    if (credentials is not Credentials.PasswordCredentials pwc) {
      throw new Exception("Unexpected credentials type");
    }
    var req = new AuthenticateByPasswordRequest {
      ClientId = clientId,
      Password = pwc.Password,
      UserContext = new Io.Deephaven.Proto.Auth.UserContext {
        AuthenticatedUser = pwc.User,
        EffectiveUser = pwc.OperateAs
      }
    };

    var res = authApi.authenticateByPassword(req).Result;
    if (!res.Authenticated) {
      throw new Exception("Password authentication failed");
    }

    var cookie = res.Cookie.ToByteArray();
    var deadline = DateTimeOffset.FromUnixTimeMilliseconds(res.CookieDeadlineTimeMillis);
    return (cookie, deadline);
  }

  private readonly ClientId _clientId;
  private readonly AuthApi.AuthApiClient _authApi;
  private readonly CancellationToken _cancellationToken;
  private readonly Atomic<byte[]> _cookie;

  private AuthClient(ClientId clientId, AuthApi.AuthApiClient authApi,
    CancellationToken cancellationToken, byte[] cookie) {
    _clientId = clientId;
    _authApi = authApi;
    _cancellationToken = cancellationToken;
    _cookie = cookie;
  }

  internal AuthToken CreateToken(string forService) {
    var request = new GetTokenRequest {
      Service = forService,
      Cookie = ByteString.CopyFrom(_cookie.Value)
    };
    var response = _authApi.getToken(request);
    return AuthUtil.AuthTokenFromProto(response.Token);
  }

  private async Task RefreshCookie(CancellationToken ct, DateTimeOffset deadline) {
    var delayMillis = (int)(Math.Max(0, (deadline - DateTimeOffset.Now).TotalMilliseconds) / 2);
    Console.WriteLine($"Hi, delaying for {delayMillis} milliseconds");
    await Task.Delay(delayMillis, ct);

    var req = new RefreshCookieRequest {
      Cookie = _cookie.Value
    };
    var resp = _authApi.refreshCookie(req);

    _cookie.Value = resp.Cookie.ToByteArray();
    var newDeadline = DateTimeOffset.FromUnixTimeMilliseconds(resp.CookieDeadlineTimeMillis);
    Task.Run(() => RefreshCookie(ct, newDeadline), ct).Forget();
  }
}

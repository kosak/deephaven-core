using Deephaven.ManagedClient;
using Google.Protobuf;
using Grpc.Core;
using Io.Deephaven.Proto.Auth;
using Io.Deephaven.Proto.Auth.Grpc;
using Io.Deephaven.Proto.Common;

namespace Deephaven.DheClient.Auth;

public class AuthClient {
  public static AuthClient Connect(string descriptiveName, string target,
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

    var authApi = new AuthApi.AuthApiClient(channel);
    // var co = new CallOptions();
    var req = new PingRequest();
    _ = authApi.ping(req);

    var uuid = System.Guid.NewGuid().ToString();
    var clientId = ClientUtil.MakeClientId(descriptiveName, uuid);

    return new AuthClient(clientId, authApi);
  }

  private readonly ClientId _clientId;
  private readonly AuthApi.AuthApiClient _authApi;

  public AuthClient(ClientId clientId, AuthApi.AuthApiClient authApi) {
    _clientId = clientId;
    _authApi = authApi;
  }

  public AuthenticationResult PasswordAuthentication(string user, string password,
    string operateAs) {
    // TODO(kosak) stuff here
    var req = new AuthenticateByPasswordRequest {
      ClientId = _clientId,
      Password = password,
      UserContext = new Io.Deephaven.Proto.Auth.UserContext {
        AuthenticatedUser = user,
        EffectiveUser = user,
      }
    };

    var resp = _authApi.authenticateByPassword(req);
    return resp.Result;
  }

  public AuthToken CreateToken(string forService) {
    var cookie = GetCookieOrThrow();
    var request = new GetTokenRequest {
      Service = forService,
      Cookie = ByteString.CopyFromUtf8(cookie)
    };
    // var context = new ClientContext();
    // const auto send_time = TimeNow();
    // SetupClientContext(context, send_time + kAuthGrpcRpcDeadlineMillis);
    var response = _authApi.getToken(request);
    return AuthTokenFromProto(response.Token);
  }

  private static AuthToken AuthTokenFromProto(Token token) {
    var uc = new UserContext(token.UserContext.AuthenticatedUser, token.UserContext.EffectiveUser);
    return new AuthToken(token.TokenId, token.Service, uc, token.IpAddress.ToStringUtf8());
  }

  private string GetCookieOrThrow() {
    return "kosak cookie time";
  }
}

using Deephaven.DheClient.Session;
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
    var authCookie = Authenticate(clientId, authApi, credentials);
    var result = new AuthClient(clientId, authApi, authCookie);
    return result;
  }

  private static byte[] Authenticate(ClientId clientId, AuthApi.AuthApiClient authApi,
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

    var resp = authApi.authenticateByPassword(req);
    if (!resp.Result.Authenticated) {
      throw new Exception("Password authentication failed");
    }

    return resp.Result.Cookie.ToByteArray();
  }

  private readonly ClientId _clientId;
  private readonly AuthApi.AuthApiClient _authApi;
  private readonly byte[] _cookie;

  private AuthClient(ClientId clientId, AuthApi.AuthApiClient authApi, byte[] cookie) {
    _clientId = clientId;
    _authApi = authApi;
    _cookie = cookie;
  }

  internal AuthToken CreateToken(string forService) {
    var request = new GetTokenRequest {
      Service = forService,
      Cookie = ByteString.CopyFrom(_cookie)
    };
    var response = _authApi.getToken(request);
    return AuthUtil.AuthTokenFromProto(response.Token);
  }
}

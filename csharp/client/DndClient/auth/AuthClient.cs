/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */

using System.Net.NetworkInformation;
using System.Net.Sockets;
using Deephaven.ManagedClient;
using Google.Protobuf;
using Grpc.Net.Client;
using Io.Deephaven.Proto.Auth;
using Io.Deephaven.Proto.Auth.Grpc;
using Io.Deephaven.Proto.Common;

namespace DeephavenEnterprise.DndClient;

/**
 * An authentication Client maintains provides the means to authenticate to
 * a Deephaven Enterprise Authentication Server, and to maintain the
 * authentication status: the protocol requires frequent refreshes of a lease,
 * which this class will do once authentication is established.
 */
public class AuthClient : IDisposable {
  private static readonly TimeSpan InitialAuthClientWait = TimeSpan.FromMinutes(2);
  private static readonly TimeSpan AuthGrpcRpcDeadlineMillis = TimeSpan.FromMinutes(1);

  /// <summary>
  /// Factory method to connect to an Authentication Server using the specified options.
  /// </summary>
  /// <param name="descriptiveName">A string describing the client useful for identifying this client in log messages</param>
  /// <param name="target">A connection string in the format host:port.For example "localhost:9031"</param>
  /// <param name="options">An options object for setting options</param>
  /// <returns>An AuthClient object connected to the Authentication Server</returns>
  public static AuthClient Connect(string descriptiveName, string target, ClientOptions? options = null) {
    var channel = ClientUtil.CreateChannel("AuthClient::Connect: ClientOptions", target, options);
    var clientId = ClientUtil.MakeClientId(descriptiveName, Guid.NewGuid().ToString());
    var authApiStub = new AuthApi.AuthApiClient(channel);
    var logId = $"AuthClient clientId={{name={clientId.Name}, UUID={clientId.Uuid.ToBase64()}}}";
    return new AuthClient(clientId, channel, authApiStub, logId);
  }

  private readonly ClientId _clientId;
  private readonly GrpcChannel _channel;
  private readonly AuthApi.AuthApiClient _authApiStub;
  private readonly string _logId;

  public AuthClient(ClientId clientId, GrpcChannel channel, AuthApi.AuthApiClient authApiStub,
    string logId) {
    _clientId = clientId;
    _channel = channel;
    _authApiStub = authApiStub;
    _logId = logId;
    Ping(InitialAuthClientWait);
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  public bool IsClosed() {
    throw new NotImplementedException("NIY");
  }

  /// <summary>
  /// Close and invalidate the client.
  /// If a valid authentication with the server exists, this client will attempt
  /// to contact the server to invalidate it.
  /// If a gRPC channel associated with this client is connected, it will be closed.
  /// After this method is called no other method on the client should be called.
  /// This method is also called by the destructor if not already called.
  /// </summary>
  public void Close() {
    throw new NotImplementedException("NIY");
  }

  private static void DoNothing() {
  }

  public bool PasswordAuthentication(string user, string password, string operateAs) {
    CheckNotClosedOrThrow();
    var sendTime = DateTimeOffset.Now;
    var deadline = sendTime + AuthGrpcRpcDeadlineMillis;
    return AuthenticateMethodWrapper(
      "PasswordAuthentication",
      AlreadyAuthenticatedStrategy.ThrowIfAlreadyAuthenticated,
      deadlineArg => AuthenticateByPassword(user, password, operateAs, deadlineArg),
      deadline, DoNothing, DoNothing);
  }

  bool AuthenticateMethodWrapper(string who, AlreadyAuthenticatedStrategy strategy,
    Func<DateTimeOffset, AuthenticationResult> authenticateMethod,
    DateTimeOffset deadline,
    Action preAction, Action postAction) {
    var state = PrepareAuthenticateMethod(
      who,
      strategy != AlreadyAuthenticatedStrategy.ThrowIfAlreadyAuthenticated,
      true,
      preAction);
    switch (state) {
      case AuthenticationState.Authenticated: {
        const string msg = "already authenticated.";
        switch (strategy) {
          case AlreadyAuthenticatedStrategy.ThrowIfAlreadyAuthenticated:
            // gpr_log(GPR_ERROR, WITH_ID_WHO("%s"), msg);
            throw new AlreadyAuthenticatedException();
          case AlreadyAuthenticatedStrategy.ReturnTrueIfAlreadyAuthenticated:
            // gpr_log(GPR_INFO, WITH_ID_WHO("%s"), msg);
            return true;
        }
      }
        break;
      case AuthenticationState.AlreadyAuthenticating: {
        // we can only receive this return value in the case
        // where strategy == THROW_IF_ALREADY_AUTHENTICATED.
        const string msg = "already trying to authenticate";
        // gpr_log(GPR_ERROR, WITH_ID_WHO("%s"), msg);
        throw new AuthException(who + ": " + msg, Tag.NonRetryable);
      }
        break;
      case AuthenticationState.NotAuthenticated:
        throw new Exception("PrepareAuthenticateMethod returned NOT_AUTHENTICATED");
      case AuthenticationState.NowAuthenticating:
        // fallthrough to rest of the code.
        break;
      default:
        throw new Exception("PrepareAuthenticateMethod returned unknown AuthenticationState");
    }

    return AuthenticateMethodSecondHalf(
      who, authenticateMethod, deadline, postAction);
  }

  bool AuthClientImpl::
AuthenticateMethodSecondHalf(
    const std::string &who,
    const AuthenticateMethod &authenticate_method,
    const time_point deadline,
    const PostAction &post_action
) {
  AuthResultPtr auth_result;
try {
  auth_result = authenticate_method(deadline);
} catch (const std::exception &ex) {
  {
    lock_t lock (auth_mux_) ;
    client_state_.Reset();
  }
  throw;
}
const bool authenticated = auth_result->authenticated();
std::size_t cookie_generation_snapshot = 0U;
time_point cookie_deadline_snapshot;
{
  lock_t lock (auth_mux_) ;
  try {
    post_action();
  } catch (...) {
    client_state_.Reset();
    throw;
  }
  client_state_.authenticated = authenticated;
  if (authenticated) {
    cookie_generation_snapshot = ++client_state_.cookie_generation;
    client_state_.cookie = auth_result->cookie();
    client_state_.user_context = {
      auth_result->usercontext().authenticateduser(),
          auth_result->usercontext().effectiveuser()
      };
    cookie_deadline_snapshot = client_state_.cookie_deadline =
        EpochMillisToTimePoint(auth_result->cookie_deadline_time_millis());
  }
  client_state_.auth_tid.reset();
  }
  auth_cond_.notify_all();
  if (authenticated) {
    const time_point now = TimeNow();
    const milliseconds millis_to_cookie_deadline =
        duration_cast<milliseconds>(cookie_deadline_snapshot - now);
    gpr_log(GPR_INFO,
            WITH_ID_WHO("finished authenticating in thread %s, "
                   "authenticated=true, "
                   "Cookie deadline in %s."),
            TidToString(std::this_thread::get_id()).c_str(),
            MillisToStr(millis_to_cookie_deadline).c_str());
    ScheduleCookieRefresh(
        auth_result->cookie(),
        cookie_generation_snapshot,
        cookie_deadline_snapshot);
  } else {
    gpr_log(GPR_ERROR,
            WITH_ID_WHO("finished authenticating in thread %s, "
                   "authenticated=false"),
            TidToString(std::this_thread::get_id()).c_str());
  }
  return authenticated;
}



/// <summary>
/// Ping a server; return and log roundtrip time.
/// </summary>
/// <param name="timeout">How long of a timeout to use in the Ping request to the server</param>
/// <returns>roundtrip time</returns>
TimeSpan Ping(TimeSpan timeout) {
    var req = new PingRequest {
      From = _logId,
      SenderSendTimeMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds()
    };
    var resp = _authApiStub.ping(req);
    return TimeSpan.Zero;
  }

  public AuthenticationResult AuthenticateByPassword(string user, string password, string operateAs, DateTimeOffset deadline) {
    var req = new AuthenticateByPasswordRequest();
    req.ClientId = _clientId;
    req.UserContext = new UserContext {
      AuthenticatedUser = user,
      EffectiveUser = operateAs
    };
    req.Password = password;
    var result = _authApiStub.authenticateByPassword(req);
    return result.Result;
  }


  bool PrivateKeyAuthentication(string privateKeyFilename) {
    throw new NotImplementedException("NIY");
  }

  AuthToken CreateToken(string forService) {
    throw new NotImplementedException("NIY");
  }


  AuthToken CreateTokenForUser(string forService, string operateAs) {
    throw new NotImplementedException("NIY");
  }


  bool VerifyToken(AuthToken token, string forService) {
    throw new NotImplementedException("NIY");
  }

  void CheckNotClosedOrThrow() {
    if (IsClosed()) {
      throw new Exception("Client is already closed");
    }
  }

  enum AuthenticationState {
    AlreadyAuthenticating,
    NowAuthenticating,
    Authenticated,
    NotAuthenticated
  }

  enum AlreadyAuthenticatedStrategy {
    ThrowIfAlreadyAuthenticated,
    ReturnTrueIfAlreadyAuthenticated
  }
}



/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */

using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
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
  private readonly object _authSync = new();
  private readonly ClientState _clientState = new();
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
    var sendTime = TimeNow();
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

  bool AuthenticateMethodSecondHalf(
    string who,
    Func<DateTimeOffset, AuthenticationResult> authenticateMethod,
    DateTimeOffset deadline, Action postAction) {
    AuthenticationResult authResult;
    try {
      authResult = authenticateMethod(deadline);
    } catch (Exception ex) {
      lock (_authSync) {
        _clientState.Reset();
      }
      throw;
    }
    var authenticated = authResult.Authenticated;
    UInt64 cookieGenerationSnapshot = 0;
    var cookieDeadlineSnapshot = new DateTimeOffset();
    lock (_authSync) {
      try {
        postAction();
      } catch {
        _clientState.Reset();
        throw;
      }
      _clientState.authenticated = authenticated;
      if (authenticated) {
        cookieGenerationSnapshot = Interlocked.Increment(ref _clientState.atomicCookieGeneration);
        _clientState.cookie = authResult.Cookie;
        _clientState.userContext = new UserContext(
          authResult.UserContext.AuthenticatedUser,
          authResult.UserContext.EffectiveUser);
        cookieDeadlineSnapshot = _clientState.cookieDeadline =
          DateTimeOffset.FromUnixTimeMilliseconds(authResult.CookieDeadlineTimeMillis);
      }

      _clientState.authTid = null;
      Monitor.PulseAll(_authSync);
    }

    if (authenticated) {
      var now = TimeNow();
      var millisToCookieDeadline = cookieDeadlineSnapshot - now;
      // gpr_log(GPR_INFO,
      //   WITH_ID_WHO("finished authenticating in thread %s, "
      // "authenticated=true, "
      // "Cookie deadline in %s."),
      // TidToString(std::this_thread::get_id()).c_str(),
      // MillisToStr(millisToCookieDeadline).c_str());
      ScheduleCookieRefresh(
        authResult.Cookie,
        cookieGenerationSnapshot,
        cookieDeadlineSnapshot);
    } else {
      // gpr_log(GPR_ERROR,
      //   WITH_ID_WHO("finished authenticating in thread %s, "
      // "authenticated=false"),
      // TidToString(std::this_thread::get_id()).c_str());
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
    var req = new AuthenticateByPasswordRequest {
      ClientId = _clientId,
      UserContext = new Io.Deephaven.Proto.Auth.UserContext {
        AuthenticatedUser = user,
        EffectiveUser = operateAs
      },
      Password = password
    };
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

  class ClientState {
    // id of the thread trying authentication; a default constructed tid if none
    public int? authTid;
    // if false, none of the fields below are valid.
    public bool authenticated = false;
    public string cookie;
    public UInt64 atomicCookieGeneration;
    public DateTimeOffset cookieDeadline;
    public UserContext userContext;

    public void Reset() {
      authTid = null;
      authenticated = false;
      cookie = "";
      Interlocked.Increment(ref atomicCookieGeneration);
      cookieDeadline = new DateTimeOffset();
      userContext = new UserContext();
    }
  };

  private static DateTimeOffset TimeNow() {
    return DateTimeOffset.UtcNow;
  }
}

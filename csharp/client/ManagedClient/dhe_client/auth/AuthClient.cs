﻿using Deephaven.ManagedClient;
using Google.Protobuf;
using Io.Deephaven.Proto.Auth;
using Io.Deephaven.Proto.Auth.Grpc;
using Grpc.Net.Client;
using System.Net;

namespace Deephaven.DheClient.Auth;

public class AuthClient : IDisposable {
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
    var result = new AuthClient(clientId, channel, authApi, authCookie, deadline);
    return result;
  }

  private static (byte[], DateTimeOffset) Authenticate(ClientId clientId,
    AuthApi.AuthApiClient authApi, Credentials credentials) {
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
  private readonly GrpcChannel _channel;
  private readonly AuthApi.AuthApiClient _authApi;

  /// <summary>
  /// These fields are all protected by a synchronization object
  /// </summary>
  private struct SyncedFields {
    public readonly object SyncRoot = new();
    public byte[] Cookie;
    public readonly Timer Keepalive;
    public bool Cancelled = false;

    public SyncedFields(byte[] cookie, Timer keepalive) {
      Cookie = cookie;
      Keepalive = keepalive;
    }
  }

  private SyncedFields _synced;

  private AuthClient(ClientId clientId, GrpcChannel channel, AuthApi.AuthApiClient authApi, 
    byte[] cookie, long cookieDeadlineTimeMillis) {
    _clientId = clientId;
    _channel = channel;
    _authApi = authApi;
    var keepalive = new Timer(RefreshCookie);
    _synced = new SyncedFields(cookie, keepalive);
    SetKeepaliveExpiration(cookieDeadlineTimeMillis);
    keepalive.Change(_expirationInterval, Timeout.InfiniteTimeSpan);
  }

  public void Dispose() {
    lock (_synced.SyncRoot) {
      if (_synced.Cancelled) {
        return;
      }
      _synced.Cancelled = true;
      _synced.Keepalive.Dispose();
    }

    _channel.Dispose();
  }

  internal AuthToken CreateToken(string forService) {
    var request = new GetTokenRequest {
      Service = forService,
      Cookie = ByteString.CopyFrom(_cookie.GetValue())
    };
    var response = _authApi.getToken(request);
    return AuthUtil.AuthTokenFromProto(response.Token);
  }

  private void RefreshCookie(object? _) {
    RefreshCookieRequest req;
    lock (_synced.SyncRoot) {
      if (_synced.Cancelled) {
        return;
      }
      req = new RefreshCookieRequest {
        Cookie = ByteString.CopyFrom(_synced.Cookie)
      };
    }

    var resp = _authApi.refreshCookie(req);
    var dueTime = CalcDueTime(resp.CookieDeadlineTimeMillis);

    lock (_synced.SyncRoot) {
      _synced.Cookie = resp.Cookie.ToByteArray();
      _synced.Keepalive.Change(dueTime, Timeout.InfiniteTimeSpan);
    }
  }

  private static TimeSpan CalcDueTime(long cookieDeadlineTimeMillis) {
    var deadline = DateTimeOffset.FromUnixTimeMilliseconds(res.CookieDeadlineTimeMillis);

  }


}

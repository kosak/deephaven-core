﻿using Deephaven.ManagedClient;
using Grpc.Core;
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
    var co = new CallOptions();
    var req = new PingRequest();
    _ = authApi.ping(req, co);

    return new AuthClient(authApi);
  }

  private readonly AuthApi.AuthApiClient _authApi;

  public AuthClient(AuthApi.AuthApiClient authApi) {
    _authApi = authApi;
  }
}

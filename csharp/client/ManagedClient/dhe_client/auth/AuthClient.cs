using Apache.Arrow.Flight.Client;
using Deephaven.ManagedClient;
using Grpc.Core;
using Io.Deephaven.Proto.Auth.Grpc;
using Io.Deephaven.Proto.Backplane.Grpc;
using Io.Deephaven.Proto.Backplane.Script.Grpc;
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
    var z = authApi.pingAsync(req, co);
    var smd = z.ResponseHeadersAsync.Result;
    var smr = z.ResponseAsync.Result;
    throw new Exception("I am sad");
  }
}

using Deephaven.ManagedClient;
using Grpc.Core;
using Io.Deephaven.Proto.Controller;
using Io.Deephaven.Proto.Controller.Grpc;

namespace Deephaven.DheClient.Controller;

public class ControllerClient {
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

    var controllerApi = new ControllerApi.ControllerApiClient(channel);
    var co = new CallOptions();
    var req = new PingRequest();
    _ = controllerApi.ping(req, co);

    return new ControllerClient(controllerApi);
  }

  private readonly ControllerApi.ControllerApiClient _controllerApi;

  public ControllerClient(ControllerApi.ControllerApiClient controllerApi) {
    _controllerApi = controllerApi;
  }
}

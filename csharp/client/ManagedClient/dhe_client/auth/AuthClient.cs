using Apache.Arrow.Flight.Client;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Backplane.Grpc;
using Io.Deephaven.Proto.Backplane.Script.Grpc;

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

    // var cs = new ConsoleService.ConsoleServiceClient(channel);
    // var ss = new SessionService.SessionServiceClient(channel);
    // var ts = new TableService.TableServiceClient(channel);
    // var cfs = new ConfigService.ConfigServiceClient(channel);
    // var its = new InputTableService.InputTableServiceClient(channel);
    // var fc = new FlightClient(channel);

    var auths = new AuthApi.AuthsClient(channel);

  }
}

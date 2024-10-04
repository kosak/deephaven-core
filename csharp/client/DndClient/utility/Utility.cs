using Deephaven.ManagedClient;
using Grpc.Net.Client;

namespace DeephavenEnterprise.DndClient;

public static class Utility {
  public static GrpcChannel CreateChannel(string who, string target, ClientOptions? options) {
    options ??= new ClientOptions();
    var channelOptions = GrpcUtil.MakeChannelOptions(options);

    // grpc::ChannelArguments channel_args;
    // for (const auto &opt : options.IntOptions()) {
    //   channel_args.SetInt(opt.first, opt.second);
    // }
    // for (const auto &opt : options.StringOptions()) {
    //   channel_args.SetString(opt.first, opt.second);
    // }
    //

    var address = GrpcUtil.MakeAddress(options, target);
    var channel = GrpcChannel.ForAddress(address, channelOptions);
    return channel;
  }
}

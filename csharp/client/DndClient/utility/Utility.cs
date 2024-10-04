using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DeephavenEnterprise.DndClient;

public static class Utility {
  public static Grpc.Net.Channel CreateChannel(
  const std::string &who,
  const std::string &target,
  const utility::ClientOptions &options) {
    if (!options.UseTls() && !options.TlsRootCerts().empty()) {
      const std::string message =
        who + ": ClientOptions: "
      "UseTls is false but pem provided";
      throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
    }


    grpc::ChannelArguments channel_args;
    for (const auto &opt : options.IntOptions()) {
      channel_args.SetInt(opt.first, opt.second);
    }
    for (const auto &opt : options.StringOptions()) {
      channel_args.SetString(opt.first, opt.second);
    }


    const auto credentials = GetCredentials(options.UseTls(), options.TlsRootCerts(),
      options.ClientCertChain(), options.ClientPrivateKey());
    auto channel = grpc::CreateCustomChannel(
      target,
      credentials,
      channel_args);
    return channel;
  }


}

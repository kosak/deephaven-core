using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Deephaven.ManagedClient;
using Grpc.Net.Client;

namespace DeephavenEnterprise.DndClient;

public static class Utility {
  public static GrpcChannel CreateChannel(string who, string target, ClientOptions? options) {
    options ??= new ClientOptions();
    if (!options.UseTls && !options.TlsRootCerts.IsEmpty()) {
      throw new Exception("UseTls is false but pem provided");
    }

    // grpc::ChannelArguments channel_args;
    // for (const auto &opt : options.IntOptions()) {
    //   channel_args.SetInt(opt.first, opt.second);
    // }
    // for (const auto &opt : options.StringOptions()) {
    //   channel_args.SetString(opt.first, opt.second);
    // }
    //

    var channelOptions = new GrpcChannelOptions {

    };


    const auto credentials = GetCredentials(options.UseTls(), options.TlsRootCerts(),
      options.ClientCertChain(), options.ClientPrivateKey());

    var channel = new GrpcChannel(address, channelOptions);
    
    auto channel = grpc::CreateCustomChannel(
      target,
      credentials,
      channel_args);
    return channel;
  }


}

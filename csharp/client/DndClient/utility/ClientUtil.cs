using Deephaven.ManagedClient;
using Google.Protobuf;
using Grpc.Net.Client;
using Io.Deephaven.Proto.Auth;
using Dns = System.Net.Dns;

namespace DeephavenEnterprise.DndClient;

public static class ClientUtil {
  public static string GetName(string descriptiveName) {
    return $"{Dns.GetHostName()}/{descriptiveName}";
  }

  public static ClientId MakeClientId(string descriptiveName, string uuid) {
    var name = GetName(descriptiveName);
    var clientId = new ClientId {
      Name = name,
      Uuid = ByteString.CopyFromUtf8(uuid)
    };
    return clientId;
  }

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

    var handler = new HttpClientHandler();
    handler.ClientCertificateOptions = ClientCertificateOption.Manual;
    handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    var hc = new HttpClient(handler);
    channelOptions.HttpClient = hc;

    var address = GrpcUtil.MakeAddress(options, target);
    var channel = GrpcChannel.ForAddress(address, channelOptions);
    return channel;
  }
}

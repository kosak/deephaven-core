using Grpc.Core;
using Grpc.Net.Client;

namespace Deephaven.ManagedClient;

public static class GrpcUtil {
  public static GrpcChannelOptions MakeChannelOptions(ClientOptions clientOptions) {
    var channelOptions = new GrpcChannelOptions();

    if (!clientOptions.UseTls && !clientOptions.TlsRootCerts.IsEmpty()) {
      throw new Exception("Server.CreateFromTarget: ClientOptions: UseTls is false but pem provided");
    }

    channelOptions.Credentials = GetCredentials(clientOptions.UseTls, clientOptions.TlsRootCerts,
      clientOptions.ClientCertChain, clientOptions.ClientPrivateKey);
    return channelOptions;
  }

  public static string MakeAddress(ClientOptions clientOptions, string target) {
    return (clientOptions.UseTls ? "https://" : "http://") + target;
  }

  private static ChannelCredentials GetCredentials(
    bool useTls,
    string tlsRootCerts,
    string clientRootChain,
    string clientPrivateKey) {
    if (!useTls) {
      return ChannelCredentials.Insecure;
    }

    var certPair = new KeyCertificatePair(clientRootChain, clientPrivateKey);
    return new SslCredentials(tlsRootCerts, certPair);
  }
}

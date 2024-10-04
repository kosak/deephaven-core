using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Net.Client;

namespace Deephaven.ManagedClient;

public static class GrpcUtil {
  public static GrpcChannelOptions MakeChannelOptions(ClientOptions clientOptions) {
    var channelOptions = new GrpcChannelOptions();

    if (!clientOptions.UseTls && !clientOptions.TlsRootCerts.IsEmpty()) {
      throw new Exception("Server.CreateFromTarget: ClientOptions: UseTls is false but pem provided");
    }

  // var httpClientHandler = new HttpClientHandler();
  // httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, _) => {
  //   chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
  //   chain.ChainPolicy.CustomTrustStore.Add(mycert);
  //   etc etc get this to work
  // https://github.com/grpc/grpc-dotnet/blob/dd72d6a38ab2984fd224aa8ed53686dc0153b9da/testassets/InteropTestsClient/InteropClient.cs#L170
    //
    //
    // };
    //
    // channelOptions.Credentials = GetCredentials(clientOptions.UseTls, clientOptions.TlsRootCerts,
    //   clientOptions.ClientCertChain, clientOptions.ClientPrivateKey);
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

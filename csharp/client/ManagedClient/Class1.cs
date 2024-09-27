using Grpc.Core;
using Grpc.Net.Client;
using Io.Deephaven.Proto.Backplane.Grpc;

namespace Deephaven.ManagedClient;

public class Class1 {
  public static void Zamboni() {
    using var channel = GrpcChannel.ForAddress("http://10.0.4.109:10000");
    var cfs = new ConfigService.ConfigServiceClient(channel);

    var headers = new Metadata();
    headers.Add("authorization", "Anonymous");

    var req = new ConfigurationConstantsRequest();
    var resp = cfs.GetConfigurationConstants(req, headers);

    Console.WriteLine(resp);
  }


}

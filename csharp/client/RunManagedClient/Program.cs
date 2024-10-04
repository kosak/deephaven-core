global using BooleanChunk = Deephaven.ManagedClient.Chunk<bool>;
using Deephaven.ManagedClient;
using DeephavenEnterprise.DndClient;

namespace Deephaven.RunManangedClient;

public static class Program {
  public static void Main(string[] args) {
    var server = "10.0.4.109:10000";
    if (args.Length > 0) {
      if (args.Length != 1 || args[0] == "-h") {
        Console.Error.WriteLine("Arguments: [host:port]");
        Environment.Exit(1);
      }

      server = args[0];
    }

    var grizzleJson = """
                      { "auth_host":["kosak-grizzle-1.int.illumon.com"],
                      "auth_port":9031,
                      "controller_port":20126,
                      "controller_host":"10.128.5.38",
                      "truststore_url":"https://kosak-grizzle-1.int.illumon.com:8443/iris//resources/truststore-iris.pem",
                      "override_authorities":true,
                      "controller_authority":"controller",
                      "auth_authority":"authserver",
                      "acl_write_server":"https://kosak-grizzle-1.int.illumon.com:9044/acl/",
                      "authentication_service_config":"{  \"methodConfig\": [\n    {\n      \"name\": [\n          {\n              \"service\": \"io.deephaven.proto.auth.grpc.AuthApi\"\n          }\n      ],\n\n      \"retryPolicy\": {\n        \"maxAttempts\": 60,\n        \"initialBackoff\": \"0.1s\",\n        \"maxBackoff\": \"2s\",\n        \"backoffMultiplier\": 2,\n        \"retryableStatusCodes\": [\n          \"UNAVAILABLE\"\n        ]\n      },\n\n      \"waitForReady\": true,\n      \"timeout\": \"60s\"\n    }\n  ]\n}\n","controller_service_config":"{\n  \"methodConfig\": [\n    {\n      \"name\": [\n          {\n              \"service\": \"io.deephaven.proto.controoler.grpc.ControllerApi\"\n          }\n      ],\n\n      \"retryPolicy\": {\n        \"maxAttempts\": 60,\n        \"initialBackoff\": \"0.1s\",\n        \"maxBackoff\": \"10s\",\n        \"backoffMultiplier\": 2,\n        \"retryableStatusCodes\": [\n          \"UNAVAILABLE\"\n        ]\n      },\n\n      \"waitForReady\": true,\n      \"timeout\": \"60s\"\n    }\n  ]\n}\n"}
                      """;
    var session = SessionManager.FromJson("hello", grizzleJson);
    var temp = session.PasswordAuthentication("iris", "iris", "iris");

    try {
      using var client = Client.Connect(server);
      using var manager = client.GetManager();
      using var t1 = manager.EmptyTable(10);
      using var t2 = t1.Update(
        "Chars = ii == 5 ? null : (char)('a' + ii)",
        "Bytes = ii == 5 ? null : (byte)(ii)",
        "Shorts = ii == 5 ? null : (short)(ii)",
        "Ints = ii == 5 ? null : (int)(ii)",
        "Longs = ii == 5 ? null : (long)(ii)",
        "Floats = ii == 5 ? null : (float)(ii)",
        "Doubles = ii == 5 ? null : (double)(ii)",
        "Bools = ii == 5 ? null : ((ii % 2) == 0)",
        "Strings = ii == 5 ? null : `hello ` + i",
        "DateTimes = ii == 5 ? null : '2001-03-01T12:34:56Z' + ii",
        "LocalDates = ii == 5 ? null : parseLocalDate(`2001-3-` + (ii + 1))",
        "LocalTimes = ii == 5 ? null : parseLocalTime(`12:34:` + (46 + ii))"
      );

      var tResult = t2;

      Console.WriteLine(tResult.ToString(true));
      // var at = tResult.ToArrowTable();
      var ct = tResult.ToClientTable();
      var cs = ct.GetColumn(0);

      var size = ct.NumRows.ToIntExact();
      var chunk = Chunk.CreateChunkFor(cs, size);
      var nulls = BooleanChunk.Create(size);
      var rs = ct.RowSequence;

      cs.FillChunk(rs, chunk, nulls);
      Console.WriteLine("hello");

      // using var tt = manager.TimeTable("PT2S");
      // using var tt2 = tt.Update("II = ii");
      tResult.ZamboniTime();
    } catch (Exception e) {
      Console.Error.WriteLine($"Caught exception: {e}");
    }
  }
}

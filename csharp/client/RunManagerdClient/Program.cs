using Deephaven.ManagedClient;

namespace Deephaven.RunManangedClient;

public static class Program {
  public static void Main(string[] args) {
    var server = "localhost:10000";
    if (args.Length > 0) {
      if (args.Length != 1 || args[0] == "-h") {
        Console.Error.WriteLine("Arguments: [host:port]");
        Environment.Exit(1);
      }

      server = args[0];
    }

    try {
      var client = Client.Connect(server);
      var manager = client.GetManager();
      var table = manager.EmptyTable(10);
      var t2 = table.Update("ABC = ii + 100");
      Console.WriteLine(t2.ToString(true));
    } catch (Exception e) {
      Console.Error.WriteLine($"Caught exception: {e.Message}");
    }
  }
}

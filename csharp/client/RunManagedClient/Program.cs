using Deephaven.ManagedClient;

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

    try {
      using var client = Client.Connect(server);
      using var manager = client.GetManager();
      using var table = manager.EmptyTable(10);
      using var t2 = table.Update("ABC = ii + 100");
      Console.WriteLine(t2.ToString(true));
    } catch (Exception e) {
      Console.Error.WriteLine($"Caught exception: {e}");
    }
  }
}

using Deephaven.ManagedClient;

namespace Deephaven.RunManangedClient;

public static class Program {
  public static void Main(string[] args) {
    try {
      Class1.Zamboni();
    } catch (Exception ex) {
      Console.WriteLine(ex.Message);
    }
  }

}
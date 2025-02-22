namespace Deephaven.ExcelAddIn.Status;

public class KeepAlive {
  public static KeptAlive<T> Register<T>(T item) where T : IDisposable {

  }

  public static KeptAlive<T> Reference<T>(T item) where T : IDisposable {

  }

}

public class KeptAlive<T> : IDisposable {
  public T Target {
    get {

    }
  }

}

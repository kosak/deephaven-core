using Io.Deephaven.Proto.Backplane.Grpc;
using Io.Deephaven.Proto.Backplane.Script.Grpc;

namespace Deephaven.ManagedClient;

public class Executor {
  public static Executor Create(string freak) {
    Console.Error.WriteLine("NOT IMPLEMENTED YET");
    return new Executor();
  }
}
/// <summary>
/// The main class for interacting with Deephaven. Start here to Connect with
/// the server and to get a TableHandleManager.
/// </summary>
public class Client : IDisposable {
  /// <summary>
  /// Factory method to Connect to a Deephaven server using the specified options.
  /// </summary>
  /// <param name="target">A connection string in the format host:port.For example "localhost:10000"</param>
  /// <param name="options">An options object for setting options like authentication and script language.</param>
  /// <returns>A Client object connected to the Deephaven server.</returns>
  public static Client Connect(string target, ClientOptions? options = null) {
    options ??= new ClientOptions();

    var server = Server.CreateFromTarget(target, options);
    var executor = Executor.Create("Client executor for " + server.Me);
    var flightExecutor = Executor.Create("Flight executor for " + server.Me);

    Ticket? consoleTicket = null;
    if (!options.SessionType.IsEmpty()) {
      var req = new StartConsoleRequest {
        ResultId = server.NewTicket(),
        SessionType = options.SessionType
      };
      var resp = server.SendRpc(opts => server.ConsoleStub.StartConsoleAsync(req, opts));
      consoleTicket = resp.ResultId;
    }

    var thm = TableHandleManager.Create(consoleTicket, server, executor, flightExecutor);
    return new Client(thm);
  }

  /// <summary>
  /// Gets the TableHandleManager which you can use to create empty tables, fetch tables, and so on.
  /// </summary>
  public TableHandleManager Manager { get; }

  protected Client(TableHandleManager tableHandleManager) {
    Manager = tableHandleManager;
  }

  /// <summary>
  /// Shuts down the Client and all associated state(GRPC connections, subscriptions, etc).
  /// The caller must not use any associated data structures(TableHandleManager, TableHandle, etc)
  /// after Dispose() is called. If the caller tries to do so, the behavior is unspecified.
  /// </summary>
  public void Dispose() {
    Console.Error.WriteLine("Client.Dispose: NIY");
  }

  /// <summary>
  /// Adds a callback to be invoked when this client is closed.
  /// On close callbacks are invoked before the client is actually shut down,
  /// so they can perform regular client and table manager operations before
  /// closing.
  /// </summary>
  public event Action? OnClose;
};

using Io.Deephaven.Proto.Backplane.Grpc;
using Io.Deephaven.Proto.Backplane.Script.Grpc;

namespace Deephaven.ManagedClient;

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

    Ticket? consoleTicket = null;
    if (!options.SessionType.IsEmpty()) {
      var req = new StartConsoleRequest {
        ResultId = server.NewTicket(),
        SessionType = options.SessionType
      };
      var resp = server.SendRpc(opts => server.ConsoleStub.StartConsoleAsync(req, opts));
      consoleTicket = resp.ResultId;
    }

    var thm = TableHandleManager.Create(consoleTicket, server);
    return new Client(thm);
  }

  private TableHandleManager? _manager;

  protected Client(TableHandleManager tableHandleManager) {
    _manager = tableHandleManager;
  }

  /// <summary>
  /// Shuts down the Client and all associated state(GRPC connections, subscriptions, etc).
  /// The caller must not use any associated data structures(TableHandleManager, TableHandle, etc)
  /// after Dispose() is called. If the caller tries to do so, the behavior is unspecified.
  /// </summary>
  public void Dispose() {
    Console.Error.WriteLine("Client.Dispose: NIY");
  }

  public TableHandleManager ReleaseTableHandleManager() {
    var temp = Manager;
    _manager = null;
    return temp;
  }

  /// <summary>
  /// Gets the TableHandleManager which you can use to create empty tables, fetch tables, and so on.
  /// </summary>
  public TableHandleManager Manager {
    get {
      var result = _manager;
      if (result == null) {
        throw new InvalidOperationException("This Client has no Manager");
      }
      return result;
    }
  }

  /// <summary>
  /// Adds a callback to be invoked when this client is closed.
  /// On close callbacks are invoked before the client is actually shut down,
  /// so they can perform regular client and table manager operations before
  /// closing.
  /// </summary>
  public event Action? OnClose;
};

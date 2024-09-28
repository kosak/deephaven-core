using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
  public static extern Client Connect(string target, ClientOptions options = new ClientOptions());

  /// <summary>
  /// Shuts down the Client and all associated state(GRPC connections, subscriptions, etc).
  /// The caller must not use any associated data structures(TableHandleManager, TableHandle, etc)
  /// after Dispose() is called. If the caller tries to do so, the behavior is unspecified.
  /// </summary>
  public extern void Dispose();

  /// <summary>
  /// Gets a TableHandleManager which you can use to create empty tables, fetch tables, and so on.
  /// You can create more than one TableHandleManager.
  /// </summary>
  /// <returns>The TableHandleManager</returns>
  public extern TableHandleManager GetManager();

  /// <summary>
  /// Adds a callback to be invoked when this client is closed.
  /// On close callbacks are invoked before the client is actually shut down,
  /// so they can perform regular client and table manager operations before
  /// closing.
  /// </summary>
  public event Action? OnClose;
};

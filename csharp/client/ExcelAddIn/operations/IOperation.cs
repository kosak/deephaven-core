namespace Deephaven.DeephavenClient.ExcelAddIn.Operations;

internal interface IOperation {
  /// <summary>
  /// Notifies the operation that there is a valid client.
  /// After this call, the next call to this interface, if any, will be Stop().
  /// That is, the caller will not invoke Status() while there is a valid client.
  /// </summary>
  /// <param name="client"></param>
  void Start(Client client);
  /// <summary>
  /// Notifies the operation that the last client is no longer valid.
  /// The operation should release its resources (close TableHandles, unsubscribe
  /// to tables, etc). If the operation happened to hold on to the Client
  /// that was passed in by Start(), it should forget that reference (set it to null).
  /// However, the operation should *not* call Client.Dispose();
  /// After this call, the next call to this interface, if any, will be Start() or Status().
  /// </summary>
  /// <param name="client"></param>
  void Stop();
  /// <summary>
  /// Send a status message to the clients (progress notes like "Connecting...") or
  /// error messages. It is an invariant that the caller will only invoke Status
  /// messages before Start() or after Stop(). Meaning that the caller will not
  /// send Status messages when there is an active client.
  /// After this call, the next call to this interface, if any, will be Start() or
  /// another Status().
  /// </summary>
  /// <param name="message"></param>
  void Status(string message);
}

namespace Deephaven.DeephavenClient.ExcelAddIn.Operations;

/// <summary>
/// This class is sent to all the registered IOperations when the client changes or
/// the system status changes. When there is no active Client, it is used to send
/// messages like "Connecting" or "Error while attempting to connect".
/// If the Client field is not null, tehre is a new Client, and any current Client
/// held by the operation should be let go (but not Disposed... the caller
/// will dispose Clients). Otherwise, if the Status field is not null,
/// 
/// describes the format of the messages sent to the currently active operations.
/// The message is devoted to either providing a new Client object or explaining why there
/// isn't a client, via a string like (like "not connected yet", or "can't connect to address")
/// </summary>
internal class NewClientOrStatus {
  public readonly Client? Client;
  public readonly string? Status;

  public static NewClientOrStatus Of(Client client) => new(client, null);
  public static NewClientOrStatus Of(string status) => new(null, status);

  private NewClientOrStatus(Client? client, string? status) {
    Client = client;
    Status = status;
  }
}

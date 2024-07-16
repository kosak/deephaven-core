using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.DeephavenClient.ExcelAddIn.Operations;

/// <summary>
/// This class describes the format of the messages sent to the currently active operations.
/// The message is devoted to either providing a new Client object or explaining why there
/// isn't a client, via a string like (like "not connected yet", or "can't connect to address")
/// </summary>
internal class OperationMessage {
  public readonly Client? Client;
  public readonly string? Status;

  public static OperationMessage Of(Client client) => new(client, null);
  public static OperationMessage Of(string status) => new(null, status);

  private OperationMessage(Client? client, string? status) {
    Client = client;
    Status = status;
  }
}

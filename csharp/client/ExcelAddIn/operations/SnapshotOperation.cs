using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Util;

namespace Deephaven.DeephavenClient.ExcelAddIn.Operations;

internal class SnapshotOperation : IOperation {
  private readonly string _tableName;
  private readonly IObserverCollectionSender _sender;

  public SnapshotOperation(string tableName, IObserverCollectionSender sender) {
    _tableName = tableName;
    _sender = sender;
  }

  public void Start(NewClientOrStatus operationMessage) {
    if (operationMessage.Status != null) {
      _sender.OnStatus(operationMessage.Status);
      return;
    }

    if (operationMessage.Client == null) {
      // Impossible.
      return;
    }

    _sender.OnStatus($"Snapshotting \"{_tableName}\"");

    try {
      using var th = operationMessage.Client.Manager.FetchTable(_tableName);
      using var ct = th.ToClientTable();
      // TODO(kosak): Filter the client table here
      var result = Renderer.Render(ct);
      _sender.OnNext(result);
    } catch (Exception ex) {
      _sender.OnError(ex);
    }
  }

  public void Stop() {
    // Nothing to do.
  }
}

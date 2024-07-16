using Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;
using Deephaven.DeephavenClient.ExcelAddIn.Util;

namespace Deephaven.DeephavenClient.ExcelAddIn.Operations;

internal class SubscribeOperation : IOperation {
  private readonly string _tableName;
  private readonly IObserverCollectionSender _sender;
  private TableHandle? _currentTableHandle;
  private SubscriptionHandle? _currentSubHandle;

  public SubscribeOperation(string tableName, IObserverCollectionSender sender) {
    _tableName = tableName;
    _sender = sender;
  }

  public void Start(OperationMessage operationMessage) {
    try {
      if (operationMessage.Status != null) {
        _sender.OnStatus(operationMessage.Status);
        return;
      }

      if (operationMessage.Client == null) {
        // Impossible.
        return;
      }

      _sender.OnStatus($"Subscribing to \"{_tableName}\"");

      _currentTableHandle = operationMessage.Client.Manager.FetchTable(_tableName);
      _currentSubHandle = _currentTableHandle.Subscribe(new MyTickingCallback(_sender));
    } catch (Exception ex) {
      _sender.OnError(ex);
    }
  }

  public void Stop() {
    if (_currentTableHandle == null) {
      return;
    }

    _currentTableHandle.Unsubscribe(_currentSubHandle!);
    _currentSubHandle!.Dispose();
    _currentSubHandle = null;
    _currentTableHandle!.Dispose();
    _currentTableHandle = null;
  }

  private class MyTickingCallback : ITickingCallback {
    private readonly IObserverCollectionSender _sender;

    public MyTickingCallback(IObserverCollectionSender sender) => _sender = sender;

    public void OnTick(TickingUpdate update) {
      try {
        var results = Renderer.Render(update.Current);
        _sender.OnNext(results);
      } catch (Exception ex) {
        _sender.OnError(ex);
      }
    }

    public void OnFailure(string errorText) {
      _sender.OnStatus(errorText);
    }
  }
}

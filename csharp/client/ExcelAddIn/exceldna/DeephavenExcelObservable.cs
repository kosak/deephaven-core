using Deephaven.DeephavenClient.ExcelAddIn.Operations;
using Deephaven.DeephavenClient.ExcelAddIn.Util;
using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn.ExcelDna;

internal sealed class DeephavenExcelObservable : IExcelObservable {
  private readonly OperationManager _operationManager;
  private readonly IOperation _tableOperation;
  private readonly IObserverCollectionManager _collectionManager;

  public DeephavenExcelObservable(OperationManager tableOperationManager, IOperation tableOperation,
    IObserverCollectionManager collectionManager) {
    _operationManager = tableOperationManager;
    _tableOperation = tableOperation;
    _collectionManager = collectionManager;
  }

  public IDisposable Subscribe(IExcelObserver observer) {
    _collectionManager.Add(observer, out var isFirst);

    if (isFirst) {
      _operationManager.Register(_tableOperation);
    }

    return new ActionDisposable(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IExcelObserver observer) {
    _collectionManager.Remove(observer, out var wasLast);
    if (wasLast) {
      _operationManager.Unregister(_tableOperation);
    }
  }
}

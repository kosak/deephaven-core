namespace Deephaven.DeephavenClient.ExcelAddIn.Operations;

internal interface IOperation {
  void Start(OperationMessage operationMessage);
  void Stop();
}

namespace Deephaven.DeephavenClient.ExcelAddIn.Operations;

public interface IOperation {
  void Start(ClientOrStatus clientOrStatus);
  void Stop();
}

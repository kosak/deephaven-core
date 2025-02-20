using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

/// <summary>
/// Common interface for TableProvider, FilteredTableProvider, and DefaultEndpointTableProvider
/// </summary>
public interface ITableProvider : IObservable<StatusOrCounted<TableHandle>> {
  void Start();
}

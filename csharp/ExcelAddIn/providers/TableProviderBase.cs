using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal interface ITableProviderBase : IValueObservable<RefCounted<TableHandle>>,
  IDisposable;

using Deephaven.ExcelAddIn.Status;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal interface ITableProviderBase : IValueObservable<StatusOr<RefCounted<TableHandle>>>;

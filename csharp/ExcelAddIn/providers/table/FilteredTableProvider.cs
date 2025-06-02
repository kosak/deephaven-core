using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;
using Io.Deephaven.Proto.Auth;

namespace Deephaven.ExcelAddIn.Providers;

/**
 * The job of this class is to subscribe to a table provider with the key
 * (endpoint, pqName, tableName, condition). Then, as that table provider provides me
 * with TableHandles or status messages, process them.
 *
 * If the message received was a status message, then forward it to my observers.
 * If it was a TableHandle, then filter it by "condition" in the background, and provide
 * the resulting filtered TableHandle (or error) to my observers.
 */
internal class FilteredTableProvider :
  IValueObserverWithCancel<StatusOr<TableHandle>>,
  IValueObservable<StatusOr<TableHandle>> {
  private const string UnsetParentText = "No Parent TableHandle";
  private const string UnsetTableHandleText = "No Filtered TableHandle";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PqName? _pqName;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private CancellationTokenSource _backgroundTokenSource = new();
  private IObservableCallbacks? _upstreamCallbacks = null;
  private readonly StatusOrHolder<TableHandle> _cachedParentHandle = new(UnsetParentText);
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private readonly StatusOrHolder<TableHandle> _filteredTableHandle = new(UnsetTableHandleText);

  public FilteredTableProvider(StateManager stateManager, EndpointId endpointId,
    PqName? pqName, string tableName, string condition) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _pqName = pqName;
    _tableName = tableName;
    _condition = condition;
  }

  public IObservableCallbacks Subscribe(IValueObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _filteredTableHandle.AddObserverAndNotify(_observers, observer, out var isFirst);
      if (isFirst) {
        var tq = new TableQuad(_endpointId, _pqName, _tableName, "");
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamCallbacks = _stateManager.SubscribeToTable(tq, voc);
      }
    }

    return ObservableCallbacks.Create(Retry, () => RemoveObserver(observer));
  }

  private void Retry() {
    lock (_sync) {
      // if parent has an error, then retry parent
      // otherwise if this has an error, then retry this
      // otherwise (if neither has an error), then retry parent, on the logic that the user
      // is doing a refresh of a healthy table because they feel like it.

      if (!_cachedParentHandle.GetValueOrStatus(out _, out _)) {
        _upstreamCallbacks?.Retry();
        return;
      }

      if (!_filteredTableHandle.GetValueOrStatus(out _, out _)) {
        OnNextHelper();
        return;
      }

      _upstreamCallbacks?.Retry();
    }
  }

  private void RemoveObserver(IValueObserver<StatusOr<TableHandle>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamCallbacks);
      _cachedParentHandle.Replace(UnsetParentText);
      _filteredTableHandle.Replace(UnsetTableHandleText);
    }
  }

  public void OnNext(StatusOr<TableHandle> parentHandle, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      _cachedParentHandle.Replace(parentHandle);
      OnNextHelper();
    }
  }

  private void OnNextHelper() {
    lock (_sync) {
      // Invalidate any background work that might be running.
      _backgroundTokenSource.Cancel();
      _backgroundTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_upstreamTokenSource.Token);

      if (!_cachedParentHandle.GetValueOrStatus(out var ph, out var status)) {
        _filteredTableHandle.ReplaceAndNotify(status, _observers);
        return;
      }

      var progress = StatusOr<TableHandle>.OfTransient("Filtering");
      _filteredTableHandle.ReplaceAndNotify(progress, _observers);

      // RefCounted item gets acquired on this thread.
      var sharedDisposer = Repository.Share(ph);
      var backgroundToken = _backgroundTokenSource.Token;
      Background.Run(() => {
        // RefCounted item gets released on this thread.
        using var cleanup = sharedDisposer;
        OnNextBackground(ph, backgroundToken);
      });
    }
  }

  private void OnNextBackground(TableHandle parentHandle, CancellationToken token) {
    IDisposable? sharedDisposer = null;
    StatusOr<TableHandle> result;
    try {
      // This is a server call that may take some time.
      var childHandle = parentHandle.Where(_condition);
      // The child handle takes a dependency on the parent handle, not because
      // TableHandles have a dependency on each other, but because the parent handle
      // has a transitive dependency on the Client or DndClient, and that's what
      // we need to keep alive while we're alive.
      sharedDisposer = Repository.Register(childHandle, parentHandle);
      result = childHandle;
    } catch (Exception ex) {
      result = ex.Message;
    }
    using var cleanup = sharedDisposer;

    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }
      _filteredTableHandle.ReplaceAndNotify(result, _observers);
    }
  }
}

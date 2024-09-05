﻿using Deephaven.DeephavenClient;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableProvider :
  IObserver<StatusOr<TableHandle>>, IObservable<StatusOr<TableHandle>> {

  public static FilteredTableProvider Create(TableTriple descriptor, SessionProviders sps,
    WorkerThread workerThread, Action onDispose) {

    var result = new FilteredTableProvider(workerThread, onDispose);
    // or don't subscribe if there's no default ugh
    var parentSubscriptionDisposer = sps.LookupAndSubscribeToTableProvider(descriptor, result);
    result.ParentSubscriptionDisposer = parentSubscriptionDisposer;
    return result;
  }

  private readonly WorkerThread _workerThread;
  private readonly ObserverContainer<StatusOr<TableHandle>> _observers = new();
  private StatusOr<TableHandle> _filteredTableHandle = StatusOr<TableHandle>.OfStatus("[No Filtered Table]");

  public IDisposable Subscribe(IObserver<StatusOr<TableHandle>> observer) {
    _workerThread.Invoke(() => {
      _observers.Add(observer, out _);
      observer.OnNext(_tableHandle);
    });

    return _workerThread.InvokeWhenDisposed(() => {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }

      Utility.Exchange(ref _parentSubscriptionDisposer, null)?.Dispose();
      Utility.Exchange(ref _ownerDisposeAction, null)?.Whatever();
      DisposeTableHandleState();
    });
  }

  public void OnNext(StatusOr<TableHandle> tableHandle) {
    // Get onto the worker thread if we're not already on it.
    if (_workerThread.InvokeIfRequired(() => OnNext(tableHandle))) {
      return;
    }

    // If the new state is just a status message, make that our state and transmit to our observers
    if (!tableHandle.GetValueOrStatus(out var th, out var status)) {
      _observers.SetAndSendStatus(ref _filteredTableHandle, status);
      return;
    }

    // It's a real TableHandle so start fetching the table. First notify our observers.
    _observers.SetAndSendStatus(ref _filteredTableHandle, "Filtering");

    Utility.RunInBackground(() => PerformFilterInBackground(th));
  }

  private void PerformFilterInBackground(TableHandle tableHandle, string condition) {
    StatusOr<TableHandle> result;
    try {
      var filtered = tableHandle.Where(condition);
      result = StatusOr<TableHandle>.OfValue(filtered);
    } catch (Exception ex) {
      result = StatusOr<TableHandle>.OfStatus(ex.Message);
    }

    // Then, back on the worker thread, set the result
    _workerThread.Invoke(() => {
      _filteredTableHandle.GetValueOrStatus(out var oldTh, out _);
      _observers.SetAndSend(ref _filteredTableHandle, result);

      // And finally, dispose the old table handle on yet another background thread
      if (oldTh != null) {
        Utility.RunInBackground(oldTh.Dispose);
      }
    });
  }

  private void DisposeTableHandleState() {
    if (_workerThread.InvokeIfRequired(DisposeTableHandleState)) {
      return;
    }

    _ = _filteredTableHandle.GetValueOrStatus(out var oldTh, out _);
    _observers.SetAndSendStatus(ref _filteredTableHandle, "Disposing TableHandle");

    if (oldTh != null) {
      Utility.RunInBackground(oldTh.Dispose);
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}

using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Gui;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn.Providers;

internal class ExcelOperation :
  IValueObserverWithCancel<StatusOr<object?[,]>>,
  IExcelObservable {
  private static Int64 _nextFreeId = 0;

  private readonly string _humanReadableFunction;
  private readonly IValueObservable<StatusOr<object?[,]>> _upstream;
  private readonly StateManager _stateManager;
  private readonly Int64 _uniqueId;
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private readonly ObserverContainer<object?[,]> _observers = new();
  private IObservableCallbacks? _upstreamCallbacks = null;
  private object?[,] _rendered = { { ExcelError.ExcelErrorNA } };

  public ExcelOperation(string humanReadableFunction,
    IValueObservable<StatusOr<object?[,]>> upstream,
    StateManager stateManager) {
    _humanReadableFunction = humanReadableFunction;
    _upstream = upstream;
    _stateManager = stateManager;
    _uniqueId = Interlocked.Increment(ref _nextFreeId);
  }

  public IDisposable Subscribe(IExcelObserver observer) {
    lock (_sync) {
      var wrapped = new ExcelObserverWrapper(observer);
      _observers.AddAndNotify(wrapped, _rendered, out var isFirst);

      if (isFirst) {
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamCallbacks = _upstream.Subscribe(voc);
      }

      return ActionAsDisposable.Create(() => RemoveObserver(wrapped));
    }
  }

  private void RemoveObserver(IValueObserver<object?[,]> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var wasLast);
      if (!wasLast) {
        return;
      }

      _stateManager.RemoveOpStatus(_uniqueId);

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamCallbacks);
    }
  }

  public void OnNext(StatusOr<object?[,]> data, CancellationToken token) {
    var wantToEnsureStatusMonitorVisible = false;
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      StatusOr<Unit> sorUnit;

      if (!data.GetValueOrStatus(out var d, out var status)) {
        var whichError = status.IsFixed ?
          ExcelError.ExcelErrorNA : ExcelError.ExcelErrorGettingData;
        _rendered = new object[,] { { whichError } };
        sorUnit = status;
        wantToEnsureStatusMonitorVisible = status.IsFixed;
      } else {
        _rendered = d;
        sorUnit = Unit.Instance;
      }

      _observers.OnNext(_rendered);
      var opStatus = new OpStatus(_humanReadableFunction, sorUnit, OnUserRetry);
      _stateManager.SetOpStatus(_uniqueId, opStatus);
    }

    // If there was a status message, and it was fixed (rather than just transient)
    // then we will want to ensure that at least one StatusDialog is visible.
    if (wantToEnsureStatusMonitorVisible) {
      StatusMonitorDialogManager.EnsureDialogShown(_stateManager);
      ControlPanelManager.EnsureDialogShown(_stateManager);
    }
  }

  private void OnUserRetry() {
    lock (_sync) {
      _upstreamCallbacks?.Retry();
    }
  }
}

using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Util;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn.Providers;

internal class ExcelOperation :
  IValueObserverWithCancel<StatusOr<object?[,]>>,
  IExcelObservable {
  private static Int64 _nextFreeId = 0;

  private readonly Int64 _uniqueId;
  private readonly string _description;
  private readonly IValueObservable<StatusOr<object?[,]>> _upstream;
  private readonly ZamboniStatusMonitor _statusMonitor = new();
  private readonly object _sync = new();
  private CancellationTokenSource _upstreamTokenSource = new();
  private readonly ObserverContainer<object?[,]> _observers = new();
  private IDisposable? _upstreamDisposer = null;
  private object?[,] _rendered = { { ExcelError.ExcelErrorNA } };

  public ExcelOperation(string description, IValueObservable<StatusOr<object?[,]>> upstream) {
    _uniqueId = Interlocked.Increment(ref _nextFreeId);
    _description = description;
    _upstream = upstream;
  }

  public IDisposable Subscribe(IExcelObserver observer) {
    lock (_sync) {
      var wrapped = new ExcelObserverWrapper(observer);
      _observers.AddAndNotify(wrapped, _rendered, out var isFirst);

      if (isFirst) {
        var voc = ValueObserverWithCancelWrapper.Create(this, _upstreamTokenSource.Token);
        _upstreamDisposer = _upstream.Subscribe(voc);
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

      _statusMonitor.ClearStatus(_uniqueId);

      _upstreamTokenSource.Cancel();
      _upstreamTokenSource = new CancellationTokenSource();

      Utility.ClearAndDispose(ref _upstreamDisposer);
    }
  }

  public void OnNext(StatusOr<object?[,]> data, CancellationToken token) {
    lock (_sync) {
      if (token.IsCancellationRequested) {
        return;
      }

      if (!data.GetValueOrStatus(out var d, out var status)) {
        // store the status in the mega table
        var whichError = status.IsState ?
          ExcelError.ExcelErrorNA : ExcelError.ExcelErrorGettingData;
        _rendered = new object[,] { { whichError } };
        _statusMonitor.SetStatus(_uniqueId, _description, status.Text, status.IsState);
      } else {
        _rendered = d;
        _statusMonitor.ClearStatus(_uniqueId);
      }

      _observers.OnNext(_rendered);
    }
  }
}

public class ZamboniStatusMonitor {
  public void ClearStatus(Int64 id) {

  }

  public void SetStatus(Int64 id, string description, string message, bool serious) {

  }

}


using ExcelDna.Integration;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal sealed class DeephavenHandler : IExcelObservable, IObserver<Unit> {
  private readonly Notifier<Unit> _notifier;
  private readonly ISuperNubbin _superNubbin;
  private readonly object _sync = new();
  private readonly HashSet<IExcelObserver> _observers = new();
  private IDisposable? _clientObserverDisposer = null;

  public DeephavenHandler(Notifier<Unit> notifier, ISuperNubbin superNubbin) {
    _notifier = notifier;
    _superNubbin = superNubbin;
  }

  public IDisposable Subscribe(IExcelObserver observer) {
    bool isFirstObserver;
    // Optimistically assume we will use this.
    var tempDisposer = _notifier.Subscribe(this);
    lock (_sync) {
      isFirstObserver = _observers.Count == 0;
      _observers.Add(observer);
      if (isFirstObserver) {
        _clientObserverDisposer = tempDisposer;
      }
    }

    // If not the first observer, then throw away the observer we just created
    if (!isFirstObserver) {
      tempDisposer.Dispose();
    }

    // if susbscribing, new observer should just wait for the next message
    // if snapshotting, everyone should probably do a redo
    // if first time, both need to do something
    // ugh
    var statusObserver = MakeStatusObserver();
    _superNubbin.OnNewObserver(observer, isFirstObserver, statusObserver);
    return new ActionDisposable(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IExcelObserver observer) {
    IDisposable tempDisposable;
    lock (_sync) {
      _observers.Remove(observer);
      if (_observers.Count != 0) {
        return;
      }

      tempDisposable = _clientObserverDisposer!;
      _clientObserverDisposer = null;
    }

    // No more observers. Stop observing client changes.
    tempDisposable.Dispose();
    _superNubbin.OnLastObserverRemoved();
  }

  void IObserver<Unit>.OnCompleted() {
    // Do nothing (for now)
  }

  void IObserver<Unit>.OnError(Exception error) {
    // Do nothing (for now)
  }

  void IObserver<Unit>.OnNext(Unit _) {
    var statusObserver = MakeStatusObserver();
    _superNubbin.Refresh(statusObserver);
  }

  private IStatusObserver MakeStatusObserver() {
    lock (_sync) {
      return new SuperNubbin666(_observers.ToArray());
    }
  }
}

public sealed class SuperNubbin666 : IStatusObserver {
  private readonly IExcelObserver[] _observers;

  public SuperNubbin666(IExcelObserver[] observers) => _observers = observers;

  public void OnNext(object?[,] result) {
    foreach (var observer in _observers) {
      observer.OnNext(result);
    }
  }
}

public sealed class Unit {
}

public interface IStatusObserver {
  public void OnStatus(string message) {
    var matrix = new object[1, 1];
    matrix[0, 0] = message;
    OnNext(matrix);
  }

  public void OnNext(object?[,] result);

  public void OnError(Exception error) {
    OnStatus(error.Message);
  }
}

public interface ISuperNubbin {
  void Refresh(IStatusObserver statusObserver);
  void OnNewObserver(IExcelObserver observer, bool isFirstObserver, IStatusObserver statusObserver);
  void OnLastObserverRemoved();
}

internal class SnapshotHandler : ISuperNubbin {
  private readonly Lender<ClientOrStatus> _clientLender;
  private readonly string _tableName;
  private readonly TableFilter _filter;

  public SnapshotHandler(Lender<ClientOrStatus> clientLender, string tableName, TableFilter filter) {
    _clientLender = clientLender;
    _tableName = tableName;
    _filter = filter;
  }

  public void OnNewObserver(IExcelObserver newObserver, bool isFirstObserver, IStatusObserver statusObserver) {
    statusObserver.OnStatus($"Snaphotting \"{_tableName}\"");
    Refresh(statusObserver);
  }

  public void OnLastObserverRemoved() {
    // Do nothing.
  }

  public void Refresh(IStatusObserver statusObserver) {
    Task.Run(() => PerformFetchTable(statusObserver));
  }

  private void PerformFetchTable(IStatusObserver statusObserver) {
    using var borrowedClient = _clientLender.Borrow();
    var cos = borrowedClient.Value;
    if (cos.Status != null) {
      statusObserver.OnStatus(cos.Status);
      return;
    }

    if (cos.Client == null) {
      return;
    }

    try {
      using var th = cos.Client.Manager.FetchTable(_tableName);
      using var ct = th.ToClientTable();
      // TODO(kosak): Filter the client table here
      var result = Renderer.Render(ct);
      statusObserver.OnNext(result);
    } catch (Exception ex) {
      statusObserver.OnError(ex);
    }
  }
}

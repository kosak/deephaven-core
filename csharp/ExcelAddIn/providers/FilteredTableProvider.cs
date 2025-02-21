using System.Diagnostics;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

internal class FilteredTableProvider :
  IObserver<StatusOr<View<TableHandle>>>,
  IObservable<StatusOr<View<TableHandle>>> {
  private const string UnsetTableHandleText = "[No Filtered Table]";

  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly PersistentQueryId? _persistentQueryId;
  private readonly string _tableName;
  private readonly string _condition;
  private readonly object _sync = new();
  private IDisposable? _onDispose;
  private IDisposable? _tableHandleSubscriptionDisposer = null;
  private readonly ObserverContainer<StatusOr<View<TableHandle>>> _observers = new();
  private StatusOr<RefCounted<TableHandle>> _filteredTableHandle =
    StatusOr<RefCounted<TableHandle>>.OfStatus(UnsetTableHandleText);
  private object _latestCookie = new();

  public FilteredTableProvider(StateManager stateManager, EndpointId endpointId,
    PersistentQueryId? persistentQueryId, string tableName, string condition,
    IDisposable? onDispose) {
    _stateManager = stateManager;
    _endpointId = endpointId;
    _persistentQueryId = persistentQueryId;
    _tableName = tableName;
    _condition = condition;
    _onDispose = onDispose;

    // Do my subscriptions on a separate thread to avoid rentrancy on StateManager
    Background.Run(Start);
  }

  private void Start() {
    // My parent is a condition-free table that I observe. I provide my observers
    // with that table filtered by a condition.
    var tq = new TableQuad(_endpointId, _persistentQueryId, _tableName, "");
    Debug.WriteLine($"FilteredTableProvider is subscribing to TableHandle with {tq}");
    lock (_sync) {
      _tableHandleSubscriptionDisposer = _stateManager.SubscribeToTable(tq, this);
    }
  }

  public IDisposable Subscribe(IObserver<StatusOr<View<TableHandle>>> observer) {
    lock (_sync) {
      _observers.Add(observer, out _);
      _observers.OnNextOne(observer, _filteredTableHandle.AsView(),
        _filteredTableHandle.Share().AsDisposable());
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<View<TableHandle>>> observer) {
    lock (_sync) {
      _observers.Remove(observer, out var isLast);
      if (!isLast) {
        return;
      }
      Background.Dispose(Utility.Exchange(ref _tableHandleSubscriptionDisposer, null));
      Background.Dispose(Utility.Exchange(ref _onDispose, null));
    }
    ResetTableHandleStateAndNotify("Disposing FilteredTable");
  }

  private void ResetTableHandleStateAndNotify(string statusMessage) {
    lock (_sync) {
      StatusOrCounted.ReplaceWithStatus(ref _filteredTableHandle, statusMessage);
      _observers.OnNext(_filteredTableHandle.AsView(),
        _filteredTableHandle.Share().AsDisposable());
    }
  }

  public void OnNext(StatusOr<View<TableHandle>> parentHandle) {
    if (!parentHandle.GetValueOrStatus(out var th, out var status)) {
      ResetTableHandleStateAndNotify(status);
      return;
    }

    ResetTableHandleStateAndNotify("Filtering");
    // Share here while still on this thread. (Sharing inside the lambda is too late).
    lock (_sync) {
      // Need these two values created in this thread (not in the body of the lambda).
      var cookie = new object();
      var sharedParent = th.Share();
      _latestCookie = cookie;
      Background.Run(() => OnNextBackground(cookie, sharedParent));
    }
  }

  private void OnNextBackground(object versionCookie, RefCounted<TableHandle> parentHandle) {
    using var cleanup1 = parentHandle;

    var newFiltered = StatusOrCounted.Empty<TableHandle>();
    try {
      // This is a server call that may take some time.
      var childHandle = parentHandle.Value.Where(_condition);

      StatusOrCounted.ReplaceWithValue(ref newFiltered, childHandle);
    } catch (Exception ex) {
      StatusOrCounted.ReplaceWithStatus(ref newFiltered, ex.Message ?? "");
    }
    using var cleanup2 = newFiltered.AsDisposable();

    lock (_sync) {
      if (!Object.ReferenceEquals(versionCookie, _latestCookie)) {
        return;
      }
      StatusOrCounted.ReplaceWith(ref _filteredTableHandle, newFiltered.AsView());
      _observers.OnNext(_filteredTableHandle.AsView(),
        _filteredTableHandle.Share().AsDisposable());
    }
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }
}

public static class StatusOrCounted {
  public static StatusOr<RefCounted<T>> Empty<T>() where T : class, IDisposable {
  }

  public static void ReplaceWithStatus<T>(ref StatusOr<RefCounted<T>> target, string statusMessage)
     where T : class, IDisposable {
    Background.Dispose(target.AsDisposable());
    target = StatusOr<RefCounted<T>>.OfStatus(statusMessage);
  }

  public static void ReplaceWithValue<T>(ref StatusOr<RefCounted<T>> target, T value)
    where T : class, IDisposable {
    Background.Dispose(target.AsDisposable());
    var rct = RefCounted<T>.Acquire(value);
    target = StatusOr<RefCounted<T>>.OfValue(rct);
  }

  public static void ReplaceWith<T>(ref StatusOr<RefCounted<T>> target,
    StatusOr<View<T>> view) where T : class, IDisposable {
    Background.Dispose(target.AsDisposable());
    target = Share(view);
  }

  /// <summary>
  /// Shares a StatusOr&lt;RefCounted&lt;T&gt;&gt;
  /// </summary>
  public static StatusOr<RefCounted<T>> Share<T>(this StatusOr<RefCounted<T>> item)
    where T : class, IDisposable {

  }

  /// <summary>
  /// Shares a StatusOr&lt;View&lt;T&gt;&gt;
  /// </summary>
  public static StatusOr<RefCounted<T>> Share<T>(this StatusOr<View<T>> item)
    where T : class, IDisposable {

  }


  /// <summary>
  /// Turns a StatusOr&lt;RefCounted&lt;T&gt;&gt; into a StatusOr&lt;View&lt;T&gt;&gt;
  /// </summary>
  public static StatusOr<View<T>> AsView<T>(this StatusOr<RefCounted<T>> item)
    where T : class, IDisposable{

  }

  /// <summary>
  /// Turns a StatusOr&lt;RefCounted&lt;T&gt;&gt; into an IDisposable
  /// </summary>
  public static IDisposable AsDisposable<T>(this StatusOr<RefCounted<T>> item)
    where T : class, IDisposable {

  }

}

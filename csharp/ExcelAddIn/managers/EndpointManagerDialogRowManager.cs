using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.ViewModels;

namespace Deephaven.ExcelAddIn.Managers;

public sealed class EndpointManagerDialogRowManager :
  IObserver<StatusOr<EndpointConfigBase>>,
  IObserver<StatusOr<EndpointHealth>>,
  IObserver<EndpointId?>,
  IDisposable {

  public static EndpointManagerDialogRowManager Create(EndpointManagerDialogRow row,
    EndpointId endpointId, StateManager stateManager) {
    var result = new EndpointManagerDialogRowManager(row, endpointId, stateManager);
    result.Resubscribe();
    return result;
  }

  public static void TryDeleteBatch(EndpointManagerDialogRowManager[] managers,
    Action<Dictionary<EndpointId, bool>> resultsAction) {
    var sync = new object();
    var results = new Dictionary<EndpointId, bool>();
    var remaining = managers.Length;

    void ProcessResult(EndpointId endpointId, bool success) {
      bool transmitResults;
      lock (sync) {
        // Use TryAdd in case our caller is being annoying and passes the same manager twice
        _ = results.TryAdd(endpointId, success);
        --remaining;
        transmitResults = remaining == 0;
      }

      if (!transmitResults) {
        return;
      }

      resultsAction(results);
    }

    foreach (var manager in managers) {
      manager.DoDelete(ProcessResult);
    }
  }


  private readonly EndpointManagerDialogRow _row;
  private readonly EndpointId _endpointId;
  private readonly StateManager _stateManager;
  private readonly WorkerThread _workerThread;
  private readonly List<IDisposable> _disposables = new();

  private EndpointManagerDialogRowManager(EndpointManagerDialogRow row, EndpointId endpointId,
    StateManager stateManager) {
    _row = row;
    _endpointId = endpointId;
    _stateManager = stateManager;
    _workerThread = stateManager.WorkerThread;
  }

  public void Dispose() {
    Unsubscribe();
  }

  private void Resubscribe() {
    if (_workerThread.EnqueueOrNop(Resubscribe)) {
      return;
    }

    if (_disposables.Count != 0) {
      throw new Exception("State error: already subscribed");
    }
    // We watch for session and credential state changes in our ID
    var d1 = _stateManager.SubscribeToEndpointHealth(_endpointId, this);
    var d2 = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
    var d3 = _stateManager.SubscribeToDefaultEndpointSelection(this);
    _disposables.AddRange(new[] { d1, d2, d3 });
  }

  private void Unsubscribe() {
    if (_workerThread.EnqueueOrNop(Unsubscribe)) {
      return;
    }
    var temp = _disposables.ToArray();
    _disposables.Clear();

    foreach (var disposable in temp) {
      disposable.Dispose();
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> ecb) {
    _row.SetCredentialsSynced(ecb);
  }

  public void OnNext(StatusOr<EndpointHealth> eh) {
    _row.SetEndpointHealthSynced(eh);
  }

  public void OnNext(EndpointId? value) {
    _row.SetDefaultEndpointIdSynced(value);
  }

  public void DoEdit() {
    var config = _row.GetEndpointConfigSynced();
    // If we have valid credentials, then make a populated viewmodel.
    // If we don't, then make an empty viewmodel with only Id populated.
    var cvm = config.AcceptVisitor(
      crs => EndpointDialogViewModel.OfIdAndCredentials(_endpointId.Id, crs),
      _ => EndpointDialogViewModel.OfIdButOtherwiseEmpty(_endpointId.Id));
    ConfigDialogFactory.CreateAndShow(_stateManager, cvm, _endpointId);
  }

  public void DoDelete(Action<EndpointId, bool> successOrFailure) {
    if (_workerThread.EnqueueOrNop(() => DoDelete(successOrFailure))) {
      return;
    }

    // Strategy:
    // 1. Unsubscribe to everything
    // 2. Ask the StateManager to delete the endpoint config.
    // 3. If this succeeds, great.
    // 4. If it fails, then there are other users of the endpoint, so resubscribe and
    //    signal that the delete failed.
    Unsubscribe();
    _stateManager.TryDeleteEndpointConfig(_endpointId,
      success => {
        if (!success) {
          Resubscribe();
        }

        successOrFailure(_endpointId, success);
      });
  }

  public void DoReconnect() {
    _stateManager.Reconnect(_endpointId);
  }

  public void DoSetAsDefault() {
    // If the connection is already the default, do nothing.
    if (_row.IsDefault) {
      return;
    }

    _stateManager.SetDefaultEndpointId(_endpointId);
  }

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }
}

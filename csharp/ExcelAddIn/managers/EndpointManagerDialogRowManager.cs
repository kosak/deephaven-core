using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
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

  private readonly object _sync = new();
  private readonly EndpointManagerDialogRow _row;
  private readonly EndpointId _endpointId;
  private readonly StateManager _stateManager;

  private EndpointManagerDialogRowManager(EndpointManagerDialogRow row, EndpointId endpointId,
    StateManager stateManager) {
    _row = row;
    _endpointId = endpointId;
    _stateManager = stateManager;
  }

  public void Dispose() {
    Unsubscribe();
  }

  private void Resubscribe() {
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

  public bool TryDelete() {
    lock (_sync) {
      // Strategy:
      // 1. Unsubscribe to everything
      // 2. Ask the StateManager to delete the endpoint config.
      // 3. If this succeeds, great.
      // 4. If it fails, then there are other users of the endpoint, so resubscribe and
      //    signal that the delete failed.
      Unsubscribe();
      var success = _stateManager.TryDeleteEndpointConfig(_endpointId);
      if (!success) {
        Resubscribe();
      }
      return success;
    }
  }

  public void DoReconnect() {
    lock (_sync) {
      _stateManager.Reconnect(_endpointId);
    }
  }

  public void DoSetAsDefault() {
    lock (_sync) {
      // If the connection is already the default, do nothing.
      if (_row.IsDefault) {
        return;
      }

      _stateManager.SetDefaultEndpoint(_endpointId);
    }
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

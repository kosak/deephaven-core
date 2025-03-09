using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Viewmodels;
using Deephaven.ExcelAddIn.ViewModels;

namespace Deephaven.ExcelAddIn.Managers;

/// <summary>
/// How to parse the name: this is the Manager for the EndpointManagerDialogRow.
/// </summary>
public sealed class EndpointManagerDialogRowManager :
  IValueObserver<StatusOr<EndpointConfigBase>>,
  IValueObserver<StatusOr<EndpointHealth>>,
  IValueObserver<StatusOr<EndpointId>>,
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
  private IDisposable? _healthDisposable;
  private IDisposable? _configDisposable;
  private IDisposable? _defaultEndpointDisposable;

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
    lock (_sync) {
      // paranoia
      Unsubscribe();

      // We watch for session and credential state changes in our ID
      _healthDisposable = _stateManager.SubscribeToEndpointHealth(_endpointId, this);
      _configDisposable = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
      _defaultEndpointDisposable = _stateManager.SubscribeToDefaultEndpoint(this);
    }
  }

  private void Unsubscribe() {
    lock (_sync) {
      Utility.ClearAndDispose(ref _healthDisposable);
      Utility.ClearAndDispose(ref _configDisposable);
      Utility.ClearAndDispose(ref _defaultEndpointDisposable);
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> ecb) {
    _row.SetCredentials(ecb);
  }

  public void OnNext(StatusOr<EndpointHealth> eh) {
    _row.SetEndpointHealthSynced(eh);
  }

  public void OnNext(StatusOr<EndpointId> value) {
    var (ep, _) = value;
    _row.SetDefaultEndpointId(ep);
  }

  public void DoEdit() {
    var config = _row.GetEndpointConfig();
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
      var success = _stateManager.TryDeleteConfig(_endpointId);
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
}

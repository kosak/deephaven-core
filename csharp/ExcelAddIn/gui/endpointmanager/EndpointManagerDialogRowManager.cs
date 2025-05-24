using Deephaven.ExcelAddIn;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;
using ExcelAddIn.gui.config;

namespace ExcelAddIn.gui.endpointmanager;

/// <summary>
/// How to parse the name: this is the Manager for the EndpointManagerDialogRow.
/// This should not be confused with EndpointManagerDialogManager, which is the
/// Manager for the EndpointManagerDialog.
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
  public readonly EndpointId EndpointId;
  private readonly StateManager _stateManager;
  private IDisposable? _healthDisposable;
  private IDisposable? _configDisposable;
  private IDisposable? _defaultEndpointDisposable;

  private EndpointManagerDialogRowManager(EndpointManagerDialogRow row, EndpointId endpointId,
    StateManager stateManager) {
    _row = row;
    EndpointId = endpointId;
    _stateManager = stateManager;
  }

  public void Dispose() {
    Unsubscribe();
  }

  private void Resubscribe() {
    lock (_sync) {
      // paranoia
      Unsubscribe();

      // We watch for endpoint health, endpoint config, and default endpoint
      _healthDisposable = _stateManager.SubscribeToEndpointHealth(EndpointId, this);
      _configDisposable = _stateManager.SubscribeToEndpointConfig(EndpointId, this);
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
    // If we get any status message (meaning there's no config) we're going
    // to translate for our purposes into an "empty config with this endpoint ID".
    if (!ecb.GetValueOrStatus(out var config, out _)) {
      config = EndpointConfigBase.OfEmpty(new EndpointId(_row.Id));
    }
    _row.SetConfig(config);
  }

  public void OnNext(StatusOr<EndpointHealth> eh) {
    _row.SetEndpointHealthSynced(eh);
  }

  public void OnNext(StatusOr<EndpointId> value) {
    var (ep, _) = value;
    _row.SetDefaultEndpointId(ep);
  }

  public void DoEdit() {
    var config = _row.GetConfig();
    // If we have valid credentials, then make a populated viewmodel.
    // If we don't, then make an empty viewmodel with only Id populated.
    var cvm = EndpointDialogViewModel.OfConfig(config);
    ConfigDialogFactory.CreateAndShow(_stateManager, cvm, EndpointId);
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
      var success = _stateManager.TryDeleteConfig(EndpointId);
      if (!success) {
        Resubscribe();
      }
      return success;
    }
  }

  public void DoReconnect() {
    lock (_sync) {
      _stateManager.Reconnect(EndpointId);
    }
  }

  public void DoSetAsDefault() {
    lock (_sync) {
      // If the connection is already the default, do nothing.
      if (_row.IsDefault) {
        return;
      }

      _stateManager.SetDefaultEndpoint(EndpointId);
    }
  }
}

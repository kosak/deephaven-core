using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Gui;

/// <summary>
/// How to parse the name: this is the Manager for the EndpointManagerRow.
/// </summary>
public sealed class EndpointManagerRowManager :
  IValueObserver<StatusOr<EndpointConfigBase>>,
  IValueObserver<StatusOr<EndpointHealth>>,
  IValueObserver<StatusOr<EndpointId>>,
  IDisposable {

  public static EndpointManagerRowManager Create(EndpointManagerRow row,
    EndpointId endpointId, StateManager stateManager) {
    var result = new EndpointManagerRowManager(row, endpointId, stateManager);
    result.Subscribe();
    return result;
  }

  private readonly object _sync = new();
  private readonly EndpointManagerRow _row;
  public readonly EndpointId EndpointId;
  private readonly StateManager _stateManager;
  private IDisposable? _healthDisposable;
  private IDisposable? _configDisposable;
  private IDisposable? _defaultEndpointDisposable;

  private EndpointManagerRowManager(EndpointManagerRow row, EndpointId endpointId,
    StateManager stateManager) {
    _row = row;
    EndpointId = endpointId;
    _stateManager = stateManager;
  }

  public void Dispose() {
    Unsubscribe();
  }

  private void Subscribe() {
    lock (_sync) {
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
    var isDefault = _row.IsDefault;

    // If we have valid credentials, then make a populated viewmodel.
    // If we don't, then make an empty viewmodel with only Id populated.
    var cvm = ConfigDialogViewModel.OfConfig(config, isDefault);
    ConfigDialogFactory.CreateAndShow(_stateManager, cvm, EndpointId);
  }

  public void DoDelete() {
    lock (_sync) {
      Unsubscribe();
      _stateManager.DeleteConfig(EndpointId);
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

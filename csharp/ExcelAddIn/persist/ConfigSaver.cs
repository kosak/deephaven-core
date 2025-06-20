using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Persist;

public class ConfigSaver : IValueObserver<SharableDict<EndpointConfigBase>>,
  IValueObserver<StatusOr<EndpointId>> {
  private record State(
    SharableDict<EndpointConfigBase> Dict,
    EndpointId? Endpoint);

  private readonly object _sync = new();
  private State _currentState;

  public ConfigSaver(SharableDict<EndpointConfigBase> dict, EndpointId? endpoint) {
    _currentState = new State(dict, endpoint);
  }

  public void OnNext(SharableDict<EndpointConfigBase> dict) {
    lock (_sync) {
      var nextState = _currentState with { Dict = dict };
      MaybeInvokeWrite(nextState);
    }
  }

  public void OnNext(StatusOr<EndpointId> value) {
    lock (_sync) {
      value.Deconstruct(out var ep, out _);
      var nextState = _currentState with { Endpoint = ep };
      MaybeInvokeWrite(nextState);
    }
  }

  private void MaybeInvokeWrite(State nextState) {
    lock (_sync) {
      if (_currentState.Equals(nextState)) {
        return;
      }

      _currentState = nextState;

      var endpoints = _currentState.Dict.Values.ToArray();
      var epId = _currentState.Endpoint != null ? _currentState.Endpoint.Id : "";
      var pc = new PersistedConfig(endpoints, epId);
      _ = PersistedConfigManager.TryWriteConfigFile(pc);
    }
  }
}

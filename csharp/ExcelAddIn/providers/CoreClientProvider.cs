﻿using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ManagedClient;

namespace Deephaven.ExcelAddIn.Providers;

/**
 * The job of this class is to observe EndpointConfig notifications for a given EndpointId,
 * and then provide Client notifications. If the EndpointConfig does not refer to a Community
 * Core instance, we notify an error. If it does, we try to connect to that instance in the
 * background. If that connection eventually succeeds, we notify our observers with the
 * corresponding Client object. Otherwise we notify an error.
 */
internal class CoreClientProvider :
  IObserver<StatusOr<EndpointConfigBase>>,
  IObservable<StatusOr<Client>> {
  private const string UnsetClientText = "[No Community Core Client]";
  private readonly StateManager _stateManager;
  private readonly EndpointId _endpointId;
  private readonly object _sync = new();
  private IDisposable? _upstreamDisposer = null;
  private readonly ObserverContainer<StatusOr<Client>> _observers = new();
  private readonly VersionTracker _versionTracker = new();
  private StatusOr<Client> _client = UnsetClientText;

  public CoreClientProvider(StateManager stateManager, EndpointId endpointId) {
    _stateManager = stateManager;
    _endpointId = endpointId;
  }

  /// <summary>
  /// Subscribe to Core client changes
  /// </summary>
  public IDisposable Subscribe(IObserver<StatusOr<Client>> observer) {
    lock (_sync) {
      _observers.AddAndNotify(observer, _client, out var isFirst);
      if (isFirst) {
        _upstreamDisposer = _stateManager.SubscribeToEndpointConfig(_endpointId, this);
      }
    }

    return ActionAsDisposable.Create(() => RemoveObserver(observer));
  }

  private void RemoveObserver(IObserver<StatusOr<Client>> observer) {
    // Do not do this under lock, because we don't want to wait while holding a lock.
    _observers.RemoveAndWait(observer, out var isLast);
    if (!isLast) {
      return;
    }

    // At this point we have no observers.
    IDisposable? disp;
    lock (_sync) {
      disp = Utility.Exchange(ref _upstreamDisposer, null);
    }
    disp?.Dispose();

    // At this point we are not observing anything.
    // Release our Deephaven resource asynchronously.
    lock (_sync) {
      ProviderUtil.SetState(ref _client, UnsetClientText);
    }
  }

  public void OnNext(StatusOr<EndpointConfigBase> credentials) {
    lock (_sync) {
      if (!credentials.GetValueOrStatus(out var cbase, out var status)) {
        ProviderUtil.SetStateAndNotify(ref _client, status, _observers);
        return;
      }

      _ = cbase.AcceptVisitor(
        core => {
          ProviderUtil.SetStateAndNotify(ref _client, "Trying to connect", _observers);
          var cookie = _versionTracker.SetNewVersion();
          Background666.Run(() => OnNextBackground(core, cookie));
          return Unit.Instance;
        },
        _ => {
          // We are a Core entity but we are getting credentials for CorePlus
          ProviderUtil.SetStateAndNotify(ref _client,
            "Enterprise Core+ requires a PQ to be specified", _observers);
          return Unit.Instance;
        });
    }
  }

  private void OnNextBackground(CoreEndpointConfig config, VersionTrackerCookie versionCookie) {
    StatusOr<Client> result;
    try {
      var client = EndpointFactory.ConnectToCore(config);
      result = StatusOr<Client>.OfValue(client);
    } catch (Exception ex) {
      result = ex.Message;
    }
    using var cleanup = result;

    lock (_sync) {
      if (versionCookie.IsCurrent) {
        ProviderUtil.SetStateAndNotify(ref _client, result, _observers);
      }
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

﻿using Grpc.Core;
using Io.Deephaven.Proto.Controller;
using Io.Deephaven.Proto.Controller.Grpc;
using System.Diagnostics;
using Deephaven.ManagedClient;
using Google.Protobuf;

namespace Deephaven.DheClient.Controller;

internal class SubscriptionContext : IDisposable {
  public static SubscriptionContext Create(ControllerApi.ControllerApiClient controllerApi,
    byte[] authCookie) {
    var req = new SubscribeRequest {
      Cookie = ByteString.CopyFrom(authCookie)
    };
    var reader = controllerApi.subscribe(req);

    var cts = new CancellationTokenSource();
    var result = new SubscriptionContext(cts);
    Task.Run(() => result.ProcessNext(reader.ResponseStream), cts.Token).Forget();

    return result;
  }

  /// <summary>
  /// These fields are all protected by a synchronization object
  /// </summary>
  private struct SyncedFields {
    public readonly object SyncRoot = new();
    public long Version = 0;
    public ControllerConfigurationMessage? PqConfig = null;
    public SharableDict<PersistentQueryInfoMessage> PqMap = new();
    public bool FirstBatchDelivered = false;
    public bool Cancelled = false;

    public SyncedFields() {
    }
  }

  private readonly CancellationTokenSource _cts;
  private SyncedFields _synced;

  private SubscriptionContext(CancellationTokenSource cts) {
    _cts = cts;
    _synced = new();
  }

  public void Dispose() {
    lock (_synced.SyncRoot) {
      if (_synced.Cancelled) {
        return;
      }
      _synced.Cancelled = true;
      Monitor.PulseAll(_synced.SyncRoot);
    }
    _cts.Cancel();
    _cts.Dispose();
  }

  private async void ProcessNext(IAsyncStreamReader<SubscribeResponse> rs) {
    try {
      var hasNext = await rs.MoveNext(_cts.Token);
      if (!hasNext) {
        Debug.WriteLine("Subscription stream ended");
        return;
      }
      ProcessResponse(rs.Current);
    } catch (Exception ex) {
      lock (_synced.SyncRoot) {
        if (_synced.Cancelled) {
          return;
        }
      }
      Debug.WriteLine($"Uncaught exception: {ex}");
      return;
    }

    Task.Run(() => ProcessNext(rs), _cts.Token).Forget();
  }

  private void ProcessResponse(SubscribeResponse resp) {
    lock (_synced.SyncRoot) {
      // In non-error cases, we always have a change and have to notify.
      // So we just optimistically notify here to keep the code simple.
      // In error cases this might result in a notification that didn't reflect
      // any changes. This is slightly "wasteful" but not an error.
      ++_synced.Version;
      Monitor.PulseAll(_synced.SyncRoot);
      switch (resp.Event) {
        case SubscriptionEvent.SePut: 
        case SubscriptionEvent.SeBatchEnd: {
          if (resp.Event == SubscriptionEvent.SeBatchEnd) {
            _synced.FirstBatchDelivered = true;
          }
          var qi = resp.QueryInfo;
          if (qi == null) {
            return;
          }

          var serial = qi.Config.Serial;
          Console.WriteLine($"Serial is {serial:x}");
          _synced.PqMap = _synced.PqMap.With(serial, qi);
          return;
        }

        case SubscriptionEvent.SeRemove: {
          var serial = resp.QuerySerial;
          _synced.PqMap = _synced.PqMap.Without(serial);
          return;
        }

        case SubscriptionEvent.SeConfigUpdate: {
          _synced.PqConfig = resp.Config;
          return;
        }

        case SubscriptionEvent.SeUnspecified: {
          Debug.WriteLine($"Got {resp.Event}");
          return;
        }

        default: {
          Debug.WriteLine($"Unhandled case {resp.Event}");
          return;
        }
      }
    }
  }

  public bool Current(out Int64 version,
    out IReadOnlyDictionary<Int64, PersistentQueryInfoMessage> map) {
    lock (_synced.SyncRoot) {
      while (true) {
        if (_synced.Cancelled) {
          version = default;
          map = null;
          return false;
        }
        if (_synced.FirstBatchDelivered) {
          break;
        }
        Monitor.Wait(_synced.SyncRoot);
      }
      version = _synced.Version;
      map = _synced.PqMap;
      return true;
    }
  }

  public bool Next(out bool hasNewerVersion, Int64 version, DateTimeOffset deadline) {
    lock (_synced.SyncRoot) {
      while (true) {
        if (_synced.Cancelled) {
          hasNewerVersion = false;
          return false;
        }
        if (version < _synced.Version) {
          hasNewerVersion = true;
          return true;
        }
        var timeToWait = DateTimeOffset.UtcNow - deadline;
        if (timeToWait <= TimeSpan.Zero) {
          hasNewerVersion = false;
          return true;
        }
        Monitor.Wait(_synced.SyncRoot, timeToWait);
      }
    }
  }

  public bool Next(Int64 version) {
    lock (_synced.SyncRoot) {
      while (true) {
        if (_synced.Cancelled) {
          return false;
        }
        if (version < _synced.Version) {
          return true;
        }
        Monitor.Wait(_synced.SyncRoot);
      }
    }
  }
}

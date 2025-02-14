using Grpc.Core;
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

    var rs = reader.ResponseStream;
    var ct = new CancellationToken();

    var result = new SubscriptionContext(ct);

    Task.Run(() => result.ProcessNext(rs), ct).Forget();

    return result;
  }

  private readonly CancellationToken _cancellation;
  private readonly object _pqSync = new();
  private long _version = 0;
  private ControllerConfigurationMessage? _pqConfig = null;
  private SharableDict<PersistentQueryInfoMessage> _pqMap = new();

  private SubscriptionContext(CancellationToken cancellation) {
    _cancellation = cancellation;
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  private async void ProcessNext(IAsyncStreamReader<SubscribeResponse> rs) {
    var hasNext = await rs.MoveNext(_cancellation);
    if (!hasNext) {
      Debug.WriteLine("Subscription stream ended");
      return;
    }

    ProcessResponse(rs.Current);
    Task.Run(() => ProcessNext(rs), _cancellation).Forget();
  }

  private void ProcessResponse(SubscribeResponse resp) {
    lock (_pqSync) {
      // In non-error cases, we always have a change and have to notify.
      // So we just optimistically notify here to keep the code simple.
      // In error cases this might result in a notification that didn't reflect
      // any changes. This is slightly "wasteful" but not an error.
      ++_version;
      Monitor.PulseAll(_pqSync);
      Console.WriteLine($"Hi, new version, don't judge me {_version} {resp.Event}");
      switch (resp.Event) {
        case SubscriptionEvent.SePut:
        case SubscriptionEvent.SeBatchEnd: {
          var qi = resp.QueryInfo;
          if (qi == null) {
            return;
          }

          var serial = qi.Config.Serial;
          _pqMap = _pqMap.With(serial, qi);
          return;
        }

        case SubscriptionEvent.SeRemove: {
          var serial = resp.QuerySerial;
          _pqMap = _pqMap.Without(serial);
          return;
        }

        case SubscriptionEvent.SeConfigUpdate: {
          _pqConfig = resp.Config;
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
    lock (_pqSync) {
      version = _version;
      map = _pqMap;
      return true;
    }
  }

  public bool Next(out bool hasNewerVersion, Int64 version, DateTimeOffset deadline) {
    lock (_pqSync) {
      while (true) {
        if (version > _version) {
          hasNewerVersion = true;
          return true;
        }
        var timeToWait = DateTimeOffset.UtcNow - deadline;
        if (timeToWait <= TimeSpan.Zero) {
          hasNewerVersion = false;
          return true;
        }
        Monitor.Wait(_pqSync, timeToWait);
      }
    }
  }

  public bool Next(Int64 version) {
    lock (_pqSync) {
      while (true) {
        if (version > _version) {
          return true;
        }
        Monitor.Wait(_pqSync);
      }
    }
  }
}

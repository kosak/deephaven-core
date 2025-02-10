using Grpc.Core;
using Grpc.Net.Client;
using Io.Deephaven.Proto.Controller;
using Io.Deephaven.Proto.Controller.Grpc;
using System.Diagnostics;
using Deephaven.ManagedClient;
using Google.Protobuf;

namespace Deephaven.DheClient.Controller;

internal class SubscriptionContext : IDisposable {
  public static SubscriptionContext Create(GrpcChannel channel, byte[] authCookie) {
    var controllerApi = new ControllerApi.ControllerApiClient(channel);

    var req = new SubscribeRequest {
      Cookie = ByteString.CopyFrom(authCookie)
    };
    var reader = controllerApi.subscribe(req);

    var rs = reader.ResponseStream;
    var ct = new CancellationToken();

    var result = new SubscriptionContext(controllerApi, ct);

    Task.Run(() => result.ProcessNext(rs, ct), ct).Forget();

    return result;
  }

  private readonly ControllerApi.ControllerApiClient _controllerApi;
  private readonly CancellationToken _cancellation;
  private readonly object _pqSync = new();
  private long _version = 0;
  private ControllerConfigurationMessage? _pqConfig = null;
  private readonly Dictionary<Int64, PersistentQueryInfoMessage> _pqMap = new();

  private SubscriptionContext(ControllerApi.ControllerApiClient controllerApi,
    CancellationToken cancellation) {
    _controllerApi = controllerApi;
    _cancellation = cancellation;
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  private async void ProcessNext(IAsyncStreamReader<SubscribeResponse> rs,
    CancellationToken ct) {
    var hasNext = await rs.MoveNext(ct);
    if (!hasNext) {
      Debug.WriteLine("Subscription stream ended");
      return;
    }

    ProcessResponse(rs.Current);
    Task.Run(() => ProcessNext(rs, ct), ct).Forget();
  }

  private void ProcessResponse(SubscribeResponse resp) {
    lock (_pqSync) {
      // In non-error cases, we always have a change and have to notify.
      // So, just notify in all cases because it's simpler.
      ++_version;
      Monitor.PulseAll(_pqSync);
      switch (resp.Event) {
        case SubscriptionEvent.SePut:
        case SubscriptionEvent.SeBatchEnd: {
          var qi = resp.QueryInfo;
          if (qi == null) {
            return;
          }

          var serial = qi.Config.Serial;
          _pqMap[serial] = qi;
          return;
        }

        case SubscriptionEvent.SeRemove: {
          var serial = resp.QuerySerial;
          _pqMap.Remove(serial);
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
}

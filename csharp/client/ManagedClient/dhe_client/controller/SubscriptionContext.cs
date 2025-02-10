using Deephaven.DheClient.Session;
using Grpc.Core;
using Grpc.Net.Client;
using Io.Deephaven.Proto.Controller;
using Io.Deephaven.Proto.Controller.Grpc;
using System.Diagnostics;

namespace Deephaven.DheClient.Controller;

internal class SubscriptionContext : IDisposable {
  public static SubscriptionContext Create(GrpcChannel channel) {
    var controllerApi = new ControllerApi.ControllerApiClient(channel);
    return new SubscriptionContext(controllerApi);
  }

  private readonly ControllerApi.ControllerApiClient _controllerApi;
  private readonly object _pqSync = new();
  private long _version = 0;
  private ControllerConfigurationMessage? _pqConfig = null;
  private readonly Dictionary<Int64, PersistentQueryInfoMessage> _pqMap = new();

  private SubscriptionContext(ControllerApi.ControllerApiClient controllerApi) {
    _controllerApi = controllerApi;
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  public void Superpain666() {
    var req = new SubscribeRequest {
      Cookie = MegaCookie666.moarCookie
    };
    var reader = _controllerApi.subscribe(req);

    var rs = reader.ResponseStream;
    var ct = new CancellationToken();

    Task.Run(() => SuperZamboniHate(rs, ct), ct).Forget();
  }

  private async void SuperZamboniHate(IAsyncStreamReader<SubscribeResponse> rs,
    CancellationToken ct) {
    var hasNext = await rs.MoveNext(ct);
    if (!hasNext) {
      Debug.WriteLine("Subscription stream ended");
      return;
    }

    ProcessResponse(rs.Current);
    Task.Run(() => SuperZamboniHate(rs, ct), ct).Forget();
  }

  private void ProcessResponse(SubscribeResponse resp) {
    lock (_pqSync) {
      // Optimistically assume a change will happen and we have to notify.
      ++_version;
      Monitor.PulseAll(_pqSync);
      Console.WriteLine($"Hi event is {resp.Event}");
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

public static class TaskExtensions {
  public static void Forget(this Task task) {
    if (!task.IsCompleted || task.IsFaulted) {
      _ = ForgetAwaited(task);
    }

    static async Task ForgetAwaited(Task task) {
      await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }
  }
}

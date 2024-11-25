namespace Deephaven.ManagedClient;

public interface ITickingCallback {
  void OnTick(TickingUpdate update);
  void OnFailure(Exception e);
}

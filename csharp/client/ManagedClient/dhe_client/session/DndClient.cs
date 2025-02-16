using Deephaven.ManagedClient;

namespace Deephaven.DheClient.Session;

public class DndClient : Client {
  public static DndClient Create(Int64 pqSerial, Client client) {
    var thm = client.ReleaseTableHandleManager();
    return new DndClient(thm);
  }
  public DndClient(TableHandleManager tableHandleManager) : base(tableHandleManager) { }
}

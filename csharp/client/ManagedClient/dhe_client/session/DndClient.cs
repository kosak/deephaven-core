using Deephaven.ManagedClient;

namespace Deephaven.DheClient.Session;

public class DndClient : Client {
  public static DndClient Create(Int64 pqSerial, Client client) {
    throw new NotImplementedException();
  }
  public DndClient(TableHandleManager tableHandleManager) : base(tableHandleManager) { }
}

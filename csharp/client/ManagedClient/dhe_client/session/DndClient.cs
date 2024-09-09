using Deephaven.ManagedClient;

namespace Deephaven.DheClient.Session;

public class DndClient : Client {
  public DndClient(TableHandleManager tableHandleManager) : base(tableHandleManager) { }
}

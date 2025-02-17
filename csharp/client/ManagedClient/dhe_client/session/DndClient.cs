using Deephaven.ManagedClient;

namespace Deephaven.DheClient.Session;

public class DndClient : Client {
  public static DndClient Create(Int64 pqSerial, Client client) {
    var thm = client.ReleaseTableHandleManager();
    var (consoleId, server) = thm.ReleaseServer();
    var dndThm = new DndTableHandleManager(consoleId, server);
    return new DndClient(pqSerial, dndThm);
  }

  public readonly Int64 PqSerial;

  private DndClient(Int64 pqSerial, DndTableHandleManager tableHandleManager)
    : base(tableHandleManager) {
    PqSerial = pqSerial;
  }

  /// <summary>
  /// Gets the TableHandleManager which you can use to create empty tables, fetch tables,
  /// and so on.
  /// </summary>
  public new DndTableHandleManager Manager => (DndTableHandleManager)base.Manager;
}

using Deephaven.DeephavenClient.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.DeephavenClient;

public class Client : IDisposable {
  internal NativePtr<NativeClient> Self;
  public TableHandleManager Manager;

  public static Client Connect(string target, ClientOptions options) {
    NativeClient.deephaven_client_Client_Connect(target, options.self, out var clientResult, out var status1);
    status1.OkOrThrow();
    NativeClient.deephaven_client_Client_GetManager(clientResult, out var managerResult, out var status2);
    status2.OkOrThrow();
    var manager = new TableHandleManager(managerResult);
    return new Client(clientResult, manager);
  }

  private Client(NativePtr<Native.Client> self, TableHandleManager manager) {
    this.self = self;
    this.Manager = manager;
  }

  ~Client() {
    Dispose();
  }

  public void Dispose() {
    if (self.ptr == IntPtr.Zero) {
      return;
    }
    Native.Client.deephaven_client_Client_dtor(self);
    self.ptr = IntPtr.Zero;
    GC.SuppressFinalize(this);
  }
}

internal class NativeClient {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Client_Connect(string target,
    NativePtr<ClientOptions> options,
    out NativePtr<Client> result,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Client_dtor(NativePtr<Client> self);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Client_Close(NativePtr<Client> self,
    out ErrorStatus status);

  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Client_GetManager(NativePtr<Client> self,
    out NativePtr<TableHandleManager> result,
    out ErrorStatus status);
}

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Operations;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.Util;
using Deephaven.ExcelAddIn.ViewModels;
using Deephaven.ExcelAddIn.Views;
using ExcelAddIn.views;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn;

internal class MySessionObserver : IObserver<AddOrRemove<SessionId>> {
  private readonly StateManager _stateManager;
  private readonly ConnectionManagerDialog _cmDialog;

  public MySessionObserver(StateManager stateManager, ConnectionManagerDialog cmDialog) {
    _stateManager = stateManager;
    _cmDialog = cmDialog;
  }

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnNext(AddOrRemove<SessionId> aor) {
    if (!aor.IsAdd) {
      // TODO(kosak)
      Debug.WriteLine("Remove is not handled");
      return;
    }

    // Add a row to the form
    // Wire up the OnClick button for that row
    // subscribe to the 

    var statusRow = new HyperZamboniRow(aor.Value.ToString(), "[disconnected]");
    var subPain666 = new SubPain666(statusRow);
    // TODO(kosak): what now
    var subPainDisposable = _stateManager.SubscribeToSession(aor.Value, subPain666);

    var onClick = () => {
      Debug.WriteLine($"I {aor.Value.Id} WAS CLICKED");
    };

    // Not sure what the deal is with threading and BindingSource,
    // so I'll Invoke it to get this change on the GUI thread.
    _cmDialog.Invoke(() => {
      _cmDialog.AddRow(statusRow, onClick);
    });
  }
}

public class SubPain666 : IObserver<StatusOr<UnifiedSession>> {
  private readonly HyperZamboniRow _statusRow;

  public SubPain666(HyperZamboniRow statusRow) {
    _statusRow = statusRow;
  }

  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }

  public void OnNext(StatusOr<UnifiedSession> value) {
    _statusRow.Status = value.TryGetValue(out _, out var status) ? "[Connected]" : status;
  }
}

public sealed class HyperZamboniRow(string id, string status) : INotifyPropertyChanged {
  public event PropertyChangedEventHandler? PropertyChanged;

  public string Id => id;

  public string Status {
    get => status;
    set {
      if (value == status) {
        return;
      }

      status = value;
      OnPropertyChanged();
    }
  }

  private void OnPropertyChanged([CallerMemberName] string? name = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}

public static class DeephavenExcelFunctions {
  private static readonly ConnectionDialogViewModel ConnectionDialogViewModel = new ();
  private static readonly EnterpriseConnectionDialogViewModel EnterpriseConnectionDialogViewModel = new ();
  private static readonly StateManager StateManager = new();

  [ExcelCommand(MenuName = "Deephaven", MenuText = "Connections")]
  public static void ManagedConnections() {
    var cmDialog = new ConnectionManagerDialog();
    var mso = new MySessionObserver(StateManager, cmDialog);
    var disposer = StateManager.SubscribeToSessions(mso);
    // TODO(kosak): where does disposer go. Maybe the Form's closed event?
    cmDialog.Show();

    var t = new Thread(() => {
      Thread.Sleep(10 * 1000);
      var creds = UnifiedCredentials.OfCore("localhost:10000");
      StateManager.SetCredentials(new SessionId("c1"), creds);
    }) { IsBackground = true };
    t.Start();

    var zamboniTime = new CredentialsDialogViewModel();
    new CredentialsDialog(zamboniTime).Show();
  }

  [ExcelFunction(Description = "Snapshots a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SNAPSHOT(string tableDescriptor, object filter, object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var td, out var filt, out var wh, out var errorText)) {
      return errorText;
    }

    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SNAPSHOT";
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    ExcelObservableSource eos = () => new SnapshotOperation(td!, filt, wh, StateManager);
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }

  [ExcelFunction(Description = "Subscribes to a table", IsThreadSafe = true)]
  public static object DEEPHAVEN_SUBSCRIBE(string tableDescriptor, object filter, object wantHeaders) {
    if (!TryInterpretCommonArgs(tableDescriptor, filter, wantHeaders, out var td, out var filt, out var wh, out string errorText)) {
      return errorText;
    }
    var parms = new[] { tableDescriptor, filter, wantHeaders };
    // These two are used by ExcelDNA to share results for identical invocations. The functionName is arbitary but unique.
    const string functionName = "Deephaven.ExcelAddIn.DeephavenExcelFunctions.DEEPHAVEN_SUBSCRIBE";
    ExcelObservableSource eos = () => new SubscribeOperation(td, filt, wh, StateManager);
    return ExcelAsyncUtil.Observe(functionName, parms, eos);
  }

  private static bool TryInterpretCommonArgs(string tableDescriptor, object filter, object wantHeaders,
    out TableDescriptor? tableDescriptorResult, out string filterResult, out bool wantHeadersResult, out string errorText) {
    filterResult = "";
    wantHeadersResult = false;
    if (!TableDescriptor.TryParse(tableDescriptor, out tableDescriptorResult, out errorText)) {
      return false;
    }

    if (!ExcelDnaHelpers.TryInterpretAs(filter, "", out filterResult)) {
      errorText = "Can't interpret FILTER argument";
      return false;
    }


    if (!ExcelDnaHelpers.TryInterpretAs(wantHeaders, false, out wantHeadersResult)) {
      errorText = "Can't interpret WANT_HEADERS argument";
      return false;
    }
    return true;
  }
}

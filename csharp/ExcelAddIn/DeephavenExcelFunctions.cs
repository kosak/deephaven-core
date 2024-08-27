﻿using System.ComponentModel;
using System.Diagnostics;
using Deephaven.ExcelAddIn.ExcelDna;
using Deephaven.ExcelAddIn.Factories;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Operations;
using Deephaven.ExcelAddIn.Providers;
using Deephaven.ExcelAddIn.ViewModels;
using Deephaven.ExcelAddIn.Views;
using ExcelAddIn.views;
using ExcelDna.Integration;

namespace Deephaven.ExcelAddIn;

internal class MySessionObserver : IObserver<AddOrRemove<EndpointId>> {
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

  public void OnNext(AddOrRemove<EndpointId> aor) {
    if (!aor.IsAdd) {
      // TODO(kosak)
      Debug.WriteLine("Remove is not handled");
      return;
    }

    // Add a row to the form
    // Wire up the OnClick button for that row
    // subscribe to the 

    var statusRow = new HyperZamboniRow(aor.Value.HumanReadableString, _stateManager);
    // TODO(kosak): what now
    var subPainDisposable = _stateManager.SubscribeToEndpoint(aor.Value, statusRow);

    // Not sure what the deal is with threading and BindingSource,
    // so I'll Invoke it to get this change on the GUI thread.
    _cmDialog.Invoke(() => {
      _cmDialog.AddRow(statusRow);
    });
  }
}

public sealed class HyperZamboniRow : IObserver<EndpointState>, INotifyPropertyChanged {
  public event PropertyChangedEventHandler? PropertyChanged;

  private readonly StateManager _stateManager;

  private readonly object _sync = new();
  private EndpointState? _endpointState;

  public HyperZamboniRow(string id, StateManager stateManager) {
    Id = id;
    _stateManager = stateManager;
  }

  public string Id { get; init; }

  public string Status {
    get {
      var es = GetEndpointStateUnderLock();
      if (es == null) {
        return "?";
      }
      if (!es.Session.TryGetValue(out _, out var status)) {
        return status;
      }

      return "[Connected]";
    }
  }

  public string ServerType {
    get {
      var creds = GetEndpointStateUnderLock()?.Credentials;
      if (creds == null) {
        return "[Unknown]";
      }

      return creds.Visit(_ => "Core", _ => "Core+");
    }
  }

  public void SettingsClicked() {
    var creds = GetEndpointStateUnderLock()?.Credentials;
    var cvm = creds == null
      ? CredentialsDialogViewModel.OfIdButOtherwiseEmpty(Id)
      : CredentialsDialogViewModel.OfIdAndCredentials(Id, creds);

    var cd = DontKnowDontCare.MakeCredentialsDialog(_stateManager, cvm);
    cd.Show();
  }

  public void ReconnectClicked() {
    _stateManager.Reconnect(new EndpointId(Id));
  }

  public void OnNext(EndpointState value) {
    lock (_sync) {
      _endpointState = value;
    }

    OnPropertyChanged(nameof(Status));
    OnPropertyChanged(nameof(ServerType));
  }

  public void OnCompleted() {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    // TODO(kosak)
    throw new NotImplementedException();
  }

  private EndpointState? GetEndpointStateUnderLock() {
    lock (_sync) {
      return _endpointState;
    }
  }

  private void OnPropertyChanged(string name) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}


public static class DeephavenExcelFunctions {
  private static readonly StateManager StateManager = new();

  [ExcelCommand(MenuName = "Deephaven", MenuText = "Connections")]
  public static void ManagedConnections() {
    var onNewButtonClicked = () => {
      var cvm = CredentialsDialogViewModel.OfEmpty();
      var dialog = CredentialsDialogFactory.Create(StateManager, cvm);
      dialog.Show();
    };
    var cmDialog = new ConnectionManagerDialog(onNewButtonClicked);
    cmDialog.Show();
    var mso = new MySessionObserver(StateManager, cmDialog);
    var disposer1 = StateManager.SubscribeToEndpoints(mso);
    // TODO(kosak): where does disposer go. Maybe the Form's closed event?
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
    out TableTriple? tableDescriptorResult, out string filterResult, out bool wantHeadersResult, out string errorText) {
    filterResult = "";
    wantHeadersResult = false;
    if (!TableTriple.TryParse(tableDescriptor, out tableDescriptorResult, out errorText)) {
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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Deephaven.ExcelAddIn.Models;

namespace Deephaven.ExcelAddIn.Gui;

public sealed class StatusMonitorDialogRow : INotifyPropertyChanged {
  public event PropertyChangedEventHandler? PropertyChanged;

  private readonly object _sync = new();
  private OpStatus _opStatus;

  public StatusMonitorDialogRow(OpStatus opStatus) {
    _opStatus = opStatus;
  }

  public string Function => _opStatus.HumanReadableFunction;

  public string Status {
    get {
      if (_opStatus.Status.GetValueOrStatus(out _, out var status)) {
        return "OK";
      }
      return status.Text;
    }
  }

  public void SetValue(OpStatus newStatus) {
    // We do extra work to avoid sending unnecessary PropertyChanged events.
    // Not sure this is necessary.
    var tempRow = new StatusMonitorDialogRow(newStatus);
    var funcChanged = Function != tempRow.Function;
    var statusChanged = Status != tempRow.Status;

    lock (_sync) {
      _opStatus = newStatus;
    }

    if (funcChanged) {
      OnPropertyChanged(nameof(Function));
    }
    if (statusChanged) {
      OnPropertyChanged(nameof(Status));
    }
  }

  private void OnPropertyChanged([CallerMemberName] string? name = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}

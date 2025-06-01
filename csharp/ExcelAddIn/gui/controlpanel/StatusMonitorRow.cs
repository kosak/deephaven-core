using System.ComponentModel;
using System.Runtime.CompilerServices;
using Deephaven.ExcelAddIn.Models;

namespace Deephaven.ExcelAddIn.Gui;

public sealed class StatusMonitorRow : INotifyPropertyChanged {
  public event PropertyChangedEventHandler? PropertyChanged;

  private readonly object _sync = new();
  private OpStatus _opStatus;

  public StatusMonitorRow(OpStatus opStatus) {
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

  [Browsable(false)]
  public OpStatus OpStatus => _opStatus;

  public void SetValue(OpStatus newStatus) {
    // We do extra work to avoid sending unnecessary PropertyChanged events.
    // Not sure this is necessary.
    var tempRow = new StatusMonitorRow(newStatus);
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

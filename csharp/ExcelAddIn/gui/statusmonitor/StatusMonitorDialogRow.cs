using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Deephaven.ExcelAddIn.Gui;

public sealed class StatusMonitorDialogRow(long id, string function) : INotifyPropertyChanged {
  public event PropertyChangedEventHandler? PropertyChanged;
  private string _status = "N/A";
  private bool _severity = false;

  public long CalcId() {
    return id;
  }

  public string Function => function;

  public string Status {
    get => _status;
    set {
      if (_status == value) {
        return;
      }
      _status = value;
      OnPropertyChanged();
    }
  }

  public bool Severity {
    get => _severity;
    set {
      if (_severity == value) {
        return;
      }
      _severity = value;
      OnPropertyChanged();
    }
  }

  private void OnPropertyChanged([CallerMemberName] string? name = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}

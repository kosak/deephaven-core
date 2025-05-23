using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Deephaven.ExcelAddIn.Viewmodels;

public sealed class StatusMonitorDialogRow(Int64 id, string function) : INotifyPropertyChanged {
  public event PropertyChangedEventHandler? PropertyChanged;
  private string _status = "N/A";
  private bool _severity = false;

  public Int64 CalcId() {
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

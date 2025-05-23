using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Deephaven.ExcelAddIn.Viewmodels;

public sealed class StatusMonitorDialogRow(Int64 id) : INotifyPropertyChanged {
  public event PropertyChangedEventHandler? PropertyChanged;
  /// <summary>
  /// Make a placeholder EmptyConfig based on our id.
  /// </summary>
  private string _function = "N/A";
  private bool _severity = false;

  public string Function {
    get => _function;
    set {
      if (_function == value) {
        return;
      }
      _function = value;
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

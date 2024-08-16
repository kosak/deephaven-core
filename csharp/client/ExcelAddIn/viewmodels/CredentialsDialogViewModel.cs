using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Deephaven.ExcelAddIn.ViewModels;

public sealed class CredentialsDialogViewModel : INotifyPropertyChanged {
  private string _id = "";
  private bool _isCorePlus = true;

  // Core properties
  private string _connectionString = "";

  // Core+ properties
  private string _jsonUrl = "";

  public event PropertyChangedEventHandler? PropertyChanged;

  public string Id {
    get => _id;
    set {
      if (value == _id) {
        return;
      }

      _id = value;
      OnPropertyChanged();
    }
  }

  /**
   * I don't know if I have to do it this way, but I bind IsCore and IsCorePlus to the
   * same underlying variable. The property "IsCore" maps to the inverse of the variable
   * _isCorePlus, meanwhile the property "IsCorePlus" maps to the normal sense of the
   * variable. Setters on either one trigger property change events for both.
   */
  public bool IsCore {
    get {
      var zamboni = !_isCorePlus;
      Debug.WriteLine($"returning IsCore={zamboni}");
      return zamboni;
    }
    set {
      if (_isCorePlus == !value) {
        return;
      }

      _isCorePlus = !value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsCorePlus));
    }
  }

  public bool IsCorePlus {
    get {
      var zamboni = _isCorePlus;
      Debug.WriteLine($"returning IsCorePlus={zamboni}");
      return zamboni;
    }
    set {
      if (_isCorePlus == value) {
        return;
      }

      _isCorePlus = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsCore));
    }
  }

  public string ConnectionString {
    get => _connectionString;
    set {
      if (_connectionString == value) {
        return;
      }

      _connectionString = value;
      OnPropertyChanged();
    }
  }

  public string JsonUrl {
    get => _jsonUrl;
    set {
      if (_jsonUrl == value) {
        return;
      }

      _jsonUrl = value;
      OnPropertyChanged();
    }
  }

  private void OnPropertyChanged([CallerMemberName] string? name = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}

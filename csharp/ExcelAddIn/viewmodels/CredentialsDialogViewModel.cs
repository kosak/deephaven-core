﻿using System.ComponentModel;
using System.Runtime.CompilerServices;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.ViewModels;

public sealed class CredentialsDialogViewModel : INotifyPropertyChanged {
  public static CredentialsDialogViewModel OfEmpty() {
    return new CredentialsDialogViewModel();
  }

  public static CredentialsDialogViewModel OfIdButOtherwiseEmpty(string id) {
    return new CredentialsDialogViewModel { Id = id };
  }

  public static CredentialsDialogViewModel OfIdAndCredentials(string id, CredentialsBase credentials) {
    var result = new CredentialsDialogViewModel { Id = id };
    _ = credentials.AcceptVisitor(
      core => {
        result._isCorePlus = false;
        result.ConnectionString = core.ConnectionString;
        return Unit.Instance;
      },
      corePlus => {
        result._isCorePlus = true;
        result.JsonUrl = corePlus.JsonUrl;
        result.UserId = corePlus.User;
        result.Password = corePlus.Password;
        result.OperateAs = corePlus.OperateAs;
        return Unit.Instance;
      });

    return result;
  }

  private string _id = "";
  private bool _isCorePlus = true;

  // Core properties
  private string _connectionString = "";

  // Core+ properties
  private string _jsonUrl = "";
  private string _userId = "";
  private string _password = "";
  private string _operateAs = "";

  public event PropertyChangedEventHandler? PropertyChanged;

  public bool TryMakeCredentials(out CredentialsBase result) {
    if (_isCorePlus) {
      result = CredentialsBase.OfCorePlus(_jsonUrl, _userId, _password, _operateAs);
      return true;
    }

    result = CredentialsBase.OfCore(_connectionString);
    return true;
  }

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
    get => !_isCorePlus;
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
    get => _isCorePlus;
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

  public string UserId {
    get => _userId;
    set {
      if (_userId == value) {
        return;
      }

      _userId = value;
      OnPropertyChanged();
    }
  }

  public string Password {
    get => _password;
    set {
      if (_password == value) {
        return;
      }

      _password = value;
      OnPropertyChanged();
    }
  }

  public string OperateAs {
    get => _operateAs;
    set {
      if (_operateAs == value) {
        return;
      }

      _operateAs = value;
      OnPropertyChanged();
    }
  }

  public bool TestCredentialsButtonEnabled => false;
  public bool SetCredentialsButtonEnabled => false;

  private void OnPropertyChanged([CallerMemberName] string? name = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}

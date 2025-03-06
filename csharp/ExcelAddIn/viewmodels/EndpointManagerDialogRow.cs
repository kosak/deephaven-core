﻿using System.ComponentModel;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;

namespace Deephaven.ExcelAddIn.Viewmodels;

public sealed class EndpointManagerDialogRow(string id) : INotifyPropertyChanged {

  public event PropertyChangedEventHandler? PropertyChanged;

  private readonly object _sync = new();
  private StatusOr<EndpointConfigBase> _endpointConfig = "[Not set]";
  private StatusOr<EndpointHealth> _endpointHealth = "[Not connected]";
  private EndpointId? _defaultEndpointId = null;

  [DisplayName("Name")]
  public string Id { get; init; } = id;

  public string Status {
    get {
      var health = GetEndpointHealth();
      // If we have a valid session, return "[Connected]", otherwise pass through the status text we have.
      return health.AcceptVisitor(
        _ => "[Connected]",
        status => status);
    }
  }

  [DisplayName("Server Type")]
  public string ServerType {
    get {
      var config = GetEndpointConfig();
      // Nested AcceptVisitor!!
      // If we have valid credentials, determine whether they are for Core or Core+ and return the appropriate string.
      // Otherwise (if we have invalid credentials), ignore their status text and just say "[Unknown]".
      return config.AcceptVisitor(
        crs => crs.AcceptVisitor(_ => "Core", _ => "Core+"),
        _ => "[Unknown]");

    }
  }

  [DisplayName("Default")]
  public bool IsDefault {
    get {
      var id = Id;  // readonly so no synchronization needed.
      var defaultEp = GetDefaultEndpointId();
      return defaultEp != null && defaultEp.Id == id;
    }
  }

  public StatusOr<EndpointConfigBase> GetEndpointConfig() {
    lock (_sync) {
      return _endpointConfig;
    }
  }

  public void SetCredentials(StatusOr<EndpointConfigBase> value) {
    lock (_sync) {
      _endpointConfig = value;
    }

    OnPropertyChanged(nameof(ServerType));
  }

  public EndpointId? GetDefaultEndpointId() {
    lock (_sync) {
      return _defaultEndpointId;
    }
  }

  public void SetDefaultEndpointId(EndpointId? value) {
    lock (_sync) {
      _defaultEndpointId = value;
    }
    OnPropertyChanged(nameof(IsDefault));
  }

  public StatusOr<EndpointHealth> GetEndpointHealth() {
    lock (_sync) {
      return _endpointHealth;
    }
  }

  public void SetEndpointHealthSynced(StatusOr<EndpointHealth> value) {
    lock (_sync) {
      _endpointHealth = value;
    }
    OnPropertyChanged(nameof(Status));
  }

  private void OnPropertyChanged(string name) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}

using System.ComponentModel;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Status;

namespace Deephaven.ExcelAddIn.Viewmodels;

public sealed class EndpointManagerDialogRow(string id) : INotifyPropertyChanged {

  public event PropertyChangedEventHandler? PropertyChanged;

  private readonly object _sync = new();
  /// <summary>
  /// Make a placeholder EmptyConfig based on our id.
  /// </summary>
  private EndpointConfigBase _endpointConfig = EndpointConfigBase.OfEmpty(new EndpointId(id));
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
      return config.AcceptVisitor(_ => "[Unknown]", _ => "Core", _ => "Core+");
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

  public EndpointConfigBase GetEndpointConfig() {
    lock (_sync) {
      return _endpointConfig;
    }
  }

  public void SetCredentials(EndpointConfigBase value) {
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

using System.Text.Json;
using Deephaven.ExcelAddIn.Models;
using System.Text.Json.Serialization;

namespace Deephaven.ExcelAddIn.Persist;

public record PersistedConfig(EndpointConfigBase[] Endpoints,
  string DefaultEndpoint) {
  public static readonly PersistedConfig Empty = new([], "");
}

public static class PersistedConfigManager {
  public static bool TryReadConfigFile(out PersistedConfig result) {
    try {
      var configPath = GetConfigPath();
      var jsonText = File.ReadAllText(configPath);
      result = PersistedConfigFromJson(jsonText);
      return true;
    } catch (Exception) {
      result = PersistedConfig.Empty;
      return false;
    }
  }

  public static bool TryWriteConfigFile(PersistedConfig config) {
    try {
      var configDir = GetConfigDirectory();
      Directory.CreateDirectory(configDir);
      var configPath = GetConfigPath();
      var jsonText = PersistedConfigToJson(config);
      File.WriteAllText(configPath, jsonText);
      return true;
    } catch (Exception) {
      return false;
    }
  }

  private static string PersistedConfigToJson(PersistedConfig config) {
    var endpoints = config.Endpoints.Select(ToJsonHolder).ToArray();
    var jpc = new JsonPersistedConfig { Endpoints = endpoints, DefaultEndpoint = config.DefaultEndpoint };
    var jsonText = JsonSerializer.Serialize(jpc);
    return jsonText;
  }

  private static PersistedConfig PersistedConfigFromJson(string jsonText) {
    var jpc = JsonSerializer.Deserialize<JsonPersistedConfig>(jsonText);
    if (jpc == null) {
      return PersistedConfig.Empty;
    }
    var endpoints = jpc.Endpoints.Select(FromJsonHolder).ToArray();
    return new PersistedConfig(endpoints, jpc.DefaultEndpoint);
  }

  private static JsonEndpointConfigBase ToJsonHolder(EndpointConfigBase ecb) {
    var result = ecb.AcceptVisitor(
      empty => (JsonEndpointConfigBase)new JsonEmptyEndpointConfig(empty),
      core => new JsonCoreEndpointConfig(core),
      corePlus => new JsonCorePlusEndpointConfig(corePlus));

    return result;
  }

  private static EndpointConfigBase FromJsonHolder(JsonEndpointConfigBase ecb) {
    var result = ecb.AcceptVisitor(
      empty => (EndpointConfigBase)new EmptyEndpointConfig(new EndpointId(empty.Id)),
      core => new CoreEndpointConfig(new EndpointId(core.Id), core.ConnectionString),
      corePlus => new CorePlusEndpointConfig(new EndpointId(corePlus.Id),
        corePlus.JsonUrl, corePlus.User, "", corePlus.OperateAs, corePlus.ValidateCertificate));
    return result;
  }

  private static string GetConfigDirectory() {
    var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var result = Path.Combine(folder, "Deephaven Data Labs LLC", "Deephaven Excel Add-In");
    return result;
  }

  private static string GetConfigPath() {
    var folder = GetConfigDirectory();
    var result = Path.Combine(folder, "config.json");
    return result;
  }
}

/// <summary>
/// The version of Deephaven.ExcelAddIn.Models.EmptyEndpointConfigBase, suitable for serialization
/// </summary>
[JsonDerivedType(typeof(JsonEmptyEndpointConfig), "empty")]
[JsonDerivedType(typeof(JsonCoreEndpointConfig), "core")]
[JsonDerivedType(typeof(JsonCorePlusEndpointConfig), "corePlus")]
public abstract class JsonEndpointConfigBase {
  protected JsonEndpointConfigBase() {
  }

  protected JsonEndpointConfigBase(EndpointConfigBase ecb) {
    Id = ecb.Id.Id;
  }

  public string Id { get; set; } = "";

  public abstract T AcceptVisitor<T>(
    Func<JsonEmptyEndpointConfig, T> ofEmpty,
    Func<JsonCoreEndpointConfig, T> ofCore,
    Func<JsonCorePlusEndpointConfig, T> ofCorePlus);
}

/// <summary>
/// The version of Deephaven.ExcelAddIn.Models.EmptyEndpointConfig, suitable for serialization
/// </summary>
public sealed class JsonEmptyEndpointConfig : JsonEndpointConfigBase {
  public JsonEmptyEndpointConfig() {
  }

  public JsonEmptyEndpointConfig(EmptyEndpointConfig config) : base(config) {
  }

  public override T AcceptVisitor<T>(
    Func<JsonEmptyEndpointConfig, T> ofEmpty,
    Func<JsonCoreEndpointConfig, T> ofCore,
    Func<JsonCorePlusEndpointConfig, T> ofCorePlus) {
    return ofEmpty(this);
  }
}

/// <summary>
/// The version of PersistedConfig, suitable for serialization
/// </summary>
public sealed class JsonPersistedConfig {
  public string DefaultEndpoint { get; set; } = "";
  public JsonEndpointConfigBase[] Endpoints { get; set; } = [];
}

/// <summary>
/// The version of Deephaven.ExcelAddIn.Models.CoreEndpointConfig, suitable for serialization
/// </summary>
public sealed class JsonCoreEndpointConfig : JsonEndpointConfigBase {
  public JsonCoreEndpointConfig() {

  }

  public JsonCoreEndpointConfig(CoreEndpointConfig config) : base(config) {
    ConnectionString = config.ConnectionString;
  }

  public string ConnectionString { get; set; } = "";

  public override T AcceptVisitor<T>(
    Func<JsonEmptyEndpointConfig, T> ofEmpty,
    Func<JsonCoreEndpointConfig, T> ofCore,
    Func<JsonCorePlusEndpointConfig, T> ofCorePlus) {
    return ofCore(this);
  }
}

/// <summary>
/// The version of Deephaven.ExcelAddIn.Models.CorePlusEndpointConfig, suitable for serialization
/// </summary>
public sealed class JsonCorePlusEndpointConfig : JsonEndpointConfigBase {
  public JsonCorePlusEndpointConfig() {

  }

  public JsonCorePlusEndpointConfig(CorePlusEndpointConfig config) : base(config) {
    JsonUrl = config.JsonUrl;
    User = config.User;
    OperateAs = config.OperateAs;
    ValidateCertificate = config.ValidateCertificate;
  }

  public string JsonUrl { get; set; } = "";
  public string User { get; set; } = "";
  // note we don't persist password
  public string OperateAs { get; set; } = "";
  public bool ValidateCertificate { get; set; } = false;

  public override T AcceptVisitor<T>(
    Func<JsonEmptyEndpointConfig, T> ofEmpty,
    Func<JsonCoreEndpointConfig, T> ofCore,
    Func<JsonCorePlusEndpointConfig, T> ofCorePlus) {
    return ofCorePlus(this);
  }

}

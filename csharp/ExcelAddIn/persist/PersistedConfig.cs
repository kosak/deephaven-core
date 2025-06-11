using System.Text.Json;
using Deephaven.ExcelAddIn.Models;
using System.Text.Json.Serialization;

namespace Deephaven.ExcelAddIn.Persist;

public static class PersistedConfig {
  public static bool TryReadConfigFile(out IList<EndpointConfigBase> result) {
    try {
      var configPath = GetConfigPath();
      var jsonText = File.ReadAllText(configPath);
      result = ConfigItemsFromJson(jsonText);
      return true;
    } catch (Exception) {
      result = Array.Empty<EndpointConfigBase>();
      return false;
    }
  }

  public static bool TryWriteConfigFile(IEnumerable<EndpointConfigBase> items) {
    try {
      var configDir = GetConfigDirectory();
      Directory.CreateDirectory(configDir);
      var configPath = GetConfigPath();
      var jsonText = ConfigItemsToJson(items);
      File.WriteAllText(configPath, jsonText);
      return true;
    } catch (Exception) {
      return false;
    }
  }

  private static string ConfigItemsToJson(IEnumerable<EndpointConfigBase> items) {
    var holderArray = items.Select(ToJsonHolder).ToArray();
    var holderArrayAsJson = JsonSerializer.Serialize(holderArray);
    return holderArrayAsJson;
  }

  private static IList<EndpointConfigBase> ConfigItemsFromJson(string jsonText) {
    var holderArray = JsonSerializer.Deserialize<JsonEndpointConfigBase[]>(jsonText);
    if (holderArray == null) {
      return Array.Empty<EndpointConfigBase>();
    }
    var configArray = holderArray.Select(FromJsonHolder).ToArray();
    return configArray;
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

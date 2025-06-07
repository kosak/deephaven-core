using System.Text.Json;
using Deephaven.ExcelAddIn.Models;
using System.Text.Json.Serialization;

namespace Deephaven.ExcelAddIn.Persist;

public static class PersistedConfig {
  public static IList<EndpointConfigBase> ReadConfigFile() {
    var configPath = GetConfigPath();
    var holderArrayAsJson = File.ReadAllText(configPath);
    var holderArray = JsonSerializer.Deserialize<JsonEndpointConfigBase[]>(holderArrayAsJson);
    if (holderArray == null) {
      return Array.Empty<EndpointConfigBase>();
    }
    var configArray = holderArray.Select(FromJsonHolder).ToArray();
    return configArray;
  }

  public static bool TryWriteConfigFile(IEnumerable<EndpointConfigBase> items) {
    try {
      var holderArray = items.Select(ToJsonHolder).ToArray();
      var holderArrayAsJson = JsonSerializer.Serialize(holderArray);
      var configPath = GetConfigPath();
      File.WriteAllText(configPath, holderArrayAsJson);
      return true;
    } catch (Exception) {
      return false;
    }
  }

  public static JsonEndpointConfigBase ToJsonHolder(EndpointConfigBase ecb) {
    var result = ecb.AcceptVisitor(
      empty => (JsonEndpointConfigBase)new JsonEmptyEndpointConfig(empty),
      core => new JsonCoreEndpointConfig(core),
      corePlus => new JsonCorePlusEndpointConfig(corePlus));

    return result;
  }

  public static EndpointConfigBase FromJsonHolder(JsonEndpointConfigBase ecb) {
    var result = ecb.AcceptVisitor(
      empty => (EndpointConfigBase)new EmptyEndpointConfig(new EndpointId(empty.Id)),
      core => new CoreEndpointConfig(new EndpointId(core.Id), core.ConnectionString),
      corePlus => new CorePlusEndpointConfig(new EndpointId(corePlus.Id),
        corePlus.JsonUrl, corePlus.User, "", corePlus.OperateAs, corePlus.ValidateCertificate));
    return result;
  }

  private static string GetConfigPath() {
    var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var result = Path.Combine(folder, "Deephaven", "ExcelAddIn", "config.json");
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

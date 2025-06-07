using System.Text.Json;
using Deephaven.ExcelAddIn.Models;
using System.Text.Json.Serialization;

namespace Deephaven.ExcelAddIn.Persist;

public static class PersistedConfig {
  public static string ToJson(EndpointConfigBase ecb) {
    var result = ecb.AcceptVisitor(
      empty => JsonSerializer.Serialize(new JsonEmptyEndpointConfig(empty)),
      core => JsonSerializer.Serialize(new JsonCoreEndpointConfig(core)),
      corePlus => JsonSerializer.Serialize(new JsonCorePlusEndpointConfig(corePlus)));

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
}

/// <summary>
/// The version of Deephaven.ExcelAddIn.Models.EmptyEndpointConfig, suitable for serialization
/// </summary>
public sealed class JsonEmptyEndpointConfig : JsonEndpointConfigBase {
  public JsonEmptyEndpointConfig() {

  }

  public JsonEmptyEndpointConfig(EmptyEndpointConfig config) : base(config) {

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
}

namespace Deephaven.ExcelAddIn.Models;

public abstract class EndpointConfigBase(EndpointId id) {
  public readonly EndpointId Id = id;

  public static EndpointConfigBase OfCore(EndpointId id, string connectionString) {
    return new CoreEndpointConfig(id, connectionString);
  }

  public static EndpointConfigBase OfCorePlus(EndpointId id, string jsonUrl, string userId,
    string password, string operateAs, bool validateCertificate) {
    return new CorePlusEndpointConfig(id, jsonUrl, userId, password, operateAs, validateCertificate);
  }

  public abstract T AcceptVisitor<T>(
    Func<CoreEndpointConfig, T> ofCore,
    Func<CorePlusEndpointConfig, T> ofCorePlus);
}

public sealed class CoreEndpointConfig(
  EndpointId id,
  string connectionString) : EndpointConfigBase(id) {
  public readonly string ConnectionString = connectionString;

  public override T AcceptVisitor<T>(Func<CoreEndpointConfig, T> ofCore,
    Func<CorePlusEndpointConfig, T> ofCorePlus) {
    return ofCore(this);
  }
}

public sealed class CorePlusEndpointConfig(
  EndpointId id,
  string jsonUrl,
  string user,
  string password,
  string operateAs,
  bool validateCertificate) : EndpointConfigBase(id) {
  public readonly string JsonUrl = jsonUrl;
  public readonly string User = user;
  public readonly string Password = password;
  public readonly string OperateAs = operateAs;
  public readonly bool ValidateCertificate = validateCertificate;

  public override T AcceptVisitor<T>(Func<CoreEndpointConfig, T> ofCore,
    Func<CorePlusEndpointConfig, T> ofCorePlus) {
    return ofCorePlus(this);
  }
}

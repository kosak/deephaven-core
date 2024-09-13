namespace Deephaven.ExcelAddIn.Models;

public abstract class ConnectionConfigBase(EndpointId id) {
  public readonly EndpointId Id = id;

  public static ConnectionConfigBase OfCore(EndpointId id, string connectionString, bool sessionTypeIsPython) {
    return new CoreConnectionConfig(id, connectionString, sessionTypeIsPython);
  }

  public static ConnectionConfigBase OfCorePlus(EndpointId id, string jsonUrl, string userId,
    string password, string operateAs, bool validateCertificate) {
    return new CorePlusConnectionConfig(id, jsonUrl, userId, password, operateAs, validateCertificate);
  }

  public abstract T AcceptVisitor<T>(Func<CoreConnectionConfig, T> ofCore,
    Func<CorePlusConnectionConfig, T> ofCorePlus);
}

public sealed class CoreConnectionConfig(
  EndpointId id,
  string connectionString,
  bool sessionTypeIsPython) : ConnectionConfigBase(id) {
  public readonly string ConnectionString = connectionString;
  public readonly bool SessionTypeIsPython = sessionTypeIsPython;

  public override T AcceptVisitor<T>(Func<CoreConnectionConfig, T> ofCore, Func<CorePlusConnectionConfig, T> ofCorePlus) {
    return ofCore(this);
  }
}

public sealed class CorePlusConnectionConfig(EndpointId id, string jsonUrl, string user, string password,
  string operateAs, bool validateCertificate) : ConnectionConfigBase(id) {
  public readonly string JsonUrl = jsonUrl;
  public readonly string User = user;
  public readonly string Password = password;
  public readonly string OperateAs = operateAs;
  public readonly bool ValidateCertificate = validateCertificate;

  public override T AcceptVisitor<T>(Func<CoreConnectionConfig, T> ofCore, Func<CorePlusConnectionConfig, T> ofCorePlus) {
    return ofCorePlus(this);
  }
}

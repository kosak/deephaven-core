namespace Deephaven.ExcelAddIn.Models;

public abstract class EndpointConfigBase(EndpointId id) {
  public readonly EndpointId Id = id;

  public static EmptyEndpointConfig OfEmpty(EndpointId id) {
    return new EmptyEndpointConfig(id);
  }

  public static CoreEndpointConfig OfCore(EndpointId id, string connectionString) {
    return new CoreEndpointConfig(id, connectionString);
  }

  public static CorePlusEndpointConfig OfCorePlus(EndpointId id, string jsonUrl, string userId,
    string password, string operateAs, bool validateCertificate) {
    return new CorePlusEndpointConfig(id, jsonUrl, userId, password, operateAs, validateCertificate);
  }

  public abstract T AcceptVisitor<T>(
    Func<EmptyEndpointConfig, T> ofEmpty,
    Func<CoreEndpointConfig, T> ofCore,
    Func<CorePlusEndpointConfig, T> ofCorePlus);
}

public sealed class EmptyEndpointConfig(
  EndpointId id) : EndpointConfigBase(id) {

  public override T AcceptVisitor<T>(
    Func<EmptyEndpointConfig, T> ofEmpty,
    Func<CoreEndpointConfig, T> ofCore,
    Func<CorePlusEndpointConfig, T> ofCorePlus) {
    return ofEmpty(this);
  }
}

public abstract class PopulatedEndpointConfig(EndpointId id) : EndpointConfigBase(id) {
  public T AcceptVisitor<T>(
    Func<CoreEndpointConfig, T> ofCore,
    Func<CorePlusEndpointConfig, T> ofCorePlus) {
    return AcceptVisitor(
      _ => throw new NotImplementedException("impossible"),
      ofCore,
      ofCorePlus);
  }
}

public sealed class CoreEndpointConfig(
  EndpointId id,
  string connectionString) : PopulatedEndpointConfig(id) {
  public readonly string ConnectionString = connectionString;

  public override T AcceptVisitor<T>(
    Func<EmptyEndpointConfig, T> ofEmpty,
    Func<CoreEndpointConfig, T> ofCore,
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
  bool validateCertificate) : PopulatedEndpointConfig(id) {
  public readonly string JsonUrl = jsonUrl;
  public readonly string User = user;
  public readonly string Password = password;
  public readonly string OperateAs = operateAs;
  public readonly bool ValidateCertificate = validateCertificate;

  public override T AcceptVisitor<T>(
    Func<EmptyEndpointConfig, T> ofEmpty,
    Func<CoreEndpointConfig, T> ofCore,
    Func<CorePlusEndpointConfig, T> ofCorePlus) {
    return ofCorePlus(this);
  }
}

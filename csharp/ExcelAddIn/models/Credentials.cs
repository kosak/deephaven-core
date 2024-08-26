namespace Deephaven.ExcelAddIn.Models;

public abstract class CredentialsBase {
  public static CredentialsBase OfCore(string connectionString) {
    return new CoreCredentials(connectionString);
  }

  public static CredentialsBase OfCorePlus(string jsonUrl, string userId,
    string password, string operateAs) {
    return new CorePlusCredentials(jsonUrl, userId, password, operateAs);
  }

  public abstract T AcceptVisitor<T>(Func<CoreCredentials, T> ofCore,
    Func<CorePlusCredentials, T> ofCorePlus);
}

public sealed class CoreCredentials(string connectionString) : CredentialsBase {
  public readonly string ConnectionString = connectionString;

  public override T AcceptVisitor<T>(Func<CoreCredentials, T> ofCore, Func<CorePlusCredentials, T> ofCorePlus) {
    return ofCore(this);
  }
}

public sealed class CorePlusCredentials(string jsonUrl, string user, string password,
  string operateAs) : CredentialsBase {
  public readonly string JsonUrl = jsonUrl;
  public readonly string User = user;
  public readonly string Password = password;
  public readonly string OperateAs = operateAs;

  public override T AcceptVisitor<T>(Func<CoreCredentials, T> ofCore, Func<CorePlusCredentials, T> ofCorePlus) {
    return ofCorePlus(this);
  }
}

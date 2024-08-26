namespace Deephaven.ExcelAddIn.Models;

public interface ICredentialsVisitor<out T> {
  T Visit(CoreCredentials entity);
  T Visit(CorePlusCredentials entity);
}

// A separate, nongeneric, class so we get type inference.
public static class CredentialsLambdaInvoker {
  public static CredentialsLambdaInvoker<T> Create<T>(Func<CoreCredentials, T> ofCore,
    Func<CorePlusCredentials, T> ofCorePlus) {
    return new CredentialsLambdaInvoker<T>(ofCore, ofCorePlus);
  }
}

public class CredentialsLambdaInvoker<T>(
  Func<CoreCredentials, T> ofCore, Func<CorePlusCredentials, T> ofCorePlus) : ICredentialsVisitor<T> {
  public T Visit(CoreCredentials self) {
    return ofCore(self);
  }

  public T Visit(CorePlusCredentials self) {
    return ofCorePlus(self);
  }
}

public abstract class CredentialsBase {
  public static CredentialsBase OfCore(string connectionString) {
    return new CoreCredentials(connectionString);
  }

  public static CredentialsBase OfCorePlus(string jsonUrl, string userId,
    string password, string operateAs) {
    return new CorePlusCredentials(jsonUrl, userId, password, operateAs);
  }

  public abstract T AcceptVisitor<T>(ICredentialsVisitor<T> visitor);

  public T AcceptVisitor<T>(Func<CoreCredentials, T> ofCore,
    Func<CorePlusCredentials, T> ofCorePlus) {
    var visitor = CredentialsLambdaInvoker.Create(ofCore, ofCorePlus);
    return AcceptVisitor(visitor);
  }
}

public sealed class CoreCredentials(string connectionString) : CredentialsBase {
  public readonly string ConnectionString = connectionString;

  public override T AcceptVisitor<T>(ICredentialsVisitor<T> visitor) {
    return visitor.Visit(this);
  }
}

public sealed class CorePlusCredentials(string jsonUrl, string user, string password,
  string operateAs) : CredentialsBase {
  public readonly string JsonUrl = jsonUrl;
  public readonly string User = user;
  public readonly string Password = password;
  public readonly string OperateAs = operateAs;

  public override T AcceptVisitor<T>(ICredentialsVisitor<T> visitor) {
    return visitor.Visit(this);
  }
}

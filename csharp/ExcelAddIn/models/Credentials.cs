﻿namespace Deephaven.ExcelAddIn.Models;

public abstract class CredentialsBase(string id) {
  public readonly string Id = id;

  public static CredentialsBase OfCore(string id, string connectionString) {
    return new CoreCredentials(id, connectionString);
  }

  public static CredentialsBase OfCorePlus(string id, string jsonUrl, string userId,
    string password, string operateAs) {
    return new CorePlusCredentials(id, jsonUrl, userId, password, operateAs);
  }

  public abstract T AcceptVisitor<T>(Func<CoreCredentials, T> ofCore,
    Func<CorePlusCredentials, T> ofCorePlus);
}

public sealed class CoreCredentials(string id, string connectionString) : CredentialsBase(id) {
  public readonly string ConnectionString = connectionString;

  public override T AcceptVisitor<T>(Func<CoreCredentials, T> ofCore, Func<CorePlusCredentials, T> ofCorePlus) {
    return ofCore(this);
  }
}

public sealed class CorePlusCredentials(string id, string jsonUrl, string user, string password,
  string operateAs) : CredentialsBase(id) {
  public readonly string JsonUrl = jsonUrl;
  public readonly string User = user;
  public readonly string Password = password;
  public readonly string OperateAs = operateAs;

  public override T AcceptVisitor<T>(Func<CoreCredentials, T> ofCore, Func<CorePlusCredentials, T> ofCorePlus) {
    return ofCorePlus(this);
  }
}

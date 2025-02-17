﻿namespace Deephaven.DheClient.Auth;

public abstract class Credentials {
  public static Credentials OfUsernamePassword(string user, string password, string operateAs) {
    return new PasswordCredentials(user, password, operateAs);
  }

  internal class PasswordCredentials(string user, string password,
    string operateAs) : Credentials {
    public readonly string User = user;
    public readonly string Password = password;
    public readonly string OperateAs = operateAs;
  }
}


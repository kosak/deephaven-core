﻿using Google.Protobuf;
using Io.Deephaven.Proto.Auth;

namespace Deephaven.DheClient.Auth;

public static class ClientUtil {
  public static string GetName(string descriptiveName) {
    return DhCoreTodo.GetHostname() + "/" + descriptiveName;
  }

  public static ClientId MakeClientId(string descriptiveName, string uuid) {
    var name = GetName(descriptiveName);
    var clientId = new ClientId {
      Name = name,
      Uuid = ByteString.CopyFromUtf8(uuid)
    };
    return clientId;
  }

}

public static class DhCoreTodo {
  public static string GetHostname() {
    return "TODO-hostname";
  }
}

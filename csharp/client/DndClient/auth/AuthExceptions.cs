namespace DeephavenEnterprise.DndClient;

public enum Tag {
  Retryable,
  NonRetryable
}

public class AuthException : Exception {

  public readonly Tag Tag;

  public AuthException(string msg, Tag tag) : base(msg) {
    Tag = tag;
  }

  bool IsRetryable => Tag == Tag.Retryable;
}

public class AlreadyAuthenticationException : AuthException {
}

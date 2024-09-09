namespace DeephavenEnterprise.DndClient;

public enum Tag {
  Retryable,
  NonRetryable
}

public class AuthException(string msg, Tag tag) : Exception(msg) {
  public readonly Tag Tag = tag;

  bool IsRetryable => tag == Tag.Retryable;
}

public class AlreadyAuthenticatedException() : AuthException("already authenticated", Tag.NonRetryable);

public class AuthenticationFailure(string msg) : AuthException(msg, Tag.NonRetryable);

public class AuthenticationRejected(string msg) : AuthenticationFailure(msg);

public class NotAuthenticatedException(string msg) : AuthException(msg, Tag.Retryable);

public class PubPrivKeyException(string msg) : AuthException(msg, Tag.NonRetryable);

public class TokenCreationException(string msg) : AuthException(msg, Tag.NonRetryable);

public class TokenVerificationException(string msg) : AuthException(msg, Tag.Retryable);

public class UnavailableException(string msg) : AuthException(msg, Tag.Retryable);

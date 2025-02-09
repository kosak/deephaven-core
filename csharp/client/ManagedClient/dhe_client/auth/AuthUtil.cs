using Google.Protobuf;
using Io.Deephaven.Proto.Auth;

namespace Deephaven.DheClient.Auth;

public static class AuthUtil {
  public static Token AuthTokenToProto(AuthToken authToken) {
    var tokenProto = new Token {
      TokenId = authToken.TokenId,
      Service = authToken.Service,
      IpAddress = ByteString.CopyFromUtf8(authToken.OriginIpAddressBytes),
      UserContext = new Io.Deephaven.Proto.Auth.UserContext {
        AuthenticatedUser = authToken.UserContext.User,
        EffectiveUser = authToken.UserContext.EffectiveUser,
      }
    };
    return tokenProto;
  }
}

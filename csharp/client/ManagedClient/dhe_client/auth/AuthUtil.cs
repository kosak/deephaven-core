using Google.Protobuf;
using Io.Deephaven.Proto.Auth;

namespace Deephaven.DheClient.Auth;

public static class AuthUtil {
  public static Token ProtoFromAuthToken(AuthToken authToken) {
    var tokenProto = new Token {
      TokenId = authToken.TokenId,
      Service = authToken.Service,
      IpAddress = authToken.OriginIpAddressBytes,
      UserContext = new Io.Deephaven.Proto.Auth.UserContext {
        AuthenticatedUser = authToken.UserContext.User,
        EffectiveUser = authToken.UserContext.EffectiveUser
      }
    };
    return tokenProto;
  }

  public static AuthToken AuthTokenFromProto(Token token) {
    var uc = new UserContext(token.UserContext.AuthenticatedUser,
      token.UserContext.EffectiveUser);
    return new AuthToken(token.TokenId, token.Service, uc, token.IpAddress);
  }
}

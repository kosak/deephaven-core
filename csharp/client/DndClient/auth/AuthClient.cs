/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */

namespace DeephavenEnterprise.Auth;

/**
 * An authentication Client maintains provides the means to authenticate to
 * a Deephaven Enterprise Authentication Server, and to maintain the
 * authentication status: the protocol requires frequent refreshes of a lease,
 * which this class will do once authentication is established.
 */
public class AuthClient : IDisposable {
  /// <summary>
  /// Factory method to connect to an Authentication Server using the specified options.
  /// </summary>
  /// <param name="descriptiveName">A string describing the client useful for identifying this client in log messages</param>
  /// <param name="target">A connection string in the format host:port.For example "localhost:9031"</param>
  /// <param name="options">An options object for setting options</param>
  /// <returns>An AuthClient object connected to the Authentication Server</returns>
  public static AuthClient Connect(string descriptiveName, string target, ClientOptions? options = null) {
    throw new NotImplementedException("NIY")
  }

  bool IsClosed() {
    throw new NotImplementedException("NIY")
  }

  /**
   * Close and invalidate the client.
   * If a valid authentication with the server exists, this client will attempt
   * to contact the server to invalidate it.
   * If a gRPC channel associated with this client is connected, it will be closed.
   * After this method is called no other method on the client should be called.
   * This method is also called by the destructor if not already called.
   */
  void Close() noexcept;

  /**
   * Ping a server; return and log roundtrip time.
   *
   * @param timeout how long of a timeout, in milliseconds, to use in the Ping request to the server.
   * @return roundtrip time in milliseconds.
   */
  std::chrono::milliseconds Ping(std::chrono::milliseconds timeout);

  [[nodiscard]] bool PasswordAuthentication(
      const std::string &user,
      const std::string &password,
      const std::string &operate_as);
  [[nodiscard]] bool PrivateKeyAuthentication(
      const std::string &private_key_filename);

  [[nodiscard]] AuthToken CreateToken(std::string for_service);
  [[nodiscard]] AuthToken CreateTokenForUser(
      std::string for_service, std::string operate_as);
  [[nodiscard]] bool VerifyToken(
      const AuthToken &token,
      const std::string &for_service);

  //
  // Useful for the R client wrapper code; not exposed in the regular API.
  //
  template<typename A1, typename A2, typename A3, typename A4>
  friend class Rcpp::Constructor_3;
  AuthClient(
      const std::string &descriptive_name,
      const std::string &target,
      const utility::ClientOptions &options = { });

private:
  [[nodiscard]] impl::AuthClientImpl* CheckImpl() const;

  using ImplPtr = std::shared_ptr<impl::AuthClientImpl>;
  explicit AuthClient(ImplPtr impl);
  // We follow the PImpl pattern for the implementation of this object.
  ImplPtr impl_;
};

}  // namespace deephaven_enterprise::auth

}



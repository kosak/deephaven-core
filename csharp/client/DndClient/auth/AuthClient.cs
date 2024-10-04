/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */

using Deephaven.ManagedClient;

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
    throw new NotImplementedException("NIY");
  }

  bool IsClosed() {
    throw new NotImplementedException("NIY");
  }

  /// <summary>
  /// Close and invalidate the client.
  /// If a valid authentication with the server exists, this client will attempt
  /// to contact the server to invalidate it.
  /// If a gRPC channel associated with this client is connected, it will be closed.
  /// After this method is called no other method on the client should be called.
  /// This method is also called by the destructor if not already called.
  /// </summary>
  void Close() {
    throw new NotImplementedException("NIY");
  }

  /// <summary>
  /// Ping a server; return and log roundtrip time.
  /// </summary>
  /// <param name="timeout">How long of a timeout to use in the Ping request to the server</param>
  /// <returns>roundtrip time</returns>
  TimeSpan Ping(TimeSpan timeout) {
    throw new NotImplementedException("NIY");
  }

  bool PasswordAuthentication(string user, string password, string operateAs) {
    throw new NotImplementedException("NIY");
  }


  bool PrivateKeyAuthentication(string privateKeyFilename) {
    throw new NotImplementedException("NIY");
  }

  AuthToken CreateToken(string forService) {
    throw new NotImplementedException("NIY");
  }


  AuthToken CreateTokenForUser(string forService, string operateAs) {
    throw new NotImplementedException("NIY");
  }


  bool VerifyToken(AuthToken token, string forService) {
  }
}



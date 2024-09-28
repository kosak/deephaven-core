﻿using Deephaven.ManagedClient;
using Google.Protobuf.WellKnownTypes;
using Io.Deephaven.Proto.Backplane.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Apache.Arrow.Flight;
using Apache.Arrow.Flight.Client;
using Grpc.Core;
using Grpc.Net.Client;
using Io.Deephaven.Proto.Backplane.Script.Grpc;
using Exception = System.Exception;

namespace Deephaven.ManagedClient;

public static class UtilSomewhere {
  public static bool IsEmpty(this string s) {
    return s.Length == 0;
  }
}

public static class Painful {
  public static ChannelCredentials GetCredentials(
    bool useTls,
    string tlsRootCerts,
    string clientRootChain,
    string clientPrivateKey) {
    if (!useTls) {
      return ChannelCredentials.Insecure;
    }

    var certPair = new KeyCertificatePair(clientRootChain, clientPrivateKey);
    return new SslCredentials(tlsRootCerts, certPair);
  }
}

public static class Stupid {
  private const string TimeoutKey = "http.session.durationMs";

  public static bool TryExtractExpirationInterval(ConfigurationConstantsResponse ccResp, out TimeSpan result) {
    if (!ccResp.ConfigValues.TryGetValue(TimeoutKey, out var value) || !value.HasStringValue) {
      result = TimeSpan.Zero;
      return false;
    }

    if (!int.TryParse(value.StringValue, out var intResult)) {
      throw new Exception($"Failed to parse {value.StringValue} as an integer");
    }

    // As a matter of policy we use half of whatever the server tells us is the expiration time.
    result = TimeSpan.FromMilliseconds((double)intResult / 2);
    return true;
  }
}

public class Server {
  private const string AuthorizationKey = "authorization";

  // fix client_options
  public static Server CreateFromTarget(string target, ClientOptions clientOptions) {
    if (!clientOptions.UseTls && !clientOptions.TlsRootCerts.IsEmpty()) {
      throw new Exception("Server.CreateFromTarget: ClientOptions: UseTls is false but pem provided");
    }

// grpc::ChannelArguments channel_args;
// auto options = arrow::flight::FlightClientOptions::Defaults();
//   for (const auto &opt : client_options.IntOptions()) {
//     channel_args.SetInt(opt.first, opt.second);
//     options.generic_options.emplace_back(opt.first, opt.second);
//   }
//   for (const auto &opt : client_options.StringOptions()) {
//   channel_args.SetString(opt.first, opt.second);
//   options.generic_options.emplace_back(opt.first, opt.second);
// }
//

    var channelOptions = new GrpcChannelOptions();
    channelOptions.Credentials = Painful.GetCredentials(clientOptions.UseTls, clientOptions.TlsRootCerts,
      clientOptions.ClientCertChain, clientOptions.ClientPrivateKey);
    var targetToUse = (clientOptions.UseTls ? "https://" : "http://") + target;

    var channel = GrpcChannel.ForAddress(targetToUse, channelOptions);

    var aps = new ApplicationService.ApplicationServiceClient(channel);
    var cs = new ConsoleService.ConsoleServiceClient(channel);
    var ss = new SessionService.SessionServiceClient(channel);
    var ts = new TableService.TableServiceClient(channel);
    var cfs = new ConfigService.ConfigServiceClient(channel);
    var its = new InputTableService.InputTableServiceClient(channel);
    var fc = new FlightClient(channel);

    string sessionToken;
    TimeSpan expirationInterval;
    var sendTime = DateTime.Now;
    {
      var metadata = new Metadata { { AuthorizationKey, clientOptions.AuthorizationValue } };
      foreach (var (k, v) in clientOptions.ExtraHeaders) {
        metadata.Add(k, v);
      }

      var ccReq = new ConfigurationConstantsRequest();
      var ccTask = cfs.GetConfigurationConstantsAsync(ccReq, metadata);
      var serverMetadata = ccTask.ResponseHeadersAsync.Result;
      var ccResp = ccTask.ResponseAsync.Result;
      var maybeToken = serverMetadata.Where(e => e.Key == AuthorizationKey).Select(e => e.Value).FirstOrDefault();
      sessionToken = maybeToken ?? throw new Exception("Configuration response didn't contain authorization token");
      if (!Stupid.TryExtractExpirationInterval(ccResp, out expirationInterval)) {
        // arbitrary
        expirationInterval = TimeSpan.FromSeconds(10);
      }
    }

    var nextHandshakeTime = sendTime + expirationInterval;

    var result = new Server(aps, cs, ss, ts, cfs, its, fc, clientOptions.ExtraHeaders, sessionToken,
      expirationInterval, nextHandshakeTime);
    // set keepaliveThread here
    return result;
  }

  public void SendRpc(Action<CallOptions> ignored) {
    throw new NotImplementedException();

  }

  public Ticket NewTicket() {
    throw new NotImplementedException();
  }

  public string Me {
    get {
      throw new NotImplementedException();
    }
  }

  public ConsoleService.ConsoleServiceClient ConsoleStub {
    get {
      throw new NotImplementedException("hi");
    }
  }
}

//   if (!result.ok()) {
//     auto message = fmt::format("Can't get configuration constants. Error {}: {}",
//         static_cast<int>(result.error_code()), result.error_message());
//     throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
//   }
//
//   const auto &md = ctx.GetServerInitialMetadata();
//   auto ip = md.find(kAuthorizationKey);
//   if (ip == md.end()) {
//     throw std::runtime_error(
//         DEEPHAVEN_LOCATION_STR("Configuration response didn't contain authorization token"));
//   }
//   session_token.assign(ip->second.begin(), ip->second.end());
//
//   // Get expiration interval.
//   auto exp_int = ExtractExpirationInterval(cc_resp);
//   if (exp_int.has_value()) {
//     expiration_interval = *exp_int;
//   } else {
//     expiration_interval = std::chrono::seconds(10);
//   }
// }
//
// auto next_handshake_time = send_time + expiration_interval;
//
// auto result = std::make_shared<Server>(Private(), std::move(as), std::move(cs),
//     std::move(ss), std::move(ts), std::move(cfs), std::move(its), std::move(*client_res),
//     clientOptions.ExtraHeaders(), std::move(session_token), expiration_interval, next_handshake_time);
// result->keepAliveThread_ = std::thread(&SendKeepaliveMessages, result);
// return result;
// }



using Deephaven.ManagedClient;
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
    return s.Length != 0;
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

public class Server {
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
    var channel = GrpcChannel.ForAddress(target, channelOptions);

    var aps = new ApplicationService.ApplicationServiceClient(channel);
    var cs = new ConsoleService.ConsoleServiceClient(channel);
    var ss = new SessionService.SessionServiceClient(channel);
    var ts = new TableService.TableServiceClient(channel);
    var cfs = new ConfigService.ConfigServiceClient(channel);
    var its = new InputTableService.InputTableServiceClient(channel);

// TODO(kosak): Warn about this string conversion or do something more general.
var flight_target = ((clientOptions.UseTls) ? "grpc+tls://" : "grpc://") + target;

var fc = new FlightClient(channel);

string session_token;
std::chrono::milliseconds expiration_interval;
var send_time = std::chrono::system_clock::now(); {
  ConfigurationConstantsRequest cc_req;
  ConfigurationConstantsResponse cc_resp;
  grpc::ClientContext ctx;
  ctx.AddMetadata(kAuthorizationKey, clientOptions.AuthorizationValue());
  for (const auto &header : clientOptions.ExtraHeaders()) {
    ctx.AddMetadata(header.first, header.second);
  }

  auto result = cfs->GetConfigurationConstants(&ctx, cc_req, &cc_resp);

  if (!result.ok()) {
    auto message = fmt::format("Can't get configuration constants. Error {}: {}",
        static_cast<int>(result.error_code()), result.error_message());
    throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
  }

  const auto &md = ctx.GetServerInitialMetadata();
  auto ip = md.find(kAuthorizationKey);
  if (ip == md.end()) {
    throw std::runtime_error(
        DEEPHAVEN_LOCATION_STR("Configuration response didn't contain authorization token"));
  }
  session_token.assign(ip->second.begin(), ip->second.end());

  // Get expiration interval.
  auto exp_int = ExtractExpirationInterval(cc_resp);
  if (exp_int.has_value()) {
    expiration_interval = *exp_int;
  } else {
    expiration_interval = std::chrono::seconds(10);
  }
}

auto next_handshake_time = send_time + expiration_interval;

auto result = std::make_shared<Server>(Private(), std::move(as), std::move(cs),
    std::move(ss), std::move(ts), std::move(cfs), std::move(its), std::move(*client_res),
    clientOptions.ExtraHeaders(), std::move(session_token), expiration_interval, next_handshake_time);
result->keepAliveThread_ = std::thread(&SendKeepaliveMessages, result);
return result;
}



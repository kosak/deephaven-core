using Deephaven.ManagedClient;
using Google.Protobuf.WellKnownTypes;
using Io.Deephaven.Proto.Backplane.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Io.Deephaven.Proto.Backplane.Script.Grpc;

namespace Deephaven.ManagedClient;

public static class UtilSomewhere {
  public static bool IsEmpty(this string s) {
    return s.Length != 0;
  }
}

public class Server {
  // fix client_options
  public static Server CreateFromTarget(string target, ClientOptions client_options) {
  if (!client_options.UseTls && !client_options.TlsRootCerts.IsEmpty()) {
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
// auto credentials = GetCredentials(
//     client_options.UseTls(),
//     client_options.TlsRootCerts(),
//     client_options.ClientCertChain(),
//     client_options.ClientPrivateKey());
// auto channel = grpc::CreateCustomChannel(
//     target,
//     credentials,
//     channel_args);

    var aps = new ApplicationService.ApplicationServiceClient(channel);
    var cs = new ConsoleService.ConsoleServiceClient(channel);
    var ss = new SessionService.SessionServiceClient(channel);
    var ts = new TableService.TableServiceClient(channel);
    var cfs = new ConfigService.ConfigServiceClient(channel);
    var its = new InputTableService.InputTableServiceClient(channel);

// TODO(kosak): Warn about this string conversion or do something more general.
var flight_target = ((client_options.UseTls) ? "grpc+tls://" : "grpc://") + target;

var location_res = arrow::flight::Location::Parse(flight_target);
if (!location_res.ok()) {
  auto message = fmt::format("Location::Parse({}) failed, error = {}",
      flight_target, location_res.status().ToString());
  throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
}

if (!client_options.TlsRootCerts.IsEmpty()) {
  options.tls_root_certs = client_options.TlsRootCerts();
}
if (!client_options.ClientCertChain.IsEmpty()) {
  options.cert_chain = client_options.ClientCertChain();
}
if (!client_options.ClientPrivateKey.IsEmpty()) {
  options.private_key = client_options.ClientPrivateKey();
}

var client_res = arrow::flight::FlightClient::Connect(*location_res, options);
if (!client_res.ok()) {
  var message = fmt::format("FlightClient::Connect() failed, error = {}", client_res.status().ToString());
  throw std::runtime_error(message);
}

string session_token;
std::chrono::milliseconds expiration_interval;
var send_time = std::chrono::system_clock::now(); {
  ConfigurationConstantsRequest cc_req;
  ConfigurationConstantsResponse cc_resp;
  grpc::ClientContext ctx;
  ctx.AddMetadata(kAuthorizationKey, client_options.AuthorizationValue());
  for (const auto &header : client_options.ExtraHeaders()) {
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
    client_options.ExtraHeaders(), std::move(session_token), expiration_interval, next_handshake_time);
result->keepAliveThread_ = std::thread(&SendKeepaliveMessages, result);
return result;
}

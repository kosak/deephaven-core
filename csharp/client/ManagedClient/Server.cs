using System.Diagnostics;
using Io.Deephaven.Proto.Backplane.Grpc;
using Apache.Arrow.Flight.Client;
using Grpc.Core;
using Grpc.Net.Client;
using Io.Deephaven.Proto.Backplane.Script.Grpc;
using Google.Protobuf;

namespace Deephaven.ManagedClient;
public class Server {
  private const string AuthorizationKey = "authorization";
  private const string TimeoutKey = "http.session.durationMs";

  // fix client_options
  public static Server CreateFromTarget(string target, ClientOptions clientOptions) {

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

    var channel = GrpcUtil.CreateChannel(target, clientOptions);

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
      if (!TryExtractExpirationInterval(ccResp, out expirationInterval)) {
        // arbitrary
        expirationInterval = TimeSpan.FromSeconds(10);
      }
    }

    var result = new Server(aps, cs, ss, ts, cfs, its, fc, clientOptions.ExtraHeaders, sessionToken,
      expirationInterval);
    return result;
  }

  private static InterlockedLong _nextFreeServerId;

  public string Me { get; }
  private readonly ApplicationService.ApplicationServiceClient _applicationStub;
  public ConsoleService.ConsoleServiceClient ConsoleStub { get; }
  public SessionService.SessionServiceClient SessionStub { get; }
  public TableService.TableServiceClient TableStub { get; }
  public ConfigService.ConfigServiceClient ConfigStub { get; }
  public InputTableService.InputTableServiceClient InputTableStub { get; }
  public FlightClient FlightClient { get; }
  private readonly IReadOnlyList<(string, string)> _extraHeaders;
  private readonly TimeSpan _expirationInterval;
  private readonly Timer _keepalive;

  private readonly object _sync = new();
  /// <summary>
  /// Protected by _sync
  /// </summary>
  private Int32 _nextFreeTicketId = 1;
  private readonly HashSet<Ticket> _outstandingTickets = new();
  private string _sessionToken;
  private bool _cancelled = false;

  private Server(ApplicationService.ApplicationServiceClient applicationStub,
    ConsoleService.ConsoleServiceClient consoleStub,
    SessionService.SessionServiceClient sessionStub,
    TableService.TableServiceClient tableStub,
    ConfigService.ConfigServiceClient configStub,
    InputTableService.InputTableServiceClient inputTableStub,
    FlightClient flightClient,
    IReadOnlyList<(string, string)> extraHeaders,
    string sessionToken,
    TimeSpan expirationInterval) {
    Me = $"{nameof(Server)}-{_nextFreeServerId.Increment()}";
    _applicationStub = applicationStub;
    ConsoleStub = consoleStub;
    SessionStub = sessionStub;
    TableStub = tableStub;
    ConfigStub = configStub;
    InputTableStub = inputTableStub;
    FlightClient = flightClient;
    _extraHeaders = extraHeaders.ToArray();
    _sessionToken = sessionToken;
    _expirationInterval = expirationInterval;
    _keepalive = new Timer(SendKeepaliveMessage, null, _expirationInterval,
      Timeout.InfiniteTimeSpan);
  }

  public TResponse SendRpc<TResponse>(Func<CallOptions, AsyncUnaryCall<TResponse>> callback,
    bool disregardCancellationState = false) {
    var metadata = new Metadata();
    ForEachHeaderNameAndValue(metadata.Add);

    if (!disregardCancellationState) {
      lock (_sync) {
        if (_cancelled) {
          throw new Exception("Server cancelled. All further RPCs are being rejected");
        }
      }
    }

    var options = new CallOptions(headers: metadata);
    var asyncResp = callback(options);

    var serverMetadata = asyncResp.ResponseHeadersAsync.Result;
    var result = asyncResp.ResponseAsync.Result;

    var maybeToken = serverMetadata.Where(e => e.Key == AuthorizationKey).Select(e => e.Value).FirstOrDefault();
    lock (_sync) {
      if (maybeToken != null) {
        _sessionToken = maybeToken;
      }

      _keepalive.Change(_expirationInterval, Timeout.InfiniteTimeSpan);
    }

    return result;
  }

  public void ForEachHeaderNameAndValue(Action<string, string> callback) {
    string tokenCopy;
    lock (_sync) {
      tokenCopy = _sessionToken;
    }
    callback(AuthorizationKey, tokenCopy);
    foreach (var entry in _extraHeaders) {
      callback(entry.Item1, entry.Item2);
    }
  }

  public Ticket MakeNewTicket(Int32 ticketId) {
    // 'e' + 4 bytes
    var bytes = new byte[5];
    bytes[0] = (byte)'e';
    var span = new Span<byte>(bytes, 1, 4);
    if (!BitConverter.TryWriteBytes(span, ticketId)) {
      throw new Exception("Programming error: TryWriteBytes failed");
    }
    var result = new Ticket {
      Ticket_ = ByteString.CopyFrom(bytes)
    };
    return result;
  }

  public Ticket NewTicket() {
    lock (_sync) {
      var ticketId = _nextFreeTicketId++;
      var ticket = MakeNewTicket(ticketId);
      _outstandingTickets.Add(ticket);
      return ticket;
    }
  }

  private void SendKeepaliveMessage(object? _) {
    try {
      Debug.WriteLine("Hi sending keepalive");
      var req = new ConfigurationConstantsRequest();
      SendRpc(opts => ConfigStub.GetConfigurationConstantsAsync(req, opts));
      Debug.WriteLine("Hi that looks like that (that = sending keepalive) worked");
    } catch (Exception e) {
      Debug.WriteLine($"Keepalive timer: ignoring {e}");
      // Successful SendRpc will reset the timer for us. For a failed SendRpc,
      // we retry relatively frequently.
      _keepalive.Change(TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
    }
  }

  private static bool TryExtractExpirationInterval(ConfigurationConstantsResponse ccResp, out TimeSpan result) {
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



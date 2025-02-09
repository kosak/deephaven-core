using Deephaven.DheClient.Auth;
using Deephaven.ManagedClient;
using Grpc.Core;
using Io.Deephaven.Proto.Auth;
using Io.Deephaven.Proto.Controller;
using Io.Deephaven.Proto.Controller.Grpc;
using System.Linq;

namespace Deephaven.DheClient.Controller;

public class ControllerClient {
  public const string ControllerServiceName = "PersistentQueryController";

  public static ControllerClient Connect(string descriptiveName, string target,
    ClientOptions options) {
    var channel = GrpcUtil.CreateChannel(target, options);
    // where does this go: arguments and also credentials
    // grpc::ChannelArguments channel_args;
    // for (const auto &opt : options.IntOptions()) {
    //   channel_args.SetInt(opt.first, opt.second);
    // }
    // for (const auto &opt : options.StringOptions()) {
    //   channel_args.SetString(opt.first, opt.second);
    // }

    var controllerApi = new ControllerApi.ControllerApiClient(channel);
    var co = new CallOptions();
    var req = new PingRequest();
    _ = controllerApi.ping(req, co);

    return new ControllerClient(controllerApi);
  }

  private readonly ControllerApi.ControllerApiClient _controllerApi;

  public ControllerClient(ControllerApi.ControllerApiClient controllerApi) {
    _controllerApi = controllerApi;
  }

  bool ControllerClientImpl::
Authenticate(const auth::AuthToken &auth_token) {
  CheckNotClosedOrThrow();
  std::unique_lock lock(auth_ctx_.mux);
  if (auth_ctx_.authenticating) {
    if (auth_ctx_.user_context != auth_token.user_context) {
      const std::string msg = DEEPHAVEN_LOCATION_STR(
          "already running authentication request doesn't match credentials.");
  gpr_log(GPR_INFO, WITH_ID("%s"), msg.c_str());
      throw ControllerClientException(msg);
    }
    gpr_log(GPR_INFO,
            WITH_ID("ControllerClientImpl::Authenticate: "
                    "holding on already running authentication request."));
    auth_ctx_.cond.wait(lock, [this]{ return !auth_ctx_.authenticating; });
const bool authenticated = !auth_ctx_.cookie.empty();
lock.unlock();
gpr_log(GPR_INFO, WITH_ID("ControllerClientImpl::Authenticate: "

                          "joined response authenticated=%s"),
        utility::BoolToCharPtr(authenticated));
return authenticated;
  }
  auth_ctx_.authenticating = true;
auth_ctx_.user_context = auth_token.user_context;
auth_ctx_.cookie.clear();
lock.unlock();  // Do not hold a lock while waiting for an RPC response.
gpr_log(GPR_INFO,
        WITH_ID("ControllerClientImpl::Authenticate: sending authentication request."));
AuthenticationRequest request;
*request.mutable_clientid() = client_id_;
*request.mutable_token() = auth::impl::AuthTokenToProto(auth_token);
request.set_getconfiguration(true);
const auto send_time = TimeNow();
ClientContext context;
AuthenticationResponse response;
SetupClientContext(
    context, send_time + deephaven_enterprise::auth::impl::kAuthTokenLifetime);
const Status status =
    controller_api_stub_->authenticate(&context, request, &response);
lock.lock () ;
auth_ctx_.authenticating = false;
auth_ctx_.cond.notify_all();
if (!status.ok()) {
  lock.unlock();
  auto failure = FailureFromStatus(context, status);
  const std::string msg = DEEPHAVEN_LOCATION_STR(
  fmt::format(
          "authentication RPC failed: {}",
          failure.msg));
  gpr_log(GPR_ERROR, WITH_ID("%s."), msg.c_str());
  throw ControllerClientException(msg);
}
if (response.authenticated()) {
  auth_ctx_.cookie = response.cookie();
  if (response.has_config()) {
    auth_ctx_.controller_config = response.config();
  }
}
lock.unlock();
gpr_log(GPR_INFO,
        WITH_ID("ControllerClientImpl::Authenticate: "

                "received authentication response=%s"),
        utility::BoolToCharPtr(response.authenticated()));
return response.authenticated();
}

}

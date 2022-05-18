#pragma once

#include <memory>
#include "deephaven/client/server/server.h"
#include "deephaven/client/ticking.h"
#include "deephaven/client/utility/misc.h"
#include "deephaven/proto/ticket.pb.h"

namespace deephaven::client::subscription {
namespace internal {
class SubscribeResult {

};
}  // namespace internal

std::unique_ptr<internal::SubscribeResult> startSubscribeThread(
    deephaven::client::server::Server *server,
    const deephaven::client::utility::ColumnDefinitions &columnDefinitions,
    const io::deephaven::proto::backplane::grpc::Ticket &ticket,
    std::shared_ptr<TickingCallback> callback);
}  // namespace deephaven::client::subscription

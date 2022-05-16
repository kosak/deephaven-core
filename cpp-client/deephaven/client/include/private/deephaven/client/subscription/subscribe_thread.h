#pragma once

#include <memory>
#include "deephaven/client/server/server.h"

namespace deephaven::client::subscription {
namespace internal {
class CancelCookie {

};
}  // namespace internal

std::unique_ptr<internal::CancelCookie> startSubscribeThread(
    deephaven::client::server::Server *server,
    const ColumnDefinitions &columnDefinitions, std::string_view ticket,
    std::shared_ptr<TickingCallback> callback);
}  // namespace deephaven::client::subscription

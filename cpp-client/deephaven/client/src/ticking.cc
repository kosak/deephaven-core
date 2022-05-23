#include "deephaven/client/ticking.h"

namespace deephaven::client {
TickingUpdate::TickingUpdate(std::shared_ptr<Table> beforeRemoves,
    std::shared_ptr<Table> beforeModifies,
    std::shared_ptr<Table> current,
    std::shared_ptr<RowSequence> removed,
    std::vector<std::shared_ptr<RowSequence>> perColumnModifies,
    std::shared_ptr<RowSequence> added) : beforeRemoves_(std::move(beforeRemoves)),
    beforeModifies_(std::move(beforeModifies)),
    current_(std::move(current)),
    removed_(std::move(removed)),
    perColumnModifies_(std::move(perColumnModifies)),
    added_(std::move(added)) {}

TickingUpdate::TickingUpdate(TickingUpdate &&other) noexcept = default;
TickingUpdate &TickingUpdate::operator=(TickingUpdate &&other) noexcept = default;
TickingUpdate::~TickingUpdate() = default;

}  // namespace deephaven::client

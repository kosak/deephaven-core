#include "deephaven/client/ticking.h"

namespace deephaven::client {
ImmerTickingUpdate::ImmerTickingUpdate(std::shared_ptr<Table> beforeRemoves,
    std::shared_ptr<Table> beforeModifies,
    std::shared_ptr<Table> current,
    std::shared_ptr<RowSequence> removed,
    std::vector<std::shared_ptr<RowSequence>> modified,
    std::shared_ptr<RowSequence> added) : beforeRemoves_(std::move(beforeRemoves)),
    beforeModifies_(std::move(beforeModifies)),
    current_(std::move(current)),
    removed_(std::move(removed)),
    modified_(std::move(modified)),
    added_(std::move(added)) {}

ImmerTickingUpdate::ImmerTickingUpdate(ImmerTickingUpdate &&other) noexcept = default;
ImmerTickingUpdate &ImmerTickingUpdate::operator=(ImmerTickingUpdate &&other) noexcept = default;
ImmerTickingUpdate::~ImmerTickingUpdate() = default;

}  // namespace deephaven::client

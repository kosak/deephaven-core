#include "deephaven/client/ticking.h"

namespace deephaven::client {
ClassicTickingUpdate::ClassicTickingUpdate(std::shared_ptr<RowSequence> removedRows,
    std::shared_ptr<RowSequence> addedRows, std::vector<std::shared_ptr<RowSequence>> modifiedRows,
    std::shared_ptr<Table> current) : removedRows_(std::move(removedRows)),
    addedRows_(std::move(addedRows)), modifiedRows_(std::move(modifiedRows)),
    current_(std::move(current)) {}
ClassicTickingUpdate::ClassicTickingUpdate(ClassicTickingUpdate &&other) noexcept = default;
ClassicTickingUpdate &ClassicTickingUpdate::operator=(ClassicTickingUpdate &&other) noexcept = default;
ClassicTickingUpdate::~ClassicTickingUpdate() = default;

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

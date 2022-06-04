#include "deephaven/client/subscription/classic_table_state.h"

#include <memory>
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/utility/utility.h"

using deephaven::client::container::RowSequence;
using deephaven::client::container::RowSequenceBuilder;
using deephaven::client::utility::stringf;

namespace deephaven::client::subscription {
std::shared_ptr<RowSequence> ClassicTableState::erase(std::shared_ptr<RowSequence> rowsToRemoveKeySpace) {
  RowSequenceBuilder resultBuilder;
  auto removeRange = [this, &resultBuilder](uint64_t beginKey, uint64_t endKey) {
    auto beginp = redirection_->find(beginKey);
    if (beginp == redirection_->end()) {
      throw std::runtime_error(stringf("Can't find beginKey %o", beginKey));
    }

    auto currentp = beginp;
    for (auto current = beginKey; current != endKey; ++current) {
      if (currentp->first != current) {
        throw std::runtime_error(stringf("Can't find key %o", current));
      }
      resultBuilder.add(currentp->second);
      ++currentp;
    }
    redirection_->erase(beginp, currentp);
  };
  rowsToRemoveKeySpace->forEachChunk(removeRange);
  return resultBuilder.build();
}
}  // namespace deephaven::client::subscription

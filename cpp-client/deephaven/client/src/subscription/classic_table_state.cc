#include "deephaven/client/subscription/classic_table_state.h"

#include <memory>
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/subscription/shift_processor.h"
#include "deephaven/client/utility/utility.h"

using deephaven::client::container::RowSequence;
using deephaven::client::container::RowSequenceBuilder;
using deephaven::client::utility::stringf;

namespace deephaven::client::subscription {
namespace {
void mapShifter(uint64_t begin, uint64_t end, uint64_t dest, std::map<uint64_t, uint64_t> *map);
}

std::shared_ptr<RowSequence> ClassicTableState::erase(const RowSequence &rowsToRemoveKeySpace) {
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
  rowsToRemoveKeySpace.forEachChunk(removeRange);
  return resultBuilder.build();
}

void ClassicTableState::applyShifts(const RowSequence &firstIndex, const RowSequence &lastIndex,
    const RowSequence &destIndex) {
  auto *redir = redirection_.get();
  auto processShift = [redir](uint64_t first, uint64_t last, uint64_t dest) {
    uint64_t begin = first;
    uint64_t end = ((uint64_t) last) + 1;
    uint64_t destBegin = dest;
    mapShifter(begin, end, destBegin, redir);
  };
  ShiftProcessor::applyShiftData(firstIndex, lastIndex, destIndex, processShift);
}

namespace {
void mapShifter(uint64_t begin, uint64_t end, uint64_t dest, std::map<uint64_t, uint64_t> *map) {
  auto delta = dest - start;
  if (delta < 0) {
    auto currentp = zm->lower_bound(start);
    while (true) {
      if (currentp == zm->end() || currentp->first > endInclusive) {
        return;
      }
      auto nextp = std::next(currentp);
      auto node = zm->extract(currentp);
      auto newKey = node.key() + delta;
      streamf(std::cerr, "Working forwards, moving key from %o to %o\n", node.key(), newKey);
      node.key() = newKey;
      zm->insert(std::move(node));
      currentp = nextp;
      ++dest;
    }
  }

  // delta >= 0 so move in the reverse direction
  auto currentp = zm->upper_bound(endInclusive);
  if (currentp == zm->begin()) {
    return;
  }
  --currentp;
  while (true) {
    if (currentp->first < start) {
      return;
    }
    std::optional<std::map<int64_t, int64_t>::iterator> nextp;
    if (currentp != zm->begin()) {
      nextp = std::prev(currentp);
    }
    auto node = zm->extract(currentp);
    auto newKey = node.key() + delta;
    streamf(std::cerr, "Working backwards, moving key from %o to %o\n", node.key(), newKey);
    node.key() = newKey;
    zm->insert(std::move(node));
    if (!nextp.has_value()) {
      return;
    }
    currentp = *nextp;
    --dest;
  }
}
}  // namespace
}  // namespace deephaven::client::subscription

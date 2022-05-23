#include "deephaven/client/subscription/subscribed_table_state.h"

#include <map>
#include <optional>
#include <utility>

#include "deephaven/client/chunk/chunk.h"
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/column/column_source.h"
#include "deephaven/client/immerutil/abstract_flex_vector.h"
#include "deephaven/client/utility/utility.h"
#include "immer/flex_vector.hpp"
#include "immer/flex_vector_transient.hpp"

#include "deephaven/client/utility/utility.h"

using deephaven::client::chunk::LongChunk;
using deephaven::client::column::ColumnSource;
using deephaven::client::container::RowSequence;
using deephaven::client::container::RowSequenceIterator;
using deephaven::client::immerutil::AbstractFlexVectorBase;
using deephaven::client::utility::makeReservedVector;
using deephaven::client::utility::streamf;
using deephaven::client::utility::stringf;

namespace deephaven::client::subscription {
namespace {
// void mapShifter(int64_t start, int64_t endInclusive, int64_t dest, std::map<int64_t, int64_t> *zm);
void applyShiftData(const RowSequence &firstIndex, const RowSequence &lastIndex, const RowSequence &destIndex,
    const std::function<void(int64_t, int64_t, int64_t)> &processShift);
}  // namespace

SubscribedTableState::SubscribedTableState(
    std::vector<std::unique_ptr<AbstractFlexVectorBase>> flexVectors) :
    flexVectors_(std::move(flexVectors)) {}

SubscribedTableState::~SubscribedTableState() = default;

void SubscribedTableState::add(std::vector<std::unique_ptr<AbstractFlexVectorBase>> addedData,
    const RowSequence &addedIndexes) {
  auto addChunk = [this, &addedData](uint64_t beginKey, uint64_t endKey) {
    auto size = endKey - beginKey;
    auto beginIndex = spaceMapper_.addRange(beginKey, endKey);

    for (size_t i = 0; i < flexVectors_.size(); ++i) {
      auto &fv = flexVectors_[i];
      auto &ad = addedData[i];

      auto fvTemp = std::move(fv);
      // Give "col" its original values up to 'beginIndex'; leave colTemp with the rest.
      fv = fvTemp->take(beginIndex);
      fvTemp->inPlaceDrop(beginIndex);

      // Append the next 'size' values from 'addedData' to 'col' and drop them from 'addedData'.
      fv->inPlaceAppend(ad->take(size));
      ad->inPlaceDrop(size);

      // Append the residual items back from colTemp.
      fv->inPlaceAppend(std::move(fvTemp));
    }
  };
  addedIndexes.forEachChunk(addChunk);
}

void SubscribedTableState::erase(const RowSequence &removedRows) {
  auto eraseChunk = [this](uint64_t beginKey, uint64_t endKey) {
    auto size = endKey - beginKey;
    auto beginIndex = spaceMapper_.eraseRange(beginKey, endKey);
    auto endIndex = beginIndex + size;

    for (auto &fv : flexVectors_) {
      auto fvTemp = std::move(fv);
      fv = fvTemp->take(beginIndex);
      fvTemp->inPlaceDrop(endIndex);
      fv->inPlaceAppend(std::move(fvTemp));
    }
  };
  removedRows.forEachChunk(eraseChunk);
}

void SubscribedTableState::modify(std::vector<std::unique_ptr<AbstractFlexVectorBase>> modifiedData,
    const std::vector<std::shared_ptr<RowSequence>> &modifiedIndicesPerColumn) {
  throw std::runtime_error("TODO(kosak)");
}


void SubscribedTableState::applyShifts(const RowSequence &firstIndex, const RowSequence &lastIndex,
    const RowSequence &destIndex) {
  auto processShift = [this](int64_t first, int64_t last, int64_t dest) {
    uint64_t begin = first;
    uint64_t end = ((uint64_t)last) + 1;
    uint64_t destBegin = dest;
    spaceMapper_.applyShift(begin, end, destBegin);
  };
  applyShiftData(firstIndex, lastIndex, destIndex, processShift);
}

namespace {
void applyShiftData(const RowSequence &firstIndex, const RowSequence &lastIndex,
    const RowSequence &destIndex,
    const std::function<void(int64_t, int64_t, int64_t)> &processShift) {
  if (firstIndex.empty()) {
    return;
  }

  // Loop twice: once in the forward direction (applying negative shifts), and once in the reverse direction
  // (applying positive shifts).
  for (auto direction = 0; direction < 2; ++direction) {
    std::shared_ptr<RowSequenceIterator> startIter, endIter, destIter;
    if (direction == 0) {
      startIter = firstIndex.getRowSequenceIterator();
      endIter = lastIndex.getRowSequenceIterator();
      destIter = destIndex.getRowSequenceIterator();
    } else {
      startIter = firstIndex.getRowSequenceReverseIterator();
      endIter = lastIndex.getRowSequenceReverseIterator();
      destIter = destIndex.getRowSequenceReverseIterator();
    }
    int64_t first, last, dest;
    while (startIter->tryGetNext(&first)) {
      if (!endIter->tryGetNext(&last) || !destIter->tryGetNext(&dest)) {
        throw std::runtime_error("Sequences not of same size");
      }
      if (direction == 0) {
        // If forward, only process negative shifts.
        if (dest >= first) {
          continue;
        }
      } else {
        // If reverse, only process positive shifts.
        if (dest <= first) {
          continue;
        }
      }
      const char *dirText = direction == 0 ? "(positive)" : "(negative)";
      streamf(std::cerr, "Processing %o shift src [%o..%o] dest %o\n", dirText, first, last, dest);
      processShift(first, last, dest);
    }
  }
}

//void mapShifter(int64_t start, int64_t endInclusive, int64_t dest, std::map<int64_t, int64_t> *zm) {
//  auto delta = dest - start;
//  if (delta < 0) {
//    auto currentp = zm->lower_bound(start);
//    while (true) {
//      if (currentp == zm->end() || currentp->first > endInclusive) {
//        return;
//      }
//      auto nextp = std::next(currentp);
//      auto node = zm->extract(currentp);
//      auto newKey = node.key() + delta;
//      streamf(std::cerr, "Working forwards, moving key from %o to %o\n", node.key(), newKey);
//      node.key() = newKey;
//      zm->insert(std::move(node));
//      currentp = nextp;
//      ++dest;
//    }
//  }
//
//  // delta >= 0 so move in the reverse direction
//  auto currentp = zm->upper_bound(endInclusive);
//  if (currentp == zm->begin()) {
//    return;
//  }
//  --currentp;
//  while (true) {
//    if (currentp->first < start) {
//      return;
//    }
//    std::optional<std::map<int64_t, int64_t>::iterator> nextp;
//    if (currentp != zm->begin()) {
//      nextp = std::prev(currentp);
//    }
//    auto node = zm->extract(currentp);
//    auto newKey = node.key() + delta;
//    streamf(std::cerr, "Working backwards, moving key from %o to %o\n", node.key(), newKey);
//    node.key() = newKey;
//    zm->insert(std::move(node));
//    if (!nextp.has_value()) {
//      return;
//    }
//    currentp = *nextp;
//    --dest;
//  }
//}
}  // namespace
}  // namespace deephaven::client::subscription

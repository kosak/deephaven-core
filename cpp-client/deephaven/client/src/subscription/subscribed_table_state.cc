#include "deephaven/client/subscription/subscribed_table_state.h"

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
using deephaven::client::container::RowSequenceBuilder;
using deephaven::client::container::RowSequenceIterator;
using deephaven::client::immerutil::AbstractFlexVectorBase;
using deephaven::client::table::Table;
using deephaven::client::utility::makeReservedVector;
using deephaven::client::utility::streamf;
using deephaven::client::utility::stringf;

namespace deephaven::client::subscription {
namespace {
// void mapShifter(int64_t start, int64_t endInclusive, int64_t dest, std::map<int64_t, int64_t> *zm);
void applyShiftData(const RowSequence &firstIndex, const RowSequence &lastIndex, const RowSequence &destIndex,
    const std::function<void(uint64_t, uint64_t, uint64_t)> &processShift);
class MyTable final : public Table {
public:
  explicit MyTable(std::vector<std::shared_ptr<ColumnSource>> sources, size_t numRows);
  ~MyTable() final;

  std::shared_ptr<RowSequence> getRowSequence() const final;
  std::shared_ptr<ColumnSource> getColumn(size_t columnIndex) const final {
    return sources_[columnIndex];
  }
  size_t numRows() const final {
    return numRows_;
  }
  size_t numColumns() const final {
    return sources_.size();
  }

private:
  std::vector<std::shared_ptr<ColumnSource>> sources_;
  size_t numRows_ = 0;
};
}  // namespace

SubscribedTableState::SubscribedTableState(
    std::vector<std::unique_ptr<AbstractFlexVectorBase>> flexVectors) :
    flexVectors_(std::move(flexVectors)) {}

SubscribedTableState::~SubscribedTableState() = default;

void SubscribedTableState::add(std::vector<std::unique_ptr<AbstractFlexVectorBase>> addedData,
    const RowSequence &addedIndexes) {
  auto addChunk = [this, &addedData](uint64_t beginIndex, uint64_t endIndex) {
    auto size = endIndex - beginIndex;

    for (size_t i = 0; i < flexVectors_.size(); ++i) {
      auto &fv = flexVectors_[i];
      auto &ad = addedData[i];

      auto fvTemp = std::move(fv);
      // Give "col" its original values up to 'beginIndex'; leave fvTemp with the rest.
      fv = fvTemp->take(beginIndex);
      fvTemp->inPlaceDrop(beginIndex);

      // Append the next 'size' values from 'addedData' to 'fv' and drop them from 'addedData'.
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

std::shared_ptr<Table> SubscribedTableState::snapshot() const {
  auto columnSources = makeReservedVector<std::shared_ptr<ColumnSource>>(flexVectors_.size());
  for (const auto &fv : flexVectors_) {
    columnSources.push_back(fv->makeColumnSource());
  }
  return std::make_shared<MyTable>(std::move(columnSources), spaceMapper_.size());
}

namespace {
void applyShiftData(const RowSequence &firstIndex, const RowSequence &lastIndex,
    const RowSequence &destIndex,
    const std::function<void(uint64_t, uint64_t, uint64_t)> &processShift) {
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
    uint64_t first, last, dest;
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
MyTable::MyTable(std::vector<std::shared_ptr<ColumnSource>> sources, size_t numRows) :
    sources_(std::move(sources)), numRows_(numRows) {}
MyTable::~MyTable() = default;

std::shared_ptr<RowSequence> MyTable::getRowSequence() const {
  // Need a utility for this
  RowSequenceBuilder rb;
  rb.addRange(0, numRows_, "zamboni time");
  return rb.build();
}
}  // namespace
}  // namespace deephaven::client::subscription

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
void mapShifter(int64_t start, int64_t endInclusive, int64_t dest, std::map<int64_t, int64_t> *zm);
void applyShiftData(const RowSequence &startIndex, const RowSequence &endInclusiveIndex,
    const RowSequence &destIndex,
    const std::function<void(int64_t, int64_t, int64_t)> &processShift);

class MyRowSequence final : public RowSequence {
  typedef std::map<int64_t, int64_t> data_t;
public:
  MyRowSequence(std::shared_ptr<data_t> data, data_t::const_iterator begin,
      data_t::const_iterator end, size_t size);
  ~MyRowSequence() final = default;
  std::shared_ptr<RowSequenceIterator> getRowSequenceIterator() const final;
  std::shared_ptr<RowSequenceIterator> getRowSequenceReverseIterator() const final;

  void forEachChunk(const std::function<void(int64_t firstKey, int64_t lastKey)> &f) const final;

  size_t size() const final {
    return size_;
  }

private:
  std::shared_ptr<data_t> data_;
  data_t::const_iterator begin_;
  data_t::const_iterator end_;
  size_t size_ = 0;
};

class MyRowSequenceIterator final : public RowSequenceIterator {
  typedef std::map<int64_t, int64_t> data_t;
public:
  MyRowSequenceIterator(std::shared_ptr<data_t> data,
      data_t::const_iterator begin, data_t::const_iterator end, size_t size, bool forward);
  ~MyRowSequenceIterator() final;

  std::shared_ptr<RowSequence> getNextRowSequenceWithLength(size_t size) final;
  bool tryGetNext(int64_t *result) final;

private:
  std::shared_ptr<data_t> data_;
  data_t::const_iterator begin_;
  data_t::const_iterator end_;
  size_t size_ = 0;
  bool forward_ = false;
};
}  // namespace

std::shared_ptr<TickingTable>
TickingTable::create(std::vector<std::shared_ptr<ColumnSource>> columns) {
  return std::make_shared<TickingTable>(Private(), std::move(columns));
}

TickingTable::TickingTable(Private, std::vector<std::shared_ptr<ColumnSource>> columns) :
    columns_(std::move(columns)) {
  redirection_ = std::make_shared<std::map<int64_t, int64_t>>();
}

TickingTable::~TickingTable() = default;

void SubscribedTableState::add(const RowSequence &addedRows) {
  auto existingInternals = makeReservedVector<std::unique_ptr<AbstractFlexVectorBase>>(
      columns_.size());
  auto newInternals = makeReservedVector<std::unique_ptr<AbstractFlexVectorBase>>(columns_.size());

  for (const auto &col: columns_) {
    auto existing = col->getInternals();
    existingInternals.push_back(std::move(existing));
    // Easy way to get an empty column of the right type.
    newInternals.push_back(existingInternals.back()->take(0));
  }

  auto addChunk = [this, &existingInternals, &newInternals](const uint64_t beginKey,
      const uint64_t endKey) {
    auto size = endKey - beginKey;
    auto beginIndex = spaceMapper_.addRange(beginKey, endKey);

    for (size_t i = 0; i < existingInternals.size(); ++i) {
      auto &src = existingInternals[i];
      auto &dest = newInternals[i];

      // Split src at 'beginIndex'. Call this before, after
      // Create a new immer vector which is before + newdata + after


      // Take the items in src up to 'beginIndex' (these are the items before our added range).
      // Then drop the items in src up to 'endIndex' (this includes the items we are not erasing,
      // followed by the items we *are* erasing).
      dest->mutatingAppend(src->take(beginIndex));
      src->mutatingDrop(endIndex);
    }
    // Note spaceMapper_ has already been updated, so it is already consistent with the coordinate
    // space of the columns.
  };
  removedRows.forEachChunk(eraseChunk);

  // Residual: add back whatever is left in "existingInternals"
  for (size_t i = 0; i < existingInternals.size(); ++i) {
    // Take the trailing matter if any and set source column
    auto &src = existingInternals[i];
    auto &dest = newInternals[i];
    dest->mutatingAppend(std::move(src));
    // I'm doing this to keep the identity of the same ColumnSource
    columns_[i]->setInternals(std::move(dest));
  }
  auto rowKeys = LongChunk::create(addedRows.size());
  auto iter = addedRows.getRowSequenceIterator();
  int64_t row;
  size_t destIndex = 0;
  while (iter->tryGetNext(&row)) {
    int64_t nextRedirectedRow;
    if (!slotsToReuse_.empty()) {
      nextRedirectedRow = slotsToReuse_.back();
      slotsToReuse_.pop_back();
    } else {
      nextRedirectedRow = (int64_t) redirection_->size();
    }
    auto result = redirection_->insert(std::make_pair(row, nextRedirectedRow));
    if (!result.second) {
      auto message = stringf("Row %o already exists", row);
      throw std::runtime_error(message);
    }
    rowKeys->data()[destIndex] = nextRedirectedRow;
    ++destIndex;
  }
  return UnwrappedTable::create(std::move(rowKeys), destIndex, columns_);
}

void SubscribedTableState::erase(const RowSequence &removedRows) {
  auto internals = makeReservedVector<std::unique_ptr<AbstractFlexVectorBase>>(columns_.size());

  for (const auto &col: columns_) {
    auto existing = col->getInternals();
    internals.push_back(std::move(existing));
  }

  auto *sm = &spaceMapper_;
  auto eraseChunk = [sm, &internals](const uint64_t beginKey, const uint64_t endKey) {
    auto size = endKey - beginKey;
    auto beginIndex = sm->eraseRange(beginKey, endKey);
    auto endIndex = beginIndex + size;

    for (auto &col : internals) {
      // Take the items in col up to 'beginIndex' (these are the items we are not erasing).
      // Then drop the items in src up to 'endIndex' (this includes the items we are not erasing,
      // followed by the items we *are* erasing).
      auto residual = std::move(col);
      col = residual->take(beginIndex);
      residual->mutatingDrop(endIndex);
      col->mutatingAppend(std::move(residual));
    }
    // Note spaceMapper_ has already been updated, so it is already consistent with the coordinate
    // space of the columns.
  };
  removedRows.forEachChunk(eraseChunk);
  for (size_t i = 0; i < internals.size(); ++i) {
    columns_[i]->setInternals(std::move(internals[i]));
  }
}

void TickingTable::shift(const RowSequence &startIndex, const RowSequence &endInclusiveIndex,
    const RowSequence &destIndex) {
  auto processShift = [this](int64_t s, int64_t ei, int64_t dest) {
    mapShifter(s, ei, dest, redirection_.get());
  };
  applyShiftData(startIndex, endInclusiveIndex, destIndex, processShift);
}

std::shared_ptr<RowSequence> TickingTable::getRowSequence() const {
  return std::make_shared<MyRowSequence>(redirection_, redirection_->begin(), redirection_->end(),
      redirection_->size());
}

std::shared_ptr<UnwrappedTable>
TickingTable::unwrap(const std::shared_ptr<RowSequence> &rows,
    const std::vector<size_t> &cols) const {
  auto rowKeys = LongChunk::create(rows->size());
  auto iter = rows->getRowSequenceIterator();
  size_t destIndex = 0;
  int64_t rowKey;
  while (iter->tryGetNext(&rowKey)) {
    auto ip = redirection_->find(rowKey);
    if (ip == redirection_->end()) {
      auto message = stringf("Can't find rowkey %o", rowKey);
      throw std::runtime_error(message);
    }
    rowKeys->data()[destIndex++] = ip->second;
  }

  std::vector<std::shared_ptr<ColumnSource>> columns;
  columns.reserve(cols.size());
  for (auto colIndex: cols) {
    if (colIndex >= columns_.size()) {
      auto message = stringf("No such columnindex %o", colIndex);
      throw std::runtime_error(message);
    }
    columns.push_back(columns_[colIndex]);
  }
  return UnwrappedTable::create(std::move(rowKeys), destIndex, std::move(columns));
}

std::shared_ptr<ColumnSource> TickingTable::getColumn(size_t columnIndex) const {
  throw std::runtime_error("TickingTable: getColumn [redirected]: not implemented yet");
}

namespace {
void applyShiftData(const RowSequence &startIndex, const RowSequence &endInclusiveIndex,
    const RowSequence &destIndex,
    const std::function<void(int64_t, int64_t, int64_t)> &processShift) {
  if (startIndex.empty()) {
    return;
  }

  // Loop twice: once in the forward direction (applying negative shifts), and once in the reverse direction
  // (applying positive shifts).
  for (auto direction = 0; direction < 2; ++direction) {
    std::shared_ptr<RowSequenceIterator> startIter, endIter, destIter;
    if (direction == 0) {
      startIter = startIndex.getRowSequenceIterator();
      endIter = endInclusiveIndex.getRowSequenceIterator();
      destIter = destIndex.getRowSequenceIterator();
    } else {
      startIter = startIndex.getRowSequenceReverseIterator();
      endIter = endInclusiveIndex.getRowSequenceReverseIterator();
      destIter = destIndex.getRowSequenceReverseIterator();
    }
    int64_t start, end, dest;
    while (startIter->tryGetNext(&start)) {
      if (!endIter->tryGetNext(&end) || !destIter->tryGetNext(&dest)) {
        throw std::runtime_error("Sequences not of same size");
      }
      if (direction == 0) {
        // If forward, only process negative shifts.
        if (dest >= 0) {
          continue;
        }
      } else {
        // If reverse, only process positive shifts.
        if (dest <= 0) {
          continue;
        }
      }
      const char *dirText = direction == 0 ? "(positive)" : "(negative)";
      streamf(std::cerr, "Processing %o shift src [%o..%o] dest %o\n", dirText, start, end, dest);
      processShift(start, end, dest);
    }
  }
}

void mapShifter(int64_t start, int64_t endInclusive, int64_t dest, std::map<int64_t, int64_t> *zm) {
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

MyRowSequence::MyRowSequence(std::shared_ptr<data_t> data,
    data_t::const_iterator begin, data_t::const_iterator end, size_t size)
    : data_(std::move(data)), begin_(begin), end_(end), size_(size) {}

std::shared_ptr<RowSequenceIterator>
MyRowSequence::getRowSequenceIterator() const {
  return std::make_shared<MyRowSequenceIterator>(data_, begin_, end_, size_, true);
}

std::shared_ptr<RowSequenceIterator>
MyRowSequence::getRowSequenceReverseIterator() const {
  return std::make_shared<MyRowSequenceIterator>(data_, begin_, end_, size_, false);
}

MyRowSequenceIterator::MyRowSequenceIterator(std::shared_ptr<data_t> data,
    data_t::const_iterator begin, data_t::const_iterator end, size_t size, bool forward)
    : data_(std::move(data)), begin_(begin), end_(end), size_(size), forward_(forward) {}

MyRowSequenceIterator::~MyRowSequenceIterator() = default;

bool MyRowSequenceIterator::tryGetNext(int64_t *result) {
  if (begin_ == end_) {
    return false;
  }
  if (forward_) {
    *result = begin_->first;
    ++begin_;
  } else {
    --end_;
    *result = end_->first;
  }
  --size_;
  return true;
}

std::shared_ptr<RowSequence>
MyRowSequenceIterator::getNextRowSequenceWithLength(size_t size) {
  // TODO(kosak): iterates whole set. ugh.
  auto remaining = std::distance(begin_, end_);
  auto sizeToUse = std::min<ssize_t>((ssize_t) size, remaining);
  data_t::const_iterator newBegin, newEnd;
  if (forward_) {
    newBegin = begin_;
    std::advance(begin_, sizeToUse);
    newEnd = begin_;
  } else {
    newEnd = end_;
    std::advance(end_, -sizeToUse);
    newBegin = end_;
  }
  size_ -= sizeToUse;
  return std::make_shared<MyRowSequence>(data_, newBegin, newEnd, sizeToUse);
}
}  // namespace
}  // namespace deephaven::client::subscription

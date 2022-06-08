#include "deephaven/client/subscription/classic_table_state.h"

#include <memory>
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/subscription/shift_processor.h"
#include "deephaven/client/utility/utility.h"

using deephaven::client::column::ColumnSource;
using deephaven::client::column::NumericArrayColumnSource;
using deephaven::client::container::RowSequence;
using deephaven::client::container::RowSequenceBuilder;
using deephaven::client::utility::ColumnDefinitions;
using deephaven::client::utility::okOrThrow;
using deephaven::client::utility::streamf;
using deephaven::client::utility::stringf;

namespace deephaven::client::subscription {
namespace {
void mapShifter(uint64_t begin, uint64_t end, uint64_t dest, std::map<uint64_t, uint64_t> *map);

std::vector<std::shared_ptr<ColumnSource>> makeColumnSources(const ColumnDefinitions &colDefs);
}

ClassicTableState::ClassicTableState(const ColumnDefinitions &colDefs) {
  columns_ = makeColumnSources(colDefs);
}

ClassicTableState::~ClassicTableState() = default;

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

std::shared_ptr<RowSequence> ClassicTableState::addKeys(const RowSequence &addedRowsKeySpace) {
  // In order to give back an ordered row sequence (because at the moment we don't have an
  // unordered row sequence), we sort the keys we're going to reuse.
  auto numKeysToReuse = std::min(addedRowsKeySpace.size(), slotsToReuse_.size());
  auto reuseBegin = slotsToReuse_.end() - static_cast<ssize_t>(numKeysToReuse);
  auto reuseEnd = slotsToReuse_.end();
  std::sort(reuseBegin, reuseEnd);

  auto reuseCurrent = reuseBegin;

  RowSequenceBuilder resultBuilder;
  auto addRange = [this, &reuseCurrent, reuseEnd](uint64_t beginKey, uint64_t endKey) {
    for (auto current = beginKey; current != endKey; ++current) {
      uint64_t keyPositionSpace;
      if (reuseCurrent != reuseEnd) {
        keyPositionSpace = *reuseCurrent++;
      } else {
        keyPositionSpace = redirection_->size();
      }
      auto result = redirection_->insert(std::make_pair(current, keyPositionSpace));
      if (!result.second) {
        throw std::runtime_error(stringf("Can't add because key %o already exists", current));
      }
    }
  };
  addedRowsKeySpace.forEachChunk(addRange);
  slotsToReuse_.erase(reuseBegin, reuseEnd);
  return resultBuilder.build();
}

void ClassicTableState::addData(const std::vector<std::shared_ptr<arrow::Array>> &data,
    const RowSequence &rowsToAddIndexSpace) {

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
  if (dest < begin) {
    // dest < begin, so shift down, moving forwards.
    auto delta = begin - dest;
    auto currentp = map->lower_bound(begin);
    // currentp points to map->end(), or to the first key >= begin
    while (true) {
      if (currentp == map->end() || currentp->first >= end) {
        return;
      }
      auto nextp = std::next(currentp);
      auto node = map->extract(currentp);
      auto newKey = node.key() - delta;
      streamf(std::cerr, "Shifting down, working forwards, moving key from %o to %o\n", node.key(),
          newKey);
      node.key() = newKey;
      map->insert(std::move(node));
      currentp = nextp;
    }
    return;
  }

  // dest >= begin, so shift up, moving backwards.
  auto delta = dest - begin;
  auto currentp = map->lower_bound(end);
  // currentp points to map->begin(), or to the first key >= end
  if (currentp == map->begin()) {
    return;
  }
  --currentp;
  // now currentp points to the last key < end
  while (true) {
    if (currentp->first < begin) {
      return;
    }
    // using map->end() as a sentinel, sort of viewing it as "wrapping around".
    auto nextp = currentp != map->begin() ? std::prev(currentp) : map->end();
    auto node = map->extract(currentp);
    auto newKey = node.key() + delta;
    streamf(std::cerr, "Shifting up, working backwards, moving key from %o to %o\n", node.key(),
        newKey);
    node.key() = newKey;
    map->insert(std::move(node));
    if (nextp == map->end()) {
      return;
    }
    currentp = nextp;
  }
}

struct MyVisitor final : public arrow::TypeVisitor {
  arrow::Status Visit(const arrow::Int32Type &type) final {
    columnSource_ = NumericArrayColumnSource<int32_t>::create();
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int64Type &type) final {
    columnSource_ = NumericArrayColumnSource<int64_t>::create();
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::DoubleType &type) final {
    columnSource_ = NumericArrayColumnSource<double>::create();
    return arrow::Status::OK();
  }

  std::shared_ptr<ColumnSource> columnSource_;
};

std::vector<std::shared_ptr<ColumnSource>> makeColumnSources(const ColumnDefinitions &colDefs) {
  std::vector<std::shared_ptr<ColumnSource>> result;
  for (const auto &[name, arrowType] : colDefs.vec()) {
    MyVisitor v;
    okOrThrow(DEEPHAVEN_EXPR_MSG(arrowType->Accept(&v)));
    result.push_back(v.columnSource_);
  }

  return result;
}
}  // namespace
}  // namespace deephaven::client::subscription

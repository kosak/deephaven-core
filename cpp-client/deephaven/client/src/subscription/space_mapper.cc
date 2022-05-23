#include "deephaven/client/subscription/space_mapper.h"
#include "deephaven/client/utility/utility.h"

using deephaven::client::utility::stringf;

namespace deephaven::client::subscription {
namespace {
// We make an "iterator" that refers to a point in a numeric range.
// This is useful because we can use the "range" version of boost::multiset::insert, which
// uses hints internally and should be somewhat faster for inserting contiguous values.
struct SimpleRangeIterator {
  explicit SimpleRangeIterator(uint64_t value) : value_(value) {}

  uint64_t operator*() const { return value_; }

  SimpleRangeIterator &operator++() {
    ++value_;
    return *this;
  }

  friend bool operator!=(const SimpleRangeIterator &lhs, const SimpleRangeIterator &rhs) {
    return lhs.value_ != rhs.value_;
  }

  uint64_t value_;
};
}
SpaceMapper::SpaceMapper() = default;
SpaceMapper::~SpaceMapper() = default;

uint64_t SpaceMapper::addRange(uint64_t beginKey, uint64_t endKey) {
  auto size = endKey - beginKey;
  if (size == 0) {
    return 0;  // arbitrary
  }
  auto initialSize = set_.size();
  set_.insert(SimpleRangeIterator(beginKey), SimpleRangeIterator(endKey));
  if (set_.size() != initialSize + size) {
    throw std::runtime_error(stringf("Some elements of [%o,%o) were already in the set", beginKey,
        endKey));
  }
  return set_.find_rank(beginKey);
}

uint64_t SpaceMapper::eraseRange(uint64_t beginKey, uint64_t endKey) {
  size_t size = endKey - beginKey;
  if (size == 0) {
    return 0;  // arbitrary
  }
  auto ip = set_.find(beginKey);
  // This is ok (I think) even in the not-found case because set_.rank(set_.end()) is defined, and
  // is set_.size(). The not-found case will throw an exception shortly in the test inside the loop.
  auto result = set_.rank(ip);
  for (auto current = beginKey; current != endKey; ++current) {
    if (ip == set_.end() || *ip != current) {
      throw std::runtime_error(stringf("key %o was not in the set", current));
    }
    ip = set_.erase(ip);
  }
  return result;
}

void SpaceMapper::applyShift(uint64_t beginKey, uint64_t endKey, uint64_t destKey) {
  if (beginKey == endKey) {
    return;
  }
  if (destKey > beginKey) {
    auto amountToAdd = destKey - beginKey;
    // positive shift: work backwards
    auto ip = set_.lower_bound(endKey);
    // ip == end, or the first element >= endKey
    while (true) {
      if (ip == set_.begin()) {
        return;
      }
      // moving to the left, starting with is the last element < endKey
      --ip;
      if (*ip < beginKey) {
        // exceeded range
        return;
      }
      // We have a live one: adjust its key
      auto node = set_.extract(ip);
      node.value() = node.value() + amountToAdd;
      set_.insert(std::move(node));
    }
    return;
  }

  // destKey <= beginKey, shifts are negative, so work in the forward direction
  auto amountToSubtract = beginKey - destKey;
  // negative shift: work forwards
  auto ip = set_.lower_bound(beginKey);
  // ip == end, or the first element >= beginKey
  while (true) {
    if (ip == set_.end() || *ip >= endKey) {
      return;
    }
    auto node = set_.extract(ip);
    node.value() = node.value() - amountToSubtract;
    set_.insert(std::move(node));
    ++ip;
  }
}
}  // namespace deephaven::client::subscription

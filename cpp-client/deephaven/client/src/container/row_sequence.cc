#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/utility/utility.h"

using deephaven::client::utility::stringf;

namespace deephaven::client::container {
namespace {
/**
 * Holds a slice of a set::set<int64_t>
 */
class MyRowSequence final : public RowSequence {
  typedef std::set<uint64_t> data_t;
public:
  MyRowSequence(std::shared_ptr<data_t> data, data_t::const_iterator begin,
      data_t::const_iterator end, size_t size);
  ~MyRowSequence() final = default;
  std::shared_ptr<RowSequenceIterator> getRowSequenceIterator() const final;
  std::shared_ptr<RowSequenceIterator> getRowSequenceReverseIterator() const final;

  void forEachChunk(const std::function<void(uint64_t firstKey, uint64_t lastKey)> &f) const final;

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
  typedef std::set<uint64_t> data_t;
public:
  MyRowSequenceIterator(std::shared_ptr<data_t> data, data_t::const_iterator begin,
      data_t::const_iterator end, size_t size, bool forward);
  ~MyRowSequenceIterator() final = default;
  std::shared_ptr<RowSequence> getNextRowSequenceWithLength(size_t size) final;
  bool tryGetNext(uint64_t *result) final;

private:
  std::shared_ptr<data_t> data_;
  data_t::const_iterator begin_;
  data_t::const_iterator end_;
  size_t size_ = 0;
  bool forward_ = true;
};
} // namespace

RowSequence::~RowSequence() = default;

std::ostream &operator<<(std::ostream &s, const RowSequence &o) {
  s << '[';
  auto iter = o.getRowSequenceIterator();
  const char *sep = "";
  uint64_t item;
  while (iter->tryGetNext(&item)) {
    s << sep << item;
    sep = ", ";
  }
  s << ']';
  return s;
}

RowSequenceIterator::~RowSequenceIterator() = default;
RowSequenceBuilder::RowSequenceBuilder() = default;
RowSequenceBuilder::~RowSequenceBuilder() = default;

void RowSequenceBuilder::addRange(uint64_t begin, uint64_t end, const char *zamboniFactor) {
  if (begin >= end) {
    return;
  }
  map_t::node_type node;
  auto ip = ranges_.upper_bound(begin);
  if (ip != ranges_.end()) {
    // ip points to the first element greater than begin, or it is end()
    if (end > ip->first) {
      auto message = stringf("Looking right: new range [%o,%o) would overlap with existing range [%o, %o)",
          begin, end, ip->first, ip->second);
      throw std::runtime_error(message);
    }
    if (end == ip->first) {
      // Extend the range we are adding to include this node
      end = ip->second;
      // Reuse the storage for this node.
      node = ranges_.extract(ip);
    }
  }

  if (ip != ranges_.begin()) {
    --ip;
    // ip points to last element not greater than begin (i.e. <=, and it had better not be equal)
    if (begin < ip->second) {
      auto message = stringf("Looking left: new range [%o,%o) would overlap with existing range [%o, %o)",
          begin, end, ip->first, ip->second);
      throw std::runtime_error(message);
    }

    if (begin == ip->second) {
      // Extend the range we are adding to include this node
      begin = ip->first;
      // Reuse the storage for this node. But if we already were intending to reuse the storage
      // for the node on the right side, then throw that one away and reuse this one.
      node = ranges_.extract(ip);
    }
  }

  if (node.empty()) {
    // We were not able to reuse any nodes
    ranges_.insert(std::make_pair(begin, end));
  } else {
    // We were able to reuse at least one node (if we merged on both sides, then we can reuse
    // one node and discard one node).
    node.key() = begin;
    node.mapped() = end;
    ranges_.insert(std::move(node));
  }
}

std::shared_ptr<RowSequence> RowSequenceBuilder::build() {
  auto sp = std::make_shared<ranges_t>(std::move(ranges_));
  auto begin = sp->begin();
  auto end = sp->end();
  auto size = sp->size();
  return std::make_shared<MyRowSequence>(std::move(sp), begin, end, size);
}

namespace {
MyRowSequence::MyRowSequence(std::shared_ptr<data_t> data, data_t::const_iterator begin,
    data_t::const_iterator end, size_t size) : data_(std::move(data)), begin_(begin),
    end_(end), size_(size) {}

std::shared_ptr<RowSequenceIterator> MyRowSequence::getRowSequenceIterator() const {
  return std::make_shared<MyRowSequenceIterator>(data_, begin_, end_, size_, true);
}

std::shared_ptr<RowSequenceIterator> MyRowSequence::getRowSequenceReverseIterator() const {
  return std::make_shared<MyRowSequenceIterator>(data_, begin_, end_, size_, false);
}

void MyRowSequence::forEachChunk(const std::function<void(int64_t firstKey,
    int64_t lastKey)> &f) const {
  throw std::runtime_error("TODO(kosak): forEachChunk");
}

MyRowSequenceIterator::MyRowSequenceIterator(
    std::shared_ptr<data_t> data, data_t::const_iterator begin,
    data_t::const_iterator end, size_t size, bool forward) : data_(std::move(data)), begin_(begin),
    end_(end), size_(size), forward_(forward) {}

bool MyRowSequenceIterator::tryGetNext(int64_t *result) {
  if (begin_ == end_) {
    return false;
  }
  if (forward_) {
    *result = *begin_++;
  } else {
    *result = *--end_;
  }
  --size_;
  return true;
}

std::shared_ptr<RowSequence>
MyRowSequenceIterator::getNextRowSequenceWithLength(size_t size) {
  auto sizeToUse = std::min(size, size_);
  data_t::const_iterator newBegin, newEnd;
  if (forward_) {
    newBegin = begin_;
    std::advance(begin_, sizeToUse);
    newEnd = begin_;
  } else {
    newEnd = end_;
    std::advance(end_, -(ssize_t)sizeToUse);
    newBegin = end_;
  }
  size_ -= sizeToUse;
  return std::make_shared<MyRowSequence>(data_, newBegin, newEnd, sizeToUse);
}

}  // namespace
}  // namespace deephaven::client::container


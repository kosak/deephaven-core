#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/utility/utility.h"

using deephaven::client::utility::stringf;

namespace deephaven::client::container {
namespace {
class MyRowSequence final : public RowSequence {
  typedef std::map<uint64_t, uint64_t> ranges_t;
public:
  MyRowSequence(std::shared_ptr<ranges_t> ranges, size_t size);
  ~MyRowSequence() final = default;
  std::shared_ptr<RowSequenceIterator> getRowSequenceIterator() const final;
  std::shared_ptr<RowSequenceIterator> getRowSequenceReverseIterator() const final;

  void forEachChunk(const std::function<void(uint64_t firstKey, uint64_t lastKey)> &f) const final;

  size_t size() const final {
    return size_;
  }

private:
  std::shared_ptr<ranges_t> ranges_;
  size_t size_ = 0;
};

class MyRowSequenceIterator final : public RowSequenceIterator {
  typedef std::map<uint64_t, uint64_t> ranges_t;
public:
  MyRowSequenceIterator(std::shared_ptr<ranges_t> ranges, bool forward);
  ~MyRowSequenceIterator() final = default;
  bool tryGetNext(uint64_t *result) final;

private:
  std::shared_ptr<ranges_t> ranges_;
  ranges_t::const_iterator current_;
  size_t nextVal_ = 0;
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
  ranges_t::node_type node;
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
  size_ += end - begin;
}

std::shared_ptr<RowSequence> RowSequenceBuilder::build() {
  auto sp = std::make_shared<ranges_t>(std::move(ranges_));
  return std::make_shared<MyRowSequence>(std::move(sp), size_);
}

namespace {
MyRowSequence::MyRowSequence(std::shared_ptr<ranges_t> ranges, size_t size) :
    ranges_(std::move(ranges)), size_(size) {}

std::shared_ptr<RowSequenceIterator> MyRowSequence::getRowSequenceIterator() const {
  return std::make_shared<MyRowSequenceIterator>(ranges_, true);
}

std::shared_ptr<RowSequenceIterator> MyRowSequence::getRowSequenceReverseIterator() const {
  return std::make_shared<MyRowSequenceIterator>(ranges_, false);
}

void MyRowSequence::forEachChunk(const std::function<void(uint64_t firstKey,
    uint64_t lastKey)> &f) const {
  throw std::runtime_error("TODO(kosak): forEachChunk");
}

MyRowSequenceIterator::MyRowSequenceIterator(std::shared_ptr<ranges_t> ranges, bool forward) :
    ranges_(std::move(ranges)), forward_(forward) {
  if (forward_) {
    current_ = ranges_->begin();
    while (true) {
      if (current_ == ranges_->end()) {
        return;
      }
      if (current_->first != current_->second) {
        nextVal_ = current_->first;
        return;
      }
      ++current_;
    }
  }

  current_ = ranges_->end();
  while (true) {
    if (current_ == ranges_->begin()) {
      // We use ranges_->end() as the "exhausted" sentinal, regardless of direction.
      current_ = ranges_->end();
      return;
    }
    --current_;
    if (current_->first != current_->second) {
      nextVal_ = current_->second - 1;
      return;
    }
  }
}

bool MyRowSequenceIterator::tryGetNext(uint64_t *result) {
  // still using ranges_->end() as a sentinel, even though going backwards
  if (current_ == ranges_->end()) {
    return false;
  }
  if (forward_) {
    *result = nextVal_;
    ++nextVal_;
    if (nextVal_ != current_->second) {
      return true;
    }
    while (true) {
      ++current_;
      if (current_ == ranges_->end()) {
        // Exhausted. Return one last value, already in *result.
        return true;
      }
      if (current_->first != current_->second) {
        // Set up next value and return current value, already in *result.
        nextVal_ = current_->first;
        return true;
      }
    }
  }

  // Reverse
  *result = nextVal_;
  if (nextVal_ != current_->first) {
    --nextVal_;
    return true;
  }
  while (true) {
    if (current_ == ranges_->begin()) {
      // Exhausted. Return one last value, already in *result.
      return true;
    }
    --current_;
    if (current_->first != current_->second) {
      // Set up next value and return current value, already in *result.
      nextVal_ = current_->second - 1;
      return true;
    }
  }
}
}  // namespace
}  // namespace deephaven::client::container


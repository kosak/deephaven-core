#include "deephaven/client/container/row_sequence.h"

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

std::shared_ptr<RowSequence> RowSequence::createSequential(int64_t begin, int64_t end) {
  // Inefficient hack for now. The efficient thing to do would be to make a special implementation
  // that just iterates over the range.
  RowSequenceBuilder builder;
  if (begin != end) {
    // : decide on whether you want half-open or fully-closed intervals.
    builder.addRange(begin, end - 1);
  }
  return builder.build();
}

RowSequence::~RowSequence() = default;

std::ostream &operator<<(std::ostream &s, const RowSequence &o) {
  s << '[';
  auto iter = o.getRowSequenceIterator();
  const char *sep = "";
  int64_t item;
  while (iter->tryGetNext(&item)) {
    s << sep << item;
    sep = ", ";
  }
  s << ']';
  return s;
}

RowSequenceIterator::~RowSequenceIterator() = default;

RowSequenceBuilder::RowSequenceBuilder() : data_(std::make_shared<std::set<int64_t>>()) {}
RowSequenceBuilder::~RowSequenceBuilder() = default;

void RowSequenceBuilder::addRange(int64_t first, int64_t last) {
  if (first > last) {
    return;
  }
  while (true) {
    data_->insert(first);
    if (first == last) {
      return;
    }
    ++first;
  }
}

std::shared_ptr<RowSequence> RowSequenceBuilder::build() {
  auto begin = data_->begin();
  auto end = data_->end();
  auto size = data_->size();
  return std::make_shared<MyRowSequence>(std::move(data_), begin, end, size);
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


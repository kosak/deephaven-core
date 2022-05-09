#pragma once

#include <cstdlib>
#include <functional>
#include <memory>
#include <ostream>
#include <set>

namespace deephaven::client::highlevel::container {
class RowSequenceIterator;

class RowSequence {
public:
  static std::shared_ptr<RowSequence> createSequential(int64_t begin, int64_t end);

  virtual ~RowSequence();

  virtual std::shared_ptr<RowSequenceIterator> getRowSequenceIterator() const = 0;
  virtual std::shared_ptr<RowSequenceIterator> getRowSequenceReverseIterator() const = 0;

  virtual void forEachChunk(const std::function<void(int64_t firstKey, int64_t lastKey)> &f) const = 0;

  virtual size_t size() const = 0;

  bool empty() const {
    return size() == 0;
  }

  friend std::ostream &operator<<(std::ostream &s, const RowSequence &o);
};

class RowSequenceIterator {
public:
  virtual ~RowSequenceIterator();

  virtual std::shared_ptr<RowSequence> getNextRowSequenceWithLength(size_t size) = 0;
  virtual bool tryGetNext(int64_t *result) = 0;
};

class RowSequenceBuilder {
public:
  RowSequenceBuilder();
  ~RowSequenceBuilder();

  void addRange(int64_t first, int64_t last);

  void add(int64_t key) {
    data_->insert(key);
  }

  std::shared_ptr<RowSequence> build();

private:
  std::shared_ptr<std::set<int64_t>> data_;
};
}  // namespace deephaven::client::highlevel::container

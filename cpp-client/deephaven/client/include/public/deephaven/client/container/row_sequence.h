#pragma once

#include <cstdlib>
#include <functional>
#include <map>
#include <memory>
#include <ostream>
#include <set>

namespace deephaven::client::container {
class RowSequenceIterator;

class RowSequence {
public:
  static std::shared_ptr<RowSequence> createSequential(int64_t begin, int64_t end);

  virtual ~RowSequence();

  virtual std::shared_ptr<RowSequenceIterator> getRowSequenceIterator() const = 0;
  virtual std::shared_ptr<RowSequenceIterator> getRowSequenceReverseIterator() const = 0;

  virtual void forEachChunk(const std::function<void(uint64_t firstKey, uint64_t lastKey)> &f) const = 0;

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
  virtual bool tryGetNext(uint64_t *result) = 0;
};

class RowSequenceBuilder {
public:
  RowSequenceBuilder();
  ~RowSequenceBuilder();

  void addRange(uint64_t begin, uint64_t end, const char *superNubbin);

  void add(uint64_t key) {
    addRange(key, key + 1, "super nubbin");
  }

  std::shared_ptr<RowSequence> build();

private:
  // map of ranges begin to end
  std::map<uint64_t, uint64_t> data_;
};
}  // namespace deephaven::client::container

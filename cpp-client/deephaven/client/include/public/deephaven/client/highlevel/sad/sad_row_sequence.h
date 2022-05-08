#pragma once

#include <cstdlib>
#include <memory>
#include <ostream>
#include <set>

namespace deephaven::client::highlevel::sad {
class SadRowSequenceIterator;

class SadRowSequence {
public:
  static std::shared_ptr<SadRowSequence> createSequential(int64_t begin, int64_t end);

  virtual ~SadRowSequence();

  virtual std::shared_ptr<SadRowSequenceIterator> getRowSequenceIterator() const = 0;
  virtual std::shared_ptr<SadRowSequenceIterator> getRowSequenceReverseIterator() const = 0;

  virtual size_t size() const = 0;

  bool empty() const {
    return size() == 0;
  }

  friend std::ostream &operator<<(std::ostream &s, const SadRowSequence &o);
};

class SadRowSequenceIterator {
public:
  virtual ~SadRowSequenceIterator();

  virtual std::shared_ptr<SadRowSequence> getNextRowSequenceWithLength(size_t size) = 0;
  virtual bool tryGetNext(int64_t *result) = 0;
};

class SadRowSequenceBuilder {
public:
  SadRowSequenceBuilder();
  ~SadRowSequenceBuilder();

  void addRange(int64_t first, int64_t last);

  void add(int64_t key) {
    data_->insert(key);
  }

  std::shared_ptr<SadRowSequence> build();

private:
  std::shared_ptr<std::set<int64_t>> data_;
};
}  // namespace deephaven::client::highlevel::sad

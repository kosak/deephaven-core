#pragma once

#include <cstdlib>
#include <functional>
#include <map>
#include <memory>
#include <ostream>
#include <set>

namespace deephaven::client::container {

class UnorderedRowSequence {
public:
  std::shared_ptr<UnorderedRowSequence> createEmpty();
  std::shared_ptr<UnorderedRowSequence> createSequential(uint64_t begin, uint64_t end);

  virtual ~UnorderedRowSequence();

  virtual std::shared_ptr<UnorderedRowSequence> take(size_t size) const = 0;
  virtual std::shared_ptr<UnorderedRowSequence> drop(size_t size) const = 0;

  virtual size_t size() const = 0;

  bool empty() const {
    return size() == 0;
  }

  friend std::ostream &operator<<(std::ostream &s, const UnorderedRowSequence &o);
};

class UnorderedRowSequenceBuilder {
public:
  UnorderedRowSequenceBuilder();
  ~UnorderedRowSequenceBuilder();

  void add(uint64_t key);

  std::shared_ptr<UnorderedRowSequence> build();

private:
  typedef std::vector<uint64_t> values_t;
  values_t values_;
};
}  // namespace deephaven::client::container

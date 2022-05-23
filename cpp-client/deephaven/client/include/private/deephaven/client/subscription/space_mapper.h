#pragma once

#include <boost/multi_index_container.hpp>
#include <boost/multi_index/ordered_index.hpp>
#include <boost/multi_index/ranked_index.hpp>
#include <boost/multi_index/identity.hpp>
#include <boost/multi_index/member.hpp>

namespace deephaven::client::subscription {
class SpaceMapper {
  typedef boost::multi_index_container<
      uint64_t,
      boost::multi_index::indexed_by<
          boost::multi_index::ranked_unique<
              boost::multi_index::identity<uint64_t>
          >
      >
  > set_t;

public:
  SpaceMapper();
  ~SpaceMapper();

  uint64_t addRange(uint64_t beginKey, uint64_t endKey);
  uint64_t eraseRange(uint64_t beginKey, uint64_t endKey);
  void applyShift(uint64_t beginKey, uint64_t endKey, uint64_t destKey);

private:
  set_t set_;
};
}  // namespace deephaven::client::subscription

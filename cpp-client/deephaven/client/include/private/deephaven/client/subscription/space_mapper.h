#pragma once

namespace deephaven::client::subscription {
class SpaceMapper {
public:
  size_t addRange(size_t beginKey, size_t endKey);
  size_t eraseRange(size_t beginKey, size_t endKey);
  void applyShift(size_t beginKey, size_t endKey, size_t destKey);
};
}  // namespace deephaven::client::subscription

/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#pragma once

#include <arrow/flight/types.h>

namespace deephaven::client::utility {
// For Deephaven use only
namespace internal {
template<arrow::TimeUnit::type UNIT>
struct InternalDateTime {
  explicit InternalDateTime(int64_t value) : value_(value) {}

  int64_t value_ = 0;
};

template<arrow::TimeUnit::type UNIT>
struct InternalLocalTime {
  // Arrow Time64 only supports micro and nano units
  static_assert(UNIT == arrow::TimeUnit::MICRO || UNIT == arrow::TimeUnit::NANO);

  explicit InternalLocalTime(int64_t value) : value_(value) {}

  int64_t value_ = 0;
};
}  // namespace internal
} // namespace deephaven::client::utility

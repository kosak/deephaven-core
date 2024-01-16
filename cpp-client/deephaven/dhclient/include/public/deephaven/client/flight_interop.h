/**
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#pragma once

#include <memory>
#include <string_view>
#include <arrow/flight/client.h>
#include "deephaven/client/client.h"

namespace deephaven::client {
class ArrowTableWrapper {
public:
  explicit ArrowTableWrapper(std::shared_ptr<arrow::Table> table);

private:
  std::shared_ptr<arrow::Table> table_;

};
}  // namespace deephaven::client

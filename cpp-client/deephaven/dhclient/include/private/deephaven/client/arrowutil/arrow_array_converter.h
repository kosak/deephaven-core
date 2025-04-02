/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#pragma once

#include <memory>
#include <arrow/type.h>
#include "deephaven/dhcore/column/column_source.h"

namespace deephaven::client::arrowutil {
class ArrowArrayConverter {
  /**
   * Convenience using.
   */
  using ColumnSource = deephaven::dhcore::column::ColumnSource;
public:
  static std::shared_ptr<ColumnSource> ArrayToColumnSource(
      const std::shared_ptr<arrow::Array> &array);

  static std::shared_ptr<arrow::Array> ColumnSourceToArray(const ColumnSource &column_source,
      size_t num_rows);
};
}  // namespace deephaven::client::arrowutil

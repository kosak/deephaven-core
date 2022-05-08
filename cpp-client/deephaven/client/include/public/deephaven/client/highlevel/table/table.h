#pragma once

#include <map>
#include <memory>
#include <vector>
#include "deephaven/client/highlevel/column/column_source.h"
#include "deephaven/client/highlevel/container/row_sequence.h"
#include "deephaven/client/highlevel/table/unwrapped_table.h"

namespace deephaven::client::highlevel::table {
class Table {
public:
  Table() = default;
  virtual ~Table() = default;

  virtual std::shared_ptr<RowSequence> getRowSequence() const = 0;
  virtual std::shared_ptr<ColumnSource> getColumn(size_t columnIndex) const = 0;

  virtual std::shared_ptr<UnwrappedTable> unwrap(const std::shared_ptr<RowSequence> &rows,
      const std::vector<size_t> &cols) const = 0;

  virtual size_t numRows() const = 0;
  virtual size_t numColumns() const = 0;
};

}  // namespace deephaven::client::highlevel::table

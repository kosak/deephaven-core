#pragma once

#include <map>
#include <memory>
#include <vector>
#include "deephaven/client/chunk/chunk.h"
#include "deephaven/client/column/column_source.h"
#include "deephaven/client/container/row_sequence.h"

namespace deephaven::client::table {
class UnwrappedTable {
  struct Private {
  };

  typedef deephaven::client::chunk::LongChunk LongChunk;
  typedef deephaven::client::column::ColumnSource ColumnSource;

public:
  static std::shared_ptr<UnwrappedTable> create(std::shared_ptr<LongChunk> rowKeys,
      size_t numRows, std::vector<std::shared_ptr<ColumnSource>> columns);

  UnwrappedTable(Private, std::shared_ptr<LongChunk> &&rowKeys,
      size_t numRows, std::vector<std::shared_ptr<ColumnSource>> &&columns);
  ~UnwrappedTable();

  std::shared_ptr<LongChunk> getUnorderedRowKeys() const;
  std::shared_ptr<ColumnSource> getColumn(size_t columnIndex) const;

  size_t numRows() const { return numRows_; }

  size_t numColumns() const { return columns_.size(); }

private:
  std::shared_ptr<LongChunk> rowKeys_;
  size_t numRows_ = 0;
  std::vector<std::shared_ptr<ColumnSource>> columns_;
};
}  // namespace deephaven::client::table

#pragma once

#include <map>
#include <memory>
#include <vector>
#include "deephaven/client/highlevel/chunk/chunk.h"
#include "deephaven/client/highlevel/column/column_source.h"
#include "deephaven/client/highlevel/container/row_sequence.h"

namespace deephaven::client::highlevel::table {
class UnwrappedTable {
  struct Private {};

  typedef deephaven::client::highlevel::chunk::LongChunk LongChunk;

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
}  // namespace deephaven::client::highlevel::table

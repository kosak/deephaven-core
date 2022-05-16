#include "deephaven/client/highlevel/table/unwrapped_table.h"

#include <utility>
#include "deephaven/client/utility/utility.h"

using deephaven::client::highlevel::chunk::LongChunk;
using deephaven::client::highlevel::column::ColumnSource;

namespace deephaven::client::highlevel::table {
std::shared_ptr<UnwrappedTable> UnwrappedTable::create(std::shared_ptr<LongChunk> rowKeys,
    size_t numRows, std::vector<std::shared_ptr<ColumnSource>> columns) {
  return std::make_shared<UnwrappedTable>(Private(), std::move(rowKeys), numRows, std::move(columns));
}

UnwrappedTable::UnwrappedTable(Private, std::shared_ptr<LongChunk> &&rowKeys, size_t numRows,
    std::vector<std::shared_ptr<ColumnSource>> &&columns) : rowKeys_(std::move(rowKeys)),
    numRows_(numRows), columns_(std::move(columns)) {}

UnwrappedTable::~UnwrappedTable() = default;

std::shared_ptr<LongChunk> UnwrappedTable::getUnorderedRowKeys() const {
  return rowKeys_;
}

std::shared_ptr<ColumnSource> UnwrappedTable::getColumn(size_t columnIndex) const {
  return columns_[columnIndex];
}
}  // namespace deephaven::client::highlevel::table

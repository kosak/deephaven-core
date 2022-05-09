#pragma once
#include <memory>
#include <vector>
#include "deephaven/client/highlevel/column/column_source.h"
#include "deephaven/client/highlevel/container/row_sequence.h"
#include "deephaven/client/highlevel/table/table.h"
#include "deephaven/client/highlevel/table/unwrapped_table.h"

namespace deephaven::client::highlevel::table {
class TickingTable final : public Table {
  struct Private {};

  typedef deephaven::client::highlevel::column::ColumnSource ColumnSource;
  typedef deephaven::client::highlevel::container::RowSequence RowSequence;

public:
  static std::shared_ptr<TickingTable> create(std::vector<std::shared_ptr<ColumnSource>> columns);

  explicit TickingTable(Private, std::vector<std::shared_ptr<ColumnSource>> columns);
  ~TickingTable() final;

  /**
   * Adds the rows (which are assumed not to exist) to the table's index in the table's (source)
   * coordinate space and returns the target aka redirected row indices in the redirected
   * coordinate space. It is the caller's responsibility
   * to actually move the new column data to the right place in the column sources.
   */
  std::shared_ptr<UnwrappedTable> add(const RowSequence &addedRows);
  /**
   * Erases the rows (which are provided in the source coordinate space).
   */
  void erase(const RowSequence &removedRows);
  void shift(const RowSequence &startIndex, const RowSequence &endIndex,
      const RowSequence &destIndex);

  std::shared_ptr<RowSequence> getRowSequence() const final;

  std::shared_ptr<UnwrappedTable> unwrap(const std::shared_ptr<RowSequence> &rows,
      const std::vector<size_t> &cols) const final;

  std::shared_ptr<ColumnSource> getColumn(size_t columnIndex) const final;

  size_t numRows() const final {
    return redirection_->size();
  }

  size_t numColumns() const final {
    return columns_.size();
  }

private:
  std::vector<std::shared_ptr<ColumnSource>> columns_;
  std::shared_ptr<std::map<int64_t, int64_t>> redirection_;
  /**
   * These are slots (in the target, aka the redirected space) that we once allocated but
   * then subsequently removed, so they're available for reuse.
   */
  std::vector<int64_t> slotsToReuse_;
};
}  // namespace deephaven::client::highlevel::table


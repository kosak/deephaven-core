#pragma once
#include <memory>
#include <vector>
#include "deephaven/client/column/column_source.h"
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/subscription/space_mapper.h"
#include "deephaven/client/table/table.h"
#include "deephaven/client/utility/misc.h"

namespace deephaven::client::subscription {
class ClassicTableState final {
  typedef deephaven::client::column::ColumnSource ColumnSource;
  typedef deephaven::client::column::MutableColumnSource MutableColumnSource;
  typedef deephaven::client::container::RowSequence RowSequence;
  typedef deephaven::client::table::Table Table;
  typedef deephaven::client::utility::ColumnDefinitions ColumnDefinitions;

public:
  explicit ClassicTableState(const ColumnDefinitions &colDefs);
  ~ClassicTableState();

  std::shared_ptr<RowSequence> addKeys(const RowSequence &rowsToAddKeySpace);
  void addData(const std::vector<std::shared_ptr<arrow::Array>> &data,
      const RowSequence &rowsToAddIndexSpace);

  std::shared_ptr<RowSequence> erase(const RowSequence &rowsToRemoveKeySpace);

  std::vector<std::shared_ptr<RowSequence>> modifyKeys(
      const std::vector<std::shared_ptr<RowSequence>> &rowsToModifyKeySpace);
  void modifyData(const std::vector<std::shared_ptr<arrow::Array>> &data,
      const std::vector<std::shared_ptr<RowSequence>> &rowsToModifyIndexSpace);

  void applyShifts(const RowSequence &firstIndex, const RowSequence &lastIndex,
      const RowSequence &destIndex);

  std::shared_ptr<Table> snapshot() const;

private:
  std::vector<std::shared_ptr<MutableColumnSource>> columns_;
  std::shared_ptr<std::map<uint64_t, uint64_t>> redirection_;
  /**
   * These are slots (in the target, aka the redirected space) that we once allocated but
   * then subsequently removed, so they're available for reuse.
   */
  std::vector<uint64_t> slotsToReuse_;
};
}  // namespace deephaven::client::subscription

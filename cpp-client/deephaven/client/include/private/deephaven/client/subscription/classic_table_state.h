#pragma once
#include <memory>
#include <vector>
#include "deephaven/client/column/column_source.h"
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/subscription/space_mapper.h"
#include "deephaven/client/table/table.h"

namespace deephaven::client::subscription {
class ClassicTableState final {
  typedef deephaven::client::column::ColumnSource ColumnSource;
  typedef deephaven::client::container::RowSequence RowSequence;
  typedef deephaven::client::table::Table Table;

public:
  ClassicTableState();
  ~ClassicTableState();

  std::shared_ptr<RowSequence> add(std::vector<std::unique_ptr<AbstractFlexVectorBase>> addedData,
      std::shared_ptr<RowSequence> rowsToAddKeySpace);
  std::shared_ptr<RowSequence> erase(const RowSequence &rowsToRemoveKeySpace);

  std::vector<std::shared_ptr<RowSequence>> modify(
      std::vector<std::unique_ptr<AbstractFlexVectorBase>> modifiedData,
      std::vector<std::shared_ptr<RowSequence>> modifiedIndicesPerColumn);

  void applyShifts(const RowSequence &firstIndex, const RowSequence &lastIndex,
      const RowSequence &destIndex);

  std::shared_ptr<Table> snapshot() const;

private:
  std::vector<std::shared_ptr<ColumnSource>> columns_;
  std::shared_ptr<std::map<uint64_t, uint64_t>> redirection_;
  /**
   * These are slots (in the target, aka the redirected space) that we once allocated but
   * then subsequently removed, so they're available for reuse.
   */
  std::vector<uint64_t> slotsToReuse_;
};
}  // namespace deephaven::client::subscription

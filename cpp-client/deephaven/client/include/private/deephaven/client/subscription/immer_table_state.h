#pragma once
#include <memory>
#include <vector>
#include "deephaven/client/column/column_source.h"
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/immerutil/abstract_flex_vector.h"
#include "deephaven/client/subscription/space_mapper.h"
#include "deephaven/client/table/table.h"

namespace deephaven::client::subscription {
class ImmerTableState final {
  typedef deephaven::client::column::ColumnSource ColumnSource;
  typedef deephaven::client::container::RowSequence RowSequence;
  typedef deephaven::client::immerutil::AbstractFlexVectorBase AbstractFlexVectorBase;
  typedef deephaven::client::table::Table Table;

public:
  explicit ImmerTableState(std::vector<std::unique_ptr<AbstractFlexVectorBase>> flexVectors,
      const char *zamboniTimeShouldTakeColumnDefinitions);
  ~ImmerTableState();

  std::shared_ptr<RowSequence> add(std::vector<std::unique_ptr<AbstractFlexVectorBase>> addedData,
      std::shared_ptr<RowSequence> rowsToAddKeySpace);
  std::shared_ptr<RowSequence> erase(std::shared_ptr<RowSequence> rowsToRemoveKeySpace);

  std::vector<std::shared_ptr<RowSequence>> modify(
      std::vector<std::unique_ptr<AbstractFlexVectorBase>> modifiedData,
      std::vector<std::shared_ptr<RowSequence>> modifiedIndicesPerColumn);

  void applyShifts(const RowSequence &startIndex, const RowSequence &endIndex,
      const RowSequence &destIndex);

  std::shared_ptr<Table> snapshot() const;

private:
  std::shared_ptr<RowSequence> modifyColumn(size_t colNum,
      std::unique_ptr<AbstractFlexVectorBase> modifiedData,
      std::shared_ptr<RowSequence> rowsToModifyKeySpace);

  std::vector<std::unique_ptr<AbstractFlexVectorBase>> flexVectors_;
  // Keeps track of keyspace -> index space mapping
  SpaceMapper spaceMapper_;
};
}  // namespace deephaven::client::subscription

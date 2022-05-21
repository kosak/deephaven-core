#pragma once
#include <memory>
#include <vector>
#include "deephaven/client/column/column_source.h"
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/immerutil/abstract_flex_vector.h"
#include "deephaven/client/subscription/space_mapper.h"
#include "deephaven/client/table/table.h"

namespace deephaven::client::subscription {
class SubscribedTableState final {
  typedef deephaven::client::column::ColumnSource ColumnSource;
  typedef deephaven::client::column::ImmerColumnSourceBase ImmerColumnSourceBase;
  typedef deephaven::client::container::RowSequence RowSequence;
  typedef deephaven::client::immerutil::AbstractFlexVectorBase AbstractFlexVectorBase;
  typedef deephaven::client::table::Table Table;

public:
  explicit SubscribedTableState(std::vector<std::shared_ptr<ImmerColumnSourceBase>> columns);
  ~SubscribedTableState();

  std::shared_ptr<Table> snapshot() const;

  void add(std::vector<std::unique_ptr<AbstractFlexVectorBase>> addedData,
      const RowSequence &addedIndexes);
  /**
   * Erases the rows (which are provided in the source coordinate space).
   */
  void erase(const RowSequence &rowsToRemove);
  void applyShifts(const RowSequence &startIndex, const RowSequence &endIndex,
      const RowSequence &destIndex);

  std::shared_ptr<RowSequence> convertKeysToIndices(const RowSequence &keys) const;

  std::shared_ptr<RowSequence> getRowSequence() const final;

  std::shared_ptr<ColumnSource> getColumn(size_t columnIndex) const final;

  const std::vector<std::unique_ptr<AbstractFlexVectorBase>> &flexVectors() const;

private:
  std::vector<std::unique_ptr<AbstractFlexVectorBase>> flexVectors_;
  // Keeps track of keyspace -> index space mapping
  SpaceMapper spaceMapper_;
};
}  // namespace deephaven::client::subscription

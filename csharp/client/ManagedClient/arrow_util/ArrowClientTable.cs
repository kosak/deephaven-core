using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.ManagedClient;

public class ArrowClientTable : ClientTable {
  struct Private { };
  using ClientTable = deephaven::dhcore::clienttable::ClientTable;
  using SchemaType = deephaven::dhcore::clienttable::Schema;
  public:

  static std::shared_ptr<ClientTable> Create(std::shared_ptr<arrow::Table> arrow_table) {
    auto schema = ArrowUtil::MakeDeephavenSchema(*arrow_table->schema());
    auto row_sequence = RowSequence::CreateSequential(0, arrow_table->num_rows());

    auto column_sources = MakeReservedVector<std::shared_ptr<ColumnSource>>(arrow_table->num_columns());
    for (const auto &chunked_array : arrow_table->columns()) {
      column_sources.push_back(MakeColumnSource(*chunked_array));
    }

    return std::make_shared<ArrowClientTable>(Private(), std::move(arrow_table),
      std::move(schema), std::move(row_sequence), std::move(column_sources));
}

  ArrowClientTable(Private, std::shared_ptr<arrow::Table> arrow_table,
    std::shared_ptr<SchemaType> schema, std::shared_ptr<RowSequence> row_sequence,
    std::vector<std::shared_ptr<ColumnSource>> column_sources);
  ArrowClientTable(ArrowClientTable &&other) noexcept;
  ArrowClientTable &operator=(ArrowClientTable &&other) noexcept;
  ~ArrowClientTable() final;

  [[nodiscard]]
  std::shared_ptr<RowSequence> GetRowSequence() const final {
    return row_sequence_;
  }

  std::shared_ptr<ColumnSource> GetColumn(size_t column_index) const final;

  [[nodiscard]]
  size_t NumRows() const final {
    return arrow_table_->num_rows();
  }

  [[nodiscard]]
  size_t NumColumns() const final {
    return arrow_table_->num_columns();
  }

  [[nodiscard]]
  std::shared_ptr<SchemaType> Schema() const final {
    return schema_;
  }

  private:
  std::shared_ptr<arrow::Table> arrow_table_;
  std::shared_ptr<SchemaType> schema_;
  std::shared_ptr<RowSequence> row_sequence_;
  std::vector<std::shared_ptr<ColumnSource>> column_sources_;
}

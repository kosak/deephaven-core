using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.ManagedClient;

public class ClientTable {
  public:
  /**
   * Alias.
   */
  using ColumnSource = deephaven::dhcore::column::ColumnSource;
  /**
   * Alias.
   */
  using RowSequence = deephaven::dhcore::container::RowSequence;
  /**
   * Constructor.
   */
  ClientTable() = default;
  /**
   * Destructor.
   */
  virtual ~ClientTable() = default;

  /**
   * Get the RowSequence (in position space) that underlies this Table.
   */
  [[nodiscard]]
  virtual std::shared_ptr<RowSequence> GetRowSequence() const = 0;
  /**
   * Gets a ColumnSource from the clienttable by index.
   * @param column_index Must be in the half-open interval [0, NumColumns).
   */
  [[nodiscard]]
  virtual std::shared_ptr<ColumnSource> GetColumn(size_t column_index) const = 0;

  /**
   * Gets a ColumnSource from the clienttable by name. 'strict' controls whether the method
   * must succeed.
   * @param name The name of the column.
   * @param strict Whether the method must succeed.
   * @return If 'name' was found, returns the ColumnSource. If 'name' was not found and 'strict'
   * is true, throws an exception. If 'name' was not found and 'strict' is false, returns nullptr.
   */
  [[nodiscard]]
  std::shared_ptr<ColumnSource> GetColumn(std::string_view name, bool strict) const;
  /**
   * Gets the index of a ColumnSource from the clienttable by name. 'strict' controls whether the method
   * must succeed.
   * @param name The name of the column.
   * @param strict Whether the method must succeed.
   * @return If 'name' was found, returns the index of the ColumnSource. If 'name' was not found and
   * 'strict' is true, throws an exception. If 'name' was not found and 'strict' is false, returns
   * an empty optional.
   */
  [[nodiscard]]
  std::optional<size_t> GetColumnIndex(std::string_view name, bool strict) const;

  /**
   * Number of rows in the clienttable.
   */
  [[nodiscard]]
  virtual size_t NumRows() const = 0;
  /**
   * Number of columns in the clienttable.
   */
  [[nodiscard]]
  virtual size_t NumColumns() const = 0;
  /**
   * The clienttable schema.
   */
  [[nodiscard]]
  virtual std::shared_ptr<deephaven::dhcore::clienttable::Schema> Schema() const = 0;

  /**
   * Creates an 'ostream adaptor' to use when printing the clienttable. Example usage:
   * std::cout << myTable.Stream(true, false).
   */
  [[nodiscard]]
  internal::TableStreamAdaptor Stream(bool want_headers, bool want_row_numbers) const;

  /**
   * Creates an 'ostream adaptor' to use when printing the clienttable. Example usage:
   * std::cout << myTable.Stream(true, false, rowSeq).
   */
  [[nodiscard]]
  internal::TableStreamAdaptor Stream(bool want_headers, bool want_row_numbers,
      std::shared_ptr<RowSequence> row_sequence) const;

  /**
   * Creates an 'ostream adaptor' to use when printing the clienttable. Example usage:
   * std::cout << myTable.Stream(true, false, rowSequences).
   */
  [[nodiscard]]
  internal::TableStreamAdaptor Stream(bool want_headers, bool want_row_numbers,
      std::vector<std::shared_ptr<RowSequence>> row_sequences) const;

  /**
   * For debugging and demos.
   */
  [[nodiscard]]
  std::string ToString(bool want_headers, bool want_row_numbers) const;

  /**
   * For debugging and demos.
   */
  [[nodiscard]]
  std::string ToString(bool want_headers, bool want_row_numbers,
      std::shared_ptr<RowSequence> row_sequence) const;

  /**
   * For debugging and demos.
   */
  [[nodiscard]]
  std::string ToString(bool want_headers, bool want_row_numbers,
      std::vector<std::shared_ptr<RowSequence>> row_sequences) const;
}

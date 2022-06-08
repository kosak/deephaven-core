#pragma once

#include <cstdlib>
#include <map>
#include <set>
#include <arrow/type.h>
#include "deephaven/client/utility/callbacks.h"
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/table/table.h"
#include "immer/flex_vector.hpp"

namespace deephaven::client {
class ClassicTickingUpdate;
class ImmerTickingUpdate;
class TickingCallback : public deephaven::client::utility::FailureCallback {
public:
  /**
   * @param update An update message which describes the changes (removes, adds, modifies) that
   * transform the previous version of the table to the new version. This class is threadsafe and
   * can be kept around for an arbitrary amount of time. On the other hand, it probably should be
   * processed and discard quickly so that the underlying resources can be reused.
   */
  virtual void onTick(const ClassicTickingUpdate &update) = 0;
  virtual void onTick(const ImmerTickingUpdate &update) = 0;
};

class ClassicTickingUpdate final {
protected:
  typedef deephaven::client::column::ColumnSource ColumnSource;
  typedef deephaven::client::container::RowSequence RowSequence;
  typedef deephaven::client::table::Table Table;

public:
  ClassicTickingUpdate(std::shared_ptr<RowSequence> removedRows,
      std::shared_ptr<RowSequence> addedRows,
      std::vector<std::shared_ptr<RowSequence>> modifiedRows,
      std::shared_ptr<Table> current);
  ClassicTickingUpdate(ClassicTickingUpdate &&other) noexcept;
  ClassicTickingUpdate &operator=(ClassicTickingUpdate &&other) noexcept;
  ~ClassicTickingUpdate();

  // In the pre-shift key space
  const std::shared_ptr<RowSequence> &removedRows() const { return removedRows_; }
  // In the post-shift key space
  const std::shared_ptr<RowSequence> &addedRows() const { return addedRows_; }
  // In the post-shift key space
  const std::vector<std::shared_ptr<RowSequence>> &modifiedRows() const { return modifiedRows_; }
  const std::shared_ptr<Table> &current() const { return current_; }

private:
  // In the pre-shift key space
  std::shared_ptr<RowSequence> removedRows_;
  // In the post-shift key space
  std::shared_ptr<RowSequence> addedRows_;
  // In the post-shift key space
  std::vector<std::shared_ptr<RowSequence>> modifiedRows_;
  std::shared_ptr<Table> current_;
};

class ImmerTickingUpdate final {
protected:
  typedef deephaven::client::container::RowSequence RowSequence;
  typedef deephaven::client::table::Table Table;

public:
  ImmerTickingUpdate(std::shared_ptr<Table> beforeRemoves,
      std::shared_ptr<Table> beforeModifies,
      std::shared_ptr<Table> current,
      std::shared_ptr<RowSequence> removed,
      std::vector<std::shared_ptr<RowSequence>> modified,
      std::shared_ptr<RowSequence> added);
  ImmerTickingUpdate(ImmerTickingUpdate &&other) noexcept;
  ImmerTickingUpdate &operator=(ImmerTickingUpdate &&other) noexcept;
  ~ImmerTickingUpdate();

  const std::shared_ptr<Table> &beforeRemoves() const { return beforeRemoves_; }
  const std::shared_ptr<Table> &beforeModifies() const { return beforeModifies_; }
  const std::shared_ptr<Table> &current() const { return current_; }
  // In the key space of 'prevTable'
  const std::shared_ptr<RowSequence> &removed() const { return removed_; }
  // In the key space of 'thisTable'
  const std::vector<std::shared_ptr<RowSequence>> &modified() const { return modified_; }
  // In the key space of 'thisTable'
  const std::shared_ptr<RowSequence> &added() const { return added_; }

private:
  std::shared_ptr<Table> beforeRemoves_;
  std::shared_ptr<Table> beforeModifies_;
  std::shared_ptr<Table> current_;
  // In the key space of 'beforeRemoves_'
  std::shared_ptr<RowSequence> removed_;
  // In the key space of beforeModifies_ and current_, which have the same key space.
  // Old values are in beforeModifies_; new values are in current_.
  std::vector<std::shared_ptr<RowSequence>> modified_;
  // In the key space of current_.
  std::shared_ptr<RowSequence> added_;
};
}  // namespace deephaven::client

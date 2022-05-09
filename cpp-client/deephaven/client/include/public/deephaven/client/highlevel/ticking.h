#pragma once

#include <cstdlib>
#include <map>
#include <set>
#include <arrow/type.h>
#include "deephaven/client/utility/callbacks.h"
#include "deephaven/client/highlevel/container/row_sequence.h"
#include "deephaven/client/highlevel/table/table.h"
#include "immer/flex_vector.hpp"

namespace deephaven::client::highlevel {
class TickingUpdate;
class TickingCallback : public deephaven::client::utility::FailureCallback {
public:
  /**
   * @param update An update message which describes the changes (removes, adds, modifies) that
   * transform the previous version of the table to the new version. This class is threadsafe and
   * can be kept around for an arbitrary amount of time. On the other hand, it probably should be
   * processed and discard quickly so that the underlying resources can be reused.
   */
  virtual void onTick(const std::shared_ptr<TickingUpdate> &update) = 0;
};

class TickingUpdate {
protected:
  typedef deephaven::client::highlevel::container::RowSequence RowSequence;
  typedef deephaven::client::highlevel::table::Table Table;

private:
  std::shared_ptr<Table> prevTable_;
  std::shared_ptr<Table> thisTable_;
  // In the key space of 'prevTable'
  std::shared_ptr<RowSequence> removes_;
  // In the key space of 'thisTable'
  std::shared_ptr<RowSequence> modifies_;
  // In the key space of 'thisTable'
  std::shared_ptr<RowSequence> adds_;
};
}  // namespace deephaven::client::highlevel

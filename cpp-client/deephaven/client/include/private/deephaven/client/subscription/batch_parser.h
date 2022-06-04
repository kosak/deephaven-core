#pragma once

#include <cstdlib>
#include <arrow/flight/client.h>
#include "deephaven/client/utility/misc.h"

namespace deephaven::client::subscription {
class BatchParser {
  typedef deephaven::client::utility::ColumnDefinitions ColumnDefinitions;
public:

  BatchParser() = delete;

  static void parseBatches(
      const ColumnDefinitions &colDefs,
      int64_t numBatches,
      bool allowInconsistentColumnSizes,
      arrow::flight::FlightStreamReader *fsr,
      arrow::flight::FlightStreamChunk *flightStreamChunk);
};
}  // namespace deephaven::client::subscription

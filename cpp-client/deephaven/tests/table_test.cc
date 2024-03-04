/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#include <iostream>
#include "tests/third_party/catch.hpp"
#include "tests/test_util.h"
#include "deephaven/client/client.h"
#include "deephaven/dhcore/chunk/chunk.h"
#include "deephaven/dhcore/container/row_sequence.h"
#include "deephaven/dhcore/types.h"
#include "deephaven/dhcore/utility/utility.h"
#include "deephaven/third_party/fmt/format.h"

using deephaven::client::Client;
using deephaven::client::TableHandle;
using deephaven::client::utility::TableMaker;
using deephaven::dhcore::chunk::BooleanChunk;
using deephaven::dhcore::chunk::Int32Chunk;
using deephaven::dhcore::container::RowSequence;
using deephaven::dhcore::DeephavenConstants;

namespace deephaven::client::tests {
TEST_CASE("Fetch a whole table (small)", "[table]") {
  auto tm = TableMakerForTests::Create();
  auto thm = tm.Client().GetManager();
  auto th = thm.EmptyTable(10).Update("II = ii");
  std::cout << th.Stream(true) << '\n';

  auto table = th.ToLocalTable();
  auto schema = table.Schema();

  auto rs = RowSequence::CreateSequential(0, table.NumRows());
  auto dest_data = Int32Chunk::Create(table.NumRows());
  auto null_flags = BooleanChunk::Create(table.NumRows());
  for (size_t col_num = 0; col_num != schema->NumCols(); ++col_num) {
    fmt::println("Processing column {}: {}", col_num, schema->Names()[col_num]);
    auto column_source = table.GetColumn(col_num);
    column_source->FillChunk(*rs, &dest_data, &null_flags);
    fmt::println("Hi, do something with chunk here");
  }

  tm.Client().Close();
}
}  // namespace deephaven::client::tests

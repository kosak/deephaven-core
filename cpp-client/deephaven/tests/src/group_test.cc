/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include <cstdint>
#include <iostream>
#include <string>
#include <vector>

#include "deephaven/third_party/catch.hpp"
#include "deephaven/tests/test_util.h"
#include "deephaven/client/client.h"
#include "deephaven/client/utility/table_maker.h"
#include "deephaven/dhcore/container/row_sequence.h"

using deephaven::client::utility::TableMaker;

namespace deephaven::client::tests {
TEST_CASE("Group a Table", "[group]") {
  auto tm = TableMakerForTests::Create();

  auto t = tm.Client().GetManager().EmptyTable(10).Update("II = ii");
  auto t2 = t.By().By().By();
  std::cout << t2.Stream(true) << '\n';
}
}  // namespace deephaven::client::tests

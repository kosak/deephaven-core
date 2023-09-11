/*
 * Copyright (c) 2016-2023 Deephaven Data Labs and Patent Pending
 */
#include "tests/third_party/catch.hpp"
#include "tests/test_util.h"

#include "deephaven/dhcore/utility/utility.h"
#include "deephaven/client/client.h"

using deephaven::client::Aggregate;

namespace deephaven::client::tests {
TEST_CASE("Range Join: Example", "[range_join]") {
  auto tm = TableMakerForTests::Create();
  auto thm = tm.Client().GetManager();

  auto lt = thm.EmptyTable(20).UpdateView({"X=ii", "Y=X % 5", "LStartValue=ii / 0.7", "LEndValue=ii / 0.1"});
  auto rt = thm.EmptyTable(20).UpdateView({"X=ii", "Y=X % 5", "RValue=ii / 0.3"});

  auto result = lt.RangeJoin(rt, {"Y", "LStartValue < RValue < LEndValue"}, Aggregate::Group({"X"}));}
}  // namespace deephaven::client::tests

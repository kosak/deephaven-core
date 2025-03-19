/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include <iostream>
#include "deephaven/third_party/catch.hpp"
#include "deephaven/tests/test_util.h"
#include "deephaven/client/client.h"
#include "deephaven/dhcore/utility/utility.h"

using deephaven::client::TableHandle;
using deephaven::client::Client;
using deephaven::client::TableHandle;
using deephaven::client::utility::TableMaker;
using deephaven::dhcore::DateTime;

namespace deephaven::client::tests {
TEST_CASE("Group a Table", "[group]") {
  auto tm = TableMakerForTests::Create();

  std::vector<std::string> type_data = {
      "Granny Smith",
      "Granny Smith",
      "Gala",
      "Gala",
      "Golden Delicious",
      "Golden Delicious"
  };

  std::vector<std::string> color_data = {
      "Green", "Green", "Red-Green", "Orange-Green", "Yellow", "Yellow"
  };

  std::vector<int32_t> weight_data = {
      // 102, 85, 79, 92, 78, 99
      0, 0, 0, 1, 1, 1
  };

  std::vector<int32_t> calorie_data = {
      53, 48, 51, 61, 46, 57
  };

  TableMaker maker;
  maker.AddColumn("Type", type_data);
  maker.AddColumn("Color", color_data);
  maker.AddColumn("Weight", weight_data);
  maker.AddColumn("Calories", calorie_data);
  auto t1 = maker.MakeTable(tm.Client().GetManager());

  auto grouped = t1.By("Weight");

  std::cout << grouped.Stream(true) << '\n';

  auto ct1 = grouped.ToClientTable();
  auto c1 = ct1->GetColumn(2);

  std::cout << "What is this\n";
}
}  // namespace deephaven::client::tests

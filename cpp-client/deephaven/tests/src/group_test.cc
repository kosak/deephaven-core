/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include <iostream>
#include "deephaven/third_party/catch.hpp"
#include "deephaven/tests/test_util.h"
#include "deephaven/client/client.h"
#include "deephaven/dhcore/utility/utility.h"
#include "deephaven/dhcore/container/row_sequence.h"

using deephaven::client::TableHandle;
using deephaven::client::Client;
using deephaven::client::TableHandle;
using deephaven::client::utility::TableMaker;
using deephaven::dhcore::DateTime;
using deephaven::dhcore::chunk::BooleanChunk;
using deephaven::dhcore::chunk::ContainerBaseChunk;
using deephaven::dhcore::chunk::StringChunk;
using deephaven::dhcore::container::RowSequence;

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
      102, 85, 79, 92, 78, 99
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

  auto grouped = t1.By("Type");

  std::cout << grouped.Stream(true) << '\n';

  std::vector<std::string> type_as_group_key = {
      "Granny Smith",
      "Gala",
      "Golden Delicious"
  };

  std::vector<std::vector<std::string>> grouped_color = {
      {"Green", "Green"},
      {"Red-Green", "Orange-Green"},
      {"Yellow", "Yellow"}
  };

  TableMaker expected;
  expected.AddColumn("Type", type_as_group_key);
  expected.AddColumn("Color", grouped_color);
  expected.AddColumn("Weight", grouped_color);
  expected.AddColumn("Calories", grouped_color);

      "Color", grouped_color,
      "Weight", grouped_color,
      "Calories", grouped_color
  );
}
}  // namespace deephaven::client::tests

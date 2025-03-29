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
using deephaven::dhcore::chunk::BooleanChunk;
using deephaven::dhcore::chunk::ContainerBaseChunk;
using deephaven::dhcore::container::RowSequence;

namespace deephaven::client::tests {
TEST_CASE("Group a Table", "[group]") {
  auto tm = TableMakerForTests::Create();

  TableMaker maker;
  maker.AddColumn<std::string>("Type", {
      "Granny Smith",
      "Granny Smith",
      "Gala",
      "Gala",
      "Golden Delicious",
      "Golden Delicious"
  });
  maker.AddColumn<std::string>("Color", {
      "Green", "Green", "Red-Green", "Orange-Green", "Yellow", "Yellow"
  });
  maker.AddColumn<int32_t>("Weight", {
      // 102, 85, 79, 92, 78, 99
      0, 0, 0, 1, 1, 1
  });
  maker.AddColumn<int32_t>("Calories", {
      53, 48, 51, 61, 46, 57
  });
  auto t1 = maker.MakeTable(tm.Client().GetManager());

  auto grouped = t1.By("Weight");

  std::cout << grouped.Stream(true) << '\n';

  auto ct1 = grouped.ToClientTable();
  auto col2 = ct1->GetColumn(1);
  auto chunk = ContainerBaseChunk::Create(50);
  auto nulls = BooleanChunk::Create(50);
  auto rs = RowSequence::CreateSequential(0, grouped.NumRows());
  col2->FillChunk(*rs, &chunk, &nulls);

  // auto nr = grouped.NumRows();
  const auto &data0 = chunk.data()[0]->AsContainer<std::string>();
  const auto &data1 = chunk.data()[1]->AsContainer<std::int32_t>();

  for (const auto &e : data0) {
    std::cout << "hello " << e << '\n';
  }

  for (const auto &e : data1) {
    std::cout << "hello " << e << '\n';
  }


  std::cout << "What is this\n";
}
}  // namespace deephaven::client::tests

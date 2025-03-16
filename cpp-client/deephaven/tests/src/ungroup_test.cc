/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/third_party/catch.hpp"
#include "deephaven/tests/test_util.h"

using deephaven::client::utility::TableMaker;

namespace deephaven::client::tests {
TEST_CASE("Ungroup columns", "[ungroup]") {
  auto tm = TableMakerForTests::Create();
  auto table = tm.Table();

  table = table.Where("ImportDate == `2017-11-01`");

  auto by_table = table.Where("Ticker == `AAPL`").View("Ticker", "Close").By("Ticker");
  std::cout << by_table.Stream(true) << '\n';
  auto ungrouped = by_table.Ungroup("Close");

  TableMaker expected1;
  expected1.AddColumn<std::string>("Ticker", {"AAPL"});
  expected1.AddColumn<std::string>("Close", {"[23.5,24.2,26.7]"});
  TableComparerForTests::Compare(expected1, by_table);

  std::vector<std::string> ug_ticker_data = {"AAPL", "AAPL", "AAPL"};
  std::vector<double> ug_close_data = {23.5, 24.2, 26.7};
  TableMaker expected2;
  expected1.AddColumn<std::string>("Ticker", {"AAPL", "AAPL", "AAPL"});
  expected1.AddColumn<double>("Close", {23.5, 24.2, 26.7});
  TableComparerForTests::Compare(expected2, ungrouped);
}
}  // namespace deephaven::client::tests

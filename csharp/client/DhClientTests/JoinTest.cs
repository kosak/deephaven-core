using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.Utility;
using System;
using Xunit.Abstractions;

namespace Deephaven.DhClientTests;

public class JoinTest {
  private readonly ITestOutputHelper _output;

  public JoinTest(ITestOutputHelper output) {
    _output = output;
  }

  [Fact]
  public void TestJoin() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var tm = ctx.Client.Manager;
    var testTable = ctx.TestTable;

    using var table = testTable.Where("ImportDate == `2017-11-01`");
    using var lastClose = table.LastBy("Ticker");
    using var avgView = table.View("Ticker", "Volume").AvgBy("Ticker");

    using var joined = lastClose.NaturalJoin(avgView, new[] {"Ticker"}, new[]{ "ADV = Volume"});
    using var filtered = joined.Select("Ticker", "Close", "ADV");

    var tickerData = new[] { "XRX", "XYZZY", "IBM", "GME", "AAPL", "ZNGA" };
    var closeData = new[] { 53.8, 88.5, 38.7, 453, 26.7, 544.9 };
    var advData = new[] { 216000, 6060842, 138000, 138000000, 123000, 47211.50 };

    var tc = new TableComparer();
    tc.AddColumn("Ticker", tickerData);
    tc.AddColumn("Close", closeData);
    tc.AddColumn("ADV", advData);
    tc.AssertEqualTo(filtered);
  }

  [Fact]
  public void TestAj() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var tm = ctx.Client.Manager;
    var testTable = ctx.TestTable;

    TableHandle trades;
    {
      var tickerData = new[] { "AAPL", "AAPL", "AAPL", "IBM", "IBM" };
      var instantDdata = new[] {
        DhDateTime.Parse("2021-04-05T09:10:00-0500"),
        DhDateTime.Parse("2021-04-05T09:31:00-0500"),
        DhDateTime.Parse("2021-04-05T16:00:00-0500"),
        DhDateTime.Parse("2021-04-05T16:00:00-0500"),
        DhDateTime.Parse("2021-04-05T16:30:00-0500")
      };
      var priceData = new[] { 2.5, 3.7, 3.0, 100.50, 110 };
      var sizeData = new[] { 52, 14, 73, 11, 6 };
      using var tableMaker = new TableMaker();
      tableMaker.AddColumn("Ticker", tickerData);
      tableMaker.AddColumn("Timestamp", instantDdata);
      tableMaker.AddColumn("Price", priceData);
      tableMaker.AddColumn("Size", sizeData);
      trades = tableMaker.MakeTable(tm);
    }

    TableHandle quotes;
    {
      var tickerData = new[] { "AAPL", "AAPL", "IBM", "IBM", "IBM" };
      var timeStampData = new[] {
        DhDateTime.Parse("2021-04-05T09:11:00-0500"),
        DhDateTime.Parse("2021-04-05T09:30:00-0500"),
        DhDateTime.Parse("2021-04-05T16:00:00-0500"),
        DhDateTime.Parse("2021-04-05T16:30:00-0500"),
        DhDateTime.Parse("2021-04-05T17:00:00-0500")
      };
      var bidData = new[] { 2.5, 3.4, 97, 102, 108 };
      var bidSizeData = new[] { 10, 20, 5, 13, 23 };
      var askData = new[] { 2.5, 3.4, 105, 110, 111 };
      var askSizeData = new[] { 83, 33, 47, 15, 5 };
      var tableMaker = new TableMaker();
      tableMaker.AddColumn("Ticker", tickerData);
      tableMaker.AddColumn("Timestamp", timeStampData);
      tableMaker.AddColumn("Bid", bidData);
      tableMaker.AddColumn("BidSize", bidSizeData);
      tableMaker.AddColumn("Ask", askData);
      tableMaker.AddColumn("AskSize", askSizeData);
      quotes = tableMaker.MakeTable(tm);
    }

    using var result = trades.Aj(quotes, new[] { "Ticker", "Timestamp" });

    // Expected data
    {
      var tickerData = new[] { "AAPL", "AAPL", "AAPL", "IBM", "IBM" };
      var timestampData = new[] {
        DhDateTime.Parse("2021-04-05T09:10:00-0500"),
        DhDateTime.Parse("2021-04-05T09:31:00-0500"),
        DhDateTime.Parse("2021-04-05T16:00:00-0500"),
        DhDateTime.Parse("2021-04-05T16:00:00-0500"),
        DhDateTime.Parse("2021-04-05T16:30:00-0500")
      };
      var priceData = new[] { 2.5, 3.7, 3.0, 100.50, 110 };
      var sizeData = new[] { 52, 14, 73, 11, 6 };
      var bidData = new double?[] { null, 3.4, 3.4, 97, 102 };
      var bidSizeData = new int?[] { null, 20, 20, 5, 13 };
      var askData = new double?[] { null, 3.4, 3.4, 105, 110 };
      var askSizeData = new int?[] { null, 33, 33, 47, 15 };

      var tc = new TableComparer();
      tc.AddColumn("Ticker", tickerData);
      tc.AddColumn("Timestamp", timestampData);
      tc.AddColumn("Price", priceData);
      tc.AddColumn("Size", sizeData);
      tc.AddColumn("Bid", bidData);
      tc.AddColumn("BidSize", bidSizeData);
      tc.AddColumn("Ask", askData);
      tc.AddColumn("AskSize", askSizeData);
      tc.AssertEqualTo(result);
  }
}
/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
# include "deephaven/third_party/catch.hpp"
# include "deephaven/tests/test_util.h"
# include "deephaven/third_party/fmt/format.h"

  using deephaven::client::TableHandleManager;
using deephaven::client::TableHandle;
using deephaven::client::utility::TableMaker;
using deephaven::dhcore::DateTime;

namespace deephaven::client::tests {

  TEST_CASE("Aj", "[join]") {
    auto tm = TableMakerForTests::Create();
    auto q = arrow::timestamp(arrow::TimeUnit::NANO, "UTC");

    TableHandle trades;
    {
      std::vector < std::string> ticker_data = { "AAPL", "AAPL", "AAPL", "IBM", "IBM"};
      std::vector<DateTime> instant_data = {
        DateTime::Parse("2021-04-05T09:10:00-0500"),
        DateTime::Parse("2021-04-05T09:31:00-0500"),
        DateTime::Parse("2021-04-05T16:00:00-0500"),
        DateTime::Parse("2021-04-05T16:00:00-0500"),
        DateTime::Parse("2021-04-05T16:30:00-0500")
    };
      std::vector<double> price_data = { 2.5, 3.7, 3.0, 100.50, 110 };
      std::vector<int32_t> size_data = { 52, 14, 73, 11, 6 };
      TableMaker table_maker;
      table_maker.AddColumn("Ticker", ticker_data);
      table_maker.AddColumn("Timestamp", instant_data);
      table_maker.AddColumn("Price", price_data);
      table_maker.AddColumn("Size", size_data);
      trades = table_maker.MakeTable(tm.Client().GetManager());
      // std::cout << trades.Stream(true) << '\n';
    }

    TableHandle quotes;
    {
      std::vector < std::string> ticker_data = { "AAPL", "AAPL", "IBM", "IBM", "IBM"};
      std::vector<DateTime> timestamp_data = {
        DateTime::Parse("2021-04-05T09:11:00-0500"),
        DateTime::Parse("2021-04-05T09:30:00-0500"),
        DateTime::Parse("2021-04-05T16:00:00-0500"),
        DateTime::Parse("2021-04-05T16:30:00-0500"),
        DateTime::Parse("2021-04-05T17:00:00-0500")
    };
      std::vector<double> bid_data = { 2.5, 3.4, 97, 102, 108 };
      std::vector<int32_t> bid_size_data = { 10, 20, 5, 13, 23 };
      std::vector<double> ask_data = { 2.5, 3.4, 105, 110, 111 };
      std::vector<int32_t> ask_size_data = { 83, 33, 47, 15, 5 };
      TableMaker table_maker;
      table_maker.AddColumn("Ticker", ticker_data);
      table_maker.AddColumn("Timestamp", timestamp_data);
      table_maker.AddColumn("Bid", bid_data);
      table_maker.AddColumn("BidSize", bid_size_data);
      table_maker.AddColumn("Ask", ask_data);
      table_maker.AddColumn("AskSize", ask_size_data);
      quotes = table_maker.MakeTable(tm.Client().GetManager());
      // std::cout << quotes.Stream(true) << '\n';
    }

    auto result = trades.Aj(quotes, { "Ticker", "Timestamp"});
    // std::cout << result.Stream(true) << '\n';

    // Expected data
    {
      std::vector < std::string> ticker_data = { "AAPL", "AAPL", "AAPL", "IBM", "IBM"};
      std::vector<DateTime> timestamp_data = {
        DateTime::Parse("2021-04-05T09:10:00-0500"),
        DateTime::Parse("2021-04-05T09:31:00-0500"),
        DateTime::Parse("2021-04-05T16:00:00-0500"),
        DateTime::Parse("2021-04-05T16:00:00-0500"),
        DateTime::Parse("2021-04-05T16:30:00-0500")
    };
      for (const auto &ts : timestamp_data) {
        fmt::print(std::cout, "{} - {}\n", ts, ts.Nanos());

      }
      std::vector<double> price_data = { 2.5, 3.7, 3.0, 100.50, 110 };
      std::vector<int32_t> size_data = { 52, 14, 73, 11, 6 };
      std::vector<std::optional<double>> bid_data = { { }, 3.4, 3.4, 97, 102 };
      std::vector<std::optional<int32_t>> bid_size_data = { { }, 20, 20, 5, 13 };
      std::vector<std::optional<double>> ask_data = { { }, 3.4, 3.4, 105, 110 };
      std::vector<std::optional<int32_t>> ask_size_data = { { }, 33, 33, 47, 15 };
      TableMaker table_maker;
      table_maker.AddColumn("Ticker", ticker_data);
      table_maker.AddColumn("Timestamp", timestamp_data);
      table_maker.AddColumn("Price", price_data);
      table_maker.AddColumn("Size", size_data);
      table_maker.AddColumn("Bid", bid_data);
      table_maker.AddColumn("BidSize", bid_size_data);
      table_maker.AddColumn("Ask", ask_data);
      table_maker.AddColumn("AskSize", ask_size_data);

      CompareTable(
          result,
          "Ticker", ticker_data,
          "Timestamp", timestamp_data,
          "Price", price_data,
          "Size", size_data,
          "Bid", bid_data,
          "BidSize", bid_size_data,
          "Ask", ask_data,
          "AskSize", ask_size_data);
    }
  }

  TEST_CASE("Raj", "[join]") {
    auto tm = TableMakerForTests::Create();
    auto q = arrow::timestamp(arrow::TimeUnit::NANO, "UTC");

    TableHandle trades;
    {
      std::vector < std::string> ticker_data = { "AAPL", "AAPL", "AAPL", "IBM", "IBM"};
      std::vector<DateTime> instant_data = {
        DateTime::Parse("2021-04-05T09:10:00-0500"),
        DateTime::Parse("2021-04-05T09:31:00-0500"),
        DateTime::Parse("2021-04-05T16:00:00-0500"),
        DateTime::Parse("2021-04-05T16:00:00-0500"),
        DateTime::Parse("2021-04-05T16:30:00-0500")
    };
      std::vector<double> price_data = { 2.5, 3.7, 3.0, 100.50, 110 };
      std::vector<int32_t> size_data = { 52, 14, 73, 11, 6 };
      TableMaker table_maker;
      table_maker.AddColumn("Ticker", ticker_data);
      table_maker.AddColumn("Timestamp", instant_data);
      table_maker.AddColumn("Price", price_data);
      table_maker.AddColumn("Size", size_data);
      trades = table_maker.MakeTable(tm.Client().GetManager());
      // std::cout << trades.Stream(true) << '\n';
    }

    TableHandle quotes;
    {
      std::vector < std::string> ticker_data = { "AAPL", "AAPL", "IBM", "IBM", "IBM"};
      std::vector<DateTime> timestamp_data = {
        DateTime::Parse("2021-04-05T09:11:00-0500"),
        DateTime::Parse("2021-04-05T09:30:00-0500"),
        DateTime::Parse("2021-04-05T16:00:00-0500"),
        DateTime::Parse("2021-04-05T16:30:00-0500"),
        DateTime::Parse("2021-04-05T17:00:00-0500")
    };
      std::vector<double> bid_data = { 2.5, 3.4, 97, 102, 108 };
      std::vector<int32_t> bid_size_data = { 10, 20, 5, 13, 23 };
      std::vector<double> ask_data = { 2.5, 3.4, 105, 110, 111 };
      std::vector<int32_t> ask_size_data = { 83, 33, 47, 15, 5 };
      TableMaker table_maker;
      table_maker.AddColumn("Ticker", ticker_data);
      table_maker.AddColumn("Timestamp", timestamp_data);
      table_maker.AddColumn("Bid", bid_data);
      table_maker.AddColumn("BidSize", bid_size_data);
      table_maker.AddColumn("Ask", ask_data);
      table_maker.AddColumn("AskSize", ask_size_data);
      quotes = table_maker.MakeTable(tm.Client().GetManager());
      // std::cout << quotes.Stream(true) << '\n';
    }

    auto result = trades.Raj(quotes, { "Ticker", "Timestamp"});
    // std::cout << result.Stream(true) << '\n';

    // Expected data
    {
      std::vector < std::string> ticker_data = { "AAPL", "AAPL", "AAPL", "IBM", "IBM"};
      std::vector<DateTime> timestamp_data = {
        DateTime::Parse("2021-04-05T09:10:00-0500"),
        DateTime::Parse("2021-04-05T09:31:00-0500"),
        DateTime::Parse("2021-04-05T16:00:00-0500"),
        DateTime::Parse("2021-04-05T16:00:00-0500"),
        DateTime::Parse("2021-04-05T16:30:00-0500")
    };
      for (const auto &ts : timestamp_data) {
        fmt::print(std::cout, "{} - {}\n", ts, ts.Nanos());

      }
      std::vector<double> price_data = { 2.5, 3.7, 3.0, 100.50, 110 };
      std::vector<int32_t> size_data = { 52, 14, 73, 11, 6 };
      std::vector<std::optional<double>> bid_data = { 2.5, { }, { }, 97, 102 };
      std::vector<std::optional<int32_t>> bid_size_data = { 10, { }, { }, 5, 13 };
      std::vector<std::optional<double>> ask_data = { 2.5, { }, { }, 105, 110 };
      std::vector<std::optional<int32_t>> ask_size_data = { 83, { }, { }, 47, 15 };
      TableMaker table_maker;
      table_maker.AddColumn("Ticker", ticker_data);
      table_maker.AddColumn("Timestamp", timestamp_data);
      table_maker.AddColumn("Price", price_data);
      table_maker.AddColumn("Size", size_data);
      table_maker.AddColumn("Bid", bid_data);
      table_maker.AddColumn("BidSize", bid_size_data);
      table_maker.AddColumn("Ask", ask_data);
      table_maker.AddColumn("AskSize", ask_size_data);

      CompareTable(
          result,
          "Ticker", ticker_data,
          "Timestamp", timestamp_data,
          "Price", price_data,
          "Size", size_data,
          "Bid", bid_data,
          "BidSize", bid_size_data,
          "Ask", ask_data,
          "AskSize", ask_size_data);
    }
  }
}  // namespace deephaven::client::tests

/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include <cstdint>
#include <iostream>
#include <limits>
#include <optional>
#include <vector>

#include "deephaven/client/client.h"
#include "deephaven/client/utility/table_maker.h"
#include "deephaven/dhcore/types.h"
#include "deephaven/third_party/catch.hpp"
#include "deephaven/tests/test_util.h"

using deephaven::client::utility::TableMaker;
using deephaven::dhcore::DeephavenConstants;
using deephaven::dhcore::DateTime;
using deephaven::dhcore::LocalDate;
using deephaven::dhcore::LocalTime;

namespace deephaven::client::tests {

// Make tables out of scalar types and see if they successfully round-trip.
TEST_CASE("Scalar Types", "[newtable]") {
  auto tm = TableMakerForTests::Create();

  TableMaker maker;
  maker.AddColumn<std::optional<bool>>("Bools",
      { {}, false, true, false, false, true });
  maker.AddColumn<std::optional<char16_t>>("Chars",
      { {}, 0, 'a', u'ᐾ', DeephavenConstants::kMinChar, DeephavenConstants::kMaxChar });
  maker.AddColumn<std::optional<int8_t>>("Bytes",
      { {}, 0, 1, -1, DeephavenConstants::kMinByte, DeephavenConstants::kMaxByte });
  maker.AddColumn<std::optional<int16_t>>("Shorts",
      { {}, 0, 1, -1, DeephavenConstants::kMinShort, DeephavenConstants::kMaxShort });
  maker.AddColumn<std::optional<int32_t>>("Ints",
      { {}, 0, 1, -1, DeephavenConstants::kMinInt, DeephavenConstants::kMaxInt });
  maker.AddColumn<std::optional<int64_t>>("Longs",
      { {}, 0L, 1L, -1L, DeephavenConstants::kMinLong, DeephavenConstants::kMaxLong });
  maker.AddColumn<std::optional<float>>("Floats",
      { {}, 0.0F, 1.0F, -1.0F, -3.4e+38F, std::numeric_limits<float>::max() });
  maker.AddColumn<std::optional<double>>("Doubles",
      { {}, 0.0, 1.0, -1.0, -1.79e+308, std::numeric_limits<double>::max() });
  maker.AddColumn<std::optional<std::string>>("Strings",
      { {}, "", "A string", "Also a string", "AAAAAA", "ZZZZZZ" });
  maker.AddColumn<std::optional<DateTime>>("DateTimes",
      { {}, DateTime(), DateTime::FromNanos(-1), DateTime::FromNanos(1),
          DateTime::Parse("2020-03-01T12:34:56Z"), DateTime::Parse("1900-05-05T11:22:33Z") });
  maker.AddColumn<std::optional<LocalDate>>("LocalDates",
      { {}, LocalDate(), LocalDate::FromMillis(-86'400'000), LocalDate::FromMillis(86'400'000),
          LocalDate::Of(2020, 3, 1), LocalDate::Of(1900, 5, 5) });
  maker.AddColumn<std::optional<LocalTime>>("LocalTimes",
      { {}, LocalTime(), LocalTime::FromNanos(1), LocalTime::FromNanos(10'000'000'000),
          LocalTime::Of(12, 34, 56), LocalTime::Of(11, 22, 33) });

  auto dh_table = maker.MakeTable(tm.Client().GetManager());

  std::cout << dh_table.Stream(true) << '\n';

  TableComparerForTests::Compare(maker, dh_table);
}

TEST_CASE("List Types", "[newtable]") {
  auto tm = TableMakerForTests::Create();

  TableMaker maker;
  maker.AddColumn<std::optional<std::vector<std::optional<bool>>>>("Bools", {
      {}, // a null list
      { { false, true } }, // a non-null list
      { { false, true, {} } } // a non-null list with a null entry
  });
//  maker.AddColumn<std::optional<char16_t>>("Chars",
//      { {}, 0, 'a', u'ᐾ', DeephavenConstants::kMinChar, DeephavenConstants::kMaxChar });
//  maker.AddColumn<std::optional<int8_t>>("Bytes",
//      { {}, 0, 1, -1, DeephavenConstants::kMinByte, DeephavenConstants::kMaxByte });
//  maker.AddColumn<std::optional<int16_t>>("Shorts",
//      { {}, 0, 1, -1, DeephavenConstants::kMinShort, DeephavenConstants::kMaxShort });
//  maker.AddColumn<std::optional<int32_t>>("Ints",
//      { {}, 0, 1, -1, DeephavenConstants::kMinInt, DeephavenConstants::kMaxInt });
//  maker.AddColumn<std::optional<int64_t>>("Longs",
//      { {}, 0L, 1L, -1L, DeephavenConstants::kMinLong, DeephavenConstants::kMaxLong });
//  maker.AddColumn<std::optional<float>>("Floats",
//      { {}, 0.0F, 1.0F, -1.0F, -3.4e+38F, std::numeric_limits<float>::max() });
//  maker.AddColumn<std::optional<double>>("Doubles",
//      { {}, 0.0, 1.0, -1.0, -1.79e+308, std::numeric_limits<double>::max() });
//  maker.AddColumn<std::optional<std::string>>("Strings",
//      { {}, "", "A string", "Also a string", "AAAAAA", "ZZZZZZ" });
//  maker.AddColumn<std::optional<DateTime>>("DateTimes",
//      { {}, DateTime(), DateTime::FromNanos(-1), DateTime::FromNanos(1),
//          DateTime::Parse("2020-03-01T12:34:56Z"), DateTime::Parse("1900-05-05T11:22:33Z") });
//  maker.AddColumn<std::optional<LocalDate>>("LocalDates",
//      { {}, LocalDate(), LocalDate::FromMillis(-86'400'000), LocalDate::FromMillis(86'400'000),
//          LocalDate::Of(2020, 3, 1), LocalDate::Of(1900, 5, 5) });
//  maker.AddColumn<std::optional<LocalTime>>("LocalTimes",
//      { {}, LocalTime(), LocalTime::FromNanos(1), LocalTime::FromNanos(10'000'000'000),
//          LocalTime::Of(12, 34, 56), LocalTime::Of(11, 22, 33) });

  auto dh_table = maker.MakeTable(tm.Client().GetManager());

  std::cout << dh_table.Stream(true) << '\n';

  TableComparerForTests::Compare(maker, dh_table);
}
}  // namespace deephaven::client::tests

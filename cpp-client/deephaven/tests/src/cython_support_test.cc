/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */
#include <array>
#include <cstddef>
#include <cstdint>
#include <memory>
#include <optional>
#include <string>
#include <utility>
#include <vector>
#include "deephaven/client/utility/table_maker.h"
#include "deephaven/dhcore/chunk/chunk.h"
#include "deephaven/dhcore/column/array_column_source.h"
#include "deephaven/dhcore/container/row_sequence.h"
#include "deephaven/dhcore/utility/cython_support.h"

#include "../../dhclient/include/private/deephaven/client/arrowutil/arrow_column_source.h"
#include "deephaven/third_party/catch.hpp"

using deephaven::client::utility::TableMaker;
using deephaven::dhcore::chunk::Int64Chunk;
using deephaven::dhcore::column::ColumnSource;
using deephaven::dhcore::column::StringArrayColumnSource;
using deephaven::dhcore::container::RowSequence;
using deephaven::dhcore::ElementType;
using deephaven::dhcore::ElementTypeId;
using deephaven::dhcore::utility::CythonSupport;
using deephaven::dhcore::chunk::StringChunk;

namespace deephaven::client::tests {
TEST_CASE("CreateStringColumnsSource", "[cython]") {
  // hello, NULL, Deephaven, abc
  // We do these as constexpr so our test won't even compile unless we get the basics right
  constexpr const char *kText = "helloDeephavenabc";
  constexpr size_t kTextSize = std::char_traits<char>::length(kText);
  constexpr std::array<uint32_t, 5> kOffsets = {0, 5, 5, 14, 17};
  constexpr std::array<uint8_t, 1> kValidity = {0b1101};

  static_assert(kTextSize == *(kOffsets.end() - 1));
  constexpr auto kNumElements = kOffsets.size() - 1;
  static_assert((kNumElements + 7) / 8 == kValidity.size());

  auto result = CythonSupport::CreateStringColumnSource(kText, kText + kTextSize,
      kOffsets.data(), kOffsets.data() + kOffsets.size(),
      kValidity.data(), kValidity.data() + kValidity.size(),
      kNumElements);

  auto rs = RowSequence::CreateSequential(0, kNumElements);
  auto data = dhcore::chunk::StringChunk::Create(kNumElements);
  auto null_flags = dhcore::chunk::BooleanChunk::Create(kNumElements);
  result->FillChunk(*rs, &data, &null_flags);

  std::vector<std::string> expected_data = {"hello", "", "Deephaven", "abc"};
  std::vector<bool> expected_nulls = {false, true, false, false};

  std::vector<std::string> actual_data(data.begin(), data.end());
  std::vector<bool> actual_nulls(null_flags.begin(), null_flags.end());

  CHECK(expected_data == actual_data);
  CHECK(expected_nulls == actual_nulls);
}

namespace {
template<typename TArrayColumnSource, typename TElement>
std::shared_ptr<ColumnSource> VectorToColumnSource(const ElementType &element_type,
  std::vector<std::optional<TElement>> vec) {
  auto elements = std::make_unique<TElement[]>(vec.size());
  auto null_flags = std::make_unique<bool[]>(vec.size());

  for (size_t i = 0; i != vec.size(); ++i) {
    auto &elt = vec[i];
    if (elt.has_value()) {
      elements[i] = elt.value();
      null_flags[i] = false;
    } else {
      elements[i] = TElement();
      null_flags[i] = true;
    }
  }

  return TArrayColumnSource::CreateFromArrays(element_type, std::move(elements),
    std::move(null_flags), vec.size());
}

}  // namespace

TEST_CASE("TestInflation", "[cython]") {
  // Input: [a, b, c, d, e, f, null, g]
  // Lengths: 3, null, 0, 4
  // Expected output:
  //   [a, b, c]
  //   null  # null list
  //   [] # empty list
  //   [d, e, null, g]

  std::vector<std::optional<std::string>> elements = {
    "a", "b", "c", "d", "e", "f", {}, "g"
  };

  std::vector<std::optional<int32_t>> slice_lengths = {
    3, {}, 0, 4
  };

  auto elements_size = elements.size();
  auto slice_lengths_size = slice_lengths.size();

  auto elements_cs = VectorToColumnSource<StringArrayColumnSource>(
    ElementType::Of(ElementTypeId::kString), std::move(elements));
  auto slice_lengths_cs = VectorToColumnSource<Int32ArrayColumnSource>(
    ElementType::Of(ElementTypeId::kInt32), std::move(slice_lengths));

  // auto elements_cs = StringArrayColumnSource::CreateFromArrays(
  //   ElementType::Of(ElementTypeId::kString), std::move(elements_data), std::move(elements_nulls),
  //      elements_size);
  //
  // auto slice_lengths_cs = Int32ArrayColumnSource::CreateFromArrays(
  //   ElementType::Of(ElementTypeId::kInt32), std::move(slice_lengths_data), std::move(slice_lengths_nulls),
  //      slice_lengths_size);

  auto actual = CythonSupport::CreateContainerColumnSource(std::move(elements_cs), elements_size,
    std::move(slice_lengths_cs), slice_lengths_size);
}
}  // namespace deephaven::client::tests

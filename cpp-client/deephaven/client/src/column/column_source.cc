#include "deephaven/client/column/column_source.h"

#include "deephaven/client/chunk/chunk.h"
#include "deephaven/client/container/context.h"
#include "deephaven/client/impl/util.h"

#include "deephaven/client/utility/utility.h"

using deephaven::client::utility::streamf;
using deephaven::client::utility::stringf;
using deephaven::client::utility::verboseCast;
using deephaven::client::column::ColumnSourceContext;
using deephaven::client::chunk::DoubleChunk;
using deephaven::client::chunk::IntChunk;

namespace deephaven::client::column {
namespace {
class MyLongColumnSourceContext final : public ColumnSourceContext {
public:
};

class MyDoubleColumnSourceContext final : public ColumnSourceContext {
public:
};

void assertFits(size_t size, size_t capacity);
void assertInRange(size_t index, size_t size);
}  // namespace
ColumnSource::~ColumnSource() = default;
MutableColumnSource::~MutableColumnSource() = default;
IntColumnSource::~IntColumnSource() = default;
LongColumnSource::~LongColumnSource() = default;
DoubleColumnSource::~DoubleColumnSource() = default;

std::shared_ptr<IntArrayColumnSource> IntArrayColumnSource::create() {
  return std::make_shared<IntArrayColumnSource>(Private());
}

IntArrayColumnSource::IntArrayColumnSource(Private) {}
IntArrayColumnSource::~IntArrayColumnSource() = default;

std::shared_ptr<ColumnSourceContext> IntArrayColumnSource::createContext(size_t chunkSize) const {
  // We're not really using contexts yet.
  return std::make_shared<MyLongColumnSourceContext>();
}

void IntArrayColumnSource::fillChunkUnordered(Context *context, const LongChunk &rowKeys,
    size_t size, Chunk *dest) const {
  auto *typedDest = verboseCast<IntChunk*>(DEEPHAVEN_PRETTY_FUNCTION, dest);
  assertFits(size, dest->capacity());

  for (size_t i = 0; i < size; ++i) {
    auto srcIndex = rowKeys.data()[i];
    assertInRange(srcIndex, data_.size());
    typedDest->data()[i] = this->data_[srcIndex];
  }
}

void IntArrayColumnSource::fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const {
  auto *typedDest = verboseCast<IntChunk*>(DEEPHAVEN_PRETTY_FUNCTION, dest);
  assertFits(rows.size(), dest->capacity());

  size_t destIndex = 0;
  int64_t srcIndex;
  auto iter = rows.getRowSequenceIterator();
  while (iter->tryGetNext(&srcIndex)) {
    assertInRange(srcIndex, data_.size());
    typedDest->data()[destIndex] = data_[srcIndex];
    ++destIndex;
  }
}

void IntArrayColumnSource::fillFromChunk(Context *context, const Chunk &src,
    const RowSequence &rows) {
  auto *typedSrc = verboseCast<const IntChunk*>(DEEPHAVEN_PRETTY_FUNCTION, &src);
  assertFits(rows.size(), src.capacity());

  size_t srcIndex = 0;
  int64_t destIndex;
  auto iter = rows.getRowSequenceIterator();
  while (iter->tryGetNext(&destIndex)) {
    ensureSize(destIndex + 1);
    data_[destIndex] = typedSrc->data()[srcIndex];
    ++srcIndex;
  }
}

void IntArrayColumnSource::fillFromChunkUnordered(Context *context, const Chunk &src,
    const LongChunk &rowKeys, size_t size) {
  auto *typedSrc = verboseCast<const IntChunk*>(DEEPHAVEN_PRETTY_FUNCTION, &src);
  assertFits(size, src.capacity());

  for (size_t i = 0; i < size; ++i) {
    auto destIndex = rowKeys.data()[i];
    ensureSize(destIndex + 1);
    data_[destIndex] = typedSrc->data()[i];
  }
}

void IntArrayColumnSource::ensureSize(size_t size) {
  if (size > data_.size()) {
    data_.resize(size);
  }
}

void IntArrayColumnSource::acceptVisitor(ColumnSourceVisitor *visitor) const {
  visitor->visit(this);
}

std::shared_ptr<LongArrayColumnSource> LongArrayColumnSource::create() {
  return std::make_shared<LongArrayColumnSource>(Private());
}

LongArrayColumnSource::LongArrayColumnSource(Private) {}
LongArrayColumnSource::~LongArrayColumnSource() = default;

std::shared_ptr<ColumnSourceContext> LongArrayColumnSource::createContext(size_t chunkSize) const {
  // We're not really using contexts yet.
  return std::make_shared<MyLongColumnSourceContext>();
}

void LongArrayColumnSource::fillChunkUnordered(Context *context, const LongChunk &rowKeys,
    size_t size, Chunk *dest) const {
  auto *typedDest = verboseCast<LongChunk*>(DEEPHAVEN_PRETTY_FUNCTION, dest);
  assertFits(size, dest->capacity());

  for (size_t i = 0; i < size; ++i) {
    auto srcIndex = rowKeys.data()[i];
    assertInRange(srcIndex, data_.size());
    typedDest->data()[i] = this->data_[srcIndex];
  }
}

void LongArrayColumnSource::fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const {
  auto *typedDest = verboseCast<LongChunk*>(DEEPHAVEN_PRETTY_FUNCTION, dest);
  assertFits(rows.size(), dest->capacity());

  size_t destIndex = 0;
  int64_t srcIndex;
  auto iter = rows.getRowSequenceIterator();
  while (iter->tryGetNext(&srcIndex)) {
    assertInRange(srcIndex, data_.size());
    typedDest->data()[destIndex] = data_[srcIndex];
    ++destIndex;
  }
}

void LongArrayColumnSource::fillFromChunk(Context *context, const Chunk &src,
    const RowSequence &rows) {
  auto *typedSrc = verboseCast<const LongChunk*>(DEEPHAVEN_PRETTY_FUNCTION, &src);
  assertFits(rows.size(), src.capacity());

  size_t srcIndex = 0;
  int64_t destIndex;
  auto iter = rows.getRowSequenceIterator();
  while (iter->tryGetNext(&destIndex)) {
    ensureSize(destIndex + 1);
    data_[destIndex] = typedSrc->data()[srcIndex];
    ++srcIndex;
  }
}

void LongArrayColumnSource::fillFromChunkUnordered(Context *context, const Chunk &src,
    const LongChunk &rowKeys, size_t size) {
  auto *typedSrc = verboseCast<const LongChunk*>(DEEPHAVEN_PRETTY_FUNCTION, &src);
  assertFits(size, src.capacity());

  streamf(std::cout, "These are the rowKeys: %o\n", rowKeys);

  for (size_t i = 0; i < size; ++i) {
    auto destIndex = rowKeys.data()[i];
    ensureSize(destIndex + 1);
    data_[destIndex] = typedSrc->data()[i];
  }
}

void LongArrayColumnSource::ensureSize(size_t size) {
  if (size > data_.size()) {
    data_.resize(size);
  }
}

void LongArrayColumnSource::acceptVisitor(ColumnSourceVisitor *visitor) const {
  visitor->visit(this);
}

std::shared_ptr<DoubleArrayColumnSource> DoubleArrayColumnSource::create() {
  return std::make_shared<DoubleArrayColumnSource>(Private());
}

DoubleArrayColumnSource::DoubleArrayColumnSource(Private) {}
DoubleArrayColumnSource::~DoubleArrayColumnSource() = default;

std::shared_ptr<ColumnSourceContext> DoubleArrayColumnSource::createContext(size_t chunkSize) const {
  // We're not really using contexts yet.
  return std::make_shared<MyDoubleColumnSourceContext>();
}

void DoubleArrayColumnSource::fillChunkUnordered(Context *context, const LongChunk &rowKeys,
    size_t size, Chunk *dest) const {
  auto *typedDest = verboseCast<DoubleChunk*>(DEEPHAVEN_PRETTY_FUNCTION, dest);
  assertFits(size, dest->capacity());

  for (size_t i = 0; i < size; ++i) {
    auto srcIndex = rowKeys.data()[i];
    assertInRange(srcIndex, data_.size());
    typedDest->data()[i] = this->data_[srcIndex];
  }
}

void DoubleArrayColumnSource::fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const {
  auto *typedDest = verboseCast<DoubleChunk*>(DEEPHAVEN_PRETTY_FUNCTION, dest);
  assertFits(rows.size(), dest->capacity());

  size_t destIndex = 0;
  int64_t srcIndex;
  auto iter = rows.getRowSequenceIterator();
  while (iter->tryGetNext(&srcIndex)) {
    assertInRange(srcIndex, data_.size());
    typedDest->data()[destIndex] = data_[srcIndex];
    ++destIndex;
  }
}

void DoubleArrayColumnSource::fillFromChunk(Context *context, const Chunk &src,
    const RowSequence &rows) {
  auto *typedSrc = verboseCast<const DoubleChunk*>(DEEPHAVEN_PRETTY_FUNCTION, &src);
  assertFits(rows.size(), src.capacity());

  size_t srcIndex = 0;
  int64_t destIndex;
  auto iter = rows.getRowSequenceIterator();
  while (iter->tryGetNext(&destIndex)) {
    ensureSize(destIndex + 1);
    data_[destIndex] = typedSrc->data()[srcIndex];
    ++srcIndex;
  }
}

void DoubleArrayColumnSource::fillFromChunkUnordered(Context *context, const Chunk &src,
    const LongChunk &rowKeys, size_t size) {
  auto *typedSrc = verboseCast<const DoubleChunk*>(DEEPHAVEN_PRETTY_FUNCTION, &src);
  assertFits(size, src.capacity());

  for (size_t i = 0; i < size; ++i) {
    auto destIndex = rowKeys.data()[i];
    ensureSize(destIndex + 1);
    data_[destIndex] = typedSrc->data()[i];
  }
}

void DoubleArrayColumnSource::ensureSize(size_t size) {
  if (size > data_.size()) {
    data_.resize(size);
  }
}

void DoubleArrayColumnSource::acceptVisitor(ColumnSourceVisitor *visitor) const {
  visitor->visit(this);
}

ColumnSourceContext::~ColumnSourceContext() = default;

namespace {
void assertFits(size_t size, size_t capacity) {
  if (size > capacity) {
    auto message = stringf("Expected capacity at least %o, have %o", size, capacity);
    throw std::runtime_error(message);
  }
}

void assertInRange(size_t index, size_t size) {
  if (index >= size) {
    auto message = stringf("srcIndex %o >= size %o", index, size);
    throw std::runtime_error(message);
  }
}
}  // namespace
}  // namespace deephaven::client::column

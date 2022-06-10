#pragma once

#include <string>
#include <vector>
#include <arrow/array.h>
#include "deephaven/client/chunk/chunk.h"
#include "deephaven/client/container/context.h"
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/utility/utility.h"

namespace deephaven::client::column {
class ColumnSourceVisitor;

// the column source interfaces
class ColumnSource {
protected:
  typedef deephaven::client::chunk::Chunk Chunk;
  typedef deephaven::client::chunk::UnsignedLongChunk UnsignedLongChunk;
  typedef deephaven::client::container::Context Context;
  typedef deephaven::client::container::RowSequence RowSequence;

public:
  virtual ~ColumnSource();

  virtual std::shared_ptr<Context> createContext(size_t chunkSize) const = 0;
  virtual void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const = 0;
  virtual void fillChunkUnordered(Context *context, const UnsignedLongChunk &rowKeys, size_t size,
      Chunk *dest) const = 0;

  virtual void acceptVisitor(ColumnSourceVisitor *visitor) const = 0;
};

class MutableColumnSource : public ColumnSource {
public:
  ~MutableColumnSource() override;

  virtual void fillFromChunk(Context *context, const Chunk &src, const RowSequence &rows) = 0;
  virtual void fillFromChunkUnordered(Context *context, const Chunk &src,
      const UnsignedLongChunk &rowKeys, size_t size) = 0;
};

// the per-type interfaces
template<typename T>
class NumericColumnSource : public ColumnSource {
};

// convenience typedefs
typedef NumericColumnSource<int32_t> IntColumnSource;
typedef NumericColumnSource<int64_t> LongColumnSource;
typedef NumericColumnSource<double> DoubleColumnSource;

// the mutable per-type interfaces
template<typename T>
class MutableNumericColumnSource : public MutableColumnSource {
};

template<typename T>
class NumericArrayColumnSource final : public MutableNumericColumnSource<T>,
    std::enable_shared_from_this<NumericArrayColumnSource<T>> {
  struct Private {};
  typedef deephaven::client::chunk::Chunk Chunk;
  typedef deephaven::client::chunk::LongChunk LongChunk;
  typedef deephaven::client::container::Context Context;
  typedef deephaven::client::container::RowSequence RowSequence;

public:
  static std::shared_ptr<NumericArrayColumnSource> create();
  explicit NumericArrayColumnSource(Private) {}
  ~NumericArrayColumnSource() final = default;

  std::shared_ptr<Context> createContext(size_t chunkSize) const final;
  void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const final;
  void fillChunkUnordered(Context *context, const LongChunk &rowKeys, size_t size,
      Chunk *dest) const final;
  void fillFromChunk(Context *context, const Chunk &src, const RowSequence &rows) final;
  void fillFromChunkUnordered(Context *context, const Chunk &src, const LongChunk &rowKeys,
      size_t size) final;

  void acceptVisitor(ColumnSourceVisitor *visitor) const final;

private:
  void ensureSize(size_t size);

  std::vector<T> data_;
};

template<typename T>
std::shared_ptr<NumericArrayColumnSource<T>> NumericArrayColumnSource<T>::create() {
  return std::make_shared<NumericArrayColumnSource<T>>(Private());
}

template<typename T>
auto NumericArrayColumnSource<T>::createContext(size_t chunkSize) const -> std::shared_ptr<Context> {
  // Contexts not used yet.
  return std::make_shared<Context>();
}

template<typename T>
void NumericArrayColumnSource<T>::fillChunk(Context *context, const RowSequence &rows,
    Chunk *dest) const {
  using deephaven::client::chunk::TypeToChunk;
  using deephaven::client::utility::verboseCast;
  typedef typename TypeToChunk<T>::type_t chunkType_t;

  auto *typedDest = verboseCast<chunkType_t*>(DEEPHAVEN_PRETTY_FUNCTION, dest);
  // assert rows.size() <= dest->capacity()
  assertLessEq(rows.size(), dest->capacity(), "rows.size()", "dest->capacity()", __PRETTY_FUNCTION__);

  size_t destIndex = 0;
  auto applyChunk = [this, typedDest, &destIndex](uint64_t begin, uint64_t end) {
    // assert end <= data_.size()
    assertLessEq(end, data_.size(), "end", "data_.size()", __PRETTY_FUNCTION__);
    for (auto current = begin; current != end; ++current) {
      typedDest->data()[destIndex] = data_[current];
      ++destIndex;
    }
  };
  rows.forEachChunk(applyChunk);
}

template<typename T>
void NumericArrayColumnSource<T>::fillChunkUnordered(Context *context, const LongChunk &rowKeys,
    size_t size, Chunk *dest) const {
  using deephaven::client::chunk::TypeToChunk;
  using deephaven::client::utility::verboseCast;
  typedef typename TypeToChunk<T>::type_t chunkType_t;

  auto *typedDest = verboseCast<chunkType_t*>(DEEPHAVEN_PRETTY_FUNCTION, dest);
  // assert size <= dest->capacity()
  assertLessEq(size, dest->capacity(), "size", "dest->capacity()", __PRETTY_FUNCTION__);

  for (size_t i = 0; i < size; ++i) {
    auto srcIndex = rowKeys.data()[i];
    assertInRange(srcIndex, data_.size());
    typedDest->data()[i] = this->data_[srcIndex];
  }
}

template<typename T>
void NumericArrayColumnSource<T>::fillFromChunk(Context *context, const Chunk &src,
    const RowSequence &rows) {
  using deephaven::client::chunk::TypeToChunk;
  using deephaven::client::utility::verboseCast;
  typedef typename TypeToChunk<T>::type_t chunkType_t;

  auto *typedSrc = verboseCast<const chunkType_t *>(DEEPHAVEN_PRETTY_FUNCTION, &src);
  // assert size <= src.capacity()
  assertLessEq(rows.size(), src.capacity(), "rows.size()", "src.capacity()", __PRETTY_FUNCTION__);

  size_t srcIndex = 0;
  auto applyChunk = [this, typedSrc, &srcIndex](uint64_t begin, uint64_t end) {
    ensureSize(end);
    for (auto current = begin; current != end; ++current) {
      data_[current] = typedSrc->data()[srcIndex];
      ++srcIndex;
    }
  };
  rows.forEachChunk(applyChunk);
}

template<typename T>
void NumericArrayColumnSource<T>::fillFromChunkUnordered(Context *context, const Chunk &src,
    const LongChunk &rowKeys, size_t size) {
  using deephaven::client::chunk::TypeToChunk;
  using deephaven::client::utility::verboseCast;
  typedef typename TypeToChunk<T>::type_t chunkType_t;

  auto *typedSrc = verboseCast<const chunkType_t*>(DEEPHAVEN_PRETTY_FUNCTION, &src);
  // assert rowKeys.size() <= src.capacity()
  assertLessEq(size, rowKeys.capacity(), "size", "rowKeys.capacity()", __PRETTY_FUNCTION__);
  assertLessEq(size, src.capacity(), "size", "src.capacity()", __PRETTY_FUNCTION__);

  for (size_t i = 0; i < size; ++i) {
    auto destIndex = rowKeys.data()[i];
    ensureSize(destIndex + 1);
    data_[destIndex] = typedSrc->data()[i];
  }
}

template<typename T>
void NumericArrayColumnSource<T>::ensureSize(size_t size) {
  if (size > data_.size()) {
    data_.resize(size);
  }
}

class ColumnSourceVisitor {
public:
  virtual void visit(const IntColumnSource *) = 0;
  virtual void visit(const LongColumnSource *) = 0;
  virtual void visit(const DoubleColumnSource *) = 0;
};

template<typename T>
void NumericArrayColumnSource<T>::acceptVisitor(ColumnSourceVisitor *visitor) const {
  visitor->visit(*this);
}
}  // namespace deephaven::client::column

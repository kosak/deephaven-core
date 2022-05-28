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
public:
  typedef deephaven::client::chunk::Chunk Chunk;
  typedef deephaven::client::chunk::LongChunk LongChunk;
  typedef deephaven::client::container::Context Context;
  typedef deephaven::client::container::RowSequence RowSequence;

public:
  virtual ~ColumnSource();

  virtual std::shared_ptr<Context> createContext(size_t chunkSize) const = 0;
  virtual void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const = 0;
  virtual void fillChunkUnordered(Context *context, const LongChunk &rowKeys, size_t size, Chunk *dest) const = 0;

  virtual void acceptVisitor(ColumnSourceVisitor *visitor) const = 0;
};

class MutableColumnSource : public ColumnSource {
public:
  ~MutableColumnSource() override;

  virtual void fillFromChunk(Context *context, const Chunk &src, const RowSequence &rows) = 0;
  virtual void fillFromChunkUnordered(Context *context, const Chunk &src, const LongChunk &rowKeys, size_t size) = 0;
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
  explicit NumericArrayColumnSource(Private) = default;
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
  auto *typedDest = verboseCast<IntChunk*>(DEEPHAVEN_PRETTY_FUNCTION, dest);
  assertFits(rows.size(), dest->capacity());

  size_t destIndex = 0;
  auto applyChunk = [this, typedDest, &destIndex](uint64_t begin, uint64_t end) {
    if (end < data_.size()) {
      throw std::runtime_error(stringf("end (%o) < data_.size (%o)", end, data_.size()));
    }
    for (auto current = begin; current != end; ++current) {
      typedDest->data()[destIndex] = data_[current];
      ++destIndex;
    }
  };
  rows.forEachChunk(applyChunk);
}

class ColumnSourceVisitor {
public:
  virtual void visit(const IntColumnSource *) = 0;
  virtual void visit(const LongColumnSource *) = 0;
  virtual void visit(const DoubleColumnSource *) = 0;
};
}  // namespace deephaven::client::column

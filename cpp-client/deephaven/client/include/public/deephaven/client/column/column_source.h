#pragma once

#include <string>
#include <vector>
#include <arrow/array.h>
#include "deephaven/client/chunk/chunk.h"
#include "deephaven/client/container/context.h"
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/utility/utility.h"

namespace deephaven::client::column {
class ColumnSourceContext;
class LongColumnSource;
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

  virtual std::shared_ptr<ColumnSourceContext> createContext(size_t chunkSize) const = 0;
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

// the per-type interfaces
template<typename T>
class MutableNumericColumnSource : public MutableColumnSource {
};

template<typename T>
class NumericArrayColumnSource final : public MutableNumericColumnSource<T>,
    std::enable_shared_from_this<NumericArrayColumnSource<T>> {
  struct Private {
  };

public:
  static std::shared_ptr<NumericArrayColumnSource> create();
  explicit NumericArrayColumnSource(Private);
  ~NumericArrayColumnSource() final;

  std::shared_ptr<ColumnSourceContext> createContext(size_t chunkSize) const final;
  void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const final;
  void fillChunkUnordered(Context *context, const LongChunk &rowKeys, size_t size,
      Chunk *dest) const final;
  void fillFromChunk(Context *context, const Chunk &src, const RowSequence &rows) final;
  void fillFromChunkUnordered(Context *context, const Chunk &src, const LongChunk &rowKeys,
      size_t size) final;

  void acceptVisitor(ColumnSourceVisitor *visitor) const final;

private:
  void ensureSize(size_t size);
  std::vector<int32_t> data_;
};

class ColumnSourceContext : public deephaven::client::container::Context {
public:
  ~ColumnSourceContext() override;
};

class ColumnSourceVisitor {
public:
  virtual void visit(const IntColumnSource *) = 0;
  virtual void visit(const LongColumnSource *) = 0;
  virtual void visit(const DoubleColumnSource *) = 0;
};
}  // namespace deephaven::client::column

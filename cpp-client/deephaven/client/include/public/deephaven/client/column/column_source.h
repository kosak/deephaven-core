#pragma once

#include <string>
#include <vector>
#include <arrow/array.h>
#include "deephaven/client/chunk/chunk.h"
#include "deephaven/client/container/context.h"
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/immerutil/abstract_flex_vector.h"
#include "deephaven/client/utility/utility.h"

#include "immer/flex_vector.hpp"

namespace deephaven::client::column {
class ColumnSourceContext;
class LongColumnSource;
class ColumnSourceVisitor;

// the column source interfaces

class ColumnSource {
protected:
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
  virtual void fillFromChunk(Context *context, const Chunk &src, const RowSequence &rows) = 0;
  virtual void fillFromChunkUnordered(Context *context, const Chunk &src, const LongChunk &rowKeys, size_t size) = 0;
};

// the per-type interfaces

class IntColumnSource : public MutableColumnSource {
public:
};

class LongColumnSource : public MutableColumnSource {
public:
};

class DoubleColumnSource : public MutableColumnSource {
public:
};

class ImmerColumnSourceBase : public MutableColumnSource {
protected:
  typedef deephaven::client::immerutil::AbstractFlexVectorBase AbstractFlexVectorBase;

  template<typename T>
  using AbstractFlexVector = deephaven::client::immerutil::AbstractFlexVector<T>;

public:
  virtual std::unique_ptr<AbstractFlexVectorBase> getInternals() const = 0;
  virtual void setInternals(std::unique_ptr<AbstractFlexVectorBase> internals) = 0;
};

template<typename T>
class ImmerColumnSource final : public ImmerColumnSourceBase, std::enable_shared_from_this<ImmerColumnSource<T>> {
public:
  ~ImmerColumnSource() final;

  std::unique_ptr<AbstractFlexVectorBase> getInternals() const final {
    return AbstractFlexVectorBase::create(data_);
  }

  void setInternals(std::unique_ptr<AbstractFlexVectorBase> internals) final {
    using deephaven::client::utility::verboseCast;
    auto *afv = verboseCast<AbstractFlexVector<T>*>(DEEPHAVEN_EXPR_MSG(&internals));
    data_ = std::move(afv->data());
  }

private:
  immer::flex_vector<T> data_;
};


class IntArrayColumnSource final : public IntColumnSource, std::enable_shared_from_this<IntArrayColumnSource> {
  struct Private {};

public:
    static std::shared_ptr<IntArrayColumnSource> create();
    explicit IntArrayColumnSource(Private);
    ~IntArrayColumnSource() final;

    std::shared_ptr<ColumnSourceContext> createContext(size_t chunkSize) const final;
    void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const final;
    void fillChunkUnordered(Context *context, const LongChunk &rowKeys, size_t size, Chunk *dest) const final;
    void fillFromChunk(Context *context, const Chunk &src, const RowSequence &rows) final;
    void fillFromChunkUnordered(Context *context, const Chunk &src, const LongChunk &rowKeys, size_t size) final;

    void acceptVisitor(ColumnSourceVisitor *visitor) const final;

private:
    void ensureSize(size_t size);
    std::vector<int32_t> data_;
};

class LongArrayColumnSource final : public LongColumnSource, std::enable_shared_from_this<LongArrayColumnSource> {
  struct Private {};
public:
  static std::shared_ptr<LongArrayColumnSource> create();
  explicit LongArrayColumnSource(Private);
  ~LongArrayColumnSource() final;

  std::shared_ptr<ColumnSourceContext> createContext(size_t chunkSize) const final;
  void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const final;
  void fillChunkUnordered(Context *context, const LongChunk &rowKeys, size_t size, Chunk *dest) const final;
  void fillFromChunk(Context *context, const Chunk &src, const RowSequence &rows) final;
  void fillFromChunkUnordered(Context *context, const Chunk &src, const LongChunk &rowKeys, size_t size) final;

  void acceptVisitor(ColumnSourceVisitor *visitor) const final;

private:
  void ensureSize(size_t size);
  std::vector<int64_t> data_;
};

class DoubleArrayColumnSource final : public DoubleColumnSource, std::enable_shared_from_this<DoubleArrayColumnSource> {
  struct Private {};
public:
  static std::shared_ptr<DoubleArrayColumnSource> create();
  explicit DoubleArrayColumnSource(Private);
  ~DoubleArrayColumnSource() final;

  std::shared_ptr<ColumnSourceContext> createContext(size_t chunkSize) const final;
  void fillChunk(Context *context, const RowSequence &rows, Chunk *dest) const final;
  void fillChunkUnordered(Context *context, const LongChunk &rowKeys, size_t size, Chunk *dest) const final;
  void fillFromChunk(Context *context, const Chunk &src, const RowSequence &rows) final;
  void fillFromChunkUnordered(Context *context, const Chunk &src, const LongChunk &rowKeys, size_t size) final;

  void acceptVisitor(ColumnSourceVisitor *visitor) const final;

private:
  void ensureSize(size_t size);
  std::vector<double> data_;
};

class ColumnSourceContext : public deephaven::client::highlevel::container::Context {
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

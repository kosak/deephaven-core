#include "deephaven/client/chunk/chunk_maker.h"

#include "deephaven/client/column/column_source.h"

using deephaven::client::column::ColumnSourceVisitor;
using deephaven::client::column::DoubleColumnSource;
using deephaven::client::column::Int32ColumnSource;
using deephaven::client::column::Int64ColumnSource;
using deephaven::client::column::UInt64ColumnSource;

namespace deephaven::client::chunk {
namespace {
struct Visitor final : ColumnSourceVisitor {
  explicit Visitor(size_t chunkSize) : chunkSize_(chunkSize) {}

  void visit(const Int32ColumnSource &source) final {
    result_ = Int32Chunk::create(chunkSize_);
  }

  void visit(const Int64ColumnSource &source) final {
    result_ = Int64Chunk::create(chunkSize_);
  }

  void visit(const UInt64ColumnSource &source) final {
    result_ = UInt64Chunk::create(chunkSize_);
  }

  void visit(const DoubleColumnSource &source) final {
    result_ = DoubleChunk::create(chunkSize_);
  }

  size_t chunkSize_;
  AnyChunk result_;
};
}  // namespace

AnyChunk ChunkMaker::createChunkFor(const ColumnSource &columnSource,
    size_t chunkSize) {
  Visitor v(chunkSize);
  columnSource.acceptVisitor(&v);
  return std::move(v.result_);
}
}  // namespace deephaven::client::chunk

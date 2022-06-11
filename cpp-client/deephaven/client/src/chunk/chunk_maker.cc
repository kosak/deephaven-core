#include "deephaven/client/chunk/chunk_maker.h"

#include "deephaven/client/column/column_source.h"

using deephaven::client::column::ColumnSourceVisitor;
using deephaven::client::column::DoubleColumnSource;
using deephaven::client::column::Int32ColumnSource;
using deephaven::client::column::Int64ColumnSource;

namespace deephaven::client::chunk {
namespace {
struct Visitor final : ColumnSourceVisitor {
  explicit Visitor(size_t chunkSize) : chunkSize_(chunkSize) {}

  void visit(const Int32ColumnSource &source) final;
  void visit(const Int64ColumnSource &source) final;
  void visit(const DoubleColumnSource &source) final;

  size_t chunkSize_;
  std::shared_ptr<Chunk> result_;
};
}  // namespace

std::shared_ptr<Chunk> ChunkMaker::createChunkFor(const ColumnSource &columnSource,
    size_t chunkSize) {
  Visitor v(chunkSize);
  columnSource.acceptVisitor(&v);
  return std::move(v.result_);
}
namespace {
void Visitor::visit(const Int32ColumnSource &source) {
  result_ = Int32Chunk::create(chunkSize_);
}

void Visitor::visit(const Int64ColumnSource &source) {
  result_ = Int64Chunk::create(chunkSize_);
}

void Visitor::visit(const DoubleColumnSource &source) {
  result_ = DoubleChunk::create(chunkSize_);
}
}  // namespace
}  // namespace deephaven::client::chunk

#include "deephaven/client/highlevel/chunk/chunk_maker.h"

#include "deephaven/client/highlevel/column/column_source.h"

using deephaven::client::highlevel::column::ColumnSourceVisitor;
using deephaven::client::highlevel::column::DoubleColumnSource;
using deephaven::client::highlevel::column::IntColumnSource;
using deephaven::client::highlevel::column::LongColumnSource;

namespace deephaven::client::highlevel::chunk {
namespace {
struct Visitor final : ColumnSourceVisitor {
  explicit Visitor(size_t chunkSize) : chunkSize_(chunkSize) {}
  void visit(const IntColumnSource *source) final;
  void visit(const LongColumnSource *source) final;
  void visit(const DoubleColumnSource *source) final;

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
void Visitor::visit(const IntColumnSource *source) {
  result_ = IntChunk::create(chunkSize_);
}

void Visitor::visit(const LongColumnSource *source) {
  result_ = LongChunk::create(chunkSize_);
}

void Visitor::visit(const DoubleColumnSource *source) {
  result_ = DoubleChunk::create(chunkSize_);
}
}  // namespace
}  // namespace deephaven::client::highlevel::chunk

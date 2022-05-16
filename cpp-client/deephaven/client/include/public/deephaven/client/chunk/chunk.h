#pragma once

#include <iostream>
#include <memory>

namespace deephaven::client::highlevel::chunk {
class ChunkVisitor;
class Chunk {
public:
  explicit Chunk(size_t capacity) : capacity_(capacity) {}
  virtual ~Chunk() = default;

  virtual void acceptVisitor(const ChunkVisitor &v) const = 0;

  size_t capacity() const { return capacity_; }

protected:
  size_t capacity_ = 0;
};

template<typename T>
class NumericChunk : public Chunk {
protected:
  NumericChunk(std::shared_ptr<T[]> buffer, size_t begin, size_t end) : Chunk(end - begin) {
    if (buffer == nullptr) {
      auto size = end - begin;
      // Note: std::make_shared<T[]>(size) doesn't DTRT until C++20
      buffer = std::shared_ptr<T[]>(new T[size]);
    }
    buffer_ = std::move(buffer);
    begin_ = buffer_.get() + begin;
    end_ = buffer_.get() + end;
  }

  ~NumericChunk() override = default;

public:
  T *data() { return begin_; }
  const T *data() const { return begin_; }

  T *begin() { return begin_; }
  const T *begin() const { return begin_; }

  T *end() { return end_; }
  const T *end() const { return end_; }

protected:
  std::shared_ptr<T[]> buffer_;
  T *begin_ = nullptr;
  T *end_ = nullptr;
};

class IntChunk;
class LongChunk;
class DoubleChunk;
class SizeTChunk;

class ChunkVisitor {
public:
  virtual void visit(const IntChunk &) const = 0;
  virtual void visit(const LongChunk &) const = 0;
  virtual void visit(const DoubleChunk &) const = 0;
  virtual void visit(const SizeTChunk &) const = 0;
};

class IntChunk final : public NumericChunk<int32_t> {
  struct Private {};
public:
  static std::shared_ptr<IntChunk> create(size_t capacity) {
    return std::make_shared<IntChunk>(Private(), nullptr, 0, capacity);
  }

  IntChunk(Private, std::shared_ptr<int32_t[]> buffer, size_t begin, size_t end) :
    NumericChunk(std::move(buffer), begin, end) {}

  void acceptVisitor(const ChunkVisitor &v) const final {
    v.visit(*this);
  }
};

class LongChunk final : public NumericChunk<int64_t> {
  struct Private {};
public:
  static std::shared_ptr<LongChunk> create(size_t capacity) {
    return std::make_shared<LongChunk>(Private(), nullptr, 0, capacity);
  }

  LongChunk(Private, std::shared_ptr<int64_t[]> buffer, size_t begin, size_t end) :
    NumericChunk(std::move(buffer), begin, end) {}

  void acceptVisitor(const ChunkVisitor &v) const final {
    v.visit(*this);
  }

  std::shared_ptr<LongChunk> slice(size_t begin, size_t end);

  friend std::ostream &operator<<(std::ostream &s, const LongChunk &o);
};

class DoubleChunk final : public NumericChunk<double> {
  struct Private {};
public:
  static std::shared_ptr<DoubleChunk> create(size_t capacity) {
    return std::make_shared<DoubleChunk>(Private(), nullptr, 0, capacity);
  }

  DoubleChunk(Private, std::shared_ptr<double[]> buffer, size_t begin, size_t end) :
    NumericChunk(std::move(buffer), begin, end) {}

  void acceptVisitor(const ChunkVisitor &v) const final {
    v.visit(*this);
  }
};

class SizeTChunk final : public NumericChunk<size_t> {
  struct Private {};
public:
  static std::shared_ptr<SizeTChunk> create(size_t capacity) {
    return std::make_shared<SizeTChunk>(Private(), nullptr, 0, capacity);
  }

  SizeTChunk(Private, std::shared_ptr<size_t[]> buffer, size_t begin, size_t end) :
    NumericChunk(std::move(buffer), begin, end) {}

  void acceptVisitor(const ChunkVisitor &v) const final {
    v.visit(*this);
  }
};
}  // namespace deephaven::client::highlevel::chunk

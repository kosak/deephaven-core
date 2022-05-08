#pragma once

#include <iostream>
#include <memory>

namespace deephaven::client::highlevel::chunk {
class SadChunkVisitor;
class SadChunk {
public:
  explicit SadChunk(size_t capacity) : capacity_(capacity) {}
  virtual ~SadChunk() = default;

  virtual void acceptVisitor(const SadChunkVisitor &v) const = 0;

  size_t capacity() const { return capacity_; }

protected:
  size_t capacity_ = 0;
};

template<typename T>
class SadNumericChunk : public SadChunk {
protected:
  SadNumericChunk(std::shared_ptr<T[]> buffer, size_t begin, size_t end) : SadChunk(end - begin) {
    if (buffer == nullptr) {
      auto size = end - begin;
      // Note: std::make_shared<T[]>(size) doesn't DTRT until C++20
      buffer = std::shared_ptr<T[]>(new T[size]);
    }
    buffer_ = std::move(buffer);
    begin_ = buffer_.get() + begin;
    end_ = buffer_.get() + end;
  }

  ~SadNumericChunk() override = default;

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

class SadIntChunk;
class SadLongChunk;
class SadDoubleChunk;
class SadSizeTChunk;

class SadChunkVisitor {
public:

  virtual void visit(const SadIntChunk &) const = 0;
  virtual void visit(const SadLongChunk &) const = 0;
  virtual void visit(const SadDoubleChunk &) const = 0;
  virtual void visit(const SadSizeTChunk &) const = 0;
};

class SadIntChunk final : public SadNumericChunk<int32_t> {
  struct Private {};
public:
  static std::shared_ptr<SadIntChunk> create(size_t capacity) {
    return std::make_shared<SadIntChunk>(Private(), nullptr, 0, capacity);
  }

  SadIntChunk(Private, std::shared_ptr<int32_t[]> buffer, size_t begin, size_t end) :
    SadNumericChunk(std::move(buffer), begin, end) {}

  void acceptVisitor(const SadChunkVisitor &v) const final {
    v.visit(*this);
  }
};

class SadLongChunk final : public SadNumericChunk<int64_t> {
  struct Private {};
public:
  static std::shared_ptr<SadLongChunk> create(size_t capacity) {
    return std::make_shared<SadLongChunk>(Private(), nullptr, 0, capacity);
  }

  SadLongChunk(Private, std::shared_ptr<int64_t[]> buffer, size_t begin, size_t end) :
    SadNumericChunk(std::move(buffer), begin, end) {}

  void acceptVisitor(const SadChunkVisitor &v) const final {
    v.visit(*this);
  }

  std::shared_ptr<SadLongChunk> slice(size_t begin, size_t end);

  friend std::ostream &operator<<(std::ostream &s, const SadLongChunk &o);
};

class SadDoubleChunk final : public SadNumericChunk<double> {
  struct Private {};
public:
  static std::shared_ptr<SadDoubleChunk> create(size_t capacity) {
    return std::make_shared<SadDoubleChunk>(Private(), nullptr, 0, capacity);
  }

  SadDoubleChunk(Private, std::shared_ptr<double[]> buffer, size_t begin, size_t end) :
    SadNumericChunk(std::move(buffer), begin, end) {}

  void acceptVisitor(const SadChunkVisitor &v) const final {
    v.visit(*this);
  }
};

class SadSizeTChunk final : public SadNumericChunk<size_t> {
  struct Private {};
public:
  static std::shared_ptr<SadSizeTChunk> create(size_t capacity) {
    return std::make_shared<SadSizeTChunk>(Private(), nullptr, 0, capacity);
  }

  SadSizeTChunk(Private, std::shared_ptr<size_t[]> buffer, size_t begin, size_t end) :
    SadNumericChunk(std::move(buffer), begin, end) {}

  void acceptVisitor(const SadChunkVisitor &v) const final {
    v.visit(*this);
  }
};
}  // namespace deephaven::client::highlevel::chunk

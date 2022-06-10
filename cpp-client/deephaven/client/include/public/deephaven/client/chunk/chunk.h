#pragma once

#include <iostream>
#include <memory>
#include "deephaven/client/utility/utility.h"

namespace deephaven::client::chunk {
class ChunkVisitor;
class Chunk {
public:
  explicit Chunk(size_t capacity) : capacity_(capacity) {}
  virtual ~Chunk() = default;

  virtual void acceptVisitor(const ChunkVisitor &v) const = 0;

  size_t capacity() const { return capacity_; }

protected:
  void checkSliceBounds(size_t begin, size_t end) const;
  virtual std::ostream &streamTo(std::ostream &s) const = 0;

  // We keep 'capacity' in the base class so it can be accessed without a virtual call.
  size_t capacity_ = 0;

  friend std::ostream &operator<<(std::ostream &s, const Chunk &o) {
    return o.streamTo(s);
  }
};

template<typename T>
class NumericChunk final : public Chunk {
  struct Private {};
public:
  static std::shared_ptr<NumericChunk<T>> create(size_t size);

  NumericChunk(Private, std::shared_ptr<T[]> data, size_t size);
  ~NumericChunk() final = default;

  std::shared_ptr<NumericChunk<T>> slice(size_t begin, size_t end) const;

  void acceptVisitor(const ChunkVisitor &v) const final;

  T *data() { return data_.get(); }
  const T *data() const { return data_.get(); }

  T *begin() { return data_.get(); }
  const T *begin() const { return data_.get(); }

  T *end() { return data_.get() + capacity_; }
  const T *end() const { return data_.get() + capacity_; }

protected:
  std::ostream &streamTo(std::ostream &s) const final {
    using deephaven::client::utility::separatedList;
    return s << '[' << separatedList(begin(), end()) << ']';
  }

  std::shared_ptr<T[]> data_;
};

template<typename T>
std::shared_ptr<NumericChunk<T>> NumericChunk<T>::create(size_t size) {
  // Note: wanted to use make_shared, but std::make_shared<T[]>(size) doesn't DTRT until C++20
  auto data = std::shared_ptr<T[]>(new T[size]);
  return std::make_shared<NumericChunk<T>>(Private(), std::move(data), size);
}

template<typename T>
NumericChunk<T>::NumericChunk(Private, std::shared_ptr<T[]> data, size_t size) :
  Chunk(size), data_(std::move(data)) {}

template<typename T>
std::shared_ptr<NumericChunk<T>> NumericChunk<T>::slice(size_t begin, size_t end) const {
  checkSliceBounds(DEEPHAVEN_PRETTY_FUNCTION, begin, end);
  // Share ownership of data_ but yield different value
  std::shared_ptr newBegin(data_, this->begin() + begin);
  auto size = end - begin;
  return std::make_shared<NumericChunk<T>>(std::move(newBegin), size);
}

typedef NumericChunk<int32_t> IntChunk;
typedef NumericChunk<int64_t> LongChunk;
typedef NumericChunk<uint64_t> UnsignedLongChunk;
typedef NumericChunk<double> DoubleChunk;
typedef NumericChunk<size_t> SizeTChunk;

class ChunkVisitor {
public:
  virtual void visit(const IntChunk &) const = 0;
  virtual void visit(const LongChunk &) const = 0;
  virtual void visit(const DoubleChunk &) const = 0;
  virtual void visit(const SizeTChunk &) const = 0;
};

template<typename T>
void NumericChunk<T>::acceptVisitor(const ChunkVisitor &v) const {
  v.visit(*this);
}

template<typename T>
struct TypeToChunk {};

template<>
struct TypeToChunk<int32_t> {
  typedef deephaven::client::chunk::IntChunk type_t;
};

template<>
struct TypeToChunk<int64_t> {
  typedef deephaven::client::chunk::LongChunk type_t;
};

template<>
struct TypeToChunk<double> {
  typedef deephaven::client::chunk::DoubleChunk type_t;
};

template<>
struct TypeToChunk<size_t> {
  typedef deephaven::client::chunk::SizeTChunk type_t;
};
}  // namespace deephaven::client::chunk

#pragma once

#include <iostream>
#include <memory>
#include <string_view>
#include "deephaven/client/utility/utility.h"

namespace deephaven::client::chunk {
class ChunkVisitor;
class Chunk {
public:
  explicit Chunk(size_t size) : size_(size) {}
  virtual ~Chunk() = default;

  virtual void acceptVisitor(const ChunkVisitor &v) const = 0;

  std::shared_ptr<Chunk> take(size_t size) const {
    return takeImpl(size);
  }
  std::shared_ptr<Chunk> drop(size_t size) const {
    return dropImpl(size);
  }

  size_t size() const { return size_; }

protected:
  virtual std::shared_ptr<Chunk> takeImpl(size_t size) const = 0;
  virtual std::shared_ptr<Chunk> dropImpl(size_t size) const = 0;

  void checkSize(std::string_view what, size_t size) const;
  virtual std::ostream &streamTo(std::ostream &s) const = 0;

  // We keep 'size' in the base class so it can be accessed without a virtual call.
  size_t size_ = 0;

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

  void acceptVisitor(const ChunkVisitor &v) const final;

  std::shared_ptr<NumericChunk> take(size_t size) const;
  std::shared_ptr<NumericChunk> drop(size_t size) const;

  T *data() { return data_.get(); }
  const T *data() const { return data_.get(); }

  T *begin() { return data_.get(); }
  const T *begin() const { return data_.get(); }

  T *end() { return data_.get() + size_; }
  const T *end() const { return data_.get() + size_; }

protected:
  std::shared_ptr<Chunk> takeImpl(size_t size) const final {
    return take(size);
  }
  std::shared_ptr<Chunk> dropImpl(size_t size) const final {
    return drop(size);
  }

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
std::shared_ptr<NumericChunk<T>> NumericChunk<T>::take(size_t size) const {
  checkSize(DEEPHAVEN_PRETTY_FUNCTION, size);
  // Share ownership of data_ but yield different value
  return std::make_shared<NumericChunk<T>>(Private(), data_, size);
}

template<typename T>
std::shared_ptr<NumericChunk<T>> NumericChunk<T>::drop(size_t size) const {
  checkSize(DEEPHAVEN_PRETTY_FUNCTION, size);
  // Share ownership of data_ but yield different value
  std::shared_ptr<T[]> newBegin(data_, data_.get() + size);
  auto newSize = size_ - size;
  return std::make_shared<NumericChunk<T>>(Private(), std::move(newBegin), newSize);
}

typedef NumericChunk<int32_t> Int32Chunk;
typedef NumericChunk<int64_t> Int64Chunk;
typedef NumericChunk<uint64_t> UInt64Chunk;
typedef NumericChunk<double> DoubleChunk;

class ChunkVisitor {
public:
  virtual void visit(const Int32Chunk &) const = 0;
  virtual void visit(const Int64Chunk &) const = 0;
  virtual void visit(const UInt64Chunk &) const = 0;
  virtual void visit(const DoubleChunk &) const = 0;
};

template<typename T>
void NumericChunk<T>::acceptVisitor(const ChunkVisitor &v) const {
  v.visit(*this);
}

template<typename T>
struct TypeToChunk {};

template<>
struct TypeToChunk<int32_t> {
  typedef deephaven::client::chunk::Int32Chunk type_t;
};

template<>
struct TypeToChunk<int64_t> {
  typedef deephaven::client::chunk::Int64Chunk type_t;
};

template<>
struct TypeToChunk<uint64_t> {
  typedef deephaven::client::chunk::UInt64Chunk type_t;
};


template<>
struct TypeToChunk<double> {
  typedef deephaven::client::chunk::DoubleChunk type_t;
};
}  // namespace deephaven::client::chunk

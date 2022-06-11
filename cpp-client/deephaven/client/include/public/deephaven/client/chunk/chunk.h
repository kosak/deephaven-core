#pragma once

#include <iostream>
#include <memory>
#include <string_view>
#include <variant>
#include "deephaven/client/utility/utility.h"

namespace deephaven::client::chunk {
namespace internal {
void checkSize(size_t proposedSize, size_t size, std::string_view what);
}
template<typename T>
class NumericChunk final {
  struct Private {};
public:
  static NumericChunk<T> create(size_t size);

  NumericChunk(Private, std::shared_ptr<T[]> data, size_t size);
  ~NumericChunk();

  std::shared_ptr<NumericChunk> take(size_t size) const;
  std::shared_ptr<NumericChunk> drop(size_t size) const;

  T *data() { return data_.get(); }
  const T *data() const { return data_.get(); }

  T *begin() { return data_.get(); }
  const T *begin() const { return data_.get(); }

  T *end() { return data_.get() + size_; }
  const T *end() const { return data_.get() + size_; }

  size_t size() const { return size_; }

protected:
  friend std::ostream &operator<<(std::ostream &s, const NumericChunk &o) {
    using deephaven::client::utility::separatedList;
    return s << '[' << separatedList(o.begin(), o.end()) << ']';
  }

  std::shared_ptr<T[]> data_;
  size_t size_ = 0;
};

typedef NumericChunk<int32_t> Int32Chunk;
typedef NumericChunk<int64_t> Int64Chunk;
typedef NumericChunk<uint64_t> UInt64Chunk;
typedef NumericChunk<double> DoubleChunk;

/**
 * Typesafe union of all the Chunk types.
 */
class AnyChunk {
  typedef std::variant<Int32Chunk, Int64Chunk, UInt64Chunk, DoubleChunk> variant_t;

public:
  template<typename T>
  AnyChunk &operator=(T &&chunk) {
    variant_ = std::forward<T>(chunk);
    return *this;
  }

  template<typename T>
  const T &get() const {
    return std::get<T>(variant_);
  }

  template<typename T>
  T &get() {
    return std::get<T>(variant_);
  }

private:
  variant_t variant_;
};


template<typename T>
NumericChunk<T> NumericChunk<T>::create(size_t size) {
  // Note: wanted to use make_shared, but std::make_shared<T[]>(size) doesn't DTRT until C++20
  auto data = std::shared_ptr<T[]>(new T[size]);
  return NumericChunk<T>(Private(), std::move(data), size);
}

template<typename T>
NumericChunk<T>::NumericChunk(Private, std::shared_ptr<T[]> data, size_t size) :
  data_(std::move(data)), size_(size) {}

template<typename T>
std::shared_ptr<NumericChunk<T>> NumericChunk<T>::take(size_t size) const {
  internal::checkSize(size, size_, DEEPHAVEN_PRETTY_FUNCTION);
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


class ChunkVisitor {
public:
  virtual void visit(const Int32Chunk &) const = 0;
  virtual void visit(const Int64Chunk &) const = 0;
  virtual void visit(const UInt64Chunk &) const = 0;
  virtual void visit(const DoubleChunk &) const = 0;
};

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

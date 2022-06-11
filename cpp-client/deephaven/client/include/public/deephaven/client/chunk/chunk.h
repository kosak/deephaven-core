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
public:
  static NumericChunk<T> create(size_t size);

  NumericChunk();
  NumericChunk(NumericChunk &&other) noexcept;
  NumericChunk &operator=(const NumericChunk &other) noexcept;
  ~NumericChunk();

  NumericChunk take(size_t size) const;
  NumericChunk drop(size_t size) const;

  T *data() { return data_.get(); }
  const T *data() const { return data_.get(); }

  T *begin() { return data_.get(); }
  const T *begin() const { return data_.get(); }

  T *end() { return data_.get() + size_; }
  const T *end() const { return data_.get() + size_; }

  size_t size() const { return size_; }

private:
  NumericChunk(std::shared_ptr<T[]> data, size_t size);

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

  template<typename Visitor>
  void visit(Visitor &&visitor) const {
    std::visit(std::forward<Visitor>(visitor), variant_);
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
  return NumericChunk<T>(std::move(data), size);
}

template<typename T>
NumericChunk<T>::NumericChunk() = default;
template<typename T>
NumericChunk<T>::~NumericChunk() = default;

template<typename T>
NumericChunk<T>::NumericChunk(std::shared_ptr<T[]> data, size_t size) :
  data_(std::move(data)), size_(size) {}

template<typename T>
NumericChunk<T> NumericChunk<T>::take(size_t size) const {
  internal::checkSize(size, size_, DEEPHAVEN_PRETTY_FUNCTION);
  // Share ownership of data_.
  return NumericChunk<T>(data_, size);
}

template<typename T>
NumericChunk<T> NumericChunk<T>::drop(size_t size) const {
  internal::checkSize(size, size_, DEEPHAVEN_PRETTY_FUNCTION);
  // Share ownership of data_, but the value of the pointer yielded by std::shared_ptr<T>::get()
  // is actually data_.get() + size.
  std::shared_ptr<T[]> newBegin(data_, data_.get() + size);
  auto newSize = size_ - size;
  return NumericChunk<T>(std::move(newBegin), newSize);
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

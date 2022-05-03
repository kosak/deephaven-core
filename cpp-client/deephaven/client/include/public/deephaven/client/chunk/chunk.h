#pragma once

#include <iostream>
#include <memory>
#include <string_view>
#include <variant>
#include "deephaven/client/utility/utility.h"

namespace deephaven::client::chunk {
/**
 * Base type for Chunks
 */
class Chunk {
protected:
  Chunk() = default;
  explicit Chunk(size_t size) : size_(size) {}
  Chunk(Chunk &&other) noexcept = default;
  Chunk &operator=(Chunk &&other) noexcept = default;
  virtual ~Chunk() = default;

public:
  size_t size() const { return size_; }

protected:
  void checkSize(size_t proposedSize, std::string_view what) const;

  size_t size_ = 0;
};

template<typename T>
class NumericChunk final : public Chunk {
public:
  static NumericChunk<T> create(size_t size);

  NumericChunk() = default;
  NumericChunk(NumericChunk &&other) noexcept = default;
  NumericChunk &operator=(NumericChunk &&other) noexcept = default;
  ~NumericChunk() final = default;

  NumericChunk take(size_t size) const;
  NumericChunk drop(size_t size) const;

  T *data() { return data_.get(); }
  const T *data() const { return data_.get(); }

  T *begin() { return data_.get(); }
  const T *begin() const { return data_.get(); }

  T *end() { return data_.get() + size_; }
  const T *end() const { return data_.get() + size_; }

private:
  NumericChunk(std::shared_ptr<T[]> data, size_t size);

  friend std::ostream &operator<<(std::ostream &s, const NumericChunk &o) {
    using deephaven::client::utility::separatedList;
    return s << '[' << separatedList(o.begin(), o.end()) << ']';
  }

  std::shared_ptr<T[]> data_;
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

  const Chunk &unwrap() const;
  Chunk &unwrap();

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
NumericChunk<T>::NumericChunk(std::shared_ptr<T[]> data, size_t size) : Chunk(size),
  data_(std::move(data)) {}

template<typename T>
NumericChunk<T> NumericChunk<T>::take(size_t size) const {
  checkSize(size, DEEPHAVEN_PRETTY_FUNCTION);
  // Share ownership of data_.
  return NumericChunk<T>(data_, size);
}

template<typename T>
NumericChunk<T> NumericChunk<T>::drop(size_t size) const {
  checkSize(size, DEEPHAVEN_PRETTY_FUNCTION);
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

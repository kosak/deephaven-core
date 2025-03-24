/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */

#pragma once

#include <memory>
#include <optional>

namespace deephaven::dhcore::container {
/**
 * Forward declaration
 * @tparam T
 */
template<typename T>
class Container;

class ContainerBase : public std::enable_shared_from_this<ContainerBase> {
public:
  explicit ContainerBase(size_t size) : size_(size) {}
  virtual ~ContainerBase();

  size_t Size() const {
    return size_;
  }

  template<class T>
  std::shared_ptr<const Container<T>> AsContainer() const {
    auto self = shared_from_this();
    return std::dynamic_pointer_cast<const Container<T>>(self);
  }

protected:
  size_t size_ = 0;
};

template<typename T>
class Container : public ContainerBase {
  struct Private{};
public:
  static std::shared_ptr<Container<T>> Create(std::shared_ptr<T[]> data,
      std::shared_ptr<bool[]> nulls, size_t size) {
    return std::make_shared<Container<T>>(Private(), std::move(data), std::move(nulls),
        size);
  }

  Container(Private, std::shared_ptr<T[]> &&data, std::shared_ptr<bool[]> &&nulls, size_t size) :
      ContainerBase(size), data_(std::move(data)), nulls_(std::move(nulls)) {}

  const T &operator[](size_t index) const {
    return data_[index];
  }

  bool IsNull(size_t index) const {
    return nulls_[index];
  }

private:
  std::shared_ptr<T[]> data_;
  std::shared_ptr<bool[]> nulls_;
};
}  // namespace deephaven::dhcore::container

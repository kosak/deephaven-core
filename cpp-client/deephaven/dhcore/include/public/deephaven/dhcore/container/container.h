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
public:
  static std::shared_ptr<Container<T>> Create(std::shared_ptr<T[]> data,
      std::shared_ptr<bool[]> nulls, size_t size) {

  }
  const std::optional<T> &operator[](size_t index) const {
    return elements_[index];
  }

  const std::optional<T> *begin() const {
    return elements_.get();
  }

  const std::optional<T> *end() const {
    return begin() + size_;
  }

private:
  std::shared_ptr<std::optional<T>[]> elements_;
};
}  // namespace deephaven::dhcore::container

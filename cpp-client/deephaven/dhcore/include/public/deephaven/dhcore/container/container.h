/*
 * Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
 */

#pragma once

#include <cstddef>
#include <memory>
#include <iostream>
#include <utility>
#include "deephaven/dhcore/utility/utility.h"

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

  size_t size() const {
    return size_;
  }

  template<class T>
  std::shared_ptr<const Container<T>> AsContainerPtr() const {
    auto self = shared_from_this();
    return std::dynamic_pointer_cast<const Container<T>>(self);
  }

  template<class T>
  const Container<T> &AsContainer() const {
    return *deephaven::dhcore::utility::VerboseCast<const Container<T>*>(DEEPHAVEN_LOCATION_EXPR(this));
  }

protected:
  virtual std::ostream &StreamTo(std::ostream &s) const = 0;

  size_t size_ = 0;

  friend std::ostream &operator<<(std::ostream &s, const ContainerBase &o) {
    return o.StreamTo(s);
  }
};

template<typename T>
class Container final : public ContainerBase {
  using ElementRenderer = deephaven::dhcore::utility::ElementRenderer;
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

  const T *begin() const {
    return data_.get();
  }

  const T *end() const {
    return begin() + size();
  }

private:
  std::ostream &StreamTo(std::ostream &s) const final {
    ElementRenderer renderer;
    s << '[';
    const char *sep = "";
    for (const auto &element : *this) {
      s << sep;
      sep = ",";
      renderer.Render(s, element);
    }
    return s << ']';
  }

  std::shared_ptr<T[]> data_;
  std::shared_ptr<bool[]> nulls_;
};
}  // namespace deephaven::dhcore::container

#pragma once

#include <memory>
#include <immer/flex_vector.hpp>

namespace deephaven::client::immerutil {
template<typename T>
class AbstractFlexVector;

/**
 * This class allows us to manipulate an immer::flex_vector without needing to know what type
 * it's instantiated on.
 */
class AbstractFlexVectorBase {
public:
  template<typename T>
  static std::unique_ptr<AbstractFlexVectorBase> create(immer::flex_vector<T> vec);

  virtual ~AbstractFlexVectorBase();

  virtual std::unique_ptr<AbstractFlexVectorBase> take(size_t n) = 0;
  virtual void mutatingDrop(size_t n) = 0;
  virtual void mutatingAppend(std::unique_ptr<AbstractFlexVectorBase> other) = 0;
};

template<typename T>
class AbstractFlexVector final : public AbstractFlexVectorBase {
public:
  AbstractFlexVector(immer::flex_vector<T> vec) : vec_(std::move(vec)) {}

  std::unique_ptr<AbstractFlexVectorBase> take(size_t n) final {
    return create(vec_.take(n));
  }

  std::unique_ptr<AbstractFlexVectorBase> drop(size_t n) final {
    return create(vec_.drop(n));
  }

  std::unique_ptr<AbstractFlexVectorBase> append(const AbstractFlexVectorBase &other) final {
    return create(vec_ + other);
  }

private:
  immer::flex_vector<T> vec_;
};

template<typename T>
std::unique_ptr<AbstractFlexVectorBase> AbstractFlexVectorBase::create(immer::flex_vector<T> vec) {
  return std::make_shared<AbstractFlexVector<T>>(std::move(vec));
}
}  // namespace deephaven::client::immerutil

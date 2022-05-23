#pragma once

#include <memory>
#include <immer/flex_vector.hpp>
#include <arrow/array.h>
#include "deephaven/client/utility/utility.h"

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
  virtual void inPlaceDrop(size_t n) = 0;
  virtual void inPlaceAppend(std::unique_ptr<AbstractFlexVectorBase> other) = 0;
  virtual void inPlaceAppendArrow(const arrow::Array &data) = 0;
};

template<typename T>
class AbstractFlexVector final : public AbstractFlexVectorBase {
public:
  explicit AbstractFlexVector(immer::flex_vector<T> vec) : vec_(std::move(vec)) {}

  std::unique_ptr<AbstractFlexVectorBase> take(size_t n) final {
    return create(vec_.take(n));
  }

  void inPlaceDrop(size_t n) final {
    auto temp = std::move(vec_).drop(n);
    vec_ = std::move(temp);
  }

  void inPlaceAppend(std::unique_ptr<AbstractFlexVectorBase> other) final {
    auto *otherVec = deephaven::client::utility::verboseCast<AbstractFlexVector*>(
        DEEPHAVEN_PRETTY_FUNCTION, other.get());
    auto temp = std::move(vec_) + std::move(otherVec->vec_);
    vec_ = std::move(temp);
  }

  void inPlaceAppendArrow(const arrow::Array &data) final;

private:
  immer::flex_vector<T> vec_;
};

template<typename T>
std::unique_ptr<AbstractFlexVectorBase> AbstractFlexVectorBase::create(immer::flex_vector<T> vec) {
  return std::make_unique<AbstractFlexVector<T>>(std::move(vec));
}
}  // namespace deephaven::client::immerutil

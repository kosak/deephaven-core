#pragma once
#include "deephaven/client/column/column_source.h"

namespace deephaven::client::immerutil {
class ImmerColumnSourceBase : public deephaven::client::column::ColumnSource {
protected:
  typedef deephaven::client::immerutil::AbstractFlexVectorBase AbstractFlexVectorBase;

  template<typename T>
  using AbstractFlexVector = deephaven::client::immerutil::AbstractFlexVector<T>;

public:
  virtual std::unique_ptr <AbstractFlexVectorBase> getInternals() const = 0;
};

template<typename T>
class ImmerColumnSource final
    : public ImmerColumnSourceBase, std::enable_shared_from_this<ImmerColumnSource<T>> {
public:
  ImmerColumnSource();
  ~ImmerColumnSource() final;

  std::unique_ptr <AbstractFlexVectorBase> getInternals() const final {
    return AbstractFlexVectorBase::create(data_);
  }

  void setInternals(std::unique_ptr <AbstractFlexVectorBase> internals) final {
    using deephaven::client::utility::verboseCast;
    auto *afv = verboseCast<AbstractFlexVector < T> * > (DEEPHAVEN_EXPR_MSG(&internals));
    data_ = std::move(afv->data());
  }

private:
  immer::flex_vector <T> data_;
};
}  // namespace deephaven::client::immerutil

#include "immer/map.hpp"
#include <iostream>
#include <map>
#include <memory>
#include <optional>

class SubscriptionContainerImpl;
struct ConstIteratorImpl;

class SubscriptionContainer {
public:
  class ConstIterator;

  explicit SubscriptionContainer(std::shared_ptr<SubscriptionContainerImpl> impl) : impl_(std::move(impl)) {}

  [[nodiscard]]
  ConstIterator begin() const;
  [[nodiscard]]
  ConstIterator end() const;
  [[nodiscard]]
  ConstIterator find(int key) const;

private:
  std::shared_ptr<SubscriptionContainerImpl> impl_;
};

class SubscriptionContainer::ConstIterator {
public:
  explicit ConstIterator(ConstIteratorImpl impl);
  ConstIterator(const ConstIterator &other);
  ConstIterator &operator=(const ConstIterator &other);
  ConstIterator(ConstIterator &&other) noexcept;
  ConstIterator &operator=(ConstIterator &&other) noexcept;
  ~ConstIterator();

  const std::pair<int, const char*> &operator*() const;
  const std::pair<int, const char*> *operator->() const;

  ConstIterator &operator++();
  ConstIterator operator++(int);

private:
  alignas(8) char storage_[160];

  ConstIteratorImpl *Impl() { return reinterpret_cast<ConstIteratorImpl*>(storage_); }
  const ConstIteratorImpl *Impl() const { return reinterpret_cast<const ConstIteratorImpl*>(storage_); }

  friend bool operator==(const ConstIterator &lhs, const ConstIterator &rhs);
  friend bool operator!=(const ConstIterator &lhs, const ConstIterator &rhs) {
    return !(lhs == rhs);
  }
};

// the cc file starts here

struct ConstIteratorImpl {
  ConstIteratorImpl(const immer::map<int, const char *>::const_iterator &iter,
    std::optional<std::pair<int, const char *>> cached_value)
      : iter_(iter), cached_value_(std::move(cached_value)) {}

  immer::map<int, const char *>::const_iterator iter_;
  std::optional<std::pair<int, const char *>> cached_value_;
};

class SubscriptionContainerImpl {
public:
  explicit SubscriptionContainerImpl(immer::map<int, const char*> m) : m_(std::move(m)) {}

  ConstIteratorImpl begin() const;
  ConstIteratorImpl end() const;
  ConstIteratorImpl find(int key) const;

private:
  immer::map<int, const char*> m_;
};

ConstIteratorImpl *temp;

SubscriptionContainer::ConstIterator::ConstIterator(ConstIteratorImpl impl) {
  static_assert(sizeof(ConstIteratorImpl) == sizeof(storage_));
  static_assert(alignof(ConstIteratorImpl) == alignof(storage_));
  new(Impl()) ConstIteratorImpl(std::move(impl));
  temp = Impl();
}

SubscriptionContainer::ConstIterator::~ConstIterator() {
  Impl()->~ConstIteratorImpl();
}

SubscriptionContainer::ConstIterator::ConstIterator(
    const SubscriptionContainer::ConstIterator &other) {
  new(Impl()) ConstIteratorImpl(*other.Impl());
}

SubscriptionContainer::ConstIterator &
SubscriptionContainer::ConstIterator::operator=(const SubscriptionContainer::ConstIterator &other) {
  if (this != &other) {
    *Impl() = *other.Impl();
  }
  return *this;
}

const std::pair<int, const char*> &SubscriptionContainer::ConstIterator::operator*() const {
  const auto *self = Impl();
  if (self->cached_value_.has_value()) {
    return *self->cached_value_;
  }
  return *self->iter_;
}

const std::pair<int, const char*> *SubscriptionContainer::ConstIterator::operator->() const {
  // aka return &this->operator*(), defined above.
  return &**this;
}

SubscriptionContainer::ConstIterator &SubscriptionContainer::ConstIterator::operator++() {
  auto *self = Impl();
  if (self->cached_value_.has_value()) {
    // If there is a cached value, it is the last (and only) element in the iteration.
    // Clearing the optional will reveal the value of iter_ for next time, which is
    // preinitialized to the map's end() value.
    self->cached_value_.reset();
  } else {
    ++self->iter_;
  }
  return *this;
}

SubscriptionContainer::ConstIterator SubscriptionContainer::ConstIterator::operator++(int) {
  auto old_value = *this;
  // aka this->operator++()
  ++*this;
  return old_value;
}

bool operator==(const SubscriptionContainer::ConstIterator &lhs,
    const SubscriptionContainer::ConstIterator &rhs) {
  const auto *lp = lhs.Impl();
  const auto *rp = rhs.Impl();

  auto lSynth = lp->cached_value_.has_value();
  auto rSynth = rp->cached_value_.has_value();

  auto itersEqual = lp->iter_ == rp->iter_;

  if (lSynth && rSynth) {
    return lp->cached_value_->first == rp->cached_value_->first;
  }

  if (lSynth) {
    // lSynth is true, rSynth is false.
    // Because l is synthetic, lp->iter_ is constructed to have the map's end() value.
    // Therefore "itersEqual", in this case, can be interpreted as rp->iter == map.end().
    // Interpretation:
    // lhs has a value (a synthetic value)
    // rhs may or may not have a value (rp->iter_ may or may not point to end())
    // If itersEqual is true, rp->iter_ points to end() (does not have a value).
    return !itersEqual && lp->cached_value_->first == rp->iter_->first;
  }

  if (rSynth) {
    // Same logic as above, but with l and r flipped.
    return !itersEqual && lp->iter_->first == rp->cached_value_->first;
  }

  return itersEqual;
}

std::map<int, const char*> MapMaker() {
  std::map<int, const char*> result;
  result[0] = "hello";
  result[3] = "goodbye";
  return result;
}

SubscriptionContainer::ConstIterator SubscriptionContainer::begin() const {
  return ConstIterator(impl_->begin());
}

SubscriptionContainer::ConstIterator SubscriptionContainer::end() const {
  return ConstIterator(impl_->end());
}

SubscriptionContainer::ConstIterator SubscriptionContainer::find(int key) const {
  return ConstIterator(impl_->find(key));
}




SubscriptionContainer WrapperMaker() {
  immer::map<int, const char *> m;
  auto m2 = m.set(0, "hello");
  auto m3 = m2.set(3, "goodbye");

  auto impl = std::make_shared<SubscriptionContainerImpl>(std::move(m3));
  SubscriptionContainer sc(std::move(impl));
  return sc;
}

ConstIteratorImpl SubscriptionContainerImpl::begin() const {
  return {m_.begin(), {}};
}

ConstIteratorImpl SubscriptionContainerImpl::end() const {
  return {m_.end(), {}};
}

ConstIteratorImpl SubscriptionContainerImpl::find(int key) const {
  const auto *p = m_.find(key);
  if (p == nullptr) {
    return end();
  }
  std::pair<int, const char*> stupid{key, *p};
  return {m_.end(), std::move(stupid)};
}

int main() {
  auto submap = WrapperMaker();
  for (const auto &e : submap) {
    std::cout << e.first << ", " << e.second << "\n";
  }

  auto j = submap.find(3);
  auto k = submap.find(4);

  auto jb = j != submap.end();
  auto kb = k != submap.end();

  std::cout << "jb is " << jb << ", " << "kb is " << kb << "\n";

  if (jb) {
    std::cout << j->first << ", " << j->second << "\n";

    ++j;
    auto jb2 = j != submap.end();
    std::cout << "jb2 is " << jb2 << "\n";
  }
}

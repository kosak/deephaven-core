#include "immer/map.hpp"
#include <iostream>
#include <map>
#include <memory>

class SubscriptionContainerImpl;
class ConstIteratorImpl;

class SubscriptionContainer {
public:
  class ConstIterator;

  explicit SubscriptionContainer(std::shared_ptr<SubscriptionContainerImpl> impl) : impl_(std::move(impl)) {}

  ConstIterator begin() const;
  ConstIterator end() const;
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
  alignas(16) char space_[384];

  ConstIteratorImpl *Impl() { return reinterpret_cast<ConstIteratorImpl*>(space_); }
  const ConstIteratorImpl *Impl() const { return reinterpret_cast<const ConstIteratorImpl*>(space_); }

  friend bool operator==(const ConstIterator &lhs, const ConstIterator &rhs);
  friend bool operator!=(const ConstIterator &lhs, const ConstIterator &rhs) {
    return !(lhs == rhs);
  }
};

int jerkyTown;

class ConstIteratorImpl {
public:
  ConstIteratorImpl(bool synthetic,
      immer::map<int, const char *>::const_iterator iter,
      immer::map<int, const char *>::const_iterator end,
      std::pair<int, const char *> cached_value) :
      synthetic_(synthetic),
      iter_(iter),
      end_(end),
      cached_value_(std::move(cached_value)) {}

  void Increment() {
    if (synthetic_) {
      synthetic_ = false;
      iter_ = end_;
      return;
    }
    ++iter_;
    if (iter_ != end_) {
      cached_value_ = *iter_;
    }
  }
  [[nodiscard]]
  const std::pair<int, const char *> &Dereference() const {
    return cached_value_;
  }

  bool Equals(const ConstIteratorImpl &other) const {
    if (synthetic_ && other.synthetic_) {
      return cached_value_ == other.cached_value_;
    }

    if (synthetic_ && !other.synthetic_) {
      return other.iter_ != other.end_ && cached_value_ == other.cached_value_;
    }

    if (!synthetic_ && other.synthetic_) {
      return iter_ != end_ && cached_value_ == other.cached_value_;
    }

    return iter_ == other.iter_;
  }

private:
  bool synthetic_ = false;
  immer::map<int, const char *>::const_iterator iter_;
  immer::map<int, const char *>::const_iterator end_;
  std::pair<int, const char *> cached_value_;
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
  static_assert(sizeof(ConstIteratorImpl) <= sizeof(space_));
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
  *Impl() = *other.Impl();
  return *this;
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

const std::pair<int, const char*> &SubscriptionContainer::ConstIterator::operator*() const {
  return Impl()->Dereference();
}

const std::pair<int, const char*> *SubscriptionContainer::ConstIterator::operator->() const {
  return &Impl()->Dereference();
}

SubscriptionContainer::ConstIterator &SubscriptionContainer::ConstIterator::operator++() {
  Impl()->Increment();
  return *this;
}

SubscriptionContainer::ConstIterator SubscriptionContainer::ConstIterator::operator++(int) {
  auto old_value = *this;
  Impl()->Increment();
  return old_value;
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
  auto iter = m_.begin();
  std::pair<int64_t, const char *> value = {};
  if (m_.begin() != m_.end()) {
    value = *iter;
  }
  return {false, iter, m_.end(), std::move(value)};
}

ConstIteratorImpl SubscriptionContainerImpl::end() const {
  std::pair<int64_t, const char *> value = {};
  return {false, m_.end(), m_.end(), std::move(value)};
}

ConstIteratorImpl SubscriptionContainerImpl::find(int key) const {
  const auto *p = m_.find(key);
  if (p == nullptr) {
    return end();
  }
  std::pair<int, const char *> value(key, *p);
  return {true, m_.end(), m_.end(), std::move(value)};
}

bool operator==(const SubscriptionContainer::ConstIterator &lhs,
    const SubscriptionContainer::ConstIterator &rhs) {
  return lhs.Impl()->Equals(*rhs.Impl());
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

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
  ConstIterator(std::unique_ptr<ConstIteratorImpl> impl);
  ConstIterator(const ConstIterator &other);
  ConstIterator &operator=(const ConstIterator &other);
  ConstIterator(ConstIterator &&other) noexcept = default;
  ConstIterator &operator=(ConstIterator &&other) noexcept = default;
  ~ConstIterator() = default;

  const std::pair<int, const char*> &operator*() const;
  const std::pair<int, const char*> *operator->() const;

  ConstIterator &operator++();
  ConstIterator operator++(int);

private:
  std::unique_ptr<ConstIteratorImpl> impl_;

  friend bool operator==(const ConstIterator &lhs, const ConstIterator &rhs);
  friend bool operator!=(const ConstIterator &lhs, const ConstIterator &rhs) {
    return !(lhs == rhs);
  }
};

class ConstIteratorImpl {
public:
  ConstIteratorImpl(bool synthetic, immer::map<int, const char *>::const_iterator iter,
    std::pair<int, const char *> cached_value);

  const std::pair<int, const char*> &operator*() const;

  void Increment();
  const std::pair<int, const char *> &Dereference() const;

private:
  bool synthetic_ = false;
  immer::map<int, const char *>::const_iterator iter_;
  std::pair<int, const char *> cached_value_;
};

class SubscriptionContainerImpl {
public:
  explicit SubscriptionContainerImpl(immer::map<int, const char*> m) : m_(std::move(m)) {}

  std::unique_ptr<ConstIteratorImpl> begin() const;
  std::unique_ptr<ConstIteratorImpl> end() const;
  std::unique_ptr<ConstIteratorImpl> find(int key) const;

private:
  immer::map<int, const char*> m_;
};

std::map<int, const char*> MapMaker() {
  immer::map<int, const char *> m;
  auto m2 = m.set(0, "hello");
  auto m3 = m2.set(3, "bye");

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
  return impl_->Dereference();
}

const std::pair<int, const char*> *SubscriptionContainer::ConstIterator::operator->() const {
  return &impl_->Dereference();
}

SubscriptionContainer::ConstIterator &SubscriptionContainer::ConstIterator::operator++() {
  impl_->Increment();
  return *this;
}

SubscriptionContainer::ConstIterator SubscriptionContainer::ConstIterator::operator++(int) {
  auto old_value = *this;
  impl_->Increment();
  return old_value;
}

SubscriptionContainer WrapperMaker() {
  immer::map<int, const char *> m;
  auto m2 = m.set(0, "hello");
  auto m3 = m2.set(3, "bye");

  auto impl = std::make_shared<SubscriptionContainerImpl>(std::move(m));
  SubscriptionContainer sc(std::move(impl));
  return sc;
}

std::unique_ptr<ConstIteratorImpl> SubscriptionContainerImpl::begin() const {
  auto iter = m_.begin();
  std::pair<int64_t, const char *> value = {};
  if (m_.begin() != m_.end()) {
    value = *iter;
  }
  return std::make_unique<ConstIteratorImpl>(false, std::move(iter), std::move(value));
}

std::unique_ptr<ConstIteratorImpl> SubscriptionContainerImpl::end() const {
  auto iter = m_.end();
  std::pair<int64_t, const char *> value = {};
  return std::make_unique<ConstIteratorImpl>(false, std::move(iter), std::move(value));
}

std::unique_ptr<ConstIteratorImpl> SubscriptionContainerImpl::find(int key) const {
  const auto *p = m_.find(key);
  if (p == nullptr) {
    return end();
  }
  auto iter = m_.end();
  std::pair<int, const char *> value(key, *p);
  return std::make_unique<ConstIteratorImpl>(false, std::move(iter), std::move(value));
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

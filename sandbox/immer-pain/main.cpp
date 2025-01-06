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
  const std::pair<int, const char*> &operator*();
  const std::pair<int, const char*> *operator->();

  ConstIterator &operator++();
  const ConstIterator operator++(int);

private:
  std::unique_ptr<ConstIteratorImpl> impl_;

  friend bool operator==(const ConstIterator &lhs, const ConstIterator &rhs);
  friend bool operator!=(const ConstIterator &lhs, const ConstIterator &rhs) {
    return !(lhs == rhs);
  }
};

class ConstIteratorImpl {

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

const std::pair<int, const char*> &SubscriptionContainer::ConstIterator::operator*() {
  return *impl_->operator*();
}

const std::pair<int, const char*> *SubscriptionContainer::ConstIterator::operator->() {
  return impl_->operator*();
}

SubscriptionContainer::ConstIterator &SubscriptionContainer::ConstIterator::operator++() {
  ++(*impl_);
  return *this;
}

SubscriptionContainer::ConstIterator SubscriptionContainer::ConstIterator::operator++(int) {
  auto result = *this;

  return result;
}


SubscriptionContainer WrapperMaker() {
  immer::map<int, const char *> m;
  auto m2 = m.set(0, "hello");
  auto m3 = m2.set(3, "bye");

  auto impl = std::make_shared<SubscriptionContainerImpl>(std::move(m));
  SubscriptionContainer sc(std::move(impl));
  return sc;
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

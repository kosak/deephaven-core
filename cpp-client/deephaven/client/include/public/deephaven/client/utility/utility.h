#pragma once

#include <iostream>
#include <memory>
#include <string>
#include <vector>
#include <arrow/type.h>

namespace deephaven::client::utility {
template<typename Dest, typename Src>
inline Dest bit_cast(const Src &item) {
  static_assert(sizeof(Src) == sizeof(Dest), "Src and Dest are not the same size");
  Dest dest;
  memcpy(static_cast<void *>(&dest), static_cast<const void *>(&item), sizeof(Dest));
  return dest;
}

template<typename T>
std::vector<T> makeReservedVector(size_t n) {
  std::vector<T> v;
  v.reserve(n);
  return v;
}

void assertLessEq(size_t lhs, size_t rhs, std::string_view context, std::string_view lhsName,
  std::string_view rhsName);

// A more efficient ostringstream that also allows you to grab the internal buffer if you want it.
// Or, if you don't want to use the internal buffer, it allows you to provide your own.
class SimpleOstringstream final : private std::basic_streambuf<char>, public std::ostream {
  using Buf = std::basic_streambuf<char>;
public:
  SimpleOstringstream();
  explicit SimpleOstringstream(std::string *clientBuffer);
  SimpleOstringstream(const SimpleOstringstream &other) = delete;
  SimpleOstringstream &operator=(const SimpleOstringstream &other) = delete;
  ~SimpleOstringstream() final;

  std::string &str() { return *dest_; }

private:
  Buf::int_type overflow(int c) final;
  std::streamsize xsputn(const char *s, std::streamsize n) final;

  std::string internalBuffer_;
  std::string *dest_;
};

namespace internal {
// Dumps chars up to the next %o or NUL. Updates *fmt to the point past the %o or at the NUL.
// Returns true iff %o was the last thing seen.
bool dumpFormat(std::ostream &s, const char **fmt, bool placeholderExpected);
}  // namespace internal

std::ostream &streamf(std::ostream &s, const char *fmt);

template<typename HEAD, typename... REST>
std::ostream &streamf(std::ostream &s, const char *fmt, const HEAD &head, REST &&... rest) {
  (void) deephaven::client::utility::internal::dumpFormat(s, &fmt, true);
  s << head;
  return streamf(s, fmt, std::forward<REST>(rest)...);
}

template<typename... ARGS>
std::ostream &coutf(const char *fmt, ARGS &&... args) {
  streamf(std::cout, fmt, std::forward<ARGS>(args)...);
#ifndef NDEBUG
  std::cout.flush();
#endif
  return std::cout;
}

template<typename... ARGS>
std::ostream &cerrf(const char *fmt, ARGS &&... args) {
  streamf(std::cerr, fmt, std::forward<ARGS>(args)...);
#ifndef NDEBUG
  std::cerr.flush();
#endif
  return std::cerr;
}

template<typename... ARGS>
void appendf(std::string *buffer, const char *fmt, ARGS &&... args) {
  SimpleOstringstream s(buffer);
  streamf(s, fmt, std::forward<ARGS>(args)...);
}

template<typename... ARGS>
std::string stringf(const char *fmt, ARGS &&... args) {
  std::string result;
  appendf(&result, fmt, std::forward<ARGS>(args)...);
  return result;
}

namespace internal {
// Forward declaration for class
template<typename Iterator, typename Callback>
class SeparatedListAdaptor;

// Then, forward declaration for operator<<
template<typename Iterator, typename Callback>
std::ostream &operator<<(std::ostream &s, const SeparatedListAdaptor<Iterator, Callback> &o);

// Finally, the class
template<typename Iterator, typename Callback>
class SeparatedListAdaptor {
public:
  SeparatedListAdaptor(Iterator begin, Iterator end, const char *separator, Callback callback) :
      begin_(begin), end_(end), separator_(separator), callback_(std::move(callback)) {}

private:
  Iterator begin_;
  Iterator end_;
  const char *separator_;
  Callback callback_;

  friend std::ostream &operator<<<>(std::ostream &s, const SeparatedListAdaptor &o);
};

template<typename Iterator, typename Callback>
std::ostream &operator<<(std::ostream &s, const SeparatedListAdaptor<Iterator, Callback> &o) {
  for (auto current = o.begin_; current != o.end_; ++current) {
    if (current != o.begin_) {
      s << o.separator_;
    }
    o.callback_(s, *current);
  }
  return s;
}

template<typename T>
void defaultCallback(std::ostream &s, const T &item) {
  s << item;
}
}  // namespace internal

template<typename Iterator>
auto separatedList(Iterator begin, Iterator end, const char *separator = ", ") {
  return internal::SeparatedListAdaptor<Iterator, void (*)(std::ostream &s,
      const std::remove_reference_t<decltype(*std::declval<Iterator>())> &)>(
      begin, end, separator, &internal::defaultCallback);
}

template<typename Iterator, typename Callback>
internal::SeparatedListAdaptor<Iterator, Callback> separatedList(Iterator begin, Iterator end,
    const char *separator, Callback cb) {
  return internal::SeparatedListAdaptor<Iterator, Callback>(begin, end, separator, std::move(cb));
}

#define DEEPHAVEN_STRINGIFY_HELPER(X) #X
#define DEEPHAVEN_STRINGIFY(X) DEEPHAVEN_STRINGIFY_HELPER(X)

/**
 * Expands an expression into that expression followed by a stringified version of that expression
 * with file and line, suitable for a method like okOrThrow that takes an expression and an optional
 * message.
 */
#define DEEPHAVEN_EXPR_MSG(EXPR) (EXPR), #EXPR "@" __FILE__ ":" DEEPHAVEN_STRINGIFY(__LINE__)


#if defined(__clang__)
#define DEEPHAVEN_PRETTY_FUNCTION __PRETTY_FUNCTION__
#elif defined(__GNUC__)
#define DEEPHAVEN_PRETTY_FUNCTION __PRETTY_FUNCTION__
#elif defined(__MSC_VER)
#define DEEPHAVEN_PRETTY_FUNCTION __FUNCSIG__
#else
# error Unsupported compiler
#endif

// https://stackoverflow.com/questions/281818/unmangling-the-result-of-stdtype-infoname
template <typename T>
constexpr std::string_view getTypeName() {
#if defined(__clang__)
  constexpr auto prefix = std::string_view{"[T = "};
  constexpr auto suffix = "]";
#elif defined(__GNUC__)
  constexpr auto prefix = std::string_view{"with T = "};
  constexpr auto suffix = "; ";
#elif defined(__MSC_VER)
  constexpr auto prefix = std::string_view{"get_type_name<"};
  constexpr auto suffix = ">(void)";
#else
# error Unsupported compiler
#endif

  constexpr auto function = std::string_view{DEEPHAVEN_PRETTY_FUNCTION};

  const auto start = function.find(prefix) + prefix.size();
  const auto end = function.find(suffix);
  const auto size = end - start;

  return function.substr(start, size);
}

template<typename DESTP, typename SRCP>
DESTP verboseCast(SRCP ptr, std::string_view caller) {
  using deephaven::client::utility::stringf;

  auto *typedPtr = dynamic_cast<DESTP>(ptr);
  if (typedPtr != nullptr) {
    return typedPtr;
  }
  typedef decltype(*std::declval<DESTP>()) destType_t;
  auto message = stringf("%o: Expected type %o. Got type %o",
      caller, getTypeName<destType_t>(), typeid(*ptr).name());
  throw std::runtime_error(message);
}

/**
 * TODO(kosak): Do something else here. Maybe.
 */
template<typename T>
void assertLessEq(const T &lhs, const T &rhs, std::string_view lhsText, std::string_view rhsText,
  std::string_view func) {
  if (lhs <= rhs) {
    return;
  }
  throw std::runtime_error(stringf("assertion failed: %o: %o <= %o (%o <= %o)", func, lhs, rhs,
      lhsText, rhsText));
}

/**
 * If result's status is OK, do nothing. Otherwise throw a runtime error with an informative message.
 * @param result an arrow::Result
 * @param optionalMessage An optional message to be included in the exception message.
 */
template<typename T>
void okOrThrow(const arrow::Result<T> &result, const char *message) {
  okOrThrow(result.status(), message);
}

/**
 * If status is OK, do nothing. Otherwise throw a runtime error with an informative message.
 * @param status the arrow::Status
 * @param optionalMessage An optional message to be included in the exception message.
 */
void okOrThrow(const arrow::Status &status, const char *optionalMessage = nullptr);

/**
 * If result's internal status is OK, return result's contained value.
 * Otherwise throw a runtime error with an informative message.
 * @param result The arrow::Result
 * @param message An optional message to be included in the exception message.
 */
template<typename T>
T valueOrThrow(arrow::Result<T> result, const char *optionalMessage = nullptr) {
  okOrThrow(result.status(), optionalMessage);
  return result.ValueUnsafe();
}


}  // namespace deephaven::client::utility

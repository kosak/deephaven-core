#pragma once

#include <string>
#include <string_view>

namespace deephaven::client::impl {
class EscapeUtils {
public:
  static std::string escapeJava(std::string_view s);
  static void appendEscapedJava(std::string_view s, std::string *dest);
};
}  // namespace deephaven::client::impl

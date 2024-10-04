using System.Text.Json;

namespace Deephaven.DndClient;

public class SessionManager {
  public static SessionManager FromJson(string descriptiveName, string jsonStr) {
    try {
      // gpr_log(GPR_INFO, "%s: Parsing JSON.", descriptive_name.c_str());

      var json = JsonDocument.Parse(jsonStr);
      const json json = json::parse(json_str);
      auto ensure_key = [&](const char*const key) {
        auto iter = json.find(key);
        if (iter == json.cend()) {
          throw std::invalid_argument(
              std::string("JSON document does not contain key '") + key + "'");
        }
        return *iter;
      };
      constexpr auto kAuthHostKey = "auth_host";
      const auto auth_host_array = ensure_key(kAuthHostKey);
      if (!auth_host_array.is_array()) {
        throw std::invalid_argument(
            std::string("JSON document value for key =") + kAuthHostKey + "' is not of type array");
      }
      const auto auth_host = auth_host_array[0].get < std::string> ();
      const auto auth_port = ensure_key("auth_port").get<std::uint16_t>();
      const auto controller_host = ensure_key("controller_host").get < std::string> ();
      const auto controller_port = ensure_key("controller_port").get<std::uint16_t>();
      std::string root_certs;
      if (json.contains(kJsonTruststoreUrl)) {
        const std::string url = json[kJsonTruststoreUrl].get < std::string> ();
        root_certs = utility::GetUrl(url);
      }
      std::string auth_authority;
      std::string controller_authority;
      if (json.contains(kJsonOverrideAuthority)) {
        if (json.find(kJsonOverrideAuthority)->get<bool>()) {
          if (json.contains(kJsonAuthAuthority)) {
            auth_authority = json.find(kJsonAuthAuthority)->get < std::string> ();
          } else {
            auth_authority = kDefaultOverrideAuthority;
          }
          if (json.contains(kJsonControllerAuthority)) {
            controller_authority = json.find(kJsonControllerAuthority)->get < std::string> ();
          } else {
            controller_authority = kDefaultOverrideAuthority;
          }
        }
      }
      return Create(
          descriptive_name,
          auth_host, auth_port, auth_authority,
          controller_host, controller_port, controller_authority,
          root_certs);
    } catch (json::exception &e) {
      throw std::runtime_error(DEEPHAVEN_LOCATION_STR(
          std::string("Error processing JSON document:") + e.what()));
    }


    }
}

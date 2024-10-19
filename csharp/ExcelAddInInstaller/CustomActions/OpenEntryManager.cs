using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace Deephaven.ExcelAddInInstaller.CustomActions {
  public class OpenEntryManager {
    public static bool GetOpenEntries(out List<OpenEntry> entries, out string failureReason) {
      entries = null;
      failureReason = "";
      var subKey = Registry.CurrentUser.OpenSubKey(RegistryKeys.OpenEntries.Key, false);
      if (subKey == null) {
        failureReason = $"Couldn't find registry key {RegistryKeys.OpenEntries.Key}";
        return false;
      }

      var valueNames = subKey.GetValueNames();
      entries = new List<OpenEntry>();
      foreach (var valueName in valueNames) {
        var value = subKey.GetValue(valueName);
        if (value == null) {
          failureReason = $"Entry is null for value valueName";
        }

        if (!OpenEntry.TryParse(value, out var openEntry)) {
          continue;
        }

        entries.Add(openEntry);
      }

      entries.Sort(oe => oe.Index);
      return true;
    }

  }
}

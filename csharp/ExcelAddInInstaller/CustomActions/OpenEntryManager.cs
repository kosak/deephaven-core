using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Deephaven.ExcelAddInInstaller.CustomActions {
  public class OpenEntryManager {
    public static bool TryCreate(out OpenEntryManager result, out string failureReason) {
      result = null;
      failureReason = "";
      var subKey = Registry.CurrentUser.OpenSubKey(RegistryKeys.OpenEntries.Key, false);
      if (subKey == null) {
        failureReason = $"Couldn't find registry key {RegistryKeys.OpenEntries.Key}";
        return false;
      }

      result = new OpenEntryManager(subKey);
      return true;
    }

    private readonly RegistryKey _subKey;

    public OpenEntryManager(RegistryKey subKey) {
      _subKey = subKey;
    }

    public bool TryCanonicalize(string openValue, bool resultMustContainOpenValue, out string failureReason) {
      if (!TryGetOpenEntries(out var currentEntries, out failureReason)) {
        return false;
      }

      var resultMap = new SortedDictionary<int, BeforeAndAfter>();
      foreach (var kvp in currentEntries) {
        resultMap.LookupOrCreate(kvp.Key).Before = kvp.Value;
      }

      // The canonicalization step
      var allowOneOpenValue = resultMustContainOpenValue;
      var destKey = 0;
      foreach (var entry in currentEntries) {
        if (entry.Value.Equals(openValue)) {
          if (!allowOneOpenValue) {
            continue;
          }

          allowOneOpenValue = false;
        }

        resultMap.LookupOrCreate(destKey++).After = entry.Value;
      }

      // Do we still need to add an open value somewhere?
      if (allowOneOpenValue) {
        resultMap.LookupOrCreate(destKey++).After = openValue;
      }

      // The commit step
      foreach (var entry in resultMap) {
        var key = entry.Key;
        var ba = entry.Value;
        if (ba.After == null) {
          Console.WriteLine($"Delete {key}");
          // delete key
          continue;
        }

        if (ba.Before == null) {
          Console.WriteLine($"Create {key},{ba.After}");
          // create key
          continue;
        }

        if (ba.Before.Equals(ba.After)) {
          Console.WriteLine($"Leave {key} alone with value {ba.Before}");
          continue;
        }

        Console.WriteLine($"Rewrite {key} from {ba.Before} to {ba.After}");
        // rewrite key
      }

      return true;
    }

    private bool TryGetOpenEntries(out SortedDictionary<int, string> entries, out string failureReason) {
      entries = new SortedDictionary<int, string>();
      failureReason = "";

      var entryKeys = _subKey.GetValueNames();
      foreach (var entryKey in entryKeys) {
        var value = _subKey.GetValue(entryKey);
        if (value == null) {
          failureReason = $"Entry is null for value {entryKey}";
        }

        if (!OpenKey2.TryParse(entryKey, out var key)) {
          continue;
        }

        var svalue = value as string;
        if (svalue == null) {
          failureReason = $"Entry is not a string for value {entryKey}";
        }

        entries.Add(key, svalue);
      }

      return true;
    }

    private class BeforeAndAfter {
      public string Before;
      public string After;
    }

    private static class OpenKey2 {
      public static bool TryParse(string key, out int index) {
        index = 0;
        var regex = new Regex(@"^OPEN(\d*)$", RegexOptions.Singleline);
        var match = regex.Match(key);
        if (!match.Success) {
          return false;
        }

        var digits = match.Groups[1].Value;
        index = digits.Length > 0 ? int.Parse(digits) : 0;
        return true;
      }

      public static string AsKey(int index) {
        return index == 0 ? "OPEN" : "OPEN" + index;
      }
    }
  }
}

static class ExtensionMethods {
  public static V LookupOrCreate<K, V>(this IDictionary<K, V> dict, K key) where V : new() {
    if (!dict.TryGetValue(key, out var value)) {
      value = new V();
      dict[key] = value;
    }
    return value;
  }
}

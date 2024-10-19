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

    private bool TryCanonicalize(string openValue, bool resultContainsOpenValue, out string failureReason) {
      if (!TryGetOpenEntries(out var currentEntries, out failureReason)) {
        return false;
      }

      var resultMap = new Dictionary<OpenKey, BeforeAfter>();
      foreach (var kvp in currentEntries) {
        resultMap[kvp.Key].Before = kvp.Value;
      }

      // The canonicalization step
      foreach (var entry in currentEntries) {
        if (entry.Value.Equals(openValue)) {
          if (!allowOneOpenValue) {
            continue;
          }
          allowOneOpenValue = false;
        }

        var desiredKey = OpenKey.CreateFromIndex(whatever);
        resultMap[desiredKey].After = entry.Value;
      }

      // Do we still need to add an open value somewhere?
      if (allowOneOpenValue) {
        var desiredKey = OpenKey.CreateFromIndex(whatever);
        resultMap[desiredKey].After = openValue;
      }

      // The commit step
      foreach (var entry in resultMap) {
        var key = entry.Key;
        var ba = entry.Value;
        if (ba.After == null) {
          // delete key
          continue;
        }

        if (ba.Before == null) {
          // create key
          continue;
        }

        if (!ba.Before.Equals(ba.After)) {
          // rewrite key
        }
      }
    }

    private bool TryGetOpenEntries(out SortedDictionary<OpenKey, string> entries, out string failureReason) {
      entries = new SortedDictionary<OpenKey, string>();
      failureReason = "";

      var entryKeys = _subKey.GetValueNames();
      foreach (var entryKey in entryKeys) {
        var value = _subKey.GetValue(entryKey);
        if (value == null) {
          failureReason = $"Entry is null for value {entryKey}";
        }

        if (!OpenKey.TryParse(entryKey, out var key)) {
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

    private class BeforeAfter {
      public string Before;
      public string After;
    }

    private class OpenKey : UWQQWEJKLQHASHCODE {
      public static bool TryParse(string key, out OpenKey result) {
        result = null;
        var regex = new Regex(@"^OPEN(\d*)$", RegexOptions.Singleline);
        var match = regex.Match(key);
        if (!match.Success) {
          return false;
        }
        var digits = match.Groups[1].Value;
        var index = digits.Length > 0 ? int.Parse(digits) : 0;
        result = new OpenKey(index, key);
        return true;
      }

      public int Index { get; }
      public String Key { get; }

      public OpenKey(int index, string key) {
        Index = index;
        Key = key;
      }
    }
  }

  public class OpenEntryManager_old {
    public static bool TryCreate(out OpenEntryManager result) {

    }
    public static bool TryAddOpenKey(string openValue, out string failureReason) {
    }

    public static bool TryRemoveEntryMatching(string openValue, out int numRemainingKeys, out string failureReason) {
      numRemainingKeys = 0;
      if (!TryGetOpenEntries(out var currentEntries, out failureReason)) {
        return false;
      }

      foreach (var entry in currentEntries) {
        map[entry.Key].Before = entry.Value;
      }

      foreach (var entry in currentEntries) {
        if (entry.Value.Equals(openValue)) {
          continue;
        }

        var key = OpenKey.CreateFromIndex(whatever);
        map[desiredKey].After = entry.Value;
      }

      foreach (var stupid in map) {
        // if After si null, delete before
        // continue

        // if Before is null, create after
        // continue

        // if values are the same, continue
        // replace
      }
            }

            if (!currentHasValue || curIt.Current.Index > desiredIt.Current.Index) {

              // write desired
              // advance desired
              continue;
            }

            // keys same
            if (entries different)
            {
              // write desired
            }
            // advance both
          }


        }
      }


        var destKey = OpenKey.OfValue(0);
      foreach (var entry in entries) {
        if (entry.Value.Equals(openValue)) {
          // DELETE IT
        }

      }
      for (var i = 0; i != entries.Count; ++i) {

      }


      var differingEntries = entries.Where(e => e.Value.Equals(openValue)).ToArray();
      numRemainingKeys = entries.Count - matchingEntries.Length;

      if (matchingEntries.Length == 0) {
        return true;
      }

      numRemainingKeys = entries.Count;
      foreach (var entry in entries) {
        if (!entry.Value.Equals(openValue)) {
          continue;
        }

      }


    }

    private static bool TryGetOpenEntries(out List<OpenEntry> entries, out string failureReason) {
      entries = null;
      failureReason = "";
      var subKey = Registry.CurrentUser.OpenSubKey(RegistryKeys.OpenEntries.Key, false);
      if (subKey == null) {
        failureReason = $"Couldn't find registry key {RegistryKeys.OpenEntries.Key}";
        return false;
      }

      var entryKeys = subKey.GetValueNames();
      entries = new List<OpenEntry>();
      foreach (var entryKey in entryKeys) {
        var value = subKey.GetValue(entryKey);
        if (value == null) {
          failureReason = $"Entry is null for value {entryKey}";
        }

        var svalue = value as string;
        if (svalue == null) {
          failureReason = $"Entry is not a string for value {entryKey}";
        }

        if (!OpenEntry.TryParse(entryKey, svalue, out var openEntry)) {
          continue;
        }

        entries.Add(openEntry);
      }

      entries.Sort((l, r) => l.Index.CompareTo(r.Index));
      return true;
    }
  }
  }
  }
}

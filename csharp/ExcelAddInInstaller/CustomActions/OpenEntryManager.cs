using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;

namespace Deephaven.ExcelAddInInstaller.CustomActions {
  public class OpenEntry {
    public static bool TryParse(string key, string value, out OpenEntry result) {
      result = null;
      var regex = new Regex(@"^OPEN(\d*)$", RegexOptions.Singleline);
      var match = regex.Match(key);
      if (!match.Success) {
        return false;
      }
      var digits = match.Groups[1].Value;
      var index = digits.Length > 0 ? int.Parse(digits) : 0;
      result = new OpenEntry(index, key, value);
      return true;
    }

    public OpenEntry(int index, string key, string value) {
      Index = index;
      Key = key;
      Value = value;
    }

    public int Index { get; }
    public String Key { get; }
    public String Value { get; }
  }

  public class OpenEntryManager {
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

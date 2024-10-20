using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Deephaven.ExcelAddInInstaller.CustomActions {
  public class OpenEntryManager {
    public static bool TryCreate(out OpenEntryManager result, out string failureReason) {
      result = null;
      failureReason = "";
      var subKey = Registry.CurrentUser.OpenSubKey(RegistryKeys.OpenEntries.Key, true);
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

    /// <summary>
    /// This method does a couple of things at the same time:
    /// 1. Makes a pass through the OPEN\d+ entries making sure they are compact (montonically increasing).
    ///    The correct sequence is OPEN,OPEN1,OPEN2,... and yes, the first one is OPEN not OPEN0.
    /// 2. Depending on valuePresentInResult, that there are either 0 or 1 OPEN\d+ entries matching
    ///    addInRegistryValue. This has a nice side effect of cleaning out duplicates, if the state
    ///    managed to end up with duplicates.
    ///
    /// Briefly if you want to install the addin, you can pass true for 'valuePresentInResult'. If you want
    /// to remove it, you can pass false.
    /// </summary>
    /// <param name="addInRegistryValue">The registry value for the OPEN\d+ key. This is normally something like /R "C:\path\to\addin.xll"
    /// with the space and quotation marks</param>
    /// <param name="valuePresentInResult">true if you want the value present in the result.  </param>
    /// <param name="failureReason">The human-readable reason the operation failed, if the method returns false</param>
    /// <returns>True if the operation succeeded. Otherwise, false</returns>
    public bool TryCanonicalize(string addInRegistryValue, bool valuePresentInResult, out string failureReason) {
      if (!TryGetOpenEntries(out var currentEntries, out failureReason)) {
        return false;
      }

      var resultMap = new SortedDictionary<int, BeforeAndAfter>();
      foreach (var kvp in currentEntries) {
        resultMap.LookupOrCreate(kvp.Item1).Before = kvp.Item2;
      }

      // The canonicalization step
      var allowOneEntry = valuePresentInResult;
      var destKey = 0;
      foreach (var entry in currentEntries) {
        if (entry.Item2.Equals(addInRegistryValue)) {
          if (!allowOneEntry) {
            continue;
          }

          allowOneEntry = false;
        }

        resultMap.LookupOrCreate(destKey++).After = entry.Item2;
      }

      // If there was no existing entry matching addInRegistryValue, and the
      // caller asked for it, then we still need to add it.
      if (allowOneEntry) {
        resultMap.LookupOrCreate(destKey).After = addInRegistryValue;
      }

      // The commit step
      foreach (var entry in resultMap) {
        var index = entry.Key;
        var ba = entry.Value;
        var valueName = IndexToKey(index);
        if (ba.After == null) {
          Console.WriteLine($"Delete {valueName}");
          _subKey.DeleteValue(valueName);
          continue;
        }

        if (ba.Before == null) {
          Console.WriteLine($"Set {valueName}={ba.After}");
          _subKey.SetValue(valueName, ba.After);
          continue;
        }

        if (ba.Before.Equals(ba.After)) {
          Console.WriteLine($"Leave {valueName} alone: already set to {ba.Before}");
          continue;
        }

        Console.WriteLine($"Rewrite {valueName} from {ba.Before} to {ba.After}");
        _subKey.SetValue(valueName, ba.After);
      }

      return true;
    }

    private bool TryGetOpenEntries(out List<Tuple<int, string>> entries, out string failureReason) {
      failureReason = "";
      entries = new List<Tuple<int, string>>();

      var entryKeys = _subKey.GetValueNames();
      foreach (var entryKey in entryKeys) {
        var value = _subKey.GetValue(entryKey);
        if (value == null) {
          failureReason = $"Entry is null for value {entryKey}";
        }

        if (!TryParseKey(entryKey, out var key)) {
          continue;
        }

        var svalue = value as string;
        if (svalue == null) {
          failureReason = $"Entry is not a string for value {entryKey}";
        }

        entries.Add(Tuple.Create(key, svalue));
      }

      return true;
    }

    public static bool TryParseKey(string key, out int index) {
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

    public static string IndexToKey(int index) {
      return index == 0 ? "OPEN" : "OPEN" + index;
    }

    private class BeforeAndAfter {
      public string Before;
      public string After;
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

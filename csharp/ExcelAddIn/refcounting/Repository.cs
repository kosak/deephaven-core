using System.Diagnostics;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Refcounting;

public static class Repository {
  private static readonly object _sync = new();
  private static readonly Dictionary<IDisposable, Entry> _dict =
    new(ReferenceEqualityComparer.Instance);

  public static IDisposable Register(IDisposable o, params IDisposable[] dependencies) {
    Debug.WriteLine(
      $"Registering an object of type {o.GetType().Name} with deps [{string.Join(',',
        dependencies.Select(d => d.GetType().Name))}]");
    lock (_sync) {
      if (_dict.ContainsKey(o)) {
        throw new Exception("Can't re-register an object that is already in the Repository");
      }

      var dependencyEntries = dependencies.Select(Increment).ToArray();

      var entry = new Entry(o, dependencyEntries);
      _dict.Add(o, entry);

      return new ShareDisposer(entry);
    }
  }

  public static IDisposable Share(IDisposable o) {
    lock (_sync) {
      var entry = Increment(o);
      return new ShareDisposer(entry);
    }
  }

  private static Entry Increment(IDisposable o) {
    lock (_sync) {
      if (!_dict.TryGetValue(o, out var entry)) {
        throw new Exception("Can't share an object that is not in the Repository");
      }

      ++entry.Count;
      Debug.WriteLine($"Item {o.GetType().Name} now has a count of ^^^ {entry.Count}");
      return entry;
    }
  }

  private record Entry(
    IDisposable Item,
    Entry[] Dependencies) {
    public int Count = 1;

    public void Decrement(List<IDisposable> toDispose) {
      lock (_sync) {
        if (Count < 1) {
          throw new Exception($"Count corrupted: {Count}");
        }
        --Count;
        Debug.WriteLine($"Item {Item.GetType().Name} now has a count of vvv {Count}");

        if (Count > 0) {
          return;
        }
        toDispose.Add(Item);
        foreach (var dep in Dependencies) {
          dep.Decrement(toDispose);
        }
      }
    }
  }

  private class ShareDisposer : IDisposable {
    private Entry? _entry = null;

    public ShareDisposer(Entry? entry) {
      _entry = entry;
    }

    public void Dispose() {
      var temp = Utility.Exchange(ref _entry, null);
      if (temp == null) {
        return;
      }

      var items = new List<IDisposable>();
      lock (_sync) {
        temp.Decrement(items);

        // Now items contains all the things that need to be disposed of and removed from
        // the dictionary.

        foreach (var item in items) {
          _dict.Remove(item);
        }
      }

      if (items.Count == 0) {
        Debug.WriteLine("nothing to dispose");
        return;
      }
      Debug.WriteLine($"Scheduling the dispose of: {string.Join(',',
        items.Select(item => item.GetType().Name))}");
      Background.Run(() => {
        foreach (var item in items) {
          item.Dispose();
        }
      });
    }
  }
}

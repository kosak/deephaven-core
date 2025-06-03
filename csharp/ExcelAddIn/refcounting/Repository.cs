using System.Diagnostics;
using Deephaven.ExcelAddIn.Util;
using static Grpc.Core.Metadata;

namespace Deephaven.ExcelAddIn.Refcounting;

public static class Repository {
  private static readonly object _sync = new();
  private static readonly Dictionary<IDisposable, ZamboniEntry> _dict =
    new(ReferenceEqualityComparer.Instance);

  public static IDisposable Register(IDisposable o, params IDisposable[] dependencies) {
    Debug.WriteLine(
      $"Registering an object of type {o.GetType().Name} with deps [{string.Join(',',
        dependencies.Select(d => d.GetType().Name))}]");
    lock (_sync) {
      if (_dict.ContainsKey(o)) {
        throw new Exception("Can't re-register an object that is already in the Repository");
      }

      var zEntries = new ZamboniEntry[dependencies.Length];

      for (var i = 0; i != dependencies.Length; ++i) {
        zEntries[i] = Increment(dependencies[i]);
      }

      var entry = new ZamboniEntry(o, zEntries);
      _dict.Add(o, entry);

      return new ZamboniDisposer(entry);
    }
  }

  public static IDisposable Share(IDisposable o) {
    lock (_sync) {
      var entry = Increment(o);
      return new ZamboniDisposer(entry);
    }
  }

  private static ZamboniEntry Increment(IDisposable o) {
    lock (_sync) {
      if (!_dict.TryGetValue(o, out var entry)) {
        throw new Exception("Can't share an object that is not in the Repository");
      }

      ++entry.Count;
      Debug.WriteLine($"Item {o.GetType().Name} now has a count of ^^^ {entry.Count}");
      return entry;
    }
  }

  private record ZamboniEntry(
    IDisposable Item,
    ZamboniEntry[] Dependencies) {
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

  private class ZamboniDisposer : IDisposable {
    private ZamboniEntry? _entry = null;

    public ZamboniDisposer(ZamboniEntry? entry) {
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
      Debug.WriteLine($"Scheduling the dispose of: {string.Join(',', items.Select(item => item.GetType().Name))}");
      Background.Run(() => {
        foreach (var item in items) {
          item.Dispose();
        }
      });
    }

  }
}

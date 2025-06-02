using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Refcounting;

public static class Repository {
  private static readonly Dictionary<IDisposable, ZamboniEntry> Dict =
    new(ReferenceEqualityComparer.Instance);

  public static IDisposable Register(IDisposable o, params IDisposable[] dependencies) {
    if (Dict.ContainsKey(o)) {
      throw new Exception("Can't re-register an object that is already in the Repository");
    }

    // Share all the dependencies and collect their disposers
    var dependencyShareDisposers = dependencies.Select(Share).ToArray();

    var entry = new ZamboniEntry(o, dependencyShareDisposers);
    Dict.Add(o, entry);

    var fun = new Fun(34);
    fun.Count = 99;

    return new ZamboniDisposer(entry);
  }

  public static IDisposable Share(IDisposable o) {
    if (!Dict.TryGetValue(o, out var entry)) {
      throw new Exception("Can't share an object that is not in the Repository");
    }

    ++entry.Count;
    return new ZamboniDisposer(entry);
  }

  private class ZamboniEntry {
    private readonly IDisposable _item;
    private readonly IDisposable[] _dependencyShareDisposers;
    public int Count;
  }

  private record Fun(int Count);

  private class ZamboniDisposer : IDisposable {
    private ZamboniEntry? _entry = null;

    public void Dispose() {

      Utility.ClearAndDispose(ref _entry);
    }
  }
}

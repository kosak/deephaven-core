namespace Deephaven.ExcelAddIn.Util;

public class ReferenceCountingDict<TKey, TValue> where TKey : notnull {
  private readonly Dictionary<TKey, WithCount> _dict = new();

  public TValue AddOrIncrement(TKey key, TValue candidateValue) {
    if (_dict.TryGetValue(key, out var withCount)) {
      ++withCount.Count;
    } else {
      withCount = new WithCount(candidateValue);
      _dict.Add(key, withCount);
    }
    return withCount.Value;
  }

  public bool DecrementOrRemove(TKey key) {
    if (!_dict.TryGetValue(key, out var withCount)) {
      return false;
    }
    if (--withCount.Count > 0) {
      return false;
    }
    _dict.Remove(key);
    return true;
  }

  private class WithCount(TValue value) {
    public int Count = 1;
    public readonly TValue Value = value;
  }
}

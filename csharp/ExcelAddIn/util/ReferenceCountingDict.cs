namespace Deephaven.ExcelAddIn.Util;

public class ReferenceCountingDict<TKey, TValue> where TKey : notnull {
  private readonly Dictionary<TKey, WithCount> _dict = new();

  public bool AddOrIncrement(TKey key, TValue candidateValue, out TValue actualValue) {
    if (_dict.TryGetValue(key, out var withCount)) {
      ++withCount.Count;
      actualValue = withCount.Value;
      return false;
    }
    withCount = new WithCount(candidateValue);
    _dict.Add(key, withCount);
    actualValue = candidateValue;
    return true;
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

  public bool ContainsKey(TKey key) {
    return _dict.ContainsKey(key);
  }

  private class WithCount(TValue value) {
    public int Count = 1;
    public readonly TValue Value = value;
  }
}

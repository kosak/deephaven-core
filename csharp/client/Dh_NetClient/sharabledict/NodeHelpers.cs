//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

using System.Runtime.CompilerServices;

public struct ValueWrapper<T> : INode<ValueWrapper<T>> {
  public readonly T Value;

  public ValueWrapper(T value) {
    Value = value;
  }

  public static ValueWrapper<T> EmptyInstance => throw new NotImplementedException();

  public int Count => throw new NotImplementedException();

  public (ValueWrapper<T>, ValueWrapper<T>, ValueWrapper<T>) CalcDifference(ValueWrapper<T> target) {
    throw new NotImplementedException();
  }

  public bool IsEmpty => throw new NotImplementedException();
}

public interface INode<TSelf> {
  // static abstract TSelf EmptyInstance { get; }
  int Count { get; }
  (TSelf, TSelf, TSelf) CalcDifference(TSelf target);
  bool IsEmpty { get; }
}

[InlineArray(64)]
public struct Array64<T> {
  public T Item;
}

//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
namespace Deephaven.Dh_NetClient;

public abstract class MutableBase<TSelf> : EitherBase<TSelf> where TSelf : class {
  public int Count { get; protected set; }

  public MutableBase(int count) {
    Count = count;
  }
}

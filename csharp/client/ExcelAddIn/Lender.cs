using System;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal class Lender<T> : IObservable<bool> {
  private readonly int _concurrentReaderLimit;
  private readonly object _sync = new();
  private T? _value; 
  private int _numReaders = 0;
  private int _numAwaitingWriters = 0;
  private readonly HashSet<IObserver<bool>> _observers = new();

  public Lender(int concurrentReaderLimit) => _concurrentReaderLimit = concurrentReaderLimit;

  public ZamboniReturner<T> Borrow() {
    lock (_sync) {
      while (true) {
        if (_numAwaitingWriters == 0 && _numReaders < _concurrentReaderLimit) {
          ++_numReaders;
          return new ZamboniReturner<T>(this, _value);
        }

        Monitor.Wait(_sync);
      }
    }
  }

  public void Return() {
    lock (_sync) {
      --_numReaders;
      Monitor.PulseAll(_sync);
    }
  }

  private void Replace(T? newValue) {
    var toNotify = ReplaceHelper(newValue);
    foreach (var observer in toNotify) {
      observer.OnNext(true);
    }
  }

  private IObserver<bool>[] ReplaceHelper(T? newValue) {
    lock (_sync) {
      ++_numAwaitingWriters;
      while (true) {
        if (_numReaders == 0) {
          --_numAwaitingWriters;
          _value = newValue;
          Monitor.PulseAll(_sync);
          return _observers.ToArray();
        }

        Monitor.Wait(_sync);
      }
    }
  }

  public IDisposable Subscribe(IObserver<bool> observer) {
    lock (_sync) {
      _observers.Add(observer);
    }

    return new ActionDisposable(() => Unsubscribe(observer));
  }

  private void Unsubscribe(IObserver<bool> observer) {
    lock (_sync) {
      _observers.Remove(observer);
    }
  }
}

class ZamboniReturner<T> : IDisposable {
  private readonly Lender<T> _lender;
  public readonly T? Value;

  public ZamboniReturner(Lender<T> lender, T? value) {
    _lender = lender;
    Value = value;
  }

  public void Dispose() {
    _lender.Return();
  }
}

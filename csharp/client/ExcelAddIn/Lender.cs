using System;

namespace Deephaven.DeephavenClient.ExcelAddIn;

internal class Lender<T> {
  private readonly int _concurrentReaderLimit;
  private readonly object _sync = new();
  private T? _value; 
  private int _numReaders = 0;
  private int _numAwaitingWriters = 0;

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

  public void Replace(T? newValue) {
    lock (_sync) {
      ++_numAwaitingWriters;
      while (true) {
        if (_numReaders == 0) {
          --_numAwaitingWriters;
          _value = newValue;
          Monitor.PulseAll(_sync);
          return;
        }

        Monitor.Wait(_sync);
      }
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

public class Notifier<T> : IObservable<T> {
  private readonly object _sync = new();
  private readonly HashSet<IObserver<T>> _observers = new();

  public IDisposable Subscribe(IObserver<T> observer) {
    lock (_sync) {
      _observers.Add(observer);
    }

    return new ActionDisposable(() => Unsubscribe(observer));
  }

  private void Unsubscribe(IObserver<T> observer) {
    lock (_sync) {
      _observers.Remove(observer);
    }
  }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Deephaven.ExcelAddIn.Util;

internal static class ObservableConverter {
  public static ObservableConverter<TFrom, TTo> Create<TFrom, TTo>(Func<TFrom, TTo> converter) {

  }

}


internal class ObservableConverter<TFrom, TTo> : IObserver<TFrom>, IObservable<TTo> {
  public void OnCompleted() {
    throw new NotImplementedException();
  }

  public void OnError(Exception error) {
    throw new NotImplementedException();
  }

  public void OnNext(TFrom value) {
    throw new NotImplementedException();
  }

  public IDisposable Subscribe(IObserver<TTo> observer) {
    throw new NotImplementedException();
  }
}

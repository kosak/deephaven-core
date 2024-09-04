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

}

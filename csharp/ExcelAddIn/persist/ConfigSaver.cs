using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Persist;

public class ConfigSaver : IValueObserver<SharableDict<EndpointConfigBase>> {
  private readonly object _sync = new();
  private SharableDict<EndpointConfigBase> _cachedValue = SharableDict<EndpointConfigBase>.Empty;
  private SharableDict<EndpointConfigBase>? _dictToWrite = null;

  public void OnNext(SharableDict<EndpointConfigBase> value) {
    lock (_sync) {
      if (ReferenceEquals(value, _cachedValue)) {
        return;
      }
      _cachedValue = value;

      var writeScheduled = _dictToWrite != null;
      _dictToWrite = value;

      if (!writeScheduled) {
        Background.Run(WriteConfig);
      }
    }
  }

  private void WriteConfig() {
    SharableDict<EndpointConfigBase>? toWrite;
    lock (_sync) {
      toWrite = Utility.Exchange(ref _dictToWrite, null);
    }

    if (toWrite == null) {
      return;
    }

    var items = toWrite.Values.ToArray();
    _ = PersistedConfig.TryWriteConfigFile(items);
  }
}

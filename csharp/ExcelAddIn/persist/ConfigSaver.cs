using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Observable;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Persist;

public class ConfigSaver : IValueObserver<SharableDict<EndpointConfigBase>> {
  private readonly object _sync = new();
  private SharableDict<EndpointConfigBase> _cachedValue;
  private SharableDict<EndpointConfigBase>? _dictToWrite = null;
  private bool _threadIsAlive = false;

  public ConfigSaver(SharableDict<EndpointConfigBase> initialValue) {
    _cachedValue = initialValue;
  }

  public void OnNext(SharableDict<EndpointConfigBase> value) {
    lock (_sync) {
      if (ReferenceEquals(value, _cachedValue)) {
        return;
      }
      _cachedValue = value;
      _dictToWrite = value;

      if (!_threadIsAlive) {
        _threadIsAlive = true;
        Background.Run(WriteConfig);
      }
    }
  }

  private void WriteConfig() {
    while (true) {
      SharableDict<EndpointConfigBase>? toWrite;
      lock (_sync) {
        toWrite = Utility.Exchange(ref _dictToWrite, null);

        if (toWrite == null) {
          _threadIsAlive = false;
          return;
        }
      }

      var items = toWrite.Values.ToArray();
      _ = PersistedConfig.TryWriteConfigFile(items);
    }
  }
}

using Deephaven.DeephavenClient;
using Xunit.Abstractions;

namespace Deephaven.DhClientTests;

public class TickingTest {
  private readonly ITestOutputHelper _output;

  public TickingTest(ITestOutputHelper output) {
    _output = output;
  }

  [Fact]
  public void EventuallyReaches10Rows() {
    const Int64 maxRows = 10;
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var thm = ctx.Client.Manager;

    using var table = thm.TimeTable(TimeSpan.FromMilliseconds(500)).Update("II = ii");
    var callback = new ReachesNRowsCallback(_output, maxRows);
    using var cookie = table.Subscribe(callback);

    while (true) {
      var (done, errorText) = callback.WaitForUpdate();
      if (done) {
        break;
      }
      if (errorText != null) {
        throw new Exception(errorText);
      }
    }

    table.Unsubscribe(cookie);
  }
}

public abstract class CommonBase : ITickingCallback {
  private readonly object _sync = new();
  private bool _done = false;
  private string? _errorText = null;

  public void OnFailure(string errorText) {
    lock (_sync) {
      _errorText = errorText;
      Monitor.PulseAll(_sync);
    }
  }

  public (bool, string?) WaitForUpdate() {
    lock (_sync) {
      while (true) {
        if (_done || _errorText != null) {
          return (_done, _errorText);
        }

        Monitor.Wait(_sync);
      }
    }
  }

  public abstract void OnTick(TickingUpdate update);

  protected void NotifyDone() {
    lock (_sync) {
      _done = true;
      Monitor.PulseAll(_sync);
    }
  }
}

public sealed class ReachesNRowsCallback : CommonBase {
  private readonly ITestOutputHelper _output;
  private readonly Int64 _targetRows;

  public ReachesNRowsCallback(ITestOutputHelper output, Int64 targetRows) {
    _output = output;
    _targetRows = targetRows;
  }

  public override void OnTick(TickingUpdate update) {
    _output.WriteLine($"=== The Full Table ===\n{update.Current.ToString(true, true)}");
    if (update.Current.NumRows >= _targetRows) {
      NotifyDone();
    }
  }
}

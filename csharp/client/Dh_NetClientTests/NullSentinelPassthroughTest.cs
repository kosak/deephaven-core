
using Deephaven.Dh_NetClient;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace Deephaven.Dh_NetClientTests;

public class NullSentinelPassthroughTest {
  [Fact]
  public void SentinelsVisible() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var manager = ctx.Client.Manager;
    using var t = manager.EmptyTable(1)
      .Update(
        "NullChar = (char)null",
        "NullByte = (byte)null",
        "NullShort = (short)null",
        "NullInt = (int)null",
        "NullLong = (long)null",
        "NullFloat = (float)null",
        "NullDouble = (double)null"
      );

    var ct = t.ToClientTable();


    tm.AddColumn("boolData", boolData);
    tm.AddColumn("charData", charData);
    tm.AddColumn("byteData", byteData);
    tm.AddColumn("shortData", shortData);
    tm.AddColumn("intData", intData);
    tm.AddColumn("longData", longData);
    tm.AddColumn("floatData", floatData);
    tm.AddColumn("doubleData", doubleData);
    tm.AddColumn("stringData", stringData);
    tm.AddColumn("dateTimeData", dateTimeData);



  }
}

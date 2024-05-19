using Deephaven.DeephavenClient.Interop;
using Deephaven.DeephavenClient;
using Xunit.Abstractions;

namespace Deephaven.DhClientTests;

public class InputTableTest {
  private readonly ITestOutputHelper _output;

  public InputTableTest(ITestOutputHelper output) {
    _output = output;
    PlatformUtf16.Init();
  }

  [Fact]
  public void TestInputTableAppend() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var tm = ctx.Client.Manager;

    var source = tm.EmptyTable(3).Update("A = ii", "B = ii + 100");
    // No keys, so InputTable will be in append-only mode.
    var inputTable = tm.InputTable(source);

    // expect inputTable to be {0, 100}, {1, 101}, {2, 102}
    {
      var aData = new Int64[] { 0, 1, 2 };
      var bData = new Int64[]{ 100, 101, 102 };
      var tc = new TableComparer();
      tc.AddColumn("A", aData);
      tc.AddColumn("A", aData);
      tc.AssertEqualTo(inputTable);
    }

    var tableToAdd = tm.EmptyTable(2).Update("A = ii", "B = ii + 200");
    inputTable.AddTable(tableToAdd);

    // Because of append, expect input_table to be {0, 100}, {1, 101}, {2, 102}, {0, 200}, {1, 201}
    {
      var aData = new Int64[] { 0, 1, 2, 0, 1 };
      var bData = new Int64[] { 100, 101, 102, 200, 201 };
      var tc = new TableComparer();
      tc.AddColumn("A", aData);
      tc.AddColumn("A", aData);
      tc.AssertEqualTo(inputTable);
    }
  }


  TEST_CASE("Input Table: keyed", "[input_table]") {
    auto client = TableMakerForTests::CreateClient();
    auto tm = client.GetManager();
    auto source = tm.EmptyTable(3).Update({ "A = ii", "B = ii + 100"});
    // Keys = {"A"}, so InputTable will be in keyed mode
    auto input_table = tm.InputTable(source, { "A"});


    // expect input_table to be {0, 100}, {1, 101}, {2, 102}
    {
      std::vector<int64_t> a_data = { 0, 1, 2 };
      std::vector<int64_t> b_data = { 100, 101, 102 };
      CompareTable(input_table,
        "A", a_data,
        "B", b_data);
    }


    auto table_to_add = tm.EmptyTable(2).Update({ "A = ii", "B = ii + 200"});
    input_table.AddTable(table_to_add);


    // Because key is "A", expect input_table to be {0, 200}, {1, 201}, {2, 102}
    {
      std::vector<int64_t> a_data = { 0, 1, 2 };
      std::vector<int64_t> b_data = { 200, 201, 102 };
      CompareTable(input_table,
        "A", a_data,
        "B", b_data);
    }
  }





  [Fact]
  public void TestHeadAndTail() {
    using var ctx = CommonContextForTests.Create(new ClientOptions());
    var table = ctx.TestTable;

    table = table.Where("ImportDate == `2017-11-01`");

    var th = table.Head(2).Select("Ticker", "Volume");
    var tt = table.Tail(2).Select("Ticker", "Volume");

    {
      var tickerData = new[] { "XRX", "XRX" };
      var volumeData = new Int64[] { 345000, 87000 };
      var tc = new TableComparer();

      tc.AddColumn("Ticker", tickerData);
      tc.AddColumn("Volume", volumeData);
      tc.AssertEqualTo(th);
    }

    {
      var tickerData = new[] { "ZNGA", "ZNGA" };
      var volumeData = new Int64[] { 46123, 48300 };
      var tc = new TableComparer();

      tc.AddColumn("Ticker", tickerData);
      tc.AddColumn("Volume", volumeData);
      tc.AssertEqualTo(tt);
    }
  }
}
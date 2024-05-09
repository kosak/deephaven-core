using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Deephaven.DeephavenClient;
using Deephaven.DeephavenClient.Utility;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Deephaven.DhClientTests;

public class CommonContextForTests {
  public readonly Client Client;
  public readonly TableHandle TestTable;
  private readonly ColumnNamesForTests cn;
  private readonly ColumnDataForTests cd;

  public static CommonContextForTests Create(ClientOptions options) {
    var client = CreateClient(options);
    var manager = client.Manager;

    var cn = new ColumnNamesForTests();
    var cd = new ColumnDataForTests();

    var maker = new TableMaker();
    maker.AddColumn(cn.ImportDate, cd.ImportDate);
    maker.AddColumn(cn.Ticker, cd.Ticker);
    maker.AddColumn(cn.Open, cd.Open);
    maker.AddColumn(cn.Close, cd.Close);
    maker.AddColumn(cn.Volume, cd.Volume);

    var testTable = maker.MakeTable(manager);
    return new CommonContextForTests(client, testTable, cn, cd);
  }

  private CommonContextForTests(Client client, TableHandle testTable,
    ColumnNamesForTests cn, ColumnDataForTests cd) {
    Client = client;
    TestTable = testTable;
    this.cn = cn;
    this.cd = cd;
  }

  private static Client CreateClient(ClientOptions clientOptions) {
    var host = GlobalEnvironmentForTests.GetEnv("DH_HOST", "localhost");
    var port = GlobalEnvironmentForTests.GetEnv("DH_PORT", "10000");
    var connectionString = $"{host}:{port}";
    var client = Client.Connect(connectionString, clientOptions);
    return client;
  }
}

// TODO(kosak): put this somewhere, and implement for real
class GlobalEnvironmentForTests {
  public static string GetEnv(string environmentVariable, string defaultValue) {
    return defaultValue;
  }
}

public class ColumnNamesForTests {
  public string ImportDate = "ImportDate";
  public string Ticker = "Ticker";
  public string Open = "Open";
  public string Close = "Close";
  public string Volume = "Volume";
}

public class ColumnDataForTests {
  public string[] ImportDate = {
    "2017-11-01",
    "2017-11-01",
    "2017-11-01",
    "2017-11-01",
    "2017-11-01",
    "2017-11-01",
    "2017-11-01",
    "2017-11-01",
    "2017-11-01",
    "2017-11-01",
    "2017-11-02"
  };

  public string[] Ticker = {
    "XRX",
    "XRX",
    "XYZZY",
    "IBM",
    "GME",
    "AAPL",
    "AAPL",
    "AAPL",
    "ZNGA",
    "ZNGA",
    "T"
  };

  public double[] Open = {
    83.1,
    50.5,
    92.3,
    40.1,
    681.43,
    22.1,
    26.8,
    31.5,
    541.2,
    685.3,
    18.8
  };

  public double[] Close = {
    88.2,
    53.8,
    88.5,
    38.7,
    453,
    23.5,
    24.2,
    26.7,
    538.2,
    544.9,
    13.4
  };

  public long[] Volume = {
    345000,
    87000,
    6060842,
    138000,
    138000000,
    100000,
    250000,
    19000,
    46123,
    48300,
    1500
  };
}

using System;
using Deephaven.Dh_NetClient;

namespace Deephaven.Dh_NetClientTests;

public sealed class CommonContextForTests : IDisposable {
  public readonly Client Client;
  public readonly TableHandle TestTable;
  public readonly ColumnNamesForTests ColumnNames;
  public readonly ColumnDataForTests ColumnData;

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
    ColumnNames = cn;
    ColumnData = cd;
  }

  public void Dispose() {
    TestTable.Dispose();
    Client.Dispose();
  }

  private static Client CreateClient(ClientOptions clientOptions) {
    var results = GlobalEnvironmentForTests.GetEnv("DH_HOST", "DH_PORT");
    var connectionString = $"{results[0]}:{results[1]}";
    var client = Client.Connect(connectionString, clientOptions);
    return client;
  }
}

internal class GlobalEnvironmentForTests {
  public static string[] GetEnv(params string[] environmentVariables) {
    var results = new List<string>();
    var missing = new List<string>();
    foreach (var enVar in environmentVariables) {
      var enVal = Environment.GetEnvironmentVariable(enVar);
      if (enVal != null) {
        results.Add(enVal);
      } else {
        missing.Add(enVar);
      }
    }

    if (missing.Count != 0) {
      throw new Exception("The following environment variables were not found. " +
      $"Please set them either in the environment or in the .RunSettings file in the project directory: {string.Join(", ", missing)}");
    }
    return results.ToArray();
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

using System.ComponentModel;
using System.Net.WebSockets;
using Deephaven.DeephavenClient.Interop;

namespace Deephaven.DhClientTests;

public class BasicInteropInteractionsTest {
  public BasicInteropInteractionsTest() {
    PlatformUtf16.Init();
  }

  [Fact]
  public void TestInAndOutPrimitives() {
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Add(3, 4, out var result);
    Assert.Equal(7, result);
  }

  [Fact]
  public void TestInAndOutPrimitiveArrays() {
    const int length = 3;
    var a = new Int32[length] { 10, 20, 30 };
    var b = new Int32[length] { -5, 10, -15 };
    var actualResult = new Int32[length];
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_AddArrays(a, b,
      length, actualResult);
    var expectedResult = new Int32[length] { -5, 10, -15 };
    Assert.Equal(expectedResult, actualResult);
  }

  [Fact]
  public void TestInAndOutBooleans() {
    foreach (var a in new[] { false, true }) {
      foreach (var b in new[] { false, true }) {
        BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Xor((InteropBool)a,
          (InteropBool)b, out var actualResult);
        var expectedResult = a ^ b;
        Assert.Equal(expectedResult, (bool)actualResult);
      }
    }
  }

  [Fact]
  public void TestInAndOutBooleanArrays() {
    var aList = new List<bool>();
    var bList = new List<bool>();
    var expectedResult = new List<bool>();
    foreach (var a in new[] { false, true }) {
      foreach (var b in new[] { false, true }) {
        aList.Add(a);
        bList.Add(b);
        expectedResult.Add(a ^ b);
      }
    }

    var aArray = aList.Select(e => (InteropBool)e).ToArray();
    var bArray = bList.Select(e => (InteropBool)e).ToArray();
    var size = aArray.Length;
    var actualResultArray = new InteropBool[size];
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_XorArrays(
      aArray, bArray, size, actualResultArray);

    var expectedResultArray = expectedResult.Select(e => (InteropBool)e).ToArray();

    Assert.Equal(expectedResultArray, actualResultArray);
  }

  [Fact]
  public void TestInAndOutStrings() {
    const string a = "Deep🎔";
    const string b = "haven";
    var expectedResult = a + b;
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_Concat(a, b,
      out var resultHandle, out var poolHandle);
    var pool = poolHandle.ImportAndDestroy();
    var actualResult = pool.Get(resultHandle);
    Assert.Equal(expectedResult, actualResult);
  }

  [Fact]
  public void TestInAndOutStringArrays() {
    const int numItems = 30;
    var prefixes = new string[numItems];
    var suffixes = new string[numItems];
    var expectedResult = new string[numItems];

    for (int i = 0; i != numItems; ++i) {
      prefixes[i] = $"Deep[{i}";
      suffixes[i] = $"-🎔-{i}haven]";
      expectedResult[i] = prefixes[i] + suffixes[i];
    }

    var resultHandles = new StringHandle[numItems];
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_ConcatArrays(
      prefixes, suffixes, numItems, resultHandles, out var poolHandle);
    var pool = poolHandle.ImportAndDestroy();
    var actualResult = resultHandles.Select(pool.Get).ToArray();
    Assert.Equal(expectedResult, actualResult);
  }

  [Fact]
  public void TestBasicStruct() {
    var bs = new BasicInteropInteractions.BasicStruct(100, "hi");
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_BasicStruct(ref bs, 123, " 🧠 there", out var result);
    Assert.Equal(223, result.i);
    Assert.Equal("hi 🧠 there", result.s);
  }

}

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
    var expectedResult = new Int32[length] { 5, 30, 15 };
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
    var pool = poolHandle.ExportAndDestroy();
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
    var pool = poolHandle.ExportAndDestroy();
    var actualResult = resultHandles.Select(pool.Get).ToArray();
    Assert.Equal(expectedResult, actualResult);
  }

  [Fact]
  public void TestInAndOutStruct() {
    var a = new BasicInteropInteractions.BasicStruct(100, 33.25);
    var b = new BasicInteropInteractions.BasicStruct(12, 8.5);
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_AddBasicStruct(ref a, ref b, out var result);
    Assert.Equal(112, result.i);
    Assert.Equal(41.75, result.d);
  }

  [Fact]
  public void TestInAndOutStructArrays() {
    const Int32 size = 37;
    var a = new BasicInteropInteractions.BasicStruct[size];
    var b = new BasicInteropInteractions.BasicStruct[size];
    var expectedResult = new BasicInteropInteractions.BasicStruct[size];

    for (Int32 i = 0; i != size; ++i) {
      var tempA = new BasicInteropInteractions.BasicStruct(i, 1234.5 + i);
      var tempB = new BasicInteropInteractions.BasicStruct(100 + i, 824.3 + i);
      a[i] = tempA;
      b[i] = tempB;
      expectedResult[i] = new BasicInteropInteractions.BasicStruct(tempA.i + tempB.i, tempA.d + tempB.d);
    }
    var actualResult = new BasicInteropInteractions.BasicStruct[size];
    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_AddBasicStructArrays(a, b, size, actualResult);
    Assert.Equal(expectedResult, actualResult);
  }

  [Fact]
  public void TestInAndOutNestedStruct() {
    var a1 = new BasicInteropInteractions.BasicStruct(11, 22.22);
    var a2 = new BasicInteropInteractions.BasicStruct(33, 44.44);
    var a = new BasicInteropInteractions.NestedStruct(a1, a2);

    var b1 = new BasicInteropInteractions.BasicStruct(55, 66.66);
    var b2 = new BasicInteropInteractions.BasicStruct(77, 88.88);
    var b = new BasicInteropInteractions.NestedStruct(b1, b2);

    BasicInteropInteractions.deephaven_dhcore_basicInteropInteractions_AddNestedStruct(ref a, ref b, out var result);
    Assert.Equal(112, result.i);
    Assert.Equal(41.75, result.d);
  }

}

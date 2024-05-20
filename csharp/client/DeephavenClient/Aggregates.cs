using System;
using System.Runtime.InteropServices;
using Deephaven.DeephavenClient.Interop;

namespace Deephaven.DeephavenClient;

public class Aggregate {
  [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
  private delegate void AggregateMethod(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
  private delegate void LazyMaterializer(out NativePtr<NativeAggregate> result, out ErrorStatus status);

  public static Aggregate AbsSum(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_AbsSum);
  }

  public static Aggregate Group(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_Group);
  }

  public static Aggregate Avg(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_Avg);
  }

  public static Aggregate First(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_First);
  }

  public static Aggregate Last(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_Last);
  }

  public static Aggregate Max(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_Max);
  }

  public static Aggregate Med(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_Med);
  }

  public static Aggregate Min(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_Min);
  }

  public static Aggregate Std(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_Std);
  }

  public static Aggregate Sum(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_Sum);
  }

  public static Aggregate Var(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_Var);
  }

  public static Aggregate WAvg(IEnumerable<string> columnSpecs) {
    return CreateHelper(columnSpecs, NativeAggregate.deephaven_client_Aggregate_WAvg);
  }

  public static Aggregate Count(string columnSpec) {
    LazyMaterializer lazyMaterializer = (out NativePtr<NativeAggregate> result, out ErrorStatus status) =>
      NativeAggregate.deephaven_client_Aggregate_Count(columnSpec, out result, out status);
    return new Aggregate(lazyMaterializer);
  }

  public static Aggregate Pct(double percentile, bool avgMedian, IEnumerable<string> columnSpecs) {
    var cols = columnSpecs.ToArray();
    LazyMaterializer lazyMaterializer = (out NativePtr<NativeAggregate> result, out ErrorStatus status) =>
      NativeAggregate.deephaven_client_Aggregate_Pct(percentile, (InteropBool)avgMedian,
        cols, cols.Length, out result, out status);
    return new Aggregate(lazyMaterializer);
  }

  /// <summary>
  /// Helper method for all the Aggregate functions except Count, which is special because
  /// it takes a string rather than an IEnumerable&lt;string&gt;
  /// </summary>
  private static Aggregate CreateHelper(IEnumerable<string> columnSpecs, AggregateMethod aggregateMethod) {
    var cs = columnSpecs.ToArray();

    LazyMaterializer lazyMaterializer = (out NativePtr<NativeAggregate> result, out ErrorStatus status) =>
      aggregateMethod(cs, cs.Length, out result, out status);

    return new Aggregate(lazyMaterializer);
  }

  private readonly LazyMaterializer _lazyMaterializer;

  private Aggregate(LazyMaterializer lazyMaterializer) => _lazyMaterializer = lazyMaterializer;

  internal InternalAggregate Materialize() {
    _lazyMaterializer(out var result, out var status);
    status.OkOrThrow();
    return new InternalAggregate(result);
  }
}

internal class InternalAggregate : IDisposable {
  internal NativePtr<NativeAggregate> Self;

  internal InternalAggregate(NativePtr<NativeAggregate> self) => Self = self;

  ~InternalAggregate() {
    ReleaseUnmanagedResources();
  }

  public void Dispose() {
    ReleaseUnmanagedResources();
    GC.SuppressFinalize(this);
  }

  private void ReleaseUnmanagedResources() {
    if (!NativePtrUtil.TryRelease(ref Self, out var old)) {
      return;
    }
    NativeAggregate.deephaven_client_Aggregate_dtor(old);
  }
}

internal class NativeAggregate {
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_dtor(NativePtr<NativeAggregate> self);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_AbsSum(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_Group(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_Avg(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_Count(
    string column, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_First(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_Last(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_Max(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_Med(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_Min(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_Pct(
    double percentile, InteropBool avgMedian,
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_Std(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_Sum(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_Var(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
  [DllImport(LibraryPaths.Dhclient, CharSet = CharSet.Unicode)]
  public static extern void deephaven_client_Aggregate_WAvg(
    [In] string[] columns, Int32 numColumns, out NativePtr<NativeAggregate> result, out ErrorStatus status);
}

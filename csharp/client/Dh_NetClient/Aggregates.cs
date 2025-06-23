using System;
using Io.Deephaven.Proto.Backplane.Grpc;

namespace Deephaven.Dh_NetClient;


/// <summary>
/// Represents a collection of Aggregate objects.
/// </summary>
public class AggregateCombo {
  /// <summary>
  /// Create an AggregateCombo
  /// </summary>
  /// <param name="list">The contained Aggregates</param>
  /// <returns></returns>
  static AggregateCombo Create(params Aggregate[] list) {
    var aggregates = list.Select(elt => elt.Descriptor).ToArray();
    return new AggregateCombo(aggregates);
  }

  private readonly ComboAggregateRequest.Types.Aggregate[] _aggregates;

  private AggregateCombo(ComboAggregateRequest.Types.Aggregate[] aggregates) {
    _aggregates = aggregates;
  }
}

public class Aggregate {
  public readonly ComboAggregateRequest.Types.Aggregate Descriptor;

  /// <summary>
  /// Returns an aggregator that computes the total sum of values, within an aggregation group,
  /// for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate AbsSum(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.AbsSum, columnSpecs);
  }

  /// <summary>
  /// Returns an aggregator that computes the average (mean) of values, within an aggregation group,
  /// for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate Avg(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.Avg, columnSpecs);
  }

  /// <summary>
  /// Returns an aggregator that computes the number of elements within an aggregation group,
  /// for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate Count(params string[] columnSpecs) {
    auto ad = CreateDescForColumn(ComboAggregateRequest::COUNT, std::move(column_spec));
    auto impl = AggregateImpl::Create(std::move(ad));
    return Aggregate(std::move(impl));
  }

  /// <summary>
  /// Returns an aggregator that computes the first value, within an aggregation group,
  /// for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate First(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.First, columnSpecs);
  }

  /// <summary>
  /// Returns an aggregator that computes an array of all values within an aggregation group,
  /// for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate Group(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.Group, columnSpecs);
  }

  /// <summary>
  /// Returns an aggregator that computes the last value, within an aggregation group,
  /// for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate Last(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.Last, columnSpecs);
  }

  /// <summary>
  /// Returns an aggregator that computes the maximum value, within an aggregation group,
  /// for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate Max(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.Max, columnSpecs);
  }

  /// <summary>
  /// Returns an aggregator that computes the median value, within an aggregation group,
  /// for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate Med(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.Median, columnSpecs);
  }

  /// <summary>
  /// Returns an aggregator that computes the minimum value, within an aggregation group,
  /// for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate Min(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.Min, columnSpecs);
  }

  /// <summary>
  /// Returns an aggregator that computes the designated percentile, within an aggregation group,
  /// for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate Pct(double percentile, bool avgMedian, params string[] columnSpecs) {
    ComboAggregateRequest::Aggregate pd;
    pd.set_type(ComboAggregateRequest::PERCENTILE);
    pd.set_percentile(percentile);
    pd.set_avg_median(avg_median);
    for (auto & cs : column_specs) {
      pd.mutable_match_pairs()->Add(std::move(cs));
    }
    auto impl = AggregateImpl::Create(std::move(pd));
    return Aggregate(std::move(impl));
  }

  /// <summary>
  /// Returns an aggregator that computes the sample standard deviation of values, within an
  /// aggregation group, for each input column.
  ///
  /// Sample standard deviation is computed using Bessel's correction (https://en.wikipedia.org/wiki/Bessel%27s_correction),
  /// which ensures that the sample variance will be an unbiased estimator of population variance.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate Std(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.Std, columnSpecs);
  }

  /// <summary>
  /// Returns an aggregator that computes the total sum of values, within an
  /// aggregation group, for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate Sum(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.Sum, columnSpecs);
  }

  /// <summary>
  /// Returns an aggregator that computes the sample variance of values, within an
  /// aggregation group, for each input column.
  ///
  /// Sample variance is computed using Bessel's correction (https://en.wikipedia.org/wiki/Bessel%27s_correction),
  /// which ensures that the sample variance will be an unbiased estimator of population variance.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate Var(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.Var, columnSpecs);
  }

  /// <summary>
  /// Returns an aggregator that computes the weighted average of values, within an
  /// aggregation group, for each input column.
  /// </summary>
  /// <param name="columnSpecs"></param>
  /// <returns>An Aggregate object representing the aggregation</returns>
  static Aggregate WAvg(params string[] columnSpecs) {
    return CreateAggForMatchPairs(ComboAggregateRequest.Types.AggType.WeightedAvg, columnSpecs);
  }
}

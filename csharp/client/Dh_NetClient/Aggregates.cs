using System;
namespace Deephaven.Dh_NetClient;


/**
 * Represents a collection of Aggregate objects.
 */

class AggregateCombo {
  public:
  /**
   * Create an AggregateCombo from an initializer list.
   */
  [[nodiscard]]
  static AggregateCombo Create(std::initializer_list<Aggregate> list);
  /**
   * Create an AggregateCombo from a vector.
   */
  [[nodiscard]]
  static AggregateCombo Create(std::vector<Aggregate> vec);

  /**
   * Copy constructor
   */
  AggregateCombo(const AggregateCombo &other);
  /**
   * Move constructor
   */
  AggregateCombo(AggregateCombo &&other) noexcept;
  /**
   * Copy assigment operator.
   */
  AggregateCombo &operator=(const AggregateCombo &other);
  /**
   * Move assigment operator.
   */
  AggregateCombo &operator=(AggregateCombo &&other) noexcept;

  ~AggregateCombo();

  /**
   * Returns the underlying "impl" object. Used internally.
   */
  [[nodiscard]]
  const std::shared_ptr<impl::AggregateComboImpl> &Impl() const { return impl_; }

  private:
  explicit AggregateCombo(std::shared_ptr<impl::AggregateComboImpl> impl);

  std::shared_ptr<impl::AggregateComboImpl> impl_;
}

class Aggregate {
  public:
  /*
 * Default constructor. Creates a (useless) empty object.
 */
  Aggregate();
  /**
   * Copy constructor
   */
  Aggregate(const Aggregate &other);
  /**
   * Move constructor
   */
  Aggregate(Aggregate &&other) noexcept;
  /**
   * Copy assigment operator.
   */
  Aggregate &operator=(const Aggregate &other);
  /**
   * Move assigment operator.
   */
  Aggregate &operator=(Aggregate &&other) noexcept;
  /**
   * Destructor
   */
  ~Aggregate();
  /**
   * Returns an aggregator that computes the total sum of values, within an aggregation group,
   * for each input column.
   */
  [[nodiscard]]
  static Aggregate AbsSum(std::vector<std::string> column_specs);
  /**
   * A variadic form of AbsSum(std::vector<std::string>) const that takes a combination of
   * argument types.
   * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
   * @param args The arguments to AbsSum
   * @return An Aggregate object representing the aggregation
   */
  template<typename...Args>
  [[nodiscard]]
  static Aggregate AbsSum(Args &&...args) {
    std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return AbsSum(std::move(vec));
  }

  /**
   * Returns an aggregator that computes an array of all values within an aggregation group,
   * for each input column.
   */
  [[nodiscard]]
  static Aggregate Group(std::vector<std::string> column_specs);
/**
 * A variadic form of Group(std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to Group
 * @return An Aggregate object representing the aggregation
 */
template < typename...Args >
[[nodiscard]]
  static Aggregate Group(Args &&...args) {
  std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return Group(std::move(vec));
  }

  /**
   * Returns an aggregator that computes the average (mean) of values, within an aggregation group,
   * for each input column.
   */
  [[nodiscard]]
  static Aggregate Avg(std::vector<std::string> column_specs);
/**
 * A variadic form of Avg(std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to Avg
 * @return An Aggregate object representing the aggregation
 */
template < typename...Args >
[[nodiscard]]
  static Aggregate Avg(Args &&...args) {
  std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return Avg(std::move(vec));
  }

  /**
   * Returns an aggregator that computes the number of elements within an aggregation group.
   */
  [[nodiscard]]
  static Aggregate Count(std::string column_spec);

/**
 * Returns an aggregator that computes the first value, within an aggregation group,
 * for each input column.
 */
[[nodiscard]]
  static Aggregate First(std::vector<std::string> column_specs);
/**
 * A variadic form of First(std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to First
 * @return An Aggregate object representing the aggregation
 */
template < typename...Args >
[[nodiscard]]
  static Aggregate First(Args &&...args) {
  std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return First(std::move(vec));
  }

  /**
   * Returns an aggregator that computes the last value, within an aggregation group,
   * for each input column.
   */
  [[nodiscard]]
  static Aggregate Last(std::vector<std::string> column_specs);
/**
 * A variadic form of First(std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to Last
 * @return An Aggregate object representing the aggregation
 */
template < typename...Args >
[[nodiscard]]
  static Aggregate Last(Args &&...args) {
  std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return Last(std::move(vec));
  }

  /**
   * Returns an aggregator that computes the maximum value, within an aggregation group,
   * for each input column.
   */
  [[nodiscard]]
  static Aggregate Max(std::vector<std::string> column_specs);
/**
 * A variadic form of Max(std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to Max
 * @return An Aggregate object representing the aggregation
 */
template < typename...Args >
[[nodiscard]]
  static Aggregate Max(Args &&...args) {
  std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return Max(std::move(vec));
  }

  /**
   * Returns an aggregator that computes the median value, within an aggregation group,
   * for each input column.
   */
  [[nodiscard]]
  static Aggregate Med(std::vector<std::string> column_specs);
/**
 * A variadic form of Med(std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to Med
 * @return An Aggregate object representing the aggregation
 */
template < typename...Args >
[[nodiscard]]
  static Aggregate Med(Args &&...args) {
  std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return Med(std::move(vec));
  }

  /**
   * Returns an aggregator that computes the minimum value, within an aggregation group,
   * for each input column.
   */
  [[nodiscard]]
  static Aggregate Min(std::vector<std::string> column_specs);
/**
 * A variadic form of Min(std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to Min
 * @return An Aggregate object representing the aggregation
 */
template < typename...Args >
[[nodiscard]]
  static Aggregate Min(Args &&...args) {
  std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return Min(std::move(vec));
  }

  /**
   * Returns an aggregator that computes the designated percentile of values, within an aggregation
   * group, for each input column.
   */
  [[nodiscard]]
  static Aggregate Pct(double percentile, bool avg_median, std::vector<std::string> column_specs);
/**
 * A variadic form of Pct(double, bool, std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to Pct
 * @return An Aggregate object representing the aggregation
 */
template < typename...Args >
[[nodiscard]]
  static Aggregate Pct(double percentile, bool avg_median, Args &&...args) {
  std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return Pct(percentile, avg_median, std::move(vec));
  }

  /**
   * Returns an aggregator that computes the sample standard deviation of values, within an
   * aggregation group, for each input column.
   *
   * Sample standard deviation is computed using Bessel's correction (https://en.wikipedia.org/wiki/Bessel%27s_correction),
   * which ensures that the sample variance will be an unbiased estimator of population variance.
   */
  [[nodiscard]]
  static Aggregate Std(std::vector<std::string> column_specs);
/**
 * A variadic form of Std(std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to Std
 * @return An Aggregate object representing the aggregation
 */
template < typename...Args >
[[nodiscard]]
  static Aggregate Std(Args &&...args) {
  std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return Std(std::move(vec));
  }

  /**
   * Returns an aggregator that computes the total sum of values, within an aggregation group,
   * for each input column.
   */
  [[nodiscard]]
  static Aggregate Sum(std::vector<std::string> column_specs);
/**
 * A variadic form of Sum(std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to Sum
 * @return An Aggregate object representing the aggregation
 */
template < typename...Args >
[[nodiscard]]
  static Aggregate Sum(Args &&...args) {
  std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return Sum(std::move(vec));
  }

  /**
   * Returns an aggregator that computes the sample variance of values, within an aggregation group,
   * for each input column.
   *
   * Sample variance is computed using Bessel's correction (https://en.wikipedia.org/wiki/Bessel%27s_correction),
   * which ensures that the sample variance will be an unbiased estimator of population variance.
   */
  [[nodiscard]]
  static Aggregate Var(std::vector<std::string> column_specs);
/**
 * A variadic form of Var(std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to Var
 * @return An Aggregate object representing the aggregation
 */
template < typename...Args >
[[nodiscard]]
  static Aggregate Var(Args &&...args) {
  std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return Var(std::move(vec));
  }

  /**
   * Returns an aggregator that computes the weighted average of values, within an aggregation
   * group, for each input column.
   */
  [[nodiscard]]
  static Aggregate WAvg(std::string weight_column, std::vector<std::string> column_specs);
/**
 * A variadic form of WAvg(std::vector<std::string>) const that takes a combination of
 * argument types.
 * @tparam Args Any combination of `std::string`, `std::string_view`, or `const char *`
 * @param args The arguments to WAvg
 * @return An Aggregate object representing the aggregation
 */
template<typename WeightArg, typename...Args>
[[nodiscard]]
  static Aggregate WAvg(WeightArg &&weight_column, Args &&...args) {
  auto weight = internal::ConvertToString::ToString(std::forward<WeightArg>(weight_column));
std::vector < std::string> vec{internal::ConvertToString::ToString(std::forward<Args>(args))...};
return WAvg(std::move(weight), std::move(vec));
  }

  /**
   * Constructor.
   */
  explicit Aggregate(std::shared_ptr<impl::AggregateImpl> impl);

/**
 * Returns the underlying "impl" object. Used internally.
 */
[[nodiscard]]
  const std::shared_ptr<impl::AggregateImpl> &Impl() const { return impl_; }

private:
  std::shared_ptr<impl::AggregateImpl> impl_;
}
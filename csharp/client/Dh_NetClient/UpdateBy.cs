using Io.Deephaven.Proto.Backplane.Grpc;
using System;
using UpdateByOperationProto = Io.Deephaven.Proto.Backplane.Grpc.UpdateByRequest.Types.UpdateByOperation;
using BadDataBehaviorProtoEnum = Io.Deephaven.Proto.Backplane.Grpc.BadDataBehavior;
using MathContextProto = Io.Deephaven.Proto.Backplane.Grpc.MathContext;
using RoundingModeProtoEnum = Io.Deephaven.Proto.Backplane.Grpc.MathContext.Types.RoundingMode;

namespace Deephaven.Dh_NetClient;

public enum MathContext : Int32 {
  Unlimited, Decimal32, Decimal64, Decimal128
}

public enum BadDataBehavior : Int32 {
  Reset, Skip, Throw, Poison
}

public enum DeltaControl : Int32 {
  NullDominates, ValueDominates, ZeroDominates
}

public record OperationControl(
  BadDataBehavior OnNull = BadDataBehavior.Skip,
  BadDataBehavior OnNan = BadDataBehavior.Skip,
  MathContext BigValueContext = MathContext.Decimal128);

public class UpdateByOperation {
  public readonly UpdateByOperationProto UpdateByProto;

  public UpdateByOperation(UpdateByRequest.Types.UpdateByOperation updateByProto) {
    UpdateByProto = updateByProto;
  }

  public static UpdateByOperation CumSum(params string[] cols) {
    var ubb = new UpdateByBuilder(cols);
    ubb.MutableColumnSpec().Sum =
      new UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec.Types.UpdateByCumulativeSum();
    return ubb.Build();
  }

  public static UpdateByOperation CumProd(params string[] cols) {
    var ubb = new UpdateByBuilder(cols);
    ubb.MutableColumnSpec().Product = new UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec.Types.UpdateByCumulativeProduct();
    return ubb.Build();
  }

  public static UpdateByOperation CumMin(params string[] cols) {
    var ubb = new UpdateByBuilder(cols);
    ubb.MutableColumnSpec().Min = new UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec.Types.UpdateByCumulativeMin();
    return ubb.Build();
  }

  public static UpdateByOperation CumMax(params string[] cols) {
    var ubb = new UpdateByBuilder(cols);
    ubb.MutableColumnSpec().Max = new UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec.Types.UpdateByCumulativeMax();
    return ubb.Build();
  }

  public static UpdateByOperation ForwardFill(params string[] cols) {
    var ubb = new UpdateByBuilder(cols);
    ubb.MutableColumnSpec().Fill = new UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec.Types.UpdateByFill();
    return ubb.Build();
  }

  public static UpdateByOperation Delta(IEnumerable<string> cols, DeltaControl deltaControl = DeltaControl.NullDominates) {
    var ubb = new UpdateByBuilder(cols);
    ubb.MutableColumnSpec().Delta =
      new UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec.Types.UpdateByDelta {
        Options = new UpdateByDeltaOptions {
          NullBehavior = ConvertDeltaControl(deltaControl)
        }
      };
    return ubb.Build();
  }

  public static UpdateByOperation EmaTick(double decayTicks, IEnumerable<string> cols, OperationControl? opControl = null) {
    var ubb = new UpdateByBuilder(cols);
    ubb.MutableColumnSpec().Ema = new UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec.Types.UpdateByEma {
      Options = ConvertOperationControl(opControl),
      WindowScale = MakeWindowScale(decayTicks)
    };
    return ubb.Build();
  }

  public static UpdateByOperation EmaTime(string timestampCol, DurationSpecifier decayTime,
    IEnumerable<string> cols, OperationControl? opControl = null) {
    var ubb = new UpdateByBuilder(cols);
    ubb.MutableColumnSpec().Ema = new UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec.Types.UpdateByEma {
      Options = ConvertOperationControl(opControl),
      WindowScale = MakeWindowScale(timestampCol, decayTime)
    };
    return ubb.Build();
  }

  public static UpdateByOperation EmsTick(double decayTicks, IEnumerable<string> cols, OperationControl? opControl = null) {
    var ubb = new UpdateByBuilder(cols);
    ubb.MutableColumnSpec().Ems = new UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec.Types.UpdateByEms {
      Options = ConvertOperationControl(opControl),
      WindowScale = MakeWindowScale(decayTicks)
    };
    return ubb.Build();
  }

  public static UpdateByOperation EmsTime(string timestampCol, DurationSpecifier decayTime,
    IEnumerable<string> cols, OperationControl? opControl = null) {
    var ubb = new UpdateByBuilder(cols);
    ubb.MutableColumnSpec().Ems = new UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec.Types.UpdateByEms {
      Options = ConvertOperationControl(opControl),
      WindowScale = MakeWindowScale(timestampCol, decayTime)
    };
    return ubb.Build();
  }
  UpdateByOperation emminTick(double decay_ticks, std::vector<std::string> cols,
  const OperationControl &op_control) {
    UpdateByBuilder ubb(std::move(cols));
    ubb.SetTicks(&UpdateBySpec::mutable_em_min, decay_ticks, op_control);
    return ubb.Build();
  }

  UpdateByOperation emminTime(std::string timestamp_col, DurationSpecifier decay_time,
  std::vector<std::string> cols, const OperationControl &op_control) {
    UpdateByBuilder ubb(std::move(cols));
    ubb.SetTime(&UpdateBySpec::mutable_em_min, std::move(timestamp_col), std::move(decay_time), op_control);
    return ubb.Build();
  }


  private static UpdateByNullBehavior ConvertDeltaControl(DeltaControl dc) {
    return dc switch {
      DeltaControl.NullDominates => UpdateByNullBehavior.NullDominates,
      DeltaControl.ValueDominates => UpdateByNullBehavior.ValueDominates,
      DeltaControl.ZeroDominates => UpdateByNullBehavior.ZeroDominates,
      _ => throw new Exception($"Unexpected DeltaControl {dc}")
    };
  }

  private static UpdateByEmOptions ConvertOperationControl(OperationControl? oc) {
    oc ??= new OperationControl();
    var result = new UpdateByEmOptions {
      OnNullValue = ConvertBadDataBehavior(oc.OnNull),
      OnNanValue = ConvertBadDataBehavior(oc.OnNan),
      BigValueContext = ConvertMathContext(oc.BigValueContext)
    };
    return result;
  }

  private static BadDataBehaviorProtoEnum ConvertBadDataBehavior(BadDataBehavior bdb) {
    return bdb switch {
      BadDataBehavior.Reset => BadDataBehaviorProtoEnum.Reset,
      BadDataBehavior.Skip => BadDataBehaviorProtoEnum.Skip,
      BadDataBehavior.Throw => BadDataBehaviorProtoEnum.Throw,
      BadDataBehavior.Poison => BadDataBehaviorProtoEnum.Poison,
      _ => throw new Exception($"Unexpected BadDataBehavior {bdb}")
    };
  }

  private static MathContextProto ConvertMathContext(MathContext mctx) {
    var (precision, roundingMode) = mctx switch {
      // For the values used here, please see the documentation for java.math.MathContext:
      // https://docs.oracle.com/javase/8/docs/api/java/math/MathContext.html

      // "A MathContext object whose settings have the values required for unlimited precision arithmetic."
      MathContext.Unlimited => (0, RoundingModeProtoEnum.HalfUp),

      // "A MathContext object with a precision setting matching the IEEE 754R Decimal32 format, 7 digits, and a rounding mode of HALF_EVEN, the IEEE 754R default."
      MathContext.Decimal32 => (7, RoundingModeProtoEnum.HalfEven),

      // "A MathContext object with a precision setting matching the IEEE 754R Decimal64 format, 16 digits, and a rounding mode of HALF_EVEN, the IEEE 754R default."
      MathContext.Decimal64 => (16, RoundingModeProtoEnum.HalfEven),

      // "A MathContext object with a precision setting matching the IEEE 754R Decimal128 format, 34 digits, and a rounding mode of HALF_EVEN, the IEEE 754R default."
      MathContext.Decimal128 => (34, RoundingModeProtoEnum.HalfEven),

      _ => throw new Exception($"Unexpected MathContext {mctx}")
    };
    var result = new MathContextProto {
      Precision = precision,
      RoundingMode = roundingMode
    };
    return result;
  }


  private static UpdateByWindowScale MakeWindowScale(double ticks) {
    return new UpdateByWindowScale {
      Ticks = new UpdateByWindowScale.Types.UpdateByWindowTicks {
        Ticks = ticks
      }
    };
  }

  private static UpdateByWindowScale MakeWindowScale(string timestampCol, DurationSpecifier decayTime) {
    var result = new UpdateByWindowScale {
      Time = new UpdateByWindowScale.Types.UpdateByWindowTime {
        Column = timestampCol
      }
    };

    decayTime.Visit(
      nanos => result.Time.Nanos = nanos,
      duration => result.Time.DurationString = duration
      );

    return result;
  }
}

internal class UpdateByBuilder {
  private readonly UpdateByOperationProto _gup = new();

  public UpdateByBuilder(IEnumerable<string> cols) {
    _gup.Column.MatchPairs.AddRange(cols);
  }

  public UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec MutableColumnSpec() {
    _gup.Column ??= new UpdateByOperationProto.Types.UpdateByColumn();
    _gup.Column.Spec ??= new UpdateByOperationProto.Types.UpdateByColumn.Types.UpdateBySpec();
    return _gup.Column.Spec;
  }

  public UpdateByOperation Build() {
    return new UpdateByOperation(_gup);
  }



  template<typename Member>
  void TouchEmpty(Member mutable_member) {
    (void)(gup_.mutable_column()->mutable_spec()->* mutable_member)();
  }

  template<typename Member>
  void SetNullBehavior(Member mutable_member, const DeltaControl delta_control) {
    auto* which = (gup_.mutable_column()->mutable_spec()->* mutable_member)();
  auto nb = convertDeltaControl(delta_control);
  which->mutable_options()->set_null_behavior(nb);
}

template<typename Member>
  void SetTicks(Member mutable_member, double decay_ticks, const OperationControl &op_control) {
  auto* which = (gup_.mutable_column()->mutable_spec()->* mutable_member)();
  *which->mutable_options() = convertOperationControl(op_control);
  which->mutable_window_scale()->mutable_ticks()->set_ticks(decay_ticks);
}

template<typename Member>
  void SetTime(Member mutable_member, std::string timestamp_col, DurationSpecifier decay_time,
      const OperationControl &op_control) {
  auto* which = (gup_.mutable_column()->mutable_spec()->* mutable_member)();
  *which->mutable_options() = convertOperationControl(op_control);
  *which->mutable_window_scale()->mutable_time() =
      convertDecayTime(std::move(timestamp_col), std::move(decay_time));
}

template<typename Member>
  void SetRevAndFwdTicks(Member mutable_member, int rev_ticks, int fwd_ticks) {
  auto* which = (gup_.mutable_column()->mutable_spec()->* mutable_member)();
  which->mutable_reverse_window_scale()->mutable_ticks()->set_ticks(rev_ticks);
  which->mutable_forward_window_scale()->mutable_ticks()->set_ticks(fwd_ticks);
}

template<typename Member>
  void SetRevAndFwdTime(Member mutable_member, std::string timestamp_col,
      DurationSpecifier rev_time, DurationSpecifier fwd_time) {
  auto* which = (gup_.mutable_column()->mutable_spec()->* mutable_member)();
  *which->mutable_reverse_window_scale()->mutable_time() =
      convertDecayTime(timestamp_col, std::move(rev_time));
  *which->mutable_forward_window_scale()->mutable_time() =
      convertDecayTime(std::move(timestamp_col), std::move(fwd_time));
}

template<typename Member>
  void SetWeightedRevAndFwdTicks(Member mutable_member, std::string weight_col, const int rev_ticks,
      const int fwd_ticks) {
  auto* which = (gup_.mutable_column()->mutable_spec()->* mutable_member)();
  *which->mutable_weight_column() = std::move(weight_col);
  SetRevAndFwdTicks(mutable_member, rev_ticks, fwd_ticks);
}

template<typename Member>
  void SetWeightedRevAndFwdTime(Member mutable_member, std::string timestamp_col,
      std::string weight_col, DurationSpecifier rev_time, DurationSpecifier fwd_time) {
  auto* which = (gup_.mutable_column()->mutable_spec()->* mutable_member)();
  *which->mutable_weight_column() = std::move(weight_col);
  SetRevAndFwdTime(mutable_member, std::move(timestamp_col), std::move(rev_time), std::move(fwd_time));
}


}



using deephaven::client::impl::MoveVectorData;
using deephaven::client::impl::UpdateByOperationImpl;
// typedef io::deephaven::proto::backplane::grpc::UpdateByDelta UpdateByDelta;
using io::deephaven::proto::backplane::grpc::UpdateByEmOptions;

using BadDataBehaviorProtoEnum = io::deephaven::proto::backplane::grpc::BadDataBehavior;
using MathContextProto = io::deephaven::proto::backplane::grpc::MathContext;
using RoundingModeProtoEnum = io::deephaven::proto::backplane::grpc::MathContext::RoundingMode;
using UpdateByNullBehavior = io::deephaven::proto::backplane::grpc::UpdateByNullBehavior;
//typedef io::deephaven::proto::backplane::grpc::UpdateByRequest::UpdateByOperation::UpdateByColumn UpdateByColumn;
using UpdateBySpec = io::deephaven::proto::backplane::grpc::UpdateByRequest::UpdateByOperation::UpdateByColumn::UpdateBySpec;
using UpdateByOperationProto = io::deephaven::proto::backplane::grpc::UpdateByRequest::UpdateByOperation;
using UpdateByWindowTime = io::deephaven::proto::backplane::grpc::UpdateByWindowScale::UpdateByWindowTime;
using DurationSpecifier = deephaven::client::utility::DurationSpecifier;


namespace deephaven::client::update_by {











UpdateByOperation emmaxTick(double decay_ticks, std::vector<std::string> cols,
    const OperationControl &op_control) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetTicks(&UpdateBySpec::mutable_em_max, decay_ticks, op_control);
  return ubb.Build();
}

UpdateByOperation emmaxTime(std::string timestamp_col, DurationSpecifier decay_time,
    std::vector<std::string> cols, const OperationControl &op_control) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetTime(&UpdateBySpec::mutable_em_max, std::move(timestamp_col), std::move(decay_time), op_control);
  return ubb.Build();
}

UpdateByOperation emstdTick(double decay_ticks, std::vector<std::string> cols,
    const OperationControl &op_control) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetTicks(&UpdateBySpec::mutable_em_std, decay_ticks, op_control);
  return ubb.Build();
}

UpdateByOperation emstdTime(std::string timestamp_col, DurationSpecifier decay_time,
    std::vector<std::string> cols, const OperationControl &op_control) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetTime(&UpdateBySpec::mutable_em_std, std::move(timestamp_col), std::move(decay_time), op_control);
  return ubb.Build();
}

UpdateByOperation rollingSumTick(std::vector<std::string> cols, int rev_ticks, int fwd_ticks) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTicks(&UpdateBySpec::mutable_rolling_sum, rev_ticks, fwd_ticks);
  return ubb.Build();
}

UpdateByOperation rollingSumTime(std::string timestamp_col, std::vector<std::string> cols,
    DurationSpecifier rev_time, DurationSpecifier fwd_time) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTime(&UpdateBySpec::mutable_rolling_sum, std::move(timestamp_col),
      std::move(rev_time), std::move(fwd_time));
  return ubb.Build();
}

UpdateByOperation rollingGroupTick(std::vector<std::string> cols, int rev_ticks, int fwd_ticks) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTicks(&UpdateBySpec::mutable_rolling_group, rev_ticks, fwd_ticks);
  return ubb.Build();
}

UpdateByOperation rollingGroupTime(std::string timestamp_col, std::vector<std::string> cols,
    DurationSpecifier rev_time, DurationSpecifier fwd_time) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTime(&UpdateBySpec::mutable_rolling_group, std::move(timestamp_col),
      std::move(rev_time), std::move(fwd_time));
  return ubb.Build();
}

UpdateByOperation rollingAvgTick(std::vector<std::string> cols, int rev_ticks, int fwd_ticks) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTicks(&UpdateBySpec::mutable_rolling_avg, rev_ticks, fwd_ticks);
  return ubb.Build();
}

UpdateByOperation rollingAvgTime(std::string timestamp_col, std::vector<std::string> cols,
    DurationSpecifier rev_time, DurationSpecifier fwd_time) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTime(&UpdateBySpec::mutable_rolling_avg, std::move(timestamp_col),
      std::move(rev_time), std::move(fwd_time));
  return ubb.Build();
}

UpdateByOperation rollingMinTick(std::vector<std::string> cols, int rev_ticks, int fwd_ticks) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTicks(&UpdateBySpec::mutable_rolling_min, rev_ticks, fwd_ticks);
  return ubb.Build();
}

UpdateByOperation rollingMinTime(std::string timestamp_col, std::vector<std::string> cols,
    DurationSpecifier rev_time, DurationSpecifier fwd_time) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTime(&UpdateBySpec::mutable_rolling_min, std::move(timestamp_col),
      std::move(rev_time), std::move(fwd_time));
  return ubb.Build();
}

UpdateByOperation rollingMaxTick(std::vector<std::string> cols, int rev_ticks, int fwd_ticks) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTicks(&UpdateBySpec::mutable_rolling_max, rev_ticks, fwd_ticks);
  return ubb.Build();
}

UpdateByOperation rollingMaxTime(std::string timestamp_col, std::vector<std::string> cols,
    DurationSpecifier rev_time, DurationSpecifier fwd_time) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTime(&UpdateBySpec::mutable_rolling_max, std::move(timestamp_col),
      std::move(rev_time), std::move(fwd_time));
  return ubb.Build();
}

UpdateByOperation rollingProdTick(std::vector<std::string> cols, int rev_ticks, int fwd_ticks) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTicks(&UpdateBySpec::mutable_rolling_product, rev_ticks, fwd_ticks);
  return ubb.Build();
}

UpdateByOperation rollingProdTime(std::string timestamp_col, std::vector<std::string> cols,
    DurationSpecifier rev_time, DurationSpecifier fwd_time) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTime(&UpdateBySpec::mutable_rolling_product, std::move(timestamp_col),
      std::move(rev_time), std::move(fwd_time));
  return ubb.Build();
}

UpdateByOperation rollingCountTick(std::vector<std::string> cols, int rev_ticks, int fwd_ticks) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTicks(&UpdateBySpec::mutable_rolling_count, rev_ticks, fwd_ticks);
  return ubb.Build();
}

UpdateByOperation rollingCountTime(std::string timestamp_col, std::vector<std::string> cols,
    DurationSpecifier rev_time, DurationSpecifier fwd_time) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTime(&UpdateBySpec::mutable_rolling_count, std::move(timestamp_col),
      std::move(rev_time), std::move(fwd_time));
  return ubb.Build();
}

UpdateByOperation rollingStdTick(std::vector<std::string> cols, int rev_ticks, int fwd_ticks) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTicks(&UpdateBySpec::mutable_rolling_std, rev_ticks, fwd_ticks);
  return ubb.Build();
}

UpdateByOperation rollingStdTime(std::string timestamp_col, std::vector<std::string> cols,
    DurationSpecifier rev_time, DurationSpecifier fwd_time) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetRevAndFwdTime(&UpdateBySpec::mutable_rolling_std, std::move(timestamp_col),
      std::move(rev_time), std::move(fwd_time));
  return ubb.Build();
}

UpdateByOperation rollingWavgTick(std::string weight_col, std::vector<std::string> cols,
    int rev_ticks, int fwd_ticks) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetWeightedRevAndFwdTicks(&UpdateBySpec::mutable_rolling_wavg, std::move(weight_col), rev_ticks,
      fwd_ticks);
  return ubb.Build();
}

UpdateByOperation rollingWavgTime(std::string timestamp_col, std::string weight_col,
    std::vector<std::string> cols, DurationSpecifier rev_time, DurationSpecifier fwd_time) {
  UpdateByBuilder ubb(std::move(cols));
  ubb.SetWeightedRevAndFwdTime(&UpdateBySpec::mutable_rolling_wavg, std::move(timestamp_col),
      std::move(weight_col), std::move(rev_time), std::move(fwd_time));
  return ubb.Build();
}
}  // namespace deephaven::client::update_by

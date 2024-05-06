using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deephaven.DeephavenClient.Utility;

/**
 * These values need to be kept in sync with the corresponding values on the C++ side.
 */
internal enum ElementTypeId {
  Char,
  Int8,
  Int16,
  Int32,
  Int64,
  Float,
  Double,
  Bool,
  String,
  Timestamp,
  List
};

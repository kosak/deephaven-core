using Io.Deephaven.Proto.Controller;

namespace Deephaven.DheClient.Controller;

public class Subscription : IDisposable {
  private readonly SubscriptionContext _context;

  internal Subscription(SubscriptionContext context) {
    _context = context;
  }

  /**
 * Get a snapshot of the current persistent query state.
 * This method stores two values in its provided pointer parameters:
 * the current version number,
 * which can be used in a subsequent call to `Next`, and a map
 * of PQ serial to persistent query infos.
 * Note version number here has nothing to do with a particular
 * Persistent Query (PQ) version; the version here is a concept about
 * the subscription information: if version X and version Y are such that
 * Y > X, that means version Y is newer than X, Y reflects a more recent
 * snapshot of the state of all PQs in the controller server, as known by the
 * client at this time.
 *
 * Note that the map stored behaves as an immutable map which
 * is a reference counted indirect object; the data payload of the map will be
 * shared by any copies made and will eventually be released
 * when the last object is destroyed. The map can be copied cheaply.
 *
 * @param version_out an out parameter where the current version number will
 *        be stored.
 * @param map_out an output parameter where the current map of PQ state
 *        will be stored.
 * @return false it this Subscription is closed and shut down already,
 *         in which case the output parameter pointer values are unchanged;
 *         true otherwise.  See the documentation for `IsClosed` for
 *         an explanation of how the subscription can become closed.
 */
  public bool Current(out Int64 version,
    out IReadOnlyDictionary<Int64, PersistentQueryInfoMessage> map) =>
  _context.Current(out version, out map);

  /**
   * Block until deadline, or a new version is available, or the subscription
   * is closed.
   * See the note on Current about the meaning of version in the context
   * of a subscription.
   *
   * @param has_newer_version an out parameter whose pointer value will be set to
   *        true if there is a version available more recent than
   *        the `version` argument supplied.
   * @param version the version reference used for setting 'has_newer_version'.
   * @param deadline the deadline
   * @return false if this Subscription is closed and shut down already.
   *         Otherwise true, meaning the deadline was reached, there is a new
   *         version available, or both. Consult *has_newer_version to
   *         distinguish between these cases.
   */
  public bool Next(out bool hasNewerVersion, Int64 version, DateTimeOffset deadline) =>
    _context.Next(out hasNewerVersion, version, deadline);

  /**
   * Block until a new version is available, or the subscription is
   * closed.
   *
   * @param version a version reference; next will return true only if a
   *        new version more recent than this argument is available.
   * @return false it this Subscription is closed and shut down already,
   *         true otherwise.  See the documentation for `IsClosed` for
   *         an explanation of how the subscription can become closed.
   */
  public bool Next(Int64 version) => _context.Next(version);
}

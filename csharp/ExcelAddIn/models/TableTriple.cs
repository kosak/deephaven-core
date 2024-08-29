using Deephaven.ExcelAddIn.Providers;

namespace Deephaven.ExcelAddIn.Models;

public record TableTriple(
  EndpointId? EndpointId,
  PersistentQueryId? PersistentQueryId,
  string TableName) {

  public static bool TryParse(string text, out TableTriple result, out string errorText) {
    // Accepts strings of the following form
    // 1. "table" (becomes "", "", "table")
    // 2. "endpoint:table" (becomes endpoint, "", table)
    // 3. "pq/table" (becomes "", pq, table)
    // 4. "endpoint:pq/table" (becomes endpoint, pq, table)
    var epId = "";
    var pqid = "";
    var tableName = "";
    var colonIndex = text.IndexOf(':');
    if (colonIndex > 0) {
      // cases 2 and 4: pull out the endpointId, and then reduce to cases 1 and 3
      epId = text.Substring(0, colonIndex);
      text = text.Substring(colonIndex + 1);
    }

    var slashIndex = text.IndexOf('/');
    if (slashIndex > 0) {
      // case 3: pull out the slash, and reduce to case 1
      pqid = text.Substring(0, slashIndex);
      text = text.Substring(slashIndex + 1);
    }

    tableName = text;
    result = new TableTriple(new EndpointId(epId), new PersistentQueryId(pqid), tableName);
    errorText = "";
    // This version never fails to parse, but we leave open the option in our API to do so.
    return true;
  }
}

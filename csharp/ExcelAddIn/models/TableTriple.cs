namespace Deephaven.ExcelAddIn.Models;

public record TableTriple(
  EndpointId? EndpointId,
  PqName? PqName,
  string TableName) {

  public static bool TryParse(string text, out TableTriple result, out string errorText) {
    // Accepts strings of the following form
    // 1. "table" (becomes null, null, "table")
    // 2. "endpoint:table" (becomes endpoint, null, table)
    // 3. "pq/table" (becomes null, pq, table)
    // 4. "endpoint:pq/table" (becomes endpoint, pq, table)
    EndpointId? epId = null;
    PqName? pqName = null;
    var tableName = "";
    var colonIndex = text.IndexOf(':');
    if (colonIndex > 0) {
      // cases 2 and 4: pull out the endpointId, and then reduce to cases 1 and 3
      epId = new EndpointId(text[..colonIndex]);
      text = text[(colonIndex + 1)..];
    }

    var slashIndex = text.IndexOf('/');
    if (slashIndex > 0) {
      // case 3: pull out the slash, and reduce to case 1
      pqName = new PqName(text[..slashIndex]);
      text = text[(slashIndex + 1)..];
    }

    tableName = text;
    result = new TableTriple(epId, pqName, tableName);
    errorText = "";
    // This version never fails to parse, but we leave open the option in our API to do so.
    return true;
  }
}

public record TableQuad(
  EndpointId? EndpointId,
  PqName? PqName,
  string TableName,
  string Condition);

public record RetryKey(
  EndpointId EndpointId,
  PqName? PqName,
  string TableName);

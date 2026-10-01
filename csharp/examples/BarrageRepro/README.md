# Barrage subscription repros for the .NET client

Four self-contained programs, each demonstrating one problem the .NET client hits when it subscribes to a
large table. Three of them kill the subscription outright; the fourth makes it quadratically slow.

Nothing here is specific to any deployment: every repro builds its own table on the server with a few lines
of Python, so a stock server and this project are all you need.

## Running

```bash
cd csharp/examples/BarrageRepro

export DH_HOST=localhost   # defaults to localhost
export DH_PORT=10000       # defaults to 10000

dotnet run          # all four
dotnet run 2        # just repro 2
dotnet run 2 3      # repros 2 and 3
```

The server needs a Python session (each repro defines its table with a Python snippet) and anonymous auth, or
else adjust `Harness.Connect`.

## The four

| # | Table | Symptom |
|---|-------|---------|
| 1 | 250k–2M rows, static | Fill time is quadratic in row count |
| 2 | 500k rows + a row every 2ms | `There is excess data in the chunk that I won't be able to process. Expected 3252, have 3277` |
| 3 | 300k rows × 31 cols, all rows modified per tick | `Chunks have inconsistent sizes: [0,0,...,0,4096]` |
| 4 | 300k rows × 31 cols, rows removed from the top | `Of the 25 keys specified in Interval { Begin = 299960, ... }, the set only contains 0 keys` |

Each `Repro*.cs` file opens with a full write-up: what the server is doing, what the client assumes, the
relevant server source, and what the fix has to account for. Short version:

**Repro 1 — quadratic fill.** `TableState.AddData` allocates a new column of the new total length and copies
the whole table into it on *every* record batch, and `TableState.Snapshot` clones every column in full three
or four times per message. Both costs scale with the table, not the batch, so the Nth batch re-copies the
N−1 before it. Measured: 250k rows fills in 1.0s, 2M rows in 43.4s — 8× the rows, 43× the time.

**Repros 2 and 3 — one root cause, two exceptions.** `BarrageUpdateMetadata` has two separate row sets:
`added_rows` (what the table gains) and `added_rows_included` (what these record batches carry data for). The
client reads only the first and sizes the add phase from it. The server omits `added_rows_included` when it
would be identical, which is why this only appears once a table is large enough for the snapshot to be split
into rounds — and during those rounds the server deliberately sends an *empty* `added_rows`. The Java client
reads the field (`BarrageMessageReaderImpl`); the .NET client never calls the generated
`GetAddedRowsIncludedBytes()`. Repro 3 is the same mis-sizing landing in the modify phase, where the "bad"
chunk sizes it complains about are in fact a perfectly valid message.

**Repro 4 — removes for rows not yet delivered.** A full subscription receives the server's *entire* removed
set, unscoped — `BarrageMessageWriterImpl` says so in as many words (`we'll send full subscriptions the full
removed set`). While a snapshot is still filling, that set can name rows the client has not been given, and
`TableState.Erase` converts them strictly and throws. Two things follow from the same fact: the row count
must drop by the number of rows actually erased rather than the number requested, and a non-empty removed set
that erases nothing should still take the "table unchanged" path.

Note that repro 4 is usually **masked** on an unpatched client — repro 2's exception fires first, or repro 1
is still grinding. It becomes reliable once `added_rows_included` is handled. If it reports "did not
reproduce", that is the masking rather than the absence of the bug.

## Reading the output

Repros 2–4 print one line per update, so you can see the snapshot rounds arrive and where it stops. Repro 1
prints a table instead — the thing to watch is `us/row`, which should be flat and is not:

```
          rows        fill    us/row  vs 2x rows
       250,000     1,120ms       4.5           -
       500,000     3,269ms       6.5        2.9x
     1,000,000    12,100ms      12.1        3.7x
     2,000,000    45,451ms      22.7        3.8x
```

A linear fill holds `us/row` constant and shows ~2.0× per doubling. ~3.8× per doubling, with `us/row`
doubling alongside, is O(n²).

For reference, the same four sizes once `AddData` appends in place and `Snapshot` shares storage:

```
          rows        fill    us/row  vs 2x rows
       250,000       617ms       2.5           -
       500,000       604ms       1.2        1.0x
     1,000,000     1,156ms       1.2        1.9x
     2,000,000     2,096ms       1.0        1.8x
```

Flat `us/row`, ~1.9× per doubling — and 2M rows goes from 45s to 2s.

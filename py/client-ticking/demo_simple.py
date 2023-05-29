from __future__ import annotations

import pydeephaven as pyd
import pyarrow as pa
import time
from pydeephaven_ticking.table_listener import TableListener, TableUpdate, TableListenerHandle, listen
from typing import Dict, List, Tuple

session = pyd.Session(host="localhost", port=10000)

table = session.time_table(1000000000).update_view([
    "Char1 = (ii % 5) == 0 ? null : (char)(ii + 65)",
    "Short1 = (ii % 5) == 0 ? null : (short)(ii + 1)",
    "Int1 = (ii % 5) == 0 ? null : (int)(ii + 2)",
    "Long1 = (ii % 5) == 0 ? null : (long)(ii + 3)",
    "Float1 = (ii % 5) == 0 ? null : (float)(ii + 4.4)",
    "Double1 = (ii % 5) == 0 ? null : (double)(ii + 5.5)",
    "Bool1 = (ii % 5) == 0 ? null : (ii % 2) == 0",
    "String1 = (ii % 5) == 0 ? null : `hello` + ii"
    ])
table = table.tail(5)

class MyListener(TableListener):
    def on_update(self, update: TableUpdate) -> None:
        self._show_deltas("removes", update.removed())
        self._show_deltas("adds", update.added())
        self._show_deltas("modified-prev", update.modified_prev())
        self._show_deltas("modified", update.modified())

    def on_error(self, error: str):
        print(f"Error happened: {error}")

    def _show_deltas(self, what: str, dict: Dict[str, pa.Array]):
        if len(dict) == 0:
            return

        print(f"*** {what} ***")
        for name, data in dict.items():
            print(f"name={name}, data={data}")

tlh = listen(table, MyListener())
# Start processing data in another thread
tlh.start()

# Show other work happening in the main thread
print("Sleeping for 15 seconds")
time.sleep(15)
print("Waking up and stopping the listener")

tlh.stop()

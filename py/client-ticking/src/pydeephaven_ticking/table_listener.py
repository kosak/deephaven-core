from abc import ABC, abstractmethod
from inspect import signature
from typing import Callable, Dict, Generator, List, Tuple, Union
import pyarrow as pa
import pyarrow.flight as flight
import pydeephaven_ticking._core as dhc
import pydeephaven_ticking.util as tick_util
import pydeephaven
from pydeephaven.table import Table
import threading

ColDictType = Dict[str, pa.Array]
DictGeneratorType = Generator[ColDictType, None, None]

def _make_generator(table: dhc.ClientTable,
                    rows: dhc.RowSequence,
                    col_names: Union[str, List[str], None],
                    chunk_size: int) -> DictGeneratorType:
    """Repeatedly pulls up to chunk_size elements from the indicated columns of the ClientTable, collects them
    in a dictionary (whose keys are column name and whose values are PyArrow arrays), and yields that dictionary)"""
    col_names = tick_util.canonicalize_cols_param(table, col_names)
    while not rows.empty:
        these_rows = rows.take(chunk_size)
        rows = rows.drop(chunk_size)

        result: ColDictType = {}
        for i in range(len(col_names)):
            col_name = col_names[i]
            col = table.get_column_by_name(col_name, True)
            data = col.get_chunk(these_rows)
            result[col_name] = data

        yield result


def _first_or_default(generator):
    """Returns the first (and assumed only) result of the generator, or the empty dictionary if the generator
    yields no values."""
    try:
        return next(generator)

    except StopIteration:
        return {}


class TableUpdate:
    """This object encapsulates the updates that have been received for the table in the latest update message.
    The updates come in four categories: removed, added, modified_prev, and modified.
    removed: rows that have been removed from the table.
    added: rows that have been added to the table.
    modified_prev: for modified rows, the data as it appeared before it was modified.
    modified: for modified rows, the data as it appeared after it was modified.

    We also provide an *_chunks variant for the above operations. The *chunks version take a chunk_size and
    return a generator that yields data chunked by chunk_size. This can be used if you expect large updates and
    want to process the data in chunks rather than all at once."""
    update: dhc.TickingUpdate

    def __init__(self, update: dhc.TickingUpdate):
        self.update = update

    def removed(self, cols: Union[str, List[str]] = None) -> ColDictType:
        return _first_or_default(self.removed_chunks(self.update.removed_rows.size, cols))

    def removed_chunks(self, chunk_size: int, cols: Union[str, List[str]] = None) -> DictGeneratorType:
        return _make_generator(self.update.before_removes, self.update.removed_rows, cols, chunk_size)

    def added(self, cols: Union[str, List[str]] = None) -> ColDictType:
        return _first_or_default(self.added_chunks(self.update.added_rows.size, cols))

    def added_chunks(self, chunk_size: int, cols: Union[str, List[str]] = None) -> DictGeneratorType:
        return _make_generator(self.update.after_adds, self.update.added_rows, cols, chunk_size)

    def modified_prev(self, cols: Union[str, List[str]] = None) -> ColDictType:
        return _first_or_default(self.modified_prev_chunks(self.update.all_modified_rows.size, cols))

    def modified_prev_chunks(self, chunk_size: int, cols: Union[str, List[str]] = None) -> DictGeneratorType:
        return _make_generator(self.update.before_modifies, self.update.all_modified_rows, cols, chunk_size)

    def modified(self, cols: Union[str, List[str]] = None) -> ColDictType:
        return _first_or_default(self.modified_chunks(self.update.all_modified_rows.size, cols))

    def modified_chunks(self, chunk_size: int, cols: Union[str, List[str]] = None) -> DictGeneratorType:
        return _make_generator(self.update.after_modifies, self.update.all_modified_rows, cols, chunk_size)


class TableListener(ABC):
    """The abstract base class definition for table listeners. Provides a default on_error implementation that
    just prints the exception."""
    @abstractmethod
    def on_update(self, update: TableUpdate) -> None:
        pass

    def on_error(self, error: Exception):
        print(f"Error happened during ticking processing: {error}")


class TableListenerHandle:
    """An object for managing the ticking callback state.

    Usage:
    handle = TableListenerHandle(table, MyListener())
    handle.start()  # subscribe to updates on the table
    # When updates arrive, they will invoke callbacks on your TableListener object in a separate thread
    handle.stop()  # unsubscribe and shut down the thread."""
    _table: Table
    _listener: TableListener
    _cancelled: bool
    _bp: dhc.BarrageProcessor
    _writer: flight.FlightStreamWriter
    _reader: flight.FlightStreamReader
    _thread: threading.Thread

    def __init__(self, table: Table, listener: TableListener):
        self._table = table
        self._listener = listener
        self._cancelled = False

    def start(self) -> None:
        fls = self._table.session.flight_service
        self._writer, self._reader = fls.do_exchange()
        self._bp = dhc.BarrageProcessor.create(self._table.schema)
        subreq = dhc.BarrageProcessor.create_subscription_request(self._table.ticket.ticket)
        self._writer.write_metadata(subreq)

        self._thread = threading.Thread(target=self._process_data)
        self._thread.start()

    def stop(self):
        self._cancelled = True
        self._reader.cancel()
        self._thread.join()

    def _process_data(self):
        try:
            while True:
                data, metadata = self._reader.read_chunk()
                ticking_update = self._bp.process_next_chunk(data.columns, metadata)
                if ticking_update is not None:
                    table_update = TableUpdate(ticking_update)
                    self._listener.on_update(table_update)
        except Exception as e:
            if not self._cancelled:
                self._listener.on_error(e)


def listen(table: Table, listener: Union[Callable, TableListener],
           on_error: Union[Callable, None] = None) -> TableListenerHandle:
    """A convenience method to create a TableListenerHandle. This method can be called in one of three ways:

    listen(MyTableListener())  # invoke with your own subclass of TableListener
    listen(on_update_callback)  # invoke with your own on_update Callback
    listen(on_update_callback, on_error_callback)  # invoke with your own on_update and on_error callbacks"""
    listener_to_use: TableListener
    if callable(listener):
        n_params = len(signature(listener).parameters)
        if n_params != 1:
            raise ValueError("Callabale listener function must have 1 (update) parameter.")

        if on_error is None:
            listener_to_use = _CallableAsListener(listener)
        else:
            n_params = len(signature(on_error).parameters)
            if n_params != 1:
                raise ValueError("on_error function must have 1 (exception) parameter.")
            listener_to_use = _CallableAsListenerWithErrorCallback(listener, on_error)
    elif isinstance(listener, TableListener):
        if on_error is not None:
            raise ValueError("When passing a TableListener object, second argument must be None")
        listener_to_use = listener
    else:
        raise ValueError("listener is neither callable nor TableListener object")
    return TableListenerHandle(table, listener_to_use)


class _CallableAsListener(TableListener):
    """A TableListener implementation that delegates on_update to a supplied Callback. This class does not
    override on_error."""
    _on_update_callback: Callable

    def __init__(self, on_update_callback: Callable):
        self._on_update_callback = on_update_callback

    def on_update(self, update: TableUpdate) -> None:
        self._on_update_callback(update)

class _CallableAsListenerWithErrorCallback(_CallableAsListener):
    """A TableListener implementation that delegates both on_update and on_error to supplied Callbacks."""
    _on_error_callback: Callable

    def __init__(self, on_update_callback: Callable, on_error_callback: Callable):
        super().__init__(on_update_callback)
        self._on_error_callback = on_error_callback

    def on_error(self, error: Exception) -> None:
        self._on_error_callback(error)

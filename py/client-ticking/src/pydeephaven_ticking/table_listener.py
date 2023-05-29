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

# TODO(kosak): get typing right
ColDictType = Dict[str, pa.Array]
DictGeneratorType = Generator[ColDictType, None, None]


def _make_generator(table: dhc.ClientTable,
                    rows: dhc.RowSequence,
                    col_names: Union[str, List[str]],
                    chunk_size: int) -> DictGeneratorType:
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


def _sole_item(generator):
    try:
        return next(generator)

    except StopIteration:
        return {}


class TableUpdate:
    update: dhc.TickingUpdate

    def __init__(self, update: dhc.TickingUpdate):
        self.update = update

    def removed(self, cols: Union[str, List[str]] = None) -> ColDictType:
        return _sole_item(self.removed_chunks(self.update.removed_rows.size, cols))

    def removed_chunks(self, chunk_size: int, cols: Union[str, List[str]] = None) -> DictGeneratorType:
        return _make_generator(self.update.before_removes, self.update.removed_rows, cols, chunk_size)

    def added(self, cols: Union[str, List[str]] = None) -> ColDictType:
        return _sole_item(self.added_chunks(self.update.added_rows.size, cols))

    def added_chunks(self, chunk_size: int, cols: Union[str, List[str]] = None) -> DictGeneratorType:
        return _make_generator(self.update.after_adds, self.update.added_rows, cols, chunk_size)

    def modified_prev(self, cols: Union[str, List[str]] = None) -> ColDictType:
        return _sole_item(self.modified_prev_chunks(self.update.all_modified_rows.size, cols))

    def modified_prev_chunks(self, chunk_size: int, cols: Union[str, List[str]] = None) -> DictGeneratorType:
        return _make_generator(self.update.before_modifies, self.update.all_modified_rows, cols, chunk_size)

    def modified(self, cols: Union[str, List[str]] = None) -> ColDictType:
        return _sole_item(self.modified_chunks(self.update.all_modified_rows.size, cols))

    def modified_chunks(self, chunk_size: int, cols: Union[str, List[str]] = None) -> DictGeneratorType:
        return _make_generator(self.update.after_modifies, self.update.all_modified_rows, cols, chunk_size)


class TableListener(ABC):
    @abstractmethod
    def on_update(self, update: TableUpdate) -> None:
        pass

    @abstractmethod
    def on_error(self, error: Exception):
        pass


class TableListenerHandle:
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


def listen(table: Table, listener: Union[Callable, TableListener]) -> TableListenerHandle:
    listenerToUse: TableListener
    if callable(listener):
        n_params = len(signature(listener).parameters)
        if n_params != 1:
            raise ValueError("Callabale listener function must have 1 (update) parameters.")
        listener_to_use = _CallableAsListener(listener)
    elif isinstance(listener, TableListener):
        listener_to_use = listener
    else:
        raise ValueError("listener is neither callable nor TableListener object")
    return TableListenerHandle(table, listener_to_use)


class _CallableAsListener(TableListener):
    _callable: Callable

    def __init__(self, qqq: Callable):
        self._callable = qqq

    def on_update(self, update: TableUpdate) -> None:
        self._callable(update)

    def on_error(self, error: str):
        print(f"Error happened: {error}")

from __future__ import annotations

from typing import List, Sequence, TypeVar, Union
import pydeephaven_ticking._core as dhc

T = TypeVar("T")


def _to_sequence(v: Union[T, Sequence[T]] = None) -> Sequence[T]:
    """This  enables a function to provide parameters that can accept both singular and plural values of the same type
    for the convenience of the users, e.g. both x= "abc" and x = ["abc"] are valid arguments.
    (adapted from table_listener.py)
    """
    if v is None:
        return ()
    if isinstance(v, Sequence) or isinstance(v, str):
        return (v,)
    return tuple(o for o in v)


def canonicalize_cols_param(table: dhc.ClientTable, col_names: Union[str, List[str]] = None) -> Sequence[str]:
    if col_names is None:
        return table.schema.names

    return _to_sequence(col_names)

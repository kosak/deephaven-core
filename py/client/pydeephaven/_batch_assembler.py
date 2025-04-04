#
# Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
#

from pydeephaven._table_ops import *


class BatchOpAssembler:
    def __init__(self, session, table_ops: List[TableOp]):
        self.session = session
        self.table_ops = table_ops
        self.grpc_table_ops = []
        self._curr_source = None

    @property
    def batch(self) -> List[Any]:
        return self.grpc_table_ops

    def build_batch(self) -> List[Any]:
        """Transforms the table ops into valid chained batch compatible ops."""
        self._curr_source = table_pb2.TableReference(ticket=self.table_ops[0].table.pb_ticket)

        for table_op in self.table_ops[1:-1]:
            result_id = None
            self.grpc_table_ops.append(
                table_op.make_grpc_request_for_batch(result_id=result_id, source_id=self._curr_source))
            self._curr_source = table_pb2.TableReference(batch_offset=len(self.grpc_table_ops) - 1)

        # the last op in the batch needs a result_id to reference the result
        export_ticket = self.session.make_export_ticket()
        self.grpc_table_ops.append(
            self.table_ops[-1].make_grpc_request_for_batch(result_id=export_ticket.pb_ticket, source_id=self._curr_source))

        return self.grpc_table_ops

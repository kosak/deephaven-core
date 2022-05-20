#include "deephaven/client/subscription/update_processor.h"

#include <iostream>
#include <memory>
#include "deephaven/client/column/column_source.h"
#include "deephaven/client/utility/utility.h"

using deephaven::client::column::MutableColumnSource;
using deephaven::client::utility::makeReservedVector;
using deephaven::client::utility::okOrThrow;

namespace deephaven::client::subscription {
namespace {
void processAddBatches(
    int64_t numAdds,
    arrow::flight::FlightStreamReader *fsr,
    arrow::flight::FlightStreamChunk *flightStreamChunk,
    TickingTable *table,
    std::vector<std::shared_ptr<MutableColumnSource>> *mutableColumns,
    const RowSequence &addedRows);

void processModBatches(int64_t numMods,
    arrow::flight::FlightStreamReader *fsr,
    arrow::flight::FlightStreamChunk *flightStreamChunk,
    TickingTable *table,
    std::vector<std::shared_ptr<MutableColumnSource>> *mutableColumns,
    const std::vector<std::shared_ptr<RowSequence>> &modIndexes);
}  // namespace

std::shared_ptr<UpdateProcessor> UpdateProcessor::startThread(
    std::unique_ptr<arrow::flight::FlightStreamReader> fsr,
    std::shared_ptr<ColumnDefinitions> colDefs,
    std::shared_ptr<TickingCallback> callback) {
  auto result = std::make_shared<UpdateProcessor>(std::move(fsr),
      std::move(colDefs), std::move(callback));
  std::thread t(&UpdateProcessor::runForever, result);
  t.detach();
  return result;
}

void UpdateProcessor::runForever(const std::shared_ptr<UpdateProcessor> &self) {
  std::cerr << "UpdateProcessor is starting.\n";
  std::exception_ptr eptr;
  try {
    self->runForeverHelper();
  } catch (...) {
    eptr = std::current_exception();
    self->callback_->onFailure(eptr);
  }
  std::cerr << "UpdateProcessor is exiting.\n";
}

void UpdateProcessor::runForeverHelper() {
  const auto &vec = colDefs_->vec();

  // This is our private concept of "TickingTable" which keeps track of a Deephaven key to index
  // mapping, and is able to perform operations like add, remove, shift, and so on. Also it is
  // able to make "snapshots" of itself. These snapshots are what we pass back to the caller via
  // the callback.
  auto mutableColumns = makeReservedVector<std::shared_ptr<MutableColumnSource>>(vec.size());
  for (const auto &entry : vec) {
    auto cs = makeColumnSource(*entry.second);
    mutableColumns.push_back(std::move(cs));
  }
  auto tickingTable = TickingTable::create(std::move(mutableColumns));

  // In this loop we process Arrow Flight messages until error or cancellation.
  arrow::flight::FlightStreamChunk flightStreamChunk;
  while (true) {
    okOrThrow(DEEPHAVEN_EXPR_MSG(fsr_->Next(&flightStreamChunk)));
    if (flightStreamChunk.app_metadata == nullptr) {
      std::cerr << "TODO(kosak) -- unexpected - chunk.app_metdata == nullptr\n";
      continue;
    }

    const auto *barrageWrapperRaw = flightStreamChunk.app_metadata->data();
    const auto *barrageWrapper = GetBarrageMessageWrapper(barrageWrapperRaw);
    if (barrageWrapper->magic() != deephavenMagicNumber) {
      continue;
    }
    if (barrageWrapper->msg_type() != BarrageMessageType::BarrageMessageType_BarrageUpdateMetadata) {
      continue;
    }
    const auto *bmdRaw = barrageWrapper->msg_payload()->data();
    const auto *bmd = flatbuffers::GetRoot<BarrageUpdateMetadata>(bmdRaw);
    auto numAdds = bmd->num_add_batches();
    auto numMods = bmd->num_mod_batches();
    streamf(std::cerr, "num_add_batches=%o, num_mod_batches=%o\n", numAdds, numMods);

    streamf(std::cerr, "FYI, my row sequence is currently %o\n", *tickingTable->getRowSequence());

    DataInput diAdded(*bmd->added_rows());
    DataInput diRemoved(*bmd->removed_rows());
    DataInput diThreeShiftIndexes(*bmd->shift_data());

    auto addedRows = readExternalCompressedDelta(&diAdded);
    auto removedRows = readExternalCompressedDelta(&diRemoved);
    auto shiftStartIndex = readExternalCompressedDelta(&diThreeShiftIndexes);
    auto shiftEndIndex = readExternalCompressedDelta(&diThreeShiftIndexes);
    auto shiftDestIndex = readExternalCompressedDelta(&diThreeShiftIndexes);

    streamf(std::cerr, "RemovedRows: {%o}\n", *removedRows);
    streamf(std::cerr, "AddedRows: {%o}\n", *addedRows);
    streamf(std::cerr, "shift start: {%o}\n", *shiftStartIndex);
    streamf(std::cerr, "shift end: {%o}\n", *shiftEndIndex);
    streamf(std::cerr, "shift dest: {%o}\n", *shiftDestIndex);


    // Correct order to process all this info is:
    // 1. removes
    // 2. shifts
    // 3. adds
    // 4. modifies

    // informational
    //    if (!addedRows->empty()) {
    //      streamf(std::cerr, "There was some new data: %o rows, %o columns:\n",
    //          flightStreamChunk.data->num_rows(), flightStreamChunk.data->num_columns());
    //      const auto &srcCols = flightStreamChunk.data->columns();
    //      for (size_t colNum = 0; colNum < srcCols.size(); ++colNum) {
    //        streamf(std::cerr, "Column %o\n", colNum);
    //        const auto &srcCol = srcCols[colNum];
    //        for (uint32_t ii = 0; ii < addedRows->size(); ++ii) {
    //          const auto &res = srcCol->GetScalar(ii);
    //          streamf(std::cerr, "%o: %o\n", ii, res.ValueOrDie()->ToString());
    //        }
    //      }
    //    }

    // 1. Removes
    auto beforeRemoves = tickingTable->snapshot();
    auto removedRowsIndexSpace = convertSpaceWhatever(*removedRows);
    // can clear removedRows here
    tickingTable->remove(*removedRowsIndexSpace);

    // 2. Shifts
    spaceMonster->applyShifts(*shiftStartIndex, *shiftEndIndex, *shiftDestIndex);

    if (numAdds != 0 && numMods != 0) {
      throw std::runtime_error("Message has both add batches and mod batches?! "
                               "kosak thinks this is not allowed");
    }

    auto beforeAddsOrModifies = tickingTable->snapshot();
    auto addedRowsIndexSpace = convertSpaceWhatever(*addedRows);
    // can clear addedRows here
    // 3. Adds
    if (numAdds != 0) {
      processAddBatches(numAdds, fsr_.get(), &flightStreamChunk, tickingTable.get(),
          &mutableColumns, *addedRows);
    }

    // 4. Modifies
    std::vector<std::shared_ptr<RowSequence>> perColumnModifiesIndexSpace;
    if (numMods != 0) {
      const auto &modColumnNodes = *bmd->mod_column_nodes();
      for (size_t i = 0; i < modColumnNodes.size(); ++i) {
        const auto &elt = modColumnNodes.Get(i);
        DataInput diModified(*elt->modified_rows());
        auto modRows = readExternalCompressedDelta(&diModified);
        auto modRowsIndexSpace = convertSpaceWhatever(*modRows);
        perColumnModifiesIndexSpace.push_back(std::move(modRowsIndexSpace));
      }
      processModBatches(numMods, fsr_.get(), &flightStreamChunk, tickingTable.get(),
          &mutableColumns, perColumnModifiesIndexSpace);
    }

    auto rowSequence = tickingTable->getRowSequence();
    streamf(std::cerr, "Now my index looks like this: [%o]\n", *rowSequence);

    auto current = tickingTable->snapshot();
    TickingUpdate tickingUpdate(std::move(beforeRemoves), std::move(beforeAddsOrModifies),
        std::move(current), std::move(removedRowsIndexSpace), std::move(perColumnModifiesIndexSpace),
        std::move(addedRowsIndexSpace));
    callback_->onTick(tickingUpdate);
  }
}

// Processes all of the adds in this add batch. Will invoke (numAdds - 1) additional calls to GetNext().
void processAddBatches(
    int64_t numAdds,
    arrow::flight::FlightStreamReader *fsr,
    arrow::flight::FlightStreamChunk *flightStreamChunk,
    TickingTable *table,
    std::vector<std::shared_ptr<MutableColumnSource>> *mutableColumns,
    const RowSequence &addedRows) {
  if (numAdds == 0) {
    return;
  }
  auto unwrappedTable = table->add(addedRows);
  auto allRowKeys = unwrappedTable->getUnorderedRowKeys();
  streamf(std::cout, "allRowKeys: %o\n", *allRowKeys);
  int64_t rowKeyBegin = 0;

  while (true) {
    const auto &srcCols = flightStreamChunk->data->columns();
    auto ncols = srcCols.size();
    if (ncols != mutableColumns->size()) {
      auto message = stringf("Received %o columns, but my table has %o columns", ncols,
          mutableColumns->size());
      throw std::runtime_error(message);
    }

    if (ncols == 0) {
      return;
    }

    auto numRows = srcCols[0]->length();
    auto sequentialRows = RowSequence::createSequential(0, numRows);
    auto rowKeys = allRowKeys->slice(rowKeyBegin, rowKeyBegin + numRows);
    rowKeyBegin += numRows;

    for (size_t i = 0; i < ncols; ++i) {
      const auto &srcColArrow = *srcCols[i];
      auto &destColDh = (*mutableColumns)[i];

      if (srcColArrow.length() != numRows) {
        auto message = stringf(
            "Inconsistent column lengths: Column 0 has %o rows, but column %o has %o rows",
            numRows, i, srcColArrow.length());
        throw std::runtime_error(message);
      }

      auto context = destColDh->createContext(numRows);
      auto chunk = ChunkMaker::createChunkFor(*destColDh, numRows);
      ChunkFiller::fillChunk(srcColArrow, *sequentialRows, chunk.get());
      destColDh->fillFromChunkUnordered(context.get(), *chunk, *rowKeys, numRows);
    }

    if (--numAdds == 0) {
      return;
    }
    okOrThrow(DEEPHAVEN_EXPR_MSG(fsr->Next(flightStreamChunk)));
  }
}

void processModBatches(int64_t numMods,
    arrow::flight::FlightStreamReader *fsr,
    arrow::flight::FlightStreamChunk *flightStreamChunk,
    TickingTable *table,
    std::vector<std::shared_ptr<MutableColumnSource>> *mutableColumns,
    const std::vector<std::shared_ptr<RowSequence>> &modIndexes) {
  if (numMods == 0) {
    return;
  }

  std::vector<std::shared_ptr<RowSequenceIterator>> iterators;
  for (const auto &mi : modIndexes) {
    iterators.push_back(mi->getRowSequenceIterator());
  }

  while (true) {
    // this is probably wrong. The modify probably only has a limited number of columns.
    const auto &srcCols = flightStreamChunk->data->columns();
    auto ncols = srcCols.size();
    if (ncols != mutableColumns->size()) {
      auto message = stringf("Received %o columns, but my table has %o columns", ncols,
          mutableColumns->size());
      throw std::runtime_error(message);
    }

    for (size_t i = 0; i < modIndexes.size(); ++i) {
      const auto &modIndex = *modIndexes[i];
      if (modIndex.empty()) {
        continue;
      }

      const auto &srcColArrow = *srcCols[i];
      auto numRows = srcColArrow.length();

      auto &destColDh = (*mutableColumns)[i];

      auto context = destColDh->createContext(numRows);
      auto chunk = ChunkMaker::createChunkFor(*destColDh, numRows);

      auto sequentialRows = RowSequence::createSequential(0, numRows);

      ChunkFiller::fillChunk(srcColArrow, *sequentialRows, chunk.get());
      auto destIndices = iterators[i]->getNextRowSequenceWithLength(numRows);
      destColDh->fillFromChunk(context.get(), *chunk, *destIndices);
    }

    if (--numMods == 0) {
      return;
    }
    okOrThrow(DEEPHAVEN_EXPR_MSG(fsr->Next(flightStreamChunk)));
  }
}
}  // namespace
}  // namespace deephaven::client::subscription

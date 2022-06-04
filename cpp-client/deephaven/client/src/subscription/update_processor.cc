#include "deephaven/client/subscription/update_processor.h"

#include <iostream>
#include <memory>
#include "deephaven/client/chunk/chunk_filler.h"
#include "deephaven/client/chunk/chunk_maker.h"
#include "deephaven/client/column/column_source.h"
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/immerutil/abstract_flex_vector.h"
#include "deephaven/client/immerutil/immer_column_source.h"
#include "deephaven/client/subscription/index_decoder.h"
#include "deephaven/client/subscription/classic_table_state.h"
#include "deephaven/client/subscription/immer_table_state.h"
#include "deephaven/client/utility/utility.h"
#include "deephaven/client/ticking.h"
#include "deephaven/flatbuf/Barrage_generated.h"

using deephaven::client::ClassicTickingUpdate;
using deephaven::client::chunk::ChunkFiller;
using deephaven::client::chunk::ChunkMaker;
using deephaven::client::column::MutableColumnSource;
using deephaven::client::container::RowSequence;
using deephaven::client::container::RowSequenceBuilder;
using deephaven::client::immerutil::AbstractFlexVectorBase;
using deephaven::client::utility::ColumnDefinitions;
using deephaven::client::utility::makeReservedVector;
using deephaven::client::utility::okOrThrow;
using deephaven::client::utility::streamf;
using deephaven::client::utility::stringf;

using io::deephaven::barrage::flatbuf::BarrageMessageType;
using io::deephaven::barrage::flatbuf::BarrageMessageWrapper;
using io::deephaven::barrage::flatbuf::BarrageModColumnMetadata;
using io::deephaven::barrage::flatbuf::BarrageUpdateMetadata;
using io::deephaven::barrage::flatbuf::ColumnConversionMode;
using io::deephaven::barrage::flatbuf::CreateBarrageMessageWrapper;
using io::deephaven::barrage::flatbuf::CreateBarrageSubscriptionOptions;
using io::deephaven::barrage::flatbuf::CreateBarrageSubscriptionRequest;

namespace deephaven::client::subscription {
namespace {
std::vector<std::unique_ptr<AbstractFlexVectorBase>> makeEmptyFlexVectors(
    const ColumnDefinitions &colDefs);

std::vector<std::unique_ptr<AbstractFlexVectorBase>> parseBatches(
    const ColumnDefinitions &colDefs,
    size_t numBatches,
    bool allowInconsistentColumnSizes,
    arrow::flight::FlightStreamReader *fsr,
    arrow::flight::FlightStreamChunk *flightStreamChunk);

struct ExtractedMetadata {
  ExtractedMetadata(size_t numAdds,
      size_t numMods,
      std::shared_ptr<RowSequence> removedRows,
      std::shared_ptr<RowSequence> shiftStartIndex,
      std::shared_ptr<RowSequence> shiftEndIndex,
      std::shared_ptr<RowSequence> shiftDestIndex,
      std::shared_ptr<RowSequence> addedRows,
      std::vector<std::shared_ptr<RowSequence>> perColumnModifiedRows);
  ~ExtractedMetadata();

  size_t numAdds_ = 0;
  size_t numMods_ = 0;
  std::shared_ptr<RowSequence> addedRows_;
  std::shared_ptr<RowSequence> removedRows_;
  std::vector<std::shared_ptr<RowSequence>> perColumnModifiedRows_;
  std::shared_ptr<RowSequence> shiftStartIndex_;
  std::shared_ptr<RowSequence> shiftEndIndex_;
  std::shared_ptr<RowSequence> shiftDestIndex_;

};

ExtractedMetadata extractMetadata(const int8_t *raw);
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

UpdateProcessor::UpdateProcessor(std::unique_ptr<arrow::flight::FlightStreamReader> fsr,
    std::shared_ptr<ColumnDefinitions> colDefs, std::shared_ptr<TickingCallback> callback) :
    fsr_(std::move(fsr)), colDefs_(std::move(colDefs)), callback_(std::move(callback)) {}

UpdateProcessor::~UpdateProcessor() = default;

void UpdateProcessor::cancel() {
  fsr_->Cancel();
}

void UpdateProcessor::classicRunForever(const std::shared_ptr<UpdateProcessor> &self) {
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

void UpdateProcessor::classicRunForeverHelper() {
  ClassicTableState state(*colDefs_);

  // In this loop we process Arrow Flight messages until error or cancellation.
  arrow::flight::FlightStreamChunk flightStreamChunk;
  while (true) {
    okOrThrow(DEEPHAVEN_EXPR_MSG(fsr_->Next(&flightStreamChunk)));

    // Parse all the metadata out of the Barrage message before we advance the cursor past it.
    auto md = extractMetadata(flightStreamChunk);
    if (md.whateverSad()) {
      continue;
    }

    // Correct order to process all this info is:
    // 1. removes
    // 2. shifts
    // 3. adds
    // 4. modifies

    // 1. Removes
    auto removedRowsIndexSpace = state.erase(*md.removedRows_);

    // 2. Shifts
    state.applyShifts(*md.shiftStartIndex_, *md.shiftEndIndex_, *md.shiftDestIndex_);

    // 3. Adds
    auto addedRowsIndexSpace = RowSequence::createEmpty();
    if (md.numAdds_ != 0) {
      auto addedRowData = parseBatches(*colDefs_, md.numAdds_, false, fsr_.get(), &flightStreamChunk);
      addedRowsIndexSpace = state.add(std::move(addedRowData), std::move(md.addedRows_));

      if (md.numMods_ != 0) {
        // Currently the FlightStreamReader is pointing to the last add record. We need to advance
        // it so it points to the first mod record.
        okOrThrow(DEEPHAVEN_EXPR_MSG(fsr_->Next(&flightStreamChunk)));
      }
    }

    auto beforeModifies = state.snapshot();

    // 4. Modifies
    std::vector<std::shared_ptr<RowSequence>> perColumnModifiesIndexSpace;
    if (md.numMods_ != 0) {
      std::vector<std::shared_ptr<RowSequence>> perColumnModifies;
      auto modifiedRowData = parseBatches(*colDefs_, md.numMods_, true, fsr_.get(), &flightStreamChunk);
      perColumnModifiesIndexSpace = state.modify(std::move(modifiedRowData), perColumnModifies);
    }

    auto current = state.snapshot();
    ClassicTickingUpdate update(std::move(beforeRemoves), std::move(beforeModifies),
        std::move(current), std::move(removedRowsIndexSpace), std::move(perColumnModifiesIndexSpace),
        std::move(addedRowsIndexSpace));
    callback_->onTick(update);
  }
}

void UpdateProcessor::immerRunForeverHelper() {
  ImmerTableState state(*colDefs_);

  // In this loop we process Arrow Flight messages until error or cancellation.
  arrow::flight::FlightStreamChunk flightStreamChunk;
  while (true) {
    okOrThrow(DEEPHAVEN_EXPR_MSG(fsr_->Next(&flightStreamChunk)));
    if (flightStreamChunk.app_metadata == nullptr) {
      std::cerr << "TODO(kosak) -- unexpected - chunk.app_metdata == nullptr\n";
      continue;
    }

    // Parse all the metadata out of the Barrage message before we advance the cursor past it.
    const auto *barrageWrapperRaw = flightStreamChunk.app_metadata->data();
    auto md = extractMetadata(barrageWrapperRaw);

    // Correct order to process all this info is:
    // 1. removes
    // 2. shifts
    // 3. adds
    // 4. modifies

    // 1. Removes
    auto beforeRemoves = state.snapshot();
    auto removedRowsIndexSpace = state.erase(std::move(md.removedRows_));

    // 2. Shifts
    state.applyShifts(*md.shiftStartIndex_, *md.shiftEndIndex_, *md.shiftDestIndex_);

    // 3. Adds
    auto addedRowsIndexSpace = RowSequence::createEmpty();
    if (md.numAdds_ != 0) {
      auto addedRowData = parseBatches(*colDefs_, md.numAdds_, false, fsr_.get(), &flightStreamChunk);
      addedRowsIndexSpace = state.add(std::move(addedRowData), std::move(md.addedRows_));

      if (md.numMods_ != 0) {
        // Currently the FlightStreamReader is pointing to the last add record. We need to advance
        // it so it points to the first mod record.
        okOrThrow(DEEPHAVEN_EXPR_MSG(fsr_->Next(&flightStreamChunk)));
      }
    }

    auto beforeModifies = state.snapshot();

    // 4. Modifies
    std::vector<std::shared_ptr<RowSequence>> perColumnModifiesIndexSpace;
    if (md.numMods_ != 0) {
      std::vector<std::shared_ptr<RowSequence>> perColumnModifies;
      auto modifiedRowData = parseBatches(*colDefs_, md.numMods_, true, fsr_.get(), &flightStreamChunk);
      perColumnModifiesIndexSpace = state.modify(std::move(modifiedRowData), perColumnModifies);
    }

    auto current = state.snapshot();
    ImmerTickingUpdate update(std::move(beforeRemoves), std::move(beforeModifies),
        std::move(current), std::move(removedRowsIndexSpace), std::move(perColumnModifiesIndexSpace),
        std::move(addedRowsIndexSpace));
    callback_->onTick(update);
  }
}

namespace {
std::optional<ExtractedMetadata> extractMetadata(const int8_t *barrageWrapperRaw) {
  if (flightStreamChunk.app_metadata == nullptr) {
    std::cerr << "TODO(kosak) -- unexpected - chunk.app_metdata == nullptr\n";
    continue;
  }

  const auto *barrageWrapper = flatbuffers::GetRoot<BarrageMessageWrapper>(barrageWrapperRaw);
  if (barrageWrapper->magic() != deephavenMagicNumber) {
    return {};
  }
  if (barrageWrapper->msg_type() !=
      BarrageMessageType::BarrageMessageType_BarrageUpdateMetadata) {
    return {};
  }

  const auto *bmdRaw = barrageWrapper->msg_payload()->data();
  const auto *bmd = flatbuffers::GetRoot<BarrageUpdateMetadata>(bmdRaw);
  auto numAdds = bmd->num_add_batches();
  auto numMods = bmd->num_mod_batches();
  // streamf(std::cerr, "num_add_batches=%o, num_mod_batches=%o\n", numAdds, numMods);
  // streamf(std::cerr, "FYI, my row sequence is currently %o\n", *tickingTable->getRowSequence());

  DataInput diAdded(*bmd->added_rows());
  DataInput diRemoved(*bmd->removed_rows());
  DataInput diThreeShiftIndexes(*bmd->shift_data());

  auto addedRows = IndexDecoder::readExternalCompressedDelta(&diAdded);
  auto removedRows = IndexDecoder::readExternalCompressedDelta(&diRemoved);
  auto shiftStartIndex = IndexDecoder::readExternalCompressedDelta(&diThreeShiftIndexes);
  auto shiftEndIndex = IndexDecoder::readExternalCompressedDelta(&diThreeShiftIndexes);
  auto shiftDestIndex = IndexDecoder::readExternalCompressedDelta(&diThreeShiftIndexes);

  std::vector<std::shared_ptr<RowSequence>> perColumnModifies;
  if (numMods != 0) {
    const auto &modColumnNodes = *bmd->mod_column_nodes();
    perColumnModifies.reserve(modColumnNodes.size());
    for (size_t i = 0; i < modColumnNodes.size(); ++i) {
      const auto &elt = modColumnNodes.Get(i);
      DataInput diModified(*elt->modified_rows());
      auto modRows = IndexDecoder::readExternalCompressedDelta(&diModified);
      perColumnModifies.push_back(std::move(modRows));
    }
  }
  streamf(std::cerr, "RemovedRows: {%o}\n", *removedRows);
  streamf(std::cerr, "AddedRows: {%o}\n", *addedRows);
  streamf(std::cerr, "shift start: {%o}\n", *shiftStartIndex);
  streamf(std::cerr, "shift end: {%o}\n", *shiftEndIndex);
  streamf(std::cerr, "shift dest: {%o}\n", *shiftDestIndex);

  return ExtractedMetadata(numAdds,
      numMods,
      std::move(removedRows),
      std::move(shiftStartIndex),
      std::move(shiftEndIndex),
      std::move(shiftDestIndex),
      std::move(addedRows),
      std::move(perColumnModifies));
}

// Processes all of the adds in this add batch. Will invoke (numAdds - 1) additional calls to GetNext().
std::vector<std::unique_ptr<AbstractFlexVectorBase>> parseBatches(
    const ColumnDefinitions &colDefs,
    int64_t numBatches,
    bool allowInconsistentColumnSizes,
    arrow::flight::FlightStreamReader *fsr,
    arrow::flight::FlightStreamChunk *flightStreamChunk) {
  auto result = makeEmptyFlexVectors(colDefs);
  if (numBatches == 0) {
    return result;
  }

  while (true) {
    const auto &srcCols = flightStreamChunk->data->columns();
    auto ncols = srcCols.size();
    if (ncols != result.size()) {
      auto message = stringf("Received %o columns, but my table has %o columns", ncols,
          result.size());
      throw std::runtime_error(message);
    }

    if (ncols == 0) {
      return result;
    }

    if (!allowInconsistentColumnSizes) {
      auto numRows = srcCols[0]->length();
      for (size_t i = 1; i < ncols; ++i) {
        const auto &srcColArrow = *srcCols[i];
        // I think you do not want this check for the modify case. When you are parsing modify
        // messages, the columns may indeed be of different sizes.
        if (srcColArrow.length() != numRows) {
          auto message = stringf(
              "Inconsistent column lengths: Column 0 has %o rows, but column %o has %o rows",
              numRows, i, srcColArrow.length());
          throw std::runtime_error(message);
        }
      }
    }

    for (size_t i = 0; i < ncols; ++i) {
      const auto &srcColArrow = *srcCols[i];
      result[i]->inPlaceAppendArrow(srcColArrow);
    }

    if (--numBatches == 0) {
      return result;
    }
    okOrThrow(DEEPHAVEN_EXPR_MSG(fsr->Next(flightStreamChunk)));
  }
}

//ThreadNubbin::ThreadNubbin(std::unique_ptr<arrow::flight::FlightStreamReader> fsr,
//    std::shared_ptr<ColumnDefinitions> colDefs, std::shared_ptr<TickingCallback> callback) :
//    fsr_(std::move(fsr)), colDefs_(std::move(colDefs)), callback_(std::move(callback)) {}

struct MyVisitor final : public arrow::TypeVisitor {
  arrow::Status Visit(const arrow::Int32Type &type) final {
    result_ = AbstractFlexVectorBase::create(immer::flex_vector<int32_t>());
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::Int64Type &type) final {
    result_ = AbstractFlexVectorBase::create(immer::flex_vector<int64_t>());
    return arrow::Status::OK();
  }

  arrow::Status Visit(const arrow::DoubleType &type) final {
    result_ = AbstractFlexVectorBase::create(immer::flex_vector<double>());
    return arrow::Status::OK();
  }

  std::unique_ptr<AbstractFlexVectorBase> result_;
};

std::vector<std::unique_ptr<AbstractFlexVectorBase>> makeEmptyFlexVectors(
    const ColumnDefinitions &colDefs) {
  const auto &vec = colDefs.vec();
  auto result = makeReservedVector<std::unique_ptr<AbstractFlexVectorBase>>(vec.size());
  for (const auto &[name, dataType] : vec) {
    MyVisitor v;
    okOrThrow(DEEPHAVEN_EXPR_MSG(dataType->Accept(&v)));
    result.push_back(std::move(v.result_));
  }
  return result;
}
}  // namespace
}  // namespace deephaven::client::subscription

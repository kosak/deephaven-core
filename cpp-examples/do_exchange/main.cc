/*
 * Copyright (c) 2016-2020 Deephaven Data Labs and Patent Pending
 */
#include <iostream>
#include <set>
#include <thread>

#include "deephaven/client/client.h"
#include "deephaven/client/ticking.h"
#include "deephaven/client/chunk/chunk_maker.h"
#include "deephaven/client/chunk/chunk.h"
#include "deephaven/client/container/context.h"
#include "deephaven/client/container/row_sequence.h"
#include "deephaven/client/table/table.h"
#include "deephaven/client/utility/table_maker.h"
#include "deephaven/client/utility/utility.h"
#include "immer/algorithm.hpp"

using deephaven::client::Client;
using deephaven::client::NumCol;
using deephaven::client::SortPair;
using deephaven::client::TableHandle;
using deephaven::client::TableHandleManager;
using deephaven::client::TickingCallback;
using deephaven::client::ClassicTickingUpdate;
using deephaven::client::ImmerTickingUpdate;
using deephaven::client::chunk::ChunkMaker;
using deephaven::client::chunk::AnyChunk;
using deephaven::client::chunk::ChunkVisitor;
using deephaven::client::container::Context;
using deephaven::client::container::RowSequence;
using deephaven::client::container::RowSequenceBuilder;
using deephaven::client::chunk::DoubleChunk;
using deephaven::client::chunk::Int32Chunk;
using deephaven::client::chunk::Int64Chunk;
using deephaven::client::chunk::UInt64Chunk;
using deephaven::client::table::Table;
using deephaven::client::utility::makeReservedVector;
using deephaven::client::utility::okOrThrow;
using deephaven::client::utility::separatedList;
using deephaven::client::utility::streamf;
using deephaven::client::utility::stringf;
using deephaven::client::utility::TableMaker;
using deephaven::client::utility::valueOrThrow;

using std::size_t;

namespace {
void doit(const TableHandleManager &manager);
void makeModifiesHappen(const TableHandleManager &manager);
void millionRows(const TableHandleManager &manager);
void lastBy(const TableHandleManager &manager);
void demo(const TableHandleManager &manager);
}  // namespace

int main() {
  const char *server = "localhost:10000";
  try {
    auto client = Client::connect(server);
    auto manager = client.getManager();
    demo(manager);
  } catch (const std::exception &e) {
    std::cerr << "Caught exception: " << e.what() << '\n';
  }
}

// TODO(kosaK): we should standardize on either deephaven or io::deephaven

namespace {
class Callback final : public TickingCallback {
public:
  void onFailure(std::exception_ptr ep) final;
  void onTick(const ClassicTickingUpdate &update) final;
  void onTick(const ImmerTickingUpdate &update) final;

  bool failed() const { return failed_; }

private:
  std::atomic<bool> failed_ = false;
};

class DemoCallback final : public TickingCallback {
public:
  void onFailure(std::exception_ptr ep) final;
  void onTick(const ClassicTickingUpdate &update) final;
  void onTick(const ImmerTickingUpdate &update) final;

private:
  std::vector<int64_t> latestValues_;
};

// or maybe make a stream manipulator
std::string getWhat(std::exception_ptr ep);

void Callback::onFailure(std::exception_ptr ep) {
  streamf(std::cerr, "Callback reported failure: %o\n", getWhat(std::move(ep)));
  failed_ = true;
}

class ElementStreamer final {
public:
  ElementStreamer(std::ostream &s, size_t index) : s_(s), index_(index) {}

  template<typename T>
  void operator()(const T &chunk) const {
    s_ << chunk.data()[index_];
  }

private:
  std::ostream &s_;
  size_t index_ = 0;
};

void dumpTable(std::string_view what, const Table &table, const std::vector<size_t> &whichCols,
    std::shared_ptr<RowSequence> rows);

void Callback::onTick(const ClassicTickingUpdate &update) {
  streamf(std::cout, "adds: %o\n", *update.addedRowsKeySpace());
  auto render = [](std::ostream &s, const std::shared_ptr<RowSequence> &rs) {
    s << *rs;
  };
  streamf(std::cout, "modifies: %o\n", separatedList(update.modifiedRowsKeySpace().begin(),
      update.modifiedRowsKeySpace().end(), " === ", render));
}

void Callback::onTick(const ImmerTickingUpdate &update) {
  auto ncols = update.current()->numColumns();
  auto allCols = makeReservedVector<size_t>(update.current()->numColumns());
  for (size_t i = 0; i < ncols; ++i) {
    allCols.push_back(i);
  }
  dumpTable("removed", *update.beforeRemoves(), allCols, update.removed());
  for (size_t i = 0; i < update.modified().size(); ++i) {
    std::vector<size_t> oneCol{i};
    auto prevText = stringf("Col%o-prev", i);
    auto currText = stringf("Col%o-curr", i);
    dumpTable(prevText, *update.beforeModifies(), allCols, update.modified()[i]);
    dumpTable(currText, *update.current(), allCols, update.modified()[i]);
  }
  dumpTable("added", *update.current(), allCols, update.added());
}

void dumpTable(std::string_view what, const Table &table, const std::vector<size_t> &whichCols,
    std::shared_ptr<RowSequence> rows) {
  if (rows->empty()) {
    return;
  }
  streamf(std::cout, "===== THIS IS %o =====\n", what);
  // Deliberately chosen to be small so I can test chunking.
  const size_t chunkSize = 16;

  auto ncols = whichCols.size();
  auto contexts = makeReservedVector<std::shared_ptr<Context>>(ncols);
  auto chunks = makeReservedVector<AnyChunk>(ncols);
  for (auto col : whichCols) {
    const auto &c = table.getColumn(col);
    auto context = c->createContext(chunkSize);
    auto chunk = ChunkMaker::createChunkFor(*c, chunkSize);
    chunks.push_back(std::move(chunk));
    contexts.push_back(std::move(context));
  }

  while (true) {
    auto chunkOfRows = rows->take(chunkSize);
    rows = rows->drop(chunkSize);
    auto thisSize = chunkOfRows->size();
    if (thisSize == 0) {
      break;
    }

    for (size_t i = 0; i < ncols; ++i) {
      const auto &c = table.getColumn(whichCols[i]);
      const auto &context = contexts[i];
      auto &chunk = chunks[i];
      c->fillChunk(context.get(), *chunkOfRows, &chunk);
    }

    for (size_t j = 0; j < thisSize; ++j) {
      ElementStreamer es(std::cout, j);
      auto chunkAcceptor = [&es](std::ostream &s, const AnyChunk &chunk) {
        chunk.visit(es);
      };
      std::cout << separatedList(chunks.begin(), chunks.end(), ", ", chunkAcceptor) << '\n';
    }
  }
}

void doit(const TableHandleManager &manager) {
  auto start = std::chrono::duration_cast<std::chrono::nanoseconds>(
      std::chrono::system_clock::now().time_since_epoch()).count();
  auto tt1 = manager
      .timeTable(start, 1 * 1'000'000'000L)
//      .select("Foo = (ii % 20)", "Bar = ii")
//      .select("Foo = ii < 3 ? ii * 10 : (i == 3 ? 15 : (i == 4 ? 12 : 3))"
//      // .select("Foo = ii < 10 ? ii * 10 : 23")
      .select("Foo = ((ii * 0xdeadbeef) + 0xbaddbabe) % 5000", "Bar = 34.2 + ii", "II = ii", "SomeInt = (int) Bar")
      .sort({SortPair::ascending("Foo", false)})
      .head(10)
      //.tail(3)  // this will give us deletes
      ;
  //auto tt2 = manager.timeTable(start, 2 * 1'000'000'000L).select("TS2 = Timestamp");
  //auto t = tt1.crossJoin(tt2, std::vector<std::string>(), {});


  // tt1.bindToVariable("showme");

  //      // .select("Foo = (ii % 20)")
  //      // .select("Foo = ii < 3 ? ii * 10 : (i == 3 ? 15 : (i == 4 ? 12 : 3))")
  //      // .select("Foo = ii < 10 ? ii * 10 : 23")
  //      .select("Foo = ((ii * 0xdeadbeef) + 0xbaddbabe) % 5000")
  //      .sort({SortPair::ascending("Foo", false)})
  //      .head(10);

  auto myCallback = std::make_shared<Callback>();
  auto subscriptionHandle = tt1.subscribe(myCallback, true);
  uint32_t tens_of_seconds_to_run = 50000;
  while (tens_of_seconds_to_run-- > 0) {
      std::this_thread::sleep_for(std::chrono::milliseconds (100));
      if (myCallback->failed()) {
          std::cerr << "callback reported failure, aborting in main subscription thread.\n";
          break;
      }
  }
  std::cerr << "I unsubscribed here.\n";
  tt1.unsubscribe(std::move(subscriptionHandle));
  std::this_thread::sleep_for(std::chrono::seconds(5));
  std::cerr << "exiting.\n";
}

// let's keep this off to the side
//  auto tLeft = manager
//      .emptyTable(10)
//      .select("II = ii", "Zamboni = `zamboni`+ii");
//  auto tRight = manager
//        .timeTable(start, 1 * 1'000'000'000L)
//        .update("II = 0L")
//        .tail(1);
//  auto tt1 = tLeft.naturalJoin(tRight, {"II"}, {});


// also this
//  auto tt1 = manager
//      .timeTable(start, 1 * 1'000'000'000L)
//      // .select("Foo = (ii % 20)")
//      // .select("Foo = ii < 3 ? ii * 10 : (i == 3 ? 15 : (i == 4 ? 12 : 3))")
//      // .select("Foo = ii < 10 ? ii * 10 : 23")
//      .select("Foo = ((ii * 0xdeadbeef) + 0xbaddbabe) % 5000")
//      .sort({SortPair::ascending("Foo", false)})
//      .head(10);
//.tail(3);  // this will give us deletes
//auto tt2 = manager.timeTable(start, 2 * 1'000'000'000L).select("TS2 = Timestamp");
//auto t = tt1.crossJoin(tt2, std::vector<std::string>(), {});


// tt1.bindToVariable("showme");

//      // .select("Foo = (ii % 20)")
//      // .select("Foo = ii < 3 ? ii * 10 : (i == 3 ? 15 : (i == 4 ? 12 : 3))")
//      // .select("Foo = ii < 10 ? ii * 10 : 23")
//      .select("Foo = ((ii * 0xdeadbeef) + 0xbaddbabe) % 5000")
//      .sort({SortPair::ascending("Foo", false)})
//      .head(10);


void makeModifiesHappen(const TableHandleManager &manager) {
  auto start = std::chrono::duration_cast<std::chrono::nanoseconds>(
      std::chrono::system_clock::now().time_since_epoch()).count();

  auto tLeft = manager
      .emptyTable(10)
      .select("II = ii", "Zamboni = ii");
  auto tRight = manager
      .timeTable(start, 1 * 1'000'000'000L)
      .select("II = 0L", "TS = (long)(Timestamp.getNanos()/1000000000)")
      .tail(1);
  auto tt1 = tLeft.naturalJoin(tRight, {"II"}, {}).select("II", "Zamboni", "TS");

  tt1.bindToVariable("showme");

  auto myCallback = std::make_shared<Callback>();
  auto handle = tt1.subscribe(myCallback, true);
  std::this_thread::sleep_for(std::chrono::seconds(5'000));
  std::cerr << "I unsubscribed here\n";
  tt1.unsubscribe(std::move(handle));
  std::this_thread::sleep_for(std::chrono::seconds(5));
  std::cerr << "exiting\n";
}

void millionRows(const TableHandleManager &manager) {
  auto start = std::chrono::duration_cast<std::chrono::nanoseconds>(
      std::chrono::system_clock::now().time_since_epoch()).count();

//   const size_t topAndBottomSize = 500'000;
  const size_t topAndBottomSize = 10;
  auto tTop = manager.emptyTable(topAndBottomSize).select("Value = ii");
  auto tBottom = manager.emptyTable(topAndBottomSize).select("Value = 10_000_000 + ii");
  auto pulsatingMiddle =  manager.timeTable(start, 1 * 1'000'000'000L)
      .tail(10)
      .select("Selector = ((int)(Timestamp.getNanos() / 1_000_000_000)) % 20")
      .where("Selector < 10")
      .select("Value = 1_000_000L + Selector");

  auto table = tTop.merge({pulsatingMiddle, tBottom});

  table.bindToVariable("showme");

  auto myCallback = std::make_shared<Callback>();
  auto handle = table.subscribe(myCallback, true);
  std::this_thread::sleep_for(std::chrono::seconds(5'000));
  std::cerr << "I unsubscribed here\n";
  table.unsubscribe(handle);
  std::this_thread::sleep_for(std::chrono::seconds(5));
  std::cerr << "exiting\n";
}

void demo(const TableHandleManager &manager) {
  auto start = std::chrono::duration_cast<std::chrono::nanoseconds>(
      std::chrono::system_clock::now().time_since_epoch()).count();

  const long modSize = 1000;
  auto table = manager.timeTable(start, 1 * 1'000'000L)
      .view("II = ii")
      .where("II < 10_000")
      .view("Temp1 = (II ^ (long)(II / 65536)) * 0x8febca6b",
          "Temp2 = (Temp1 ^ ((long)(Temp1 / 8192))) * 0xc2b2ae35",
          "HashValue = Temp2 ^ (long)(Temp2 / 65536)",
          "II");

  // might as well use this interface once in a while
  auto [hv, ii] = table.getCols<NumCol, NumCol>("HashValue", "II");
  auto t2 = table.view((hv % modSize).as("Key"), "Value = II");
  auto key = t2.getNumCol("Key");
  auto lb = t2.lastBy(key).sort({key.ascending()});

  lb.bindToVariable("showme");

  auto myCallback = std::make_shared<Callback>();
  auto handle = lb.subscribe(myCallback, true);
  std::this_thread::sleep_for(std::chrono::seconds(5'000));
  std::cerr << "I unsubscribed here\n";
  lb.unsubscribe(handle);
  std::this_thread::sleep_for(std::chrono::seconds(5));
  std::cerr << "exiting\n";
}


void lastBy(const TableHandleManager &manager) {
  auto start = std::chrono::duration_cast<std::chrono::nanoseconds>(
      std::chrono::system_clock::now().time_since_epoch()).count();

  const long modSize = 10;
  auto table = manager.timeTable(start, 1 * 10'000'000L)
      .select("II = ii")
      .where("II < 2000")
      .select("Temp1 = (II ^ (long)(II / 65536)) * 0x8febca6b",
          "Temp2 = (Temp1 ^ ((long)(Temp1 / 8192))) * 0xc2b2ae35",
          "HashValue = Temp2 ^ (long)(Temp2 / 65536)",
          "II");

  // might as well use this interface once in a while
  auto [hv, ii] = table.getCols<NumCol, NumCol>("HashValue", "II");
  auto t2 = table.select((hv % modSize).as("Key"), "Value = II");
  auto key = t2.getNumCol("Key");
  auto lb = t2.lastBy(key).sort({key.ascending()});

  lb.bindToVariable("showme");

  auto myCallback = std::make_shared<Callback>();
  auto handle = lb.subscribe(myCallback, true);
  std::this_thread::sleep_for(std::chrono::seconds(5'000));
  std::cerr << "I unsubscribed here\n";
  lb.unsubscribe(handle);
  std::this_thread::sleep_for(std::chrono::seconds(5));
  std::cerr << "exiting\n";
}

void DemoCallback::onTick(const ClassicTickingUpdate &update) {
  processClassicAdds(*currentTableIndexSpace, update.addedRowsIndexSpace());
  processClassicModifies(*currentTableIndexSpace, update.modifiedRowsIndexSpace());
  if (zamboniTime - lastTime > 1 sec) {
    lastTime = zamboniTime;
    showUpdate();
  }
}

void DemoCallback::processClassicAdds() {

}


void DemoCallback::onTick(const ImmerTickingUpdate &update) {
  const auto &adds = update.added();
  const auto &mods = update.modified();
  adds->forEachChunk(processAdd);
  for (size_t i = 0; i < update.modified().size(); ++i) {
    mods[i]->forEachChunk(processModified);
  }
  if (zamboniTime - lastTime > 1 sec) {
    lastTime = zamboniTime;
    showUpdate();
  }
}

std::string getWhat(std::exception_ptr ep) {
  try {
    std::rethrow_exception(std::move(ep));
  } catch (const std::exception &e) {
    return e.what();
  } catch (...) {
    return "(unknown exception)";
  }
}
}  // namespace

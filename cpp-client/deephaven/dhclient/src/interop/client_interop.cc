/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/client/interop/client_interop.h"

#include <codecvt>
#include <locale>
#include <arrow/table.h>
#include "deephaven/client/client.h"
#include "deephaven/client/client_options.h"
#include "deephaven/client/subscription/subscription_handle.h"
#include "deephaven/client/utility/arrow_util.h"
#include "deephaven/client/utility/table_maker.h"
#include "deephaven/dhcore/interop/interop_util.h"
#include "deephaven/dhcore/utility/utility.h"
#include "deephaven/third_party/fmt/format.h"

using deephaven::client::Aggregate;
using deephaven::client::AggregateCombo;
using deephaven::client::Client;
using deephaven::client::ClientOptions;
using deephaven::client::TableHandle;
using deephaven::client::TableHandleManager;
using deephaven::client::subscription::SubscriptionHandle;
using deephaven::client::interop::ArrowTable;
using deephaven::client::utility::ArrowUtil;
using deephaven::client::interop::ClientTableSpWrapper;
using deephaven::client::utility::DurationSpecifier;
using deephaven::client::utility::TableMaker;
using deephaven::client::utility::TimePointSpecifier;
using deephaven::dhcore::DateTime;
using deephaven::dhcore::chunk::BooleanChunk;
using deephaven::dhcore::chunk::CharChunk;
using deephaven::dhcore::chunk::Chunk;
using deephaven::dhcore::chunk::DateTimeChunk;
using deephaven::dhcore::chunk::DoubleChunk;
using deephaven::dhcore::chunk::FloatChunk;
using deephaven::dhcore::chunk::Int8Chunk;
using deephaven::dhcore::chunk::Int16Chunk;
using deephaven::dhcore::chunk::Int32Chunk;
using deephaven::dhcore::chunk::Int64Chunk;
using deephaven::dhcore::chunk::StringChunk;
using deephaven::dhcore::interop::ErrorStatus;
using deephaven::dhcore::interop::ErrorStatusNew;
using deephaven::dhcore::interop::InteropBool;
using deephaven::dhcore::interop::NativePtr;
using deephaven::dhcore::interop::PlatformUtf16;
using deephaven::dhcore::interop::StringHandle;
using deephaven::dhcore::interop::StringPoolBuilder;
using deephaven::dhcore::interop::StringPoolHandle;
using deephaven::dhcore::ticking::TickingCallback;
using deephaven::dhcore::ticking::TickingUpdate;
using deephaven::dhcore::interop::Utf16Converter;
using deephaven::dhcore::utility::GetWhat;
using deephaven::dhcore::utility::MakeReservedVector;

namespace {
std::vector<std::string> MakeStringVec(const char16_t **key_columns, int64_t num_key_columns) {
  auto result = MakeReservedVector<std::string>(num_key_columns);
  Utf16Converter converter;
  for (int64_t i = 0; i < num_key_columns; ++i) {
    result.emplace_back(converter.to_bytes(key_columns[i]));
  }
  return result;
}

void GetColumnHelper(ClientTableSpWrapper *self,
    int32_t column_index,
    Chunk *data_chunk, InteropBool *optional_dest_null_flags, int64_t num_rows) {
  auto cs = self->table_->GetColumn(column_index);
  std::unique_ptr<bool[]> null_data;
  BooleanChunk null_chunk;
  BooleanChunk *null_chunkp = nullptr;
  if (optional_dest_null_flags != nullptr) {
    null_data.reset(new bool[num_rows]);
    null_chunk = BooleanChunk::CreateView(null_data.get(), num_rows);
    null_chunkp = &null_chunk;
  }

  auto rows = self->table_->GetRowSequence();
  cs->FillChunk(*rows, data_chunk, null_chunkp);

  if (optional_dest_null_flags != nullptr) {
    for (int64_t i = 0; i != num_rows; ++i) {
      optional_dest_null_flags[i] = InteropBool(null_data[i]);
    }
  }
}
}  // namespace

extern "C" {
// Takes a UTF-16 platform string
void invokelab_r0(const char16_t *s) {
  Utf16Converter converter;
  fmt::println(stderr, "r0 received {}", converter.to_bytes(s));
}

// Returns a UTF-16 platform string
const PlatformUtf16 *invokelab_r1() {
  const char *message = "tpnn(🎔) - U+1F394";
  fmt::println(stderr, "r1 returning {}", message);

  Utf16Converter converter;
  auto wstring = converter.from_bytes(message);
  return PlatformUtf16::Create(wstring);
}

// Returns the string passed into it
const PlatformUtf16 *invokelab_r2(const char16_t *s) {
  {
    // Wouldn't need the Utf16Converter except we want to print a debug message
    Utf16Converter converter;
    fmt::println(stderr, "r2 got {} and returning it", converter.to_bytes(s));
  }
  return PlatformUtf16::Create(s);
}

// Gets a UTF-16 platform strings from in array
void invokelab_r3(const char16_t **data_in, int32_t count) {
  Utf16Converter converter;
  for (int i = 0; i != count; ++i) {
    auto s = converter.to_bytes(data_in[i]);
    fmt::println(stderr, "r3 index {} is {}", i, s);
  }
  std::cerr << "nothing else to do why am I still here\n";
}

// Store UTF-16 platform strings in out array. For extra credit, do it in one call to the
// PlatformUtf16 allocator.
void invokelab_r4(const PlatformUtf16 **data_out, int32_t count) {
  Utf16Converter converter;
  auto u16strings = MakeReservedVector<std::u16string>(count);
  for (int i = 0; i != count; ++i) {
    const char *fmt_string = "💫✨element ⦕{}⦖,🌟⭐";
    auto s = fmt::format(fmt_string, i);
    // fmt::println(stderr, "r4 index {} storing {}", i, s);
    auto s16 = converter.from_bytes(s);
    u16strings.push_back(std::move(s16));
  }
  PlatformUtf16::CreateBulk(u16strings.data(), count, data_out);
}

// Copy in to out, but reverse them. Just because.
void invokelab_r5(const char16_t **data_in, const PlatformUtf16 **data_out, int32_t count) {
  PlatformUtf16::CreateBulk(data_in, count, data_out);
  std::reverse(data_out, data_out + count);
}

struct Big {
  int a, b, c, d, e, f, g, h, i;
};

Big invokelab_r6(int a, int b, int c) {
  // fmt::println(std::cerr, "r6 {} {} {}", a, b, c);
  return Big {10+a, 10+b, 10+c, 100+a, 100+b, 100+c, 1000+a, 1000+b, 1000+c};
}

int invokelab_r7(int a, int b, int c) {
  // fmt::println(std::cerr, "r7 {} {} {}", a, b, c);
  return 12 + a;
}

class NENever {
public:
  NENever() = default;
  explicit NENever(std::string_view s) {
    std::cerr << "trying to make a " << s << '\n';
    auto utf16 = Utf16Converter().from_bytes(s.data());
    text_ = PlatformUtf16::Create(utf16);
    std::cerr << "constructor returning ok\n";
  }

  const PlatformUtf16 *text_;
};

struct NEOtherString {
  const char16_t *text_;
};
struct WrappedOtherString {
  NEOtherString neos_;
};

NENever invokelab_r8(int a, int b, int c) {
  fmt::println(std::cerr, "r8 {} {} {}", a, b, c);
  return NENever("r8 is sad");
}

void invokelab_r9(int a, int b, int c, NENever *result) {
  fmt::println(std::cerr, "r9 {} {} {}", a, b, c);
  result->text_ = PlatformUtf16::Create(Utf16Converter().from_bytes("r9 is sad"));
}

void invokelab_r10(NENever *result, int32_t count) {
  fmt::println(std::cerr, "r10 result is {}, count is {}",
      (void*)result, count);
  for (int i = 0; i != count; ++i) {
    auto message = fmt::format("r10 is {} happy", i);
    result[i].text_ = PlatformUtf16::Create(Utf16Converter().from_bytes(message));
  }
}

void invokelab_r11(NEOtherString *result, int32_t count) {
  fmt::println(std::cerr, "r10 result is {}, count is {}",
      (void*)result, count);
  for (int i = 0; i != count; ++i) {
    auto message = fmt::format("r11 is {} not-happy", i);
    const auto *freakshow = PlatformUtf16::Create(Utf16Converter().from_bytes(message));
    result[i].text_ = reinterpret_cast<const char16_t*>(freakshow);
  }
}

void invokelab_r12(WrappedOtherString *result, int32_t count) {
  fmt::println(std::cerr, "r10 result is {}, count is {}",
      (void*)result, count);
  for (int i = 0; i != count; ++i) {
    auto message = fmt::format("r11 is {} not-happy", i);
    const auto *freakshow = PlatformUtf16::Create(Utf16Converter().from_bytes(message));
    result[i].neos_.text_ = reinterpret_cast<const char16_t*>(freakshow);
  }
}


// There is no TableHandleManager_ctor entry point because we don't need callers to invoke
// the TableHandleManager ctor directly.
void deephaven_client_TableHandleManager_dtor(TableHandleManager *self) {
  delete self;
}

void deephaven_client_TableHandleManager_EmptyTable(const deephaven::client::TableHandleManager *self,
    int64_t size,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto table = self->EmptyTable(size);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandleManager_FetchTable(const deephaven::client::TableHandleManager *self,
    const char16_t *table_name,
    TableHandle **result,
    ErrorStatus *status) {
  status->Run([=]() {
    auto tn = Utf16Converter().to_bytes(table_name);
    auto table = self->FetchTable(tn);
    *result = new TableHandle(std::move(table));
  });
}


void deephaven_client_TableHandleManager_TimeTable(const TableHandleManager *self,
    const DurationSpecifier *period,
    const TimePointSpecifier *start_time,
    InteropBool blink_table,
    TableHandle **result,
    ErrorStatus *status) {
  status->Run([=]() {
    auto table = self->TimeTable(*period, *start_time, (bool)blink_table);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandleManager_InputTable(const TableHandleManager *self,
    const TableHandle *initial_table, const char16_t **key_columns,
    int64_t num_key_columns,
    TableHandle **result,
    ErrorStatus *status) {
  status->Run([=]() {
    auto kcs = MakeStringVec(key_columns, num_key_columns);
    auto table = self->InputTable(*initial_table, std::move(kcs));
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandleManager_RunScript(const TableHandleManager *self,
    const char16_t *code,
    ErrorStatus *status) {
  status->Run([self, code]() {
    auto s = Utf16Converter().to_bytes(code);
    self->RunScript(s);
  });
}

void deephaven_client_Client_Connect(const char *target,
    NativePtr<ClientOptions> options,
    NativePtr<Client> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto res = Client::Connect(target, *options);
    result->Reset(new Client(std::move(res)));
  });
}

// There is no Client_ctor entry point because we don't need callers to invoke
// the Client ctor directly.
void deephaven_client_Client_dtor(NativePtr<Client> self) {
  delete self;
}


void deephaven_client_Client_Close(NativePtr<Client> self,
    ErrorStatusNew *status) {
  status->Run([=]() {
    self->Close();
  });
}

void deephaven_client_Client_GetManager(NativePtr<Client> self,
    NativePtr<TableHandleManager> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto res = self->GetManager();
    result->Reset(new TableHandleManager(std::move(res)));
  });
}

void deephaven_client_TableHandle_GetAttributes(TableHandle *self,
    int32_t *num_columns, int64_t *num_rows, InteropBool *is_static,
    ErrorStatus *status) {
  status->Run([=]() {
    *num_columns = self->Schema()->NumCols();
    *num_rows = self->NumRows();
    *is_static = (InteropBool)self->IsStatic();
  });
}

void deephaven_client_TableHandle_GetSchema(TableHandle *self,
    int32_t /*num_columns*/, const PlatformUtf16 **columns, int32_t *column_types,
    ErrorStatus *status) {
  status->Run([=]() {
    const auto &schema = self->Schema();

    // Gather all the names, so we can do a bulk allocate call.
    const auto &names = schema->Names();
    PlatformUtf16::CreateBulk(names.data(), names.size(), columns);

    // Now do the column types
    size_t next_field_index = 0;
    for (auto element_type_id : schema->Types()) {
      column_types[next_field_index++] = static_cast<int32_t>(element_type_id);
    }
  });
}

// There is no TableHandle_ctor entry point because we don't need callers to invoke
// the TableHandle ctor directly.
void deephaven_client_TableHandle_dtor(NativePtr<TableHandle> self) {
  delete self;
}

void deephaven_client_TableHandle_GetManager(deephaven::client::TableHandle *self,
    deephaven::client::TableHandleManager **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto table = self->GetManager();
    *result = new TableHandleManager(std::move(table));
  });
}

void deephaven_client_TableHandle_Where(deephaven::client::TableHandle *self,
    const char16_t *condition,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    Utf16Converter converter;
    auto table = self->Where(converter.to_bytes(condition));
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_Select(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto cols = MakeStringVec(column_specs, num_column_specs);
    auto table = self->Select(cols);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_SelectDistinct(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto cols = MakeStringVec(column_specs, num_column_specs);
    auto table = self->SelectDistinct(cols);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_View(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto cols = MakeStringVec(column_specs, num_column_specs);
    auto table = self->View(cols);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_DropColumns(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto cols = MakeStringVec(column_specs, num_column_specs);
    auto table = self->DropColumns(cols);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_Update(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto cols = MakeStringVec(column_specs, num_column_specs);
    auto table = self->Update(cols);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_LazyUpdate(TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    TableHandle **result,
    ErrorStatus *status) {
  status->Run([=]() {
    auto cols = MakeStringVec(column_specs, num_column_specs);
    auto table = self->LazyUpdate(cols);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_LastBy(TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    TableHandle **result,
    ErrorStatus *status) {
  status->Run([=]() {
    auto cols = MakeStringVec(column_specs, num_column_specs);
    auto table = self->LastBy(cols);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_WhereIn(TableHandle *self,
    TableHandle *filter_table,
    const char16_t **column_specs, int64_t num_column_specs,
    TableHandle **result,
    ErrorStatus *status) {
  status->Run([=]() {
    auto cols = MakeStringVec(column_specs, num_column_specs);
    auto table = self->WhereIn(*filter_table, cols);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_AddTable(TableHandle *self,
    TableHandle *table_to_add,
    ErrorStatus *status) {
  status->Run([=]() {
    self->AddTable(*table_to_add);
  });
}

void deephaven_client_TableHandle_RemoveTable(TableHandle *self,
    TableHandle *table_to_remove,
    ErrorStatus *status) {
  status->Run([=]() {
    self->RemoveTable(*table_to_remove);
  });
}

void deephaven_client_TableHandle_By(TableHandle *self,
    const AggregateCombo *combo,
    const char16_t **column_specs, int64_t num_column_specs,
    TableHandle **result,
    ErrorStatus *status) {
  status->Run([=]() {
    auto cols = MakeStringVec(column_specs, num_column_specs);
    auto table = self->By(*combo, std::move(cols));
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_Head(TableHandle *self,
    int64_t num_rows,
    TableHandle **result,
    ErrorStatus *status) {
  status->Run([=]() {
    auto table = self->Head(num_rows);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_Tail(TableHandle *self,
    int64_t num_rows,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto table = self->Tail(num_rows);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_BindToVariable(deephaven::client::TableHandle *self,
    const char16_t *variable,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto v = Utf16Converter().to_bytes(variable);
    self->BindToVariable(v);
  });
}

void deephaven_client_TableHandle_ToString(TableHandle *self,
    int32_t want_headers, const PlatformUtf16 **result, ErrorStatus *status) {
  std::cerr << "want headers came in as " << want_headers << '\n';
  status->Run([self, want_headers, result]() {
    auto text = self->ToString(want_headers != 0);
    *result = PlatformUtf16::Create(std::move(text));
  });
}

void deephaven_client_TableHandle_ToArrowTable(TableHandle *self,
    ArrowTable **arrow_table, ErrorStatus *status) {
  status->Run([=]() {
    auto at = self->ToArrowTable();
    *arrow_table = new ArrowTable(std::move(at));
  });
}

class WrappedTickingCallback final : public TickingCallback {
public:
  WrappedTickingCallback(NativeOnUpdate *on_update, NativeOnFailure *on_failure) :
     on_update_(on_update), on_failure_(on_failure) {}

  void OnTick(TickingUpdate update) final {
    auto *copy = new TickingUpdate(std::move(update));
    (*on_update_)(copy);
  }

  void OnFailure(std::exception_ptr eptr) final {
    Utf16Converter converter;
    auto message = GetWhat(eptr);
    auto wide_string = converter.from_bytes(message);
    (*on_failure_)(wide_string.data());
  }

private:
  NativeOnUpdate *on_update_ = nullptr;
  NativeOnFailure *on_failure_ = nullptr;
};

void deephaven_client_TableHandle_Subscribe(deephaven::client::TableHandle *self,
    NativeOnUpdate *native_on_update, NativeOnFailure *native_on_failure,
    std::shared_ptr<SubscriptionHandle> **native_subscription_handle,
    ErrorStatus *status) {
  status->Run([=]() {
    auto wtc = std::make_shared<WrappedTickingCallback>(native_on_update, native_on_failure);
    auto handle = self->Subscribe(std::move(wtc));
    *native_subscription_handle = new std::shared_ptr<SubscriptionHandle>(std::move(handle));
  });
}

void deephaven_client_TableHandle_ToClientTable(TableHandle *self,
    ClientTableSpWrapper **client_table, ErrorStatus *status) {
  status->Run([=]() {
    auto ct = self->ToClientTable();
    *client_table = new ClientTableSpWrapper(std::move(ct));
  });
}

void deephaven_client_ArrowTable_dtor(NativePtr<ArrowTable> self) {
  delete self.Get();
}

void deephaven_client_ArrowTable_GetDimensions(NativePtr<ArrowTable> self,
    int32_t *num_columns, int64_t *num_rows,
    ErrorStatusNew *status) {
  status->Run([=]() {
    *num_columns = self->table_->num_columns();
    *num_rows = self->table_->num_rows();
  });
}

void deephaven_client_ArrowTable_GetSchema(NativePtr<ArrowTable> self,
  int32_t num_columns, StringHandle *column_handles, int32_t *column_types,
  StringPoolHandle *string_pool_handle, ErrorStatusNew *status) {
  status->Run([=]() {
    const auto &schema = self->table_->schema();
    if (schema->num_fields() != num_columns) {
      auto message = fmt::format("Expected schema->num_fields ({}) == num_columns ({})",
          schema->num_fields(), num_columns);
      throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
    }

    StringPoolBuilder builder;
    for (int32_t i = 0; i != num_columns; ++i) {
      const auto &field = schema->fields()[i];
      column_handles[i] = builder.Add(field->name());
      auto element_type_id = *ArrowUtil::GetElementTypeId(*field->type(), true);
      column_types[i] = static_cast<int32_t>(element_type_id);
    }
    *string_pool_handle = builder.Build();
  });
}

void deephaven_client_TickingUpdate_dtor(TickingUpdate *self) {
  delete self;
}

void deephaven_client_TickingUpdate_Current(TickingUpdate *self,
    ClientTableSpWrapper **result,
    ErrorStatus *status) {
  status->Run([=]() {
    *result = new ClientTableSpWrapper(self->Current());
  });
}

void deephaven_client_ClientTable_GetDimensions(
    NativePtr<ClientTableSpWrapper> self,
    int32_t *num_columns, int64_t *num_rows,
    ErrorStatusNew *status) {
  status->Run([=]() {
    *num_columns = self->table_->NumColumns();
    *num_rows = self->table_->NumRows();
  });
}

void deephaven_client_ClientTable_Schema(NativePtr<ClientTableSpWrapper> self,
    int32_t num_columns, StringHandle *column_handles, int32_t *column_types,
    StringPoolHandle *string_pool_handle,
    ErrorStatusNew *status) {
  status->Run([=]() {
    const auto &schema = self->table_->Schema();
    if (schema->NumCols() != num_columns) {
      auto message = fmt::format("Expected schema->num_fields ({}) == num_columns ({})",
          schema->NumCols(), num_columns);
      throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
    }

    StringPoolBuilder builder;
    for (int32_t i = 0; i != num_columns; ++i) {
      column_handles[i] = builder.Add(schema->Names()[i]);
      column_types[i] = static_cast<int32_t>(schema->Types()[i]);
    }
    *string_pool_handle = builder.Build();
  });
}

void deephaven_client_ClientTable_dtor(NativePtr<ClientTableSpWrapper> self) {
  delete self.Get();
}

void deephaven_client_ClientTableHelper_GetInt8Column(
    NativePtr<ClientTableSpWrapper> self,
    int32_t column_index,
    int8_t *data, InteropBool *optional_dest_null_flags, int64_t num_rows,
    StringPoolHandle *string_pool_handle,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto data_chunk = Int8Chunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    *string_pool_handle = StringPoolHandle();
  });
}

void deephaven_client_ClientTableHelper_GetInt16Column(
    NativePtr<ClientTableSpWrapper> self,
    int32_t column_index,
    int16_t *data, InteropBool *optional_dest_null_flags, int64_t num_rows,
    StringPoolHandle *string_pool_handle,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto data_chunk = Int16Chunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    *string_pool_handle = StringPoolHandle();
  });
}

void deephaven_client_ClientTableHelper_GetInt32Column(
    NativePtr<ClientTableSpWrapper> self,
    int32_t column_index,
    int32_t *data, InteropBool *optional_dest_null_flags, int64_t num_rows,
    StringPoolHandle *string_pool_handle,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto data_chunk = Int32Chunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    *string_pool_handle = StringPoolHandle();
  });
}

void deephaven_client_ClientTableHelper_GetInt64Column(
    NativePtr<ClientTableSpWrapper> self,
    int32_t column_index,
    int64_t *data, InteropBool *optional_dest_null_flags, int64_t num_rows,
    StringPoolHandle *string_pool_handle,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto data_chunk = Int64Chunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    *string_pool_handle = StringPoolHandle();
  });
}

void deephaven_client_ClientTableHelper_GetFloatColumn(
    NativePtr<ClientTableSpWrapper> self,
    int32_t column_index, float *data, InteropBool *optional_dest_null_flags, int64_t num_rows,
    StringPoolHandle *string_pool_handle,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto data_chunk = FloatChunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    *string_pool_handle = StringPoolHandle();
  });
}

void deephaven_client_ClientTableHelper_GetDoubleColumn(
    NativePtr<ClientTableSpWrapper> self,
    int32_t column_index, double *data, InteropBool *optional_dest_null_flags, int64_t num_rows,
    StringPoolHandle *string_pool_handle,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto data_chunk = DoubleChunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    *string_pool_handle = StringPoolHandle();
  });
}

void deephaven_client_ClientTableHelper_GetCharAsInt16Column(
    NativePtr<ClientTableSpWrapper> self,
    int32_t column_index, int16_t *data, InteropBool *optional_dest_null_flags, int64_t num_rows,
    StringPoolHandle *string_pool_handle,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto data_chunk = CharChunk::Create(num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    // For char, boolean, DateTime, and String we have to do a little data conversion.
    for (int64_t i = 0; i != num_rows; ++i) {
      data[i] = static_cast<int16_t>(data_chunk.data()[i]);
    }
    *string_pool_handle = StringPoolHandle();
  });
}

void deephaven_client_ClientTableHelper_GetBooleanAsInteropBoolColumn(
    NativePtr<ClientTableSpWrapper> self,
    int32_t column_index, int8_t *data, InteropBool *optional_dest_null_flags, int64_t num_rows,
    StringPoolHandle *string_pool_handle,
    ErrorStatusNew *status) {
  status->Run([=]() {
    // For char, boolean, DateTime, and String we have to do a little data conversion.
    auto data_chunk = BooleanChunk::Create(num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    for (int64_t i = 0; i != num_rows; ++i) {
      data[i] = data_chunk.data()[i] ? 1 : 0;
    }
    *string_pool_handle = StringPoolHandle();
  });
}

void deephaven_client_ClientTableHelper_GetStringColumn(
    NativePtr<ClientTableSpWrapper> self,
    int32_t column_index, StringHandle *data, InteropBool *optional_dest_null_flags,
    int64_t num_rows,
    StringPoolHandle *string_pool_handle,
    ErrorStatusNew *status) {
  status->Run([=]() {
    // For char, boolean, DateTime, and String we have to do a little data conversion.
    auto data_chunk = StringChunk::Create(num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    StringPoolBuilder builder;
    for (int64_t i = 0; i != num_rows; ++i) {
      data[i] = builder.Add(data_chunk.data()[i]);
    }
    *string_pool_handle = builder.Build();
  });
}

void deephaven_client_ClientTableHelper_GetDateTimeAsInt64Column(
    NativePtr<ClientTableSpWrapper> self,
    int32_t column_index, int64_t *data, InteropBool *optional_dest_null_flags, int64_t num_rows,
    StringPoolHandle *string_pool_handle,
    ErrorStatusNew *status) {
  status->Run([=]() {
    // For char, boolean, DateTime, and String we have to do a little data conversion.
    auto data_chunk = DateTimeChunk::Create(num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    for (int64_t i = 0; i != num_rows; ++i) {
      data[i] = data_chunk.data()[i].Nanos();
    }
    *string_pool_handle = StringPoolHandle();
  });
}

void deephaven_client_AggregateCombo_Create(
    const NativePtr<Aggregate> *aggregate_ptrs,
    int32_t num_aggregates,
    NativePtr<AggregateCombo> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto agg_copies = MakeReservedVector<Aggregate>(num_aggregates);
    for (int32_t i = 0; i != num_aggregates; ++i) {
      agg_copies.push_back(*aggregate_ptrs[i]);
    }
    auto ac = AggregateCombo::Create(std::move(agg_copies));
    result->Reset(new AggregateCombo(std::move(ac)));
  });
}

void deephaven_client_AggregateCombo_dtor(NativePtr<AggregateCombo> self) {
  delete self.Get();
}

void deephaven_client_Aggregate_dtor(NativePtr<Aggregate> self) {
  delete self.Get();
}

void deephaven_client_Aggregate_AbsSum(
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::AbsSum(std::move(cols))));
  });
}

void deephaven_client_Aggregate_Group(
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::Group(std::move(cols))));
  });
}

void deephaven_client_Aggregate_Avg(
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::Avg(std::move(cols))));
  });
}

void deephaven_client_Aggregate_Count(
    const char *column,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    result->Reset(new Aggregate(Aggregate::Count(column)));
  });
}

void deephaven_client_Aggregate_First(
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::First(std::move(cols))));
  });
}

void deephaven_client_Aggregate_Last(
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::Last(std::move(cols))));
  });
}

void deephaven_client_Aggregate_Max(
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::Max(std::move(cols))));
  });
}

void deephaven_client_Aggregate_Med(
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::Med(std::move(cols))));
  });
}

void deephaven_client_Aggregate_Min(
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::Min(std::move(cols))));
  });
}

void deephaven_client_Aggregate_Pct(
    double percentile, InteropBool avg_median,
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::Pct(percentile, (bool)avg_median, std::move(cols))));
  });
}

void deephaven_client_Aggregate_Std(
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::Std(std::move(cols))));
  });
}

void deephaven_client_Aggregate_Sum(
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::Sum(std::move(cols))));
  });
}

void deephaven_client_Aggregate_Var(
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::Var(std::move(cols))));
  });
}

void deephaven_client_Aggregate_WAvg(const char *weight,
    const char **columns, int32_t num_columns,
    NativePtr<Aggregate> *result,
    ErrorStatusNew *status) {
  status->Run([=]() {
    auto cols = std::vector<std::string>(columns, columns + num_columns);
    result->Reset(new Aggregate(Aggregate::WAvg(std::string(weight), std::move(cols))));
  });
}

void deephaven_client_utility_DurationSpecifier_ctor_nanos(int64_t nanos,
    NativePtr<DurationSpecifier> *result,
    ErrorStatusNew *status) {
  status->Run([=] {
    result->Reset(new DurationSpecifier(nanos));
  });
}

void deephaven_client_utility_DurationSpecifier_ctor_durationstr(
    const char *duration_str,
    NativePtr<DurationSpecifier> *result,
    ErrorStatusNew *status) {
  status->Run([=] {
    result->Reset(new DurationSpecifier(duration_str));
  });
}

void deephaven_client_utility_DurationSpecifier_dtor(NativePtr<DurationSpecifier> self) {
  delete self.Get();
}

void deephaven_client_utility_TimePointSpecifier_ctor_nanos(
    int64_t nanos,
    NativePtr<TimePointSpecifier> *result,
    ErrorStatusNew *status) {
  status->Run([=] {
    result->Reset(new TimePointSpecifier(nanos));
  });
}

void deephaven_client_utility_TimePointSpecifier_ctor_timepointstr(
    const char *time_point_str,
    NativePtr<TimePointSpecifier> *result,
    ErrorStatusNew *status) {
  status->Run([=] {
    Utf16Converter converter;
    result->Reset(new TimePointSpecifier(time_point_str));
  });
}

void deephaven_client_utility_TimePointSpecifier_dtor(NativePtr<TimePointSpecifier> self) {
  delete self.Get();
}

void deephaven_dhclient_utility_TableMaker_ctor(
    NativePtr<TableMaker> *result,
    ErrorStatusNew *status) {
  status->Run([=] {
    result->Reset(new TableMaker());
  });
}

void deephaven_dhclient_utility_TableMaker_dtor(NativePtr<TableMaker> self) {
  delete self.Get();
}

void deephaven_dhclient_utility_TableMaker_MakeTable(
    NativePtr<TableMaker> self,
    NativePtr<TableHandleManager> manager,
    NativePtr<TableHandle> *result,
    ErrorStatusNew *status) {
  status->Run([=] {
    auto th = self->MakeTable(*manager);
    result->Reset(new TableHandle(std::move(th)));
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__CharAsInt16(
    NativePtr<TableMaker> self,
    const char *name, const int16_t *data, int32_t length,
    const InteropBool *optional_nulls,
    ErrorStatusNew *status) {
  status->Run([=] {
    auto get_value = [=](size_t index) -> int16_t { return static_cast<int16_t>(data[index]); };
    auto is_null = [=](size_t index) { return optional_nulls != nullptr && (bool)optional_nulls[index]; };
    self->AddColumn<char16_t>(name, get_value, is_null, length);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Int8(
    NativePtr<TableMaker> self,
    const char *name, const int8_t *data, int32_t length,
    const InteropBool *optional_nulls,
    ErrorStatusNew *status) {
  status->Run([=] {
    auto get_value = [=](size_t index) { return data[index]; };
    auto is_null = [=](size_t index) { return optional_nulls != nullptr && (bool)optional_nulls[index]; };
    self->AddColumn<int8_t>(name, get_value, is_null, length);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Int16(
    NativePtr<TableMaker> self,
    const char *name, const int16_t *data, int32_t length,
    const InteropBool *optional_nulls,
    ErrorStatusNew *status) {
  status->Run([=] {
    auto get_value = [=](size_t index) { return data[index]; };
    auto is_null = [=](size_t index) { return optional_nulls != nullptr && (bool)optional_nulls[index]; };
    self->AddColumn<int16_t>(name, get_value, is_null, length);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Int32(
    NativePtr<TableMaker> self,
    const char *name, const int32_t *data, int32_t length,
    const InteropBool *optional_nulls,
    ErrorStatusNew *status) {
  status->Run([=] {
    auto get_value = [=](size_t index) { return data[index]; };
    auto is_null = [=](size_t index) { return optional_nulls != nullptr && (bool)optional_nulls[index]; };
    self->AddColumn<int32_t>(name, get_value, is_null, length);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Int64(
    NativePtr<TableMaker> self,
    const char *name, const int64_t *data, int32_t length,
    const InteropBool *optional_nulls,
    ErrorStatusNew *status) {
  status->Run([=] {
    auto get_value = [=](size_t index) { return data[index]; };
    auto is_null = [=](size_t index) { return optional_nulls != nullptr && (bool)optional_nulls[index]; };
    self->AddColumn<int64_t>(name, get_value, is_null, length);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Float(
    NativePtr<TableMaker> self,
    const char *name, const float *data, int32_t length,
    const InteropBool *optional_nulls,
    ErrorStatusNew *status) {
  status->Run([=] {
    auto get_value = [=](size_t index) { return data[index]; };
    auto is_null = [=](size_t index) { return optional_nulls != nullptr && (bool)optional_nulls[index]; };
    self->AddColumn<float>(name, get_value, is_null, length);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Double(
    NativePtr<TableMaker> self,
    const char *name, const double *data, int32_t length,
    const InteropBool *optional_nulls,
    ErrorStatusNew *status) {
  status->Run([=] {
    auto get_value = [=](size_t index) { return data[index]; };
    auto is_null = [=](size_t index) { return optional_nulls != nullptr && (bool)optional_nulls[index]; };
    self->AddColumn<double>(name, get_value, is_null, length);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__BoolAsInteropBool(
    NativePtr<TableMaker> self,
    const char *name, const int8_t *data, int32_t length,
    const InteropBool *optional_nulls,
    ErrorStatusNew *status) {
  status->Run([=] {
    auto get_value = [=](size_t index) { return data[index] != 0; };
    auto is_null = [=](size_t index) { return optional_nulls != nullptr && (bool)optional_nulls[index]; };
    self->AddColumn<bool>(name, get_value, is_null, length);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__DateTimeAsInt64(
    NativePtr<TableMaker> self,
    const char *name, const int64_t *data, int32_t length,
    const InteropBool *optional_nulls,
    ErrorStatusNew *status) {
  status->Run([=] {
    auto get_value = [=](size_t index) { return DateTime::FromNanos(data[index]); };
    auto is_null = [=](size_t index) { return optional_nulls != nullptr && (bool)optional_nulls[index]; };
    self->AddColumn<DateTime>(name, get_value, is_null, length);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__String(
    NativePtr<TableMaker> self,
    const char *name, const char **data, int32_t length,
    const InteropBool *optional_nulls,
    ErrorStatusNew *status) {
  status->Run([=] {
    auto get_value = [=](size_t index) -> std::string {
      if (data[index] == nullptr) {
        return "";
      }
      return data[index];
    };
    auto is_null = [=](size_t index) { return optional_nulls != nullptr && (bool)optional_nulls[index]; };
    self->AddColumn<std::string>(name, get_value, is_null, length);
  });
}
}  // extern "C"

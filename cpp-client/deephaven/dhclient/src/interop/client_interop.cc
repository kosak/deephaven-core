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

using deephaven::client::Client;
using deephaven::client::ClientOptions;
using deephaven::client::TableHandle;
using deephaven::client::TableHandleManager;
using deephaven::client::subscription::SubscriptionHandle;
using deephaven::client::utility::ArrowUtil;
using deephaven::client::utility::DurationSpecifier;
using deephaven::client::utility::TableMaker;
using deephaven::client::utility::TimePointSpecifier;
using deephaven::client::interop::ArrowTable;
using deephaven::client::interop::ClientTable;
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
using deephaven::dhcore::interop::NativePtr;
using deephaven::dhcore::interop::PlatformUtf16;
using deephaven::dhcore::interop::Utf16Converter;
using deephaven::dhcore::ticking::TickingCallback;
using deephaven::dhcore::ticking::TickingUpdate;
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

void GetColumnHelper(deephaven::client::interop::ClientTable *self,
    int32_t column_index, Chunk *data_chunk, bool *optional_dest_null_flags, int64_t num_rows) {
  auto cs = self->table_->GetColumn(column_index);
  BooleanChunk null_chunk;
  BooleanChunk *null_chunkp = nullptr;
  if (optional_dest_null_flags != nullptr) {
    null_chunk = BooleanChunk::CreateView(optional_dest_null_flags, num_rows);
    null_chunkp = &null_chunk;
  }
  auto rows = self->table_->GetRowSequence();
  cs->FillChunk(*rows, data_chunk, null_chunkp);
}

template<typename T>
struct BasicValueGrabber {
  BasicValueGrabber(const T *data, int32_t length, const int8_t *optional_nulls)
    : data_(data), length_(length), optional_nulls_(optional_nulls) {}

  std::optional<T> operator()(size_t index) const {
    if (optional_nulls_ != nullptr && optional_nulls_[index]) {
      return {};
    }
    return DateTime::FromNanos(data_[index]);
  }

  const T *data_ = nullptr;
  int32_t length_ = 0;
  const int8_t *optional_nulls_ = nullptr;
};
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
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto tn = Utf16Converter().to_bytes(table_name);
    auto table = self->FetchTable(tn);
    *result = new TableHandle(std::move(table));
  });
}


void deephaven_client_TableHandleManager_TimeTable(const deephaven::client::TableHandleManager *self,
    const deephaven::client::utility::DurationSpecifier *period,
    const deephaven::client::utility::TimePointSpecifier *start_time,
    bool blink_table,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto table = self->TimeTable(*period, *start_time, blink_table);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandleManager_InputTable(const deephaven::client::TableHandleManager *self,
    const deephaven::client::TableHandle *initial_table, const char16_t **key_columns,
    int64_t num_key_columns,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto kcs = MakeStringVec(key_columns, num_key_columns);
    auto table = self->InputTable(*initial_table, std::move(kcs));
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandleManager_RunScript(const deephaven::client::TableHandleManager *self,
    const char16_t *code,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([self, code]() {
    auto s = Utf16Converter().to_bytes(code);
    self->RunScript(s);
  });
}

void deephaven_client_Client_Connect(const char16_t *target,
    NativePtr<ClientOptions> options,
    NativePtr<Client> *result,
    ErrorStatus *status) {
  status->Run([=]() {
    auto s = Utf16Converter().to_bytes(target);
    auto res = Client::Connect(s, *options.ptr_);
    result->ptr_ = new Client(std::move(res));
  });
}

// There is no Client_ctor entry point because we don't need callers to invoke
// the Client ctor directly.
void deephaven_client_Client_dtor(Client *self) {
  delete self;
}

void deephaven_client_TableHandle_GetDimensions(TableHandle *self,
    int32_t *num_columns, int64_t *num_rows, ErrorStatus *status) {
  status->Run([=]() {
    *num_columns = self->Schema()->NumCols();
    *num_rows = self->NumRows();
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


void deephaven_client_Client_Close(Client *self,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->Close();
  });
}

void deephaven_client_Client_GetManager(Client *self,
    TableHandleManager **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto res = self->GetManager();
    *result = new TableHandleManager(std::move(res));
  });
}

// There is no TableHandle_ctor entry point because we don't need callers to invoke
// the TableHandle ctor directly.
void deephaven_client_TableHandle_dtor(TableHandle *self) {
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

void deephaven_client_TableHandle_LazyUpdate(deephaven::client::TableHandle *self,
    const char16_t **column_specs, int64_t num_column_specs,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto cols = MakeStringVec(column_specs, num_column_specs);
    auto table = self->LazyUpdate(cols);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_Head(deephaven::client::TableHandle *self,
    int64_t num_rows,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    auto table = self->Head(num_rows);
    *result = new TableHandle(std::move(table));
  });
}

void deephaven_client_TableHandle_Tail(deephaven::client::TableHandle *self,
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

void deephaven_client_ArrowTable_dtor(deephaven::client::interop::ArrowTable *self) {
  delete self;
}

void deephaven_client_ArrowTable_GetDimensions(ArrowTable *self,
    int32_t *num_columns, int64_t *num_rows, ErrorStatus *status) {
  status->Run([=]() {
    *num_columns = self->table_->num_columns();
    *num_rows = self->table_->num_rows();
  });
}

void deephaven_client_ArrowTable_GetSchema(deephaven::client::interop::ArrowTable *self,
  int32_t num_columns, const PlatformUtf16 **columns, int32_t *column_types,
  ErrorStatus *status) {
  status->Run([=]() {
    const auto &schema = self->table_->schema();
    if (schema->num_fields() != num_columns) {
      auto message = fmt::format("Expected schema->num_fields ({}) == num_columns ({})",
          schema->num_fields(), num_columns);
      throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
    }

    // Gather all the names, so we can do a bulk allocate call.
    auto names = MakeReservedVector<std::string>(num_columns);
    for (const auto &field : schema->fields()) {
      names.push_back(field->name());
    }
    PlatformUtf16::CreateBulk(names.data(), names.size(), columns);

    // Now do the column types
    size_t next_field_index = 0;
    for (const auto &field : schema->fields()) {
      auto element_type_id = *ArrowUtil::GetElementTypeId(*field->type(), true);
      column_types[next_field_index++] = static_cast<int32_t>(element_type_id);
    }
  });
}

void deephaven_client_TableHandle_ToClientTable(TableHandle *self,
    ClientTable **client_table, ErrorStatus *status) {
  status->Run([=]() {
    auto ct = self->ToClientTable();
    *client_table = new ClientTable(std::move(ct));
  });
}

void deephaven_client_ClientTable_GetDimensions(ClientTable *self,
    int32_t *num_columns, int64_t *num_rows, ErrorStatus *status) {
  status->Run([=]() {
    *num_columns = self->table_->NumColumns();
    *num_rows = self->table_->NumRows();
  });
}

void deephaven_client_ClientTable_Schema(deephaven::client::interop::ClientTable *self,
    int32_t num_columns, const PlatformUtf16 **columns, int32_t *column_types,
    ErrorStatus *status) {
  status->Run([=]() {
    const auto &schema = self->table_->Schema();
    if (schema->NumCols() != num_columns) {
      auto message = fmt::format("Expected schema->num_fields ({}) == num_columns ({})",
          schema->NumCols(), num_columns);
      throw std::runtime_error(DEEPHAVEN_LOCATION_STR(message));
    }

    // Gather all the names, so we can do a bulk allocate call.
    auto names = MakeReservedVector<std::string>(num_columns);
    for (const auto &field : schema->Names()) {
      names.push_back(field);
    }
    PlatformUtf16::CreateBulk(names.data(), names.size(), columns);

    // Now do the column types
    size_t next_field_index = 0;
    for (const auto element_type_id : schema->Types()) {
      column_types[next_field_index++] = static_cast<int32_t>(element_type_id);
    }
  });
}

void deephaven_client_TickingUpdate_dtor(deephaven::dhcore::ticking::TickingUpdate *self) {
  delete self;
}

void deephaven_client_TickingUpdate_Current(deephaven::dhcore::ticking::TickingUpdate *self,
    deephaven::client::interop::ClientTable **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    std::cerr << "Change this back\n";
    std::shared_ptr<deephaven::dhcore::clienttable::ClientTable> current(self->Current());
    *result = new ClientTable(std::move(current));
  });
}

void deephaven_client_ClientTable_dtor(deephaven::client::interop::ClientTable *self) {
  delete self;
}

void deephaven_client_ClientTableHelper_GetInt8Column(deephaven::client::interop::ClientTable *self,
    int32_t column_index, int8_t *data, bool *optional_dest_null_flags, int64_t num_rows,
    ErrorStatus *status) {
  status->Run([=]() {
    auto data_chunk = Int8Chunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
  });
}

void deephaven_client_ClientTableHelper_GetInt16Column(deephaven::client::interop::ClientTable *self,
    int32_t column_index, int16_t *data, bool *optional_dest_null_flags, int64_t num_rows,
    ErrorStatus *status) {
  status->Run([=]() {
    auto data_chunk = Int16Chunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
  });
}

void deephaven_client_ClientTableHelper_GetInt32Column(deephaven::client::interop::ClientTable *self,
    int32_t column_index, int32_t *data, bool *optional_dest_null_flags, int64_t num_rows,
    ErrorStatus *status) {
  status->Run([=]() {
    auto data_chunk = Int32Chunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
  });
}

void deephaven_client_ClientTableHelper_GetInt64Column(deephaven::client::interop::ClientTable *self,
    int32_t column_index, int64_t *data, bool *optional_dest_null_flags, int64_t num_rows,
    ErrorStatus *status) {
  status->Run([=]() {
    auto data_chunk = Int64Chunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
  });
}

void deephaven_client_ClientTableHelper_GetFloatColumn(deephaven::client::interop::ClientTable *self,
    int32_t column_index, float *data, bool *optional_dest_null_flags, int64_t num_rows,
    ErrorStatus *status) {
  status->Run([=]() {
    auto data_chunk = FloatChunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
  });
}

void deephaven_client_ClientTableHelper_GetDoubleColumn(deephaven::client::interop::ClientTable *self,
    int32_t column_index, double *data, bool *optional_dest_null_flags, int64_t num_rows,
    ErrorStatus *status) {
  status->Run([=]() {
    auto data_chunk = DoubleChunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
  });
}

void deephaven_client_ClientTableHelper_GetCharColumn(deephaven::client::interop::ClientTable *self,
    int32_t column_index, char16_t *data, bool *optional_dest_null_flags, int64_t num_rows,
    ErrorStatus *status) {
  status->Run([=]() {
    auto data_chunk = CharChunk::CreateView(data, num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
  });
}

void deephaven_client_ClientTableHelper_GetBooleanAsInt32Column(deephaven::client::interop::ClientTable *self,
    int32_t column_index, int32_t *data, bool *optional_dest_null_flags, int64_t num_rows,
    ErrorStatus *status) {
  status->Run([=]() {
    // For Boolean, DateTime, and String we have to do a little data conversion.
    auto data_chunk = BooleanChunk::Create(num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    for (int64_t i = 0; i != num_rows; ++i) {
      data[i] = data_chunk.data()[i] ? 1 : 0;
    }
  });
}

void deephaven_client_ClientTableHelper_GetStringColumn(deephaven::client::interop::ClientTable *self,
    int32_t column_index, const PlatformUtf16 **data, bool *optional_dest_null_flags, int64_t num_rows,
    ErrorStatus *status) {
  status->Run([=]() {
    // For Boolean, DateTime, and String we have to do a little data conversion.
    auto data_chunk = StringChunk::Create(num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    PlatformUtf16::CreateBulk(data_chunk.data(), num_rows, data);
  });
}

void deephaven_client_ClientTableHelper_GetDateTimeAsLongColumn(deephaven::client::interop::ClientTable *self,
    int32_t column_index, int64_t *data, bool *optional_dest_null_flags, int64_t num_rows,
    ErrorStatus *status) {
  status->Run([=]() {
    // For Boolean, DateTime, and String we have to do a little data conversion.
    auto data_chunk = DateTimeChunk::Create(num_rows);
    GetColumnHelper(self, column_index, &data_chunk, optional_dest_null_flags, num_rows);
    for (int64_t i = 0; i != num_rows; ++i) {
      data[i] = data_chunk.data()[i].Nanos();
    }
  });
}

void deephaven_client_utility_DurationSpecifier_ctor_nanos(int64_t nanos,
    DurationSpecifier **result, ErrorStatus *status) {
  status->Run([=] {
    *result = new DurationSpecifier(nanos);
  });
}

void deephaven_client_utility_DurationSpecifier_ctor_duration(const char16_t *duration,
    DurationSpecifier **result, ErrorStatus *status) {
  status->Run([=] {
    Utf16Converter converter;
    *result = new DurationSpecifier(converter.to_bytes(duration));
  });
}

void deephaven_client_utility_DurationSpecifier_dtor(DurationSpecifier *self) {
  delete self;
}

void deephaven_client_utility_TimePointSpecifier_ctor_nanos(int64_t nanos,
    TimePointSpecifier **result, ErrorStatus *status) {
  status->Run([=] {
    *result = new TimePointSpecifier(nanos);
  });
}

void deephaven_client_utility_TimePointSpecifier_ctor_duration(const char16_t *duration,
    TimePointSpecifier **result, ErrorStatus *status) {
  status->Run([=] {
    Utf16Converter converter;
    *result = new TimePointSpecifier(converter.to_bytes(duration));
  });
}

void deephaven_client_utility_TimePointSpecifier_dtor(TimePointSpecifier *self) {
  delete self;
}

void deephaven_dhclient_utility_TableMaker_ctor(TableMaker **result, ErrorStatus *status) {
  status->Run([=] {
    *result = new TableMaker();
  });
}

void deephaven_dhclient_utility_TableMaker_dtor(TableMaker *self) {
  delete self;
}

void deephaven_dhclient_utility_TableMaker_MakeTable(
    deephaven::client::utility::TableMaker *self,
    deephaven::client::TableHandleManager *manager,
    deephaven::client::TableHandle **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=] {
    auto th = self->MakeTable(*manager);
    *result = new TableHandle(std::move(th));
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Char(TableMaker *self,
    const char16_t *name, const char16_t *data, int32_t length,
    const int8_t *optional_nulls, ErrorStatus *status) {
  status->Run([=] {
    BasicValueGrabber<char16_t> value_grabber(data, length, optional_nulls);
    Utf16Converter converter;
    self->GrabColumn(converter.to_bytes(name), value_grabber);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Int8(TableMaker *self,
    const char16_t *name, const int8_t *data, int32_t length,
    const int8_t *optional_nulls, ErrorStatus *status) {
  status->Run([=] {
    BasicValueGrabber<int8_t> value_grabber(data, length, optional_nulls);
    Utf16Converter converter;
    self->GrabColumn(converter.to_bytes(name), value_grabber);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Int16(TableMaker *self,
    const char16_t *name, const int16_t *data, int32_t length,
    const int8_t *optional_nulls, ErrorStatus *status) {
  status->Run([=] {
    BasicValueGrabber<int16_t> value_grabber(data, length, optional_nulls);
    Utf16Converter converter;
    self->GrabColumn(converter.to_bytes(name), value_grabber);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Int32(TableMaker *self,
    const char16_t *name, const int32_t *data, int32_t length,
    const int8_t *optional_nulls, ErrorStatus *status) {
  status->Run([=] {
    BasicValueGrabber<int32_t> value_grabber(data, length, optional_nulls);
    Utf16Converter converter;
    self->GrabColumn(converter.to_bytes(name), value_grabber);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Int64(TableMaker *self,
    const char16_t *name, const int64_t *data, int32_t length,
    const int8_t *optional_nulls, ErrorStatus *status) {
  status->Run([=] {
    BasicValueGrabber<int64_t> value_grabber(data, length, optional_nulls);
    Utf16Converter converter;
    self->GrabColumn(converter.to_bytes(name), value_grabber);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Float(TableMaker *self,
    const char16_t *name, const float *data, int32_t length,
    const int8_t *optional_nulls, ErrorStatus *status) {
  status->Run([=] {
    BasicValueGrabber<float> value_grabber(data, length, optional_nulls);
    Utf16Converter converter;
    self->GrabColumn(converter.to_bytes(name), value_grabber);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__Double(TableMaker *self,
    const char16_t *name, const double *data, int32_t length,
    const int8_t *optional_nulls, ErrorStatus *status) {
  status->Run([=] {
    BasicValueGrabber<double> value_grabber(data, length, optional_nulls);
    Utf16Converter converter;
    self->GrabColumn(converter.to_bytes(name), value_grabber);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__BoolAsByte(TableMaker *self,
    const char16_t *name, const int8_t *data, int32_t length,
    const int8_t *optional_nulls, ErrorStatus *status) {
  status->Run([=] {
    auto value_grabber = [=](size_t index) -> std::optional<bool> {
      if (optional_nulls != nullptr && optional_nulls[index]) {
        return {};
      }
      return data[index] != 0;
    };
    Utf16Converter converter;
    self->GrabColumn(converter.to_bytes(name), value_grabber);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__DateTimeAsLong(TableMaker *self,
    const char16_t *name, const int64_t *data, int32_t length,
    const int8_t *optional_nulls, ErrorStatus *status) {
  status->Run([=] {
    auto value_grabber = [=](size_t index) -> std::optional<DateTime> {
      if (optional_nulls != nullptr && optional_nulls[index]) {
        return {};
      }
      return DateTime::FromNanos(data[index]);
    };
    Utf16Converter converter;
    self->GrabColumn(converter.to_bytes(name), value_grabber);
  });
}

void deephaven_dhclient_utility_TableMaker_AddColumn__String(TableMaker *self,
    const char16_t *name, const char16_t **data, int32_t length, const int8_t *optional_nulls,
    ErrorStatus *status) {
  status->Run([=] {
    Utf16Converter converter;
    auto value_grabber = [&](size_t index) -> std::optional<std::string> {
      if (data[index] == nullptr || (optional_nulls != nullptr && optional_nulls[index])) {
        return {};
      }
      return converter.to_bytes(data[index]);
    };
    self->GrabColumn(converter.to_bytes(name), value_grabber);
  });
}
}  // extern "C"


struct Visitor final : ColumnSourceVisitor {
  explicit Visitor(size_t num_rows) : num_rows_(num_rows),
      row_sequence_(RowSequence::CreateSequential(0, num_rows)),
      null_flags_(BooleanChunk::Create(num_rows)) {
  }

  void Visit(const dhcore::column::CharColumnSource &source) final {
    SimpleCopyValues<CharChunk, arrow::UInt16Builder>(source);
  }

  void Visit(const dhcore::column::Int8ColumnSource &source) final {
    SimpleCopyValues<Int8Chunk, arrow::Int8Builder>(source);
  }

  void Visit(const dhcore::column::Int16ColumnSource &source) final {
    SimpleCopyValues<Int16Chunk, arrow::Int16Builder>(source);
  }

  void Visit(const dhcore::column::Int32ColumnSource &source) final {
    SimpleCopyValues<Int32Chunk, arrow::Int32Builder>(source);
  }

  void Visit(const dhcore::column::Int64ColumnSource &source) final {
    SimpleCopyValues<Int64Chunk, arrow::Int64Builder>(source);
  }

  void Visit(const dhcore::column::FloatColumnSource &source) final {
    SimpleCopyValues<FloatChunk, arrow::FloatBuilder>(source);
  }

  void Visit(const dhcore::column::DoubleColumnSource &source) final {
    SimpleCopyValues<DoubleChunk, arrow::DoubleBuilder>(source);
  }

  void Visit(const dhcore::column::BooleanColumnSource &source) final {
    SimpleCopyValues<BooleanChunk, arrow::BooleanBuilder>(source);
  }

  void Visit(const dhcore::column::StringColumnSource &source) final {
    SimpleCopyValues<StringChunk, arrow::StringBuilder>(source);
  }

  void Visit(const dhcore::column::DateTimeColumnSource &source) final {
    auto src_chunk = PopulateChunk<DateTimeChunk>(source);
    auto dest_chunk = Int64Chunk::Create(num_rows_);
    for (size_t i = 0; i != num_rows_; ++i) {
      dest_chunk[i] = src_chunk[i].Nanos();
    }
    arrow::TimestampBuilder builder(arrow::timestamp(arrow::TimeUnit::NANO, "UTC"),
        arrow::default_memory_pool());
    PopulateAndFinishBuilder(dest_chunk, &builder);
  }

  void Visit(const dhcore::column::LocalDateColumnSource &source) final {
    auto src_chunk = PopulateChunk<LocalDateChunk>(source);
    auto dest_chunk = Int64Chunk::Create(num_rows_);
    for (size_t i = 0; i != num_rows_; ++i) {
      dest_chunk[i] = src_chunk[i].Millis();
    }
    arrow::Date64Builder builder;
    PopulateAndFinishBuilder(dest_chunk, &builder);
  }

  void Visit(const dhcore::column::LocalTimeColumnSource &source) final {
    auto src_chunk = PopulateChunk<LocalTimeChunk>(source);
    auto dest_chunk = Int64Chunk::Create(num_rows_);
    for (size_t i = 0; i != num_rows_; ++i) {
      dest_chunk[i] = src_chunk[i].Nanos();
    }
    arrow::Time64Builder builder(arrow::time64(arrow::TimeUnit::NANO), arrow::default_memory_pool());
    PopulateAndFinishBuilder(dest_chunk, &builder);
  }

  void Visit(const dhcore::column::ContainerBaseColumnSource &source) final {
    throw std::runtime_error(DEEPHAVEN_LOCATION_STR("TODO(kosak)"));
  }

  template<typename TChunk, typename TBuilder, typename TColumnSource>
  void SimpleCopyValues(const TColumnSource &source) {
    auto chunk = PopulateChunk<TChunk>(source);
    TBuilder builder;
    PopulateAndFinishBuilder(chunk, &builder);
  }

  template<typename TChunk, typename TColumnSource>
  TChunk PopulateChunk(const TColumnSource &source) {
    auto chunk = TChunk::Create(num_rows_);
    source.FillChunk(*row_sequence_, &chunk, &null_flags_);
    return chunk;
  }

  template<typename TChunk, typename TBuilder>
  void PopulateAndFinishBuilder(const TChunk &chunk, TBuilder *builder) {
    for (size_t i = 0; i != num_rows_; ++i) {
      if (!null_flags_[i]) {
        OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder->Append(chunk.data()[i])));
      } else {
        OkOrThrow(DEEPHAVEN_LOCATION_EXPR(builder->AppendNull()));
      }
    }
    result_ = ValueOrThrow(DEEPHAVEN_LOCATION_EXPR(builder->Finish()));
  }

  size_t num_rows_;
  std::shared_ptr<RowSequence> row_sequence_;
  BooleanChunk null_flags_;
  std::shared_ptr<arrow::Array> result_;
};


std::shared_ptr<arrow::Array> ArrowUtil::MakeArrowArray(const ColumnSource &column_source,
    size_t num_rows) {
  Visitor visitor(num_rows);
  column_source.AcceptVisitor(&visitor);
  return std::move(visitor.result_);
}

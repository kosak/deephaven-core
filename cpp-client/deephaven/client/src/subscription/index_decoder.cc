struct Constants {
  static constexpr const int8_t SHORT_VALUE = 1;
  static constexpr const int8_t INT_VALUE = 2;
  static constexpr const int8_t LONG_VALUE = 3;
  static constexpr const int8_t BYTE_VALUE = 4;

  static constexpr const int8_t VALUE_MASK = 7;

  static constexpr const int8_t OFFSET = 8;
  static constexpr const int8_t SHORT_ARRAY = 16;
  static constexpr const int8_t BYTE_ARRAY = 24;
  static constexpr const int8_t END = 32;
  static constexpr const int8_t CMD_MASK = 0x78;
};

class DataInput {
public:
  explicit DataInput(const flatbuffers::Vector<int8_t> &vec) : DataInput(vec.data(), vec.size()) {}
  DataInput(const void *start, size_t size) : data_(static_cast<const char*>(start)), size_(size) {}
  int64_t readLong();
  int32_t readInt();
  int16_t readShort();
  int8_t readByte();

private:
  const char *data_ = nullptr;
  size_t size_ = 0;
};


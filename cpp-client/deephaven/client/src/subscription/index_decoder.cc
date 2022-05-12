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

std::shared_ptr<RowSequence> readExternalCompressedDelta(DataInput *in) {
  RowSequenceBuilder builder;

  int64_t offset = 0;

  int64_t pending = -1;
  auto consume = [&pending, &builder](int64_t v) {
    auto s = pending;
    if (s == -1) {
      pending = v;
    } else if (v < 0) {
      builder.addRange(s, -v);
      pending = -1;
    } else {
      builder.add(s);
      pending = v;
    }
  };

  while (true) {
    int64_t actualValue;
    int command = in->readByte();

    switch (command & Constants::CMD_MASK) {
      case Constants::OFFSET: {
        int64_t value = readValue(in, command);
        actualValue = offset + (value < 0 ? -value : value);
        consume(value < 0 ? -actualValue : actualValue);
        offset = actualValue;
        break;
      }

      case Constants::SHORT_ARRAY: {
        int shortCount = (int) readValue(in, command);
        for (int ii = 0; ii < shortCount; ++ii) {
          int16_t shortValue = in->readShort();
          actualValue = offset + (shortValue < 0 ? -shortValue : shortValue);
          consume(shortValue < 0 ? -actualValue : actualValue);
          offset = actualValue;
        }
        break;
      }

      case Constants::BYTE_ARRAY: {
        int byteCount = (int) readValue(in, command);
        for (int ii = 0; ii < byteCount; ++ii) {
          int8_t byteValue = in->readByte();
          actualValue = offset + (byteValue < 0 ? -byteValue : byteValue);
          consume(byteValue < 0 ? -actualValue : actualValue);
          offset = actualValue;
        }
        break;
      }

      case Constants::END: {
        if (pending >= 0) {
          builder.add(pending);
        }
        return builder.build();
      }

      default:
        throw std::runtime_error(stringf("Bad command: %o", command));
    }
  }
}

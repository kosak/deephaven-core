#pragma once

namespace deephaven::client::subscription {
class UpdateProcessor {
public:
  UpdateProcessor(std::unique_ptr<arrow::flight::FlightStreamReader>
  fsr,
  std::shared_ptr <internal::ColumnDefinitions> colDefs,
      std::shared_ptr<TickingCallback>
  callback);

  static void runForever(const std::shared_ptr <ThreadNubbin> &self);

private:
  void runForeverHelper();
  void runForeverHelperImpl();

public:
  std::unique_ptr <arrow::flight::FlightStreamReader> fsr_;
  std::shared_ptr <internal::ColumnDefinitions> colDefs_;
  std::shared_ptr <TickingCallback> callback_;
};


}  // namespace deephaven::client::subscription


extern "C" {
__declspec(dllexport) void zamboni_doadd(int a, int b, int *result) {
  *result = a + b;
}
}

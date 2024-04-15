/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/client/interop/client_options_interop.h"

#include "deephaven/client/client.h"
#include "deephaven/client/client_options.h"
#include "deephaven/dhcore/interop/interop_util.h"

using deephaven::client::ClientOptions;

extern "C" {
void deephaven_client_ClientOptions_ctor(ClientOptions **result,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    *result = new ClientOptions();
  });
}

void deephaven_client_ClientOptions_dtor(ClientOptions *self) {
  delete self;
}

void deephaven_client_ClientOptions_SetDefaultAuthentication(ClientOptions *self,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->SetDefaultAuthentication();
  });
}

void deephaven_client_ClientOptions_SetBasicAuthentication(ClientOptions *self,
    const char *username, const char *password,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->SetBasicAuthentication(username, password);
  });
}

void deephaven_client_ClientOptions_SetCustomAuthentication(ClientOptions *self,
    const char *authentication_key, const char *authentication_value,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->SetCustomAuthentication(authentication_key, authentication_value);
  });
}

void deephaven_client_ClientOptions_SetSessionType(ClientOptions *self,
    const char *session_type,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->SetSessionType(session_type);
  });
}

void deephaven_client_ClientOptions_SetUseTls(ClientOptions *self,
    bool use_tls,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->SetUseTls(use_tls);
  });

}

void deephaven_client_ClientOptions_SetTlsRootCerts(ClientOptions *self,
    const char *tls_root_certs,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->SetTlsRootCerts(tls_root_certs);
  });
}

void deephaven_client_ClientOptions_SetClientCertChain(ClientOptions *self,
    const char *client_cert_chain,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->SetClientCertChain(client_cert_chain);
  });

}

void deephaven_client_ClientOptions_SetClientPrivateKey(ClientOptions *self,
    const char *client_private_key,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->SetClientPrivateKey(client_private_key);
  });

}

void deephaven_client_ClientOptions_AddIntOption(ClientOptions *self,
    const char *opt, int32_t val,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->AddIntOption(opt, val);
  });

}

void deephaven_client_ClientOptions_AddStringOption(ClientOptions *self,
    const char *opt, const char *val,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->AddStringOption(opt, val);
  });
}

void deephaven_client_ClientOptions_AddExtraHeader(ClientOptions *self,
    const char *header_name, const char *header_value,
    deephaven::dhcore::interop::ErrorStatus *status) {
  status->Run([=]() {
    self->AddExtraHeader(header_name, header_value);
  });
}
}  // extern "C"

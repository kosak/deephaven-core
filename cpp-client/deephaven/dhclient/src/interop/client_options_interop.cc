/*
 * Copyright (c) 2016-2024 Deephaven Data Labs and Patent Pending
 */
#include "deephaven/client/interop/client_options_interop.h"

#include "deephaven/client/client.h"
#include "deephaven/client/client_options.h"
#include "deephaven/dhcore/interop/interop_util.h"

using deephaven::dhcore::interop::ResultOrError;
using deephaven::client::ClientOptions;

extern "C" {
void deephaven_client_ClientOptions_ctor(ResultOrError<ClientOptions> *roe) {
  roe->SetResult([]() {
    return new ClientOptions();
  });
}

void deephaven_client_ClientOptions_dtor(ClientOptions *self) {
  delete self;
}

void deephaven_client_ClientOptions_SetDefaultAuthentication(ClientOptions *self,
    ResultOrError<void> *roe) {
  roe->SetResult([self]() {
    self->SetDefaultAuthentication();
    return nullptr;
  });
}

void deephaven_client_ClientOptions_SetBasicAuthentication(ClientOptions *self,
    const char *username, const char *password, ResultOrError<void> *roe) {
  roe->SetResult([self, username, password]() {
    self->SetBasicAuthentication(username, password);
    return nullptr;
  });
}

void deephaven_client_ClientOptions_SetCustomAuthentication(ClientOptions *self,
    const char *authentication_key, const char *authentication_value,
    ResultOrError<void> *roe) {
  roe->SetResult([self, authentication_key, authentication_value]() {
    self->SetCustomAuthentication(authentication_key, authentication_value);
    return nullptr;
  });
}

void deephaven_client_ClientOptions_SetSessionType(ClientOptions *self,
    const char *session_type, ResultOrError<void> *roe) {
  roe->SetResult([self, session_type]() {
    self->SetSessionType(session_type);
    return nullptr;
  });
}

void deephaven_client_ClientOptions_SetUseTls(ClientOptions *self,
    bool use_tls, ResultOrError<void> *roe) {
  roe->SetResult([self, use_tls]() {
    self->SetUseTls(use_tls);
    return nullptr;
  });

}

void deephaven_client_ClientOptions_SetTlsRootCerts(ClientOptions *self,
    const char *tls_root_certs, ResultOrError<void> *roe) {
  roe->SetResult([self, tls_root_certs]() {
    self->SetTlsRootCerts(tls_root_certs);
    return nullptr;
  });


}

void deephaven_client_ClientOptions_SetClientCertChain(ClientOptions *self,
    const char *client_cert_chain, ResultOrError<void> *roe) {
  roe->SetResult([self, client_cert_chain]() {
    self->SetClientCertChain(client_cert_chain);
    return nullptr;
  });

}

void deephaven_client_ClientOptions_SetClientPrivateKey(ClientOptions *self,
    const char *client_private_key, ResultOrError<void> *roe) {
  roe->SetResult([self, client_private_key]() {
    self->SetClientPrivateKey(client_private_key);
    return nullptr;
  });

}

void deephaven_client_ClientOptions_AddIntOption(ClientOptions *self,
    const char *opt, int32_t val, ResultOrError<void> *roe) {
  roe->SetResult([self, opt, val]() {
    self->AddIntOption(opt, val);
    return nullptr;
  });

}

void deephaven_client_ClientOptions_AddStringOption(ClientOptions *self,
    const char *opt, const char *val, ResultOrError<void> *roe) {
  roe->SetResult([self, opt, val]() {
    self->AddStringOption(opt, val);
    return nullptr;
  });
}

void deephaven_client_ClientOptions_AddExtraHeader(ClientOptions *self,
    const char *header_name, const char *header_value, ResultOrError<void> *roe) {
  roe->SetResult([self, header_name, header_value]() {
    self->AddExtraHeader(header_name, header_value);
    return nullptr;
  });
}
}  // extern "C"

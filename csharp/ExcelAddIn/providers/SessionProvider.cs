﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Deephaven.DeephavenClient;
using Deephaven.DheClient.session;
using Deephaven.ExcelAddIn.Models;
using Deephaven.ExcelAddIn.Util;

namespace Deephaven.ExcelAddIn.Providers;

internal class SessionProvider : IObserver<StatusOr<CredentialsBase>>, IObservable<StatusOr<SessionBase>> {
  private StatusOr<SessionBase> _session = StatusOr<SessionBase>.OfStatus("[no credentials]");
  private readonly WorkerThread _workerThread;

  public void OnNext(StatusOr<CredentialsBase> credentials) {
    if (_workerThread.InvokeIfRequired(() => OnNext(credentials))) {
      return;
    }

    // Dispose existing session
    if (_session.GetValueOrStatus(out var sess, out var status)) {
      sess.Dispose();
    }

    _session = StatusOr<SessionBase>.OfStatus("Trying to connect");
    if (!credentials.GetValueOrStatus(out var creds, out var credStatus)) {
      _session = StatusOr<SessionBase>.OfStatus(credStatus);
    } else {
      _session = MakeSession(creds);
    }

    _observers.OnNext(_session);
  }

  private StatusOr<SessionBase> MakeSession(CredentialsBase credentials) {
    try {
      var sb = credentials.AcceptVisitor<SessionBase>(
        core => {
          var client = Client.Connect(core.ConnectionString, new ClientOptions());
          return new CoreSession(client);
        },

        corePlus => {
          var session = SessionManager.FromUrl("Deephaven Excel", corePlus.JsonUrl);
          if (!session.PasswordAuthentication(corePlus.User, corePlus.Password, corePlus.OperateAs)) {
            throw new Exception("Authentication failed");
          }
          return new CorePlusSession(session, _workerThread);
        });
      return StatusOr<SessionBase>.OfValue(sb);
    } catch (Exception ex) {
      return StatusOr<SessionBase>.OfStatus(ex.Message);
    }
  }
}

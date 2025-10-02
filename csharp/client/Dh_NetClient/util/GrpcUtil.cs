//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//

using System.Diagnostics;
using System.Formats.Asn1;
using Grpc.Core;
using Grpc.Net.Client;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Deephaven.Dh_NetClient;

// https://stackoverflow.com/questions/7688445/extract-common-name-from-distinguished-name/71376270#71376270
public static class X509DistinguishedNameExtensions {
  public static IEnumerable<KeyValuePair<string, string>> GetRelativeNames(this X500DistinguishedName dn) {
    var reader = new AsnReader(dn.RawData, AsnEncodingRules.BER);
    var snSeq = reader.ReadSequence();
    if (!snSeq.HasData) {
      throw new InvalidOperationException();
    }

    // Many types are allowable.  We're only going to support the string-like ones
    // (This excludes IPAddress, X400 address, and other wierd stuff)
    // https://www.rfc-editor.org/rfc/rfc5280#page-37
    // https://www.rfc-editor.org/rfc/rfc5280#page-112
    var allowedRdnTags = new[]
    {
      UniversalTagNumber.TeletexString, UniversalTagNumber.PrintableString,
      UniversalTagNumber.UniversalString, UniversalTagNumber.UTF8String,
      UniversalTagNumber.BMPString, UniversalTagNumber.IA5String,
      UniversalTagNumber.NumericString, UniversalTagNumber.VisibleString,
      UniversalTagNumber.T61String
    };
    while (snSeq.HasData) {
      var rdnSeq = snSeq.ReadSetOf().ReadSequence();
      var attrOid = rdnSeq.ReadObjectIdentifier();
      var attrValueTagNo = (UniversalTagNumber)rdnSeq.PeekTag().TagValue;
      if (!allowedRdnTags.Contains(attrValueTagNo)) {
        throw new NotSupportedException($"Unknown tag type {attrValueTagNo} for attr {attrOid}");
      }
      var attrValue = rdnSeq.ReadCharacterString(attrValueTagNo);
      var friendlyName = new Oid(attrOid).FriendlyName;
      yield return new KeyValuePair<string, string>(friendlyName ?? attrOid, attrValue);
    }
  }
}

public static class GrpcUtil {
  public static GrpcChannel CreateChannel(string target, ClientOptions clientOptions) {
    Debug.WriteLine($"FUN TIMES: target {target} zamboni {clientOptions.ExtraStupid}");
    var channelOptions = GrpcUtil.MakeChannelOptions(clientOptions);
    var address = GrpcUtil.MakeAddress(clientOptions, target);

    var channel = GrpcChannel.ForAddress(address, channelOptions);
    return channel;
  }

  public static GrpcChannelOptions MakeChannelOptions(ClientOptions clientOptions) {
    var channelOptions = new GrpcChannelOptions();

    if (!clientOptions.UseTls && !clientOptions.TlsRootCerts.IsEmpty()) {
      throw new Exception("Server.CreateFromTarget: ClientOptions: UseTls is false but pem provided");
    }

    if (true) {
      if (clientOptions.ExtraStupid != null) {
        var handler = new SocketsHttpHandler {
          // Crucial step: Set the target host for SSL validation.
          // This is the functional equivalent of GRPC_SSL_TARGET_NAME_OVERRIDE_ARG.
          SslOptions = new System.Net.Security.SslClientAuthenticationOptions {
            TargetHost = clientOptions.ExtraStupid,
            // You may need to set other options here, like RemoteCertificateValidationCallback
            // if you have a custom validation strategy.
            RemoteCertificateValidationCallback = (a, cert, c, d) => {
              Debug.WriteLine($"extra-extra-stupid subjecft is {cert?.Subject}");
              return true;

            }
          }
        };
        channelOptions.HttpHandler = handler;
      }
    } else {
      if (true || clientOptions.ExtraStupid != null) {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, tpnn) => {
          Debug.WriteLine($"extra-stupid is {clientOptions.ExtraStupid}. cert is ${cert}");
          Debug.WriteLine($"extra-extra-stupid is {cert?.SubjectName.Name}");
          var temp = cert?.SubjectName.Decode(X500DistinguishedNameFlags.UseNewLines);
          Debug.WriteLine($"hell {temp}");

          foreach (var stupid in cert?.SubjectName.GetRelativeNames()) {
            Debug.WriteLine($"{stupid.Key} -- {stupid.Value}");
          }
          return true;
        };
        channelOptions.HttpHandler = handler;
      }
    }

    // var shh = new SocketsHttpHandler();
    // shh.SslOptions.TargetHost = "authserver";


    //
    // var httpClientHandler = new HttpClientHandler();
    // httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, _) => {
    //   chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
    //   chain.ChainPolicy.CustomTrustStore.Add(mycert);
    //   etc etc get this to work
    // https://github.com/grpc/grpc-dotnet/blob/dd72d6a38ab2984fd224aa8ed53686dc0153b9da/testassets/InteropTestsClient/InteropClient.cs#L170
    //
    //
    // };
    //
    // channelOptions.Credentials = GetCredentials(clientOptions.UseTls, clientOptions.TlsRootCerts,
    //   clientOptions.ClientCertChain, clientOptions.ClientPrivateKey);
    return channelOptions;
  }

  public static string MakeAddress(ClientOptions clientOptions, string target) {
    return (clientOptions.UseTls ? "https://" : "http://") + target;
  }

  private static ChannelCredentials GetCredentials(
    bool useTls,
    string tlsRootCerts,
    string clientRootChain,
    string clientPrivateKey) {
    if (!useTls) {
      return ChannelCredentials.Insecure;
    }

    var certPair = new KeyCertificatePair(clientRootChain, clientPrivateKey);
    return new SslCredentials(tlsRootCerts, certPair);
  }
}

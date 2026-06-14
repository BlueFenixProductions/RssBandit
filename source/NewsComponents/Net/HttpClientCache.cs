#region Version Info Header
/*
 * $Id$
 * $HeadURL$
 * Last modified by $Author$
 * Last modified at $Date$
 * $Revision$
 */
#endregion

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NewsComponents.Net
{
    /// <summary>
    /// Provides pooled <see cref="HttpClient"/> instances for the engine's HTTP(S) requests
    /// (used by <see cref="AsyncWebRequest"/> and <see cref="SyncWebRequest"/>).
    /// Clients are cached by the handler-affecting settings: proxy identity, credential
    /// content bound to the target authority, and client certificate. Everything that can
    /// vary per request (user agent, conditional GET headers, cookies, timeout) is applied
    /// to the <see cref="HttpRequestMessage"/> or per-request CancellationToken instead.
    /// </summary>
    internal static class HttpClientCache
    {
        /// <summary>
        /// Lifetime of a pooled connection: ensures we respect DNS changes and do not
        /// hold sockets open forever (replaces the old ServicePoint recycling behavior).
        /// </summary>
        private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);

        private static readonly ConcurrentDictionary<string, HttpClient> Clients =
            new ConcurrentDictionary<string, HttpClient>(StringComparer.Ordinal);

        private static readonly string[] AuthSchemes = { "Basic", "Digest", "NTLM", "Negotiate", "Kerberos" };

        /// <summary>
        /// Gets (or creates) a cached <see cref="HttpClient"/> suitable for the
        /// provided per-request settings.
        /// </summary>
        /// <param name="proxy">Optional proxy. If null, the system default proxy is used
        /// (with default credentials for proxy authentication, as before).</param>
        /// <param name="credentials">Optional request credentials.</param>
        /// <param name="requestUri">The request Uri (used to resolve/bind credentials).</param>
        /// <param name="clientCertificate">Optional client certificate.</param>
        public static HttpClient GetClient(IWebProxy proxy, ICredentials credentials, Uri requestUri,
                                           X509Certificate2 clientCertificate)
        {
            bool useDefaultCredentials = false;
            NetworkCredential resolvedCredential = null;
            Uri credentialRoot = null;
            string credentialKey = "anon";

            if (credentials != null)
            {
                if (ReferenceEquals(credentials, CredentialCache.DefaultCredentials) ||
                    ReferenceEquals(credentials, CredentialCache.DefaultNetworkCredentials))
                {
                    useDefaultCredentials = true;
                    credentialKey = "default";
                }
                else
                {
                    resolvedCredential = ResolveCredential(credentials, requestUri);
                    if (ReferenceEquals(resolvedCredential, CredentialCache.DefaultNetworkCredentials))
                    {
                        resolvedCredential = null;
                        useDefaultCredentials = true;
                        credentialKey = "default";
                    }
                    else if (resolvedCredential != null)
                    {
                        // bind the client (and its credential) to the target authority, so
                        // CredentialCache prefix matching cannot leak credentials across hosts:
                        credentialRoot = new Uri(requestUri.GetLeftPart(UriPartial.Authority));
                        credentialKey = String.Concat("cred:", credentialRoot.ToString(), ":",
                                                      HashCredential(resolvedCredential));
                    }
                    // else: the supplied ICredentials have no entry matching this Uri. Same
                    // effective behavior as the old HttpWebRequest stack: the request goes
                    // out anonymous (and 401 handling upstream kicks in).
                }
            }

            string key = String.Concat(
                proxy == null
                    ? "sysproxy"
                    : "proxy:" + RuntimeHelpers.GetHashCode(proxy).ToString(CultureInfo.InvariantCulture),
                "|", credentialKey,
                "|", clientCertificate != null ? clientCertificate.Thumbprint : "nocert");

            return Clients.GetOrAdd(key,
                _ => CreateClient(proxy, useDefaultCredentials, resolvedCredential, credentialRoot, clientCertificate));
        }

        private static HttpClient CreateClient(IWebProxy proxy, bool useDefaultCredentials,
                                               NetworkCredential credential, Uri credentialRoot,
                                               X509Certificate2 clientCertificate)
        {
            var handler = new SocketsHttpHandler
            {
                // redirects are tracked manually by Async-/SyncWebRequest: permanent
                // redirects (301) must surface the new Uri to the caller(s):
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                // no implicit cookie container: the engine no longer shares the IE/WinInet
                // cookie jar (that bridge was retired with the WebView2 migration). Explicit
                // per-request cookies (RequestParameter.Cookies) are still applied as headers.
                UseCookies = false,
                PooledConnectionLifetime = PooledConnectionLifetime,
            };

            if (proxy != null)
            {
                handler.UseProxy = true;
                handler.Proxy = proxy;
            }
            else
            {
                // use the system default proxy and allow it to authenticate with
                // the default (logged on user) credentials - previous behavior:
                handler.UseProxy = true;
                handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
            }

            if (useDefaultCredentials)
            {
                handler.Credentials = CredentialCache.DefaultCredentials;
            }
            else if (credential != null && credentialRoot != null)
            {
                handler.Credentials = new CredentialCache
                {
                    {credentialRoot, "Basic", credential},
                    {credentialRoot, "Digest", credential},
                    {credentialRoot, "NTLM", credential},
                    {credentialRoot, "Negotiate", credential}
                };
            }

            handler.SslOptions = new SslClientAuthenticationOptions
            {
                // per-handler certificate trust override honoring
                // AsyncWebRequest.TrustedCertificateIssues / OnCertificateIssue:
                RemoteCertificateValidationCallback = TrustSelectedCertificatePolicy.CheckServerCertificate
            };
            if (clientCertificate != null)
            {
                handler.SslOptions.ClientCertificates = new X509Certificate2Collection(clientCertificate);
            }

            // timeouts are applied per request (CancellationTokenSource):
            return new HttpClient(handler, true) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        }

        private static NetworkCredential ResolveCredential(ICredentials credentials, Uri requestUri)
        {
            NetworkCredential nc = credentials as NetworkCredential;
            if (nc != null)
                return nc;

            foreach (string scheme in AuthSchemes)
            {
                try
                {
                    nc = credentials.GetCredential(requestUri, scheme);
                }
                catch (Exception)
                {
                    nc = null;
                }
                if (nc != null)
                    return nc;
            }
            return null;
        }

        private static string HashCredential(NetworkCredential credential)
        {
            // do not keep plain text passwords within cache keys:
            byte[] hash;
            using (var sha = SHA256.Create())
            {
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(
                    String.Concat(credential.Domain, "|", credential.UserName, "|", credential.Password)));
            }
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Translates exceptions thrown by <see cref="HttpClient"/> send operations to the
        /// <see cref="WebException"/> types/statuses existing consumers expect
        /// (e.g. <see cref="WebExceptionStatus.NameResolutionFailure"/> checks in the UI).
        /// </summary>
        /// <param name="exception">The thrown exception.</param>
        /// <param name="requestUri">The request Uri (for the message).</param>
        /// <param name="timedOut">True, if the per-request timeout token was canceled.</param>
        public static Exception TranslateSendException(Exception exception, Uri requestUri, bool timedOut)
        {
            if (exception is WebException)
                return exception;

            if (exception is OperationCanceledException && timedOut)
            {
                return new WebException(
                    String.Format("The request to '{0}' has timed out.", requestUri),
                    exception, WebExceptionStatus.Timeout, null);
            }

            HttpRequestException hre = exception as HttpRequestException;
            if (hre != null)
            {
                WebExceptionStatus status;
                switch (hre.HttpRequestError)
                {
                    case HttpRequestError.NameResolutionError:
                        status = WebExceptionStatus.NameResolutionFailure;
                        break;
                    case HttpRequestError.ConnectionError:
                        status = WebExceptionStatus.ConnectFailure;
                        break;
                    case HttpRequestError.SecureConnectionError:
                        status = hre.InnerException is AuthenticationException
                                     ? WebExceptionStatus.TrustFailure
                                     : WebExceptionStatus.SecureChannelFailure;
                        break;
                    case HttpRequestError.ProxyTunnelError:
                        status = WebExceptionStatus.ConnectFailure;
                        break;
                    default:
                        status = WebExceptionStatus.UnknownError;
                        break;
                }
                return new WebException(hre.Message, hre, status, null);
            }

            return exception;
        }
    }
}

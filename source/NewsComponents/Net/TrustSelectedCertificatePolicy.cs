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
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace NewsComponents.Net
{
    #region CertificateIssue enum; CertificateIssueCancelEventArgs - used by Certificate policy handling

    /// <summary>
    /// Possible Certificate issues.
    /// </summary>
    /// <remarks> The .NET Framework should expose these, but they don't.</remarks>
    [Serializable]
    public enum CertificateIssue : long
    {
        /// <summary>
        ///
        /// </summary>
        CertEXPIRED = 0x800B0101,
        /// <summary>
        ///
        /// </summary>
        CertVALIDITYPERIODNESTING = 0x800B0102,
        /// <summary>
        ///
        /// </summary>
        CertROLE = 0x800B0103,
        /// <summary>
        ///
        /// </summary>
        CertPATHLENCONST = 0x800B0104,
        /// <summary>
        ///
        /// </summary>
        CertCRITICAL = 0x800B0105,
        /// <summary>
        ///
        /// </summary>
        CertPURPOSE = 0x800B0106,
        /// <summary>
        ///
        /// </summary>
        CertISSUERCHAINING = 0x800B0107,
        /// <summary>
        ///
        /// </summary>
        CertMALFORMED = 0x800B0108,
        /// <summary>
        ///
        /// </summary>
        CertUNTRUSTEDROOT = 0x800B0109,
        /// <summary>
        ///
        /// </summary>
        CertCHAINING = 0x800B010A,
        /// <summary>
        ///
        /// </summary>
        CertREVOKED = 0x800B010C,
        /// <summary>
        ///
        /// </summary>
        CertUNTRUSTEDTESTROOT = 0x800B010D,
        /// <summary>
        ///
        /// </summary>
        CertREVOCATION_FAILURE = 0x800B010E,
        /// <summary>
        ///
        /// </summary>
        CertCN_NO_MATCH = 0x800B010F,
        /// <summary>
        ///
        /// </summary>
        CertWRONG_USAGE = 0x800B0110,
        /// <summary>
        ///
        /// </summary>
        CertUNTRUSTEDCA = 0x800B0112
    }

    /// <summary>
    /// Cancelable Event Argument class to handle certificate issues on web requests.
    /// </summary>
    public class CertificateIssueCancelEventArgs : CancelEventArgs
    {
        /// <summary>
        /// Problem/Issue caused
        /// </summary>
        public CertificateIssue CertificateIssue;

        /// <summary>
        /// The certificate, that casued the problem
        /// </summary>
        public X509Certificate Certificate;

        /// <summary>
        /// The involved WebRequest.
        /// </summary>
        public WebRequest WebRequest;

        /// <summary>
        /// Designated initializer
        /// </summary>
        /// <param name="issue">CertificateIssue</param>
        /// <param name="cert">X509Certificate</param>
        /// <param name="request">WebRequest</param>
        /// <param name="cancel">bool</param>
        public CertificateIssueCancelEventArgs(CertificateIssue issue, X509Certificate cert, WebRequest request,
                                               bool cancel)
            : base(cancel)
        {
            CertificateIssue = issue;
            Certificate = cert;
            WebRequest = request;
        }
    }

    #endregion

    /// <summary>
    /// Server certificate validation honoring the user-accepted certificate issues
    /// (see <see cref="AsyncWebRequest.TrustedCertificateIssues"/> and the
    /// <see cref="AsyncWebRequest.OnCertificateIssue"/> event). Wired up as the
    /// per-handler RemoteCertificateValidationCallback on the pooled
    /// SocketsHttpHandler instances (see HttpClientCache) - no process-global
    /// ServicePointManager hook anymore.
    /// </summary>
    internal static class TrustSelectedCertificatePolicy
    {
        private static readonly AsyncLocal<Uri> _currentRequestUri = new AsyncLocal<Uri>();

        /// <summary>
        /// Ambient request Uri of the request that currently establishes a (TLS)
        /// connection. Set it right before sending a request; it flows into the
        /// validation callback via the execution context.
        /// </summary>
        internal static Uri CurrentRequestUri
        {
            get { return _currentRequestUri.Value; }
            set { _currentRequestUri.Value = value; }
        }

        /// <summary>
        /// Checks the server certificate. On policy errors, the issue is mapped to a
        /// <see cref="CertificateIssue"/> and raised via
        /// <see cref="AsyncWebRequest.RaiseOnCertificateIssue"/>: previously accepted
        /// issues pass silently, unknown ones are surfaced to the user (event).
        /// </summary>
        public static bool CheckServerCertificate(object sender, X509Certificate certificate, X509Chain chain,
                                                  SslPolicyErrors sslPolicyErrors)
        {
            try
            {
                if (sslPolicyErrors == SslPolicyErrors.None)
                    return true;

                Uri requestUri = CurrentRequestUri;
                if (requestUri == null)
                {
                    // fallback: derive at least the host from the TLS stream
                    var sslStream = sender as SslStream;
                    if (sslStream != null && !String.IsNullOrEmpty(sslStream.TargetHostName))
                        requestUri = new Uri("https://" + sslStream.TargetHostName + "/");
                }
                if (requestUri == null)
                    return false;

                CertificateIssue issue = MapToCertificateIssue(sslPolicyErrors, chain);

#pragma warning disable SYSLIB0014
                // data holder only (this request instance is never executed); the public
                // CertificateIssueCancelEventArgs consumers expect a WebRequest that
                // carries the request Uri:
                WebRequest infoRequest = WebRequest.Create(requestUri);
#pragma warning restore SYSLIB0014

                var args = new CertificateIssueCancelEventArgs(issue, certificate, infoRequest, true);
                AsyncWebRequest.RaiseOnCertificateIssue(sender, args);
                return !args.Cancel;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("TrustSelectedCertificatePolicy.CheckServerCertificate() error: " + ex.Message);
                return false;
            }
        }

        private static CertificateIssue MapToCertificateIssue(SslPolicyErrors errors, X509Chain chain)
        {
            if ((errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
                return CertificateIssue.CertMALFORMED;

            if ((errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
                return CertificateIssue.CertCN_NO_MATCH;

            if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) != 0 && chain != null)
            {
                foreach (X509ChainStatus status in chain.ChainStatus)
                {
                    X509ChainStatusFlags flags = status.Status;

                    if ((flags & X509ChainStatusFlags.NotTimeValid) != 0)
                        return CertificateIssue.CertEXPIRED;
                    if ((flags & X509ChainStatusFlags.NotTimeNested) != 0)
                        return CertificateIssue.CertVALIDITYPERIODNESTING;
                    if ((flags & X509ChainStatusFlags.Revoked) != 0)
                        return CertificateIssue.CertREVOKED;
                    if ((flags & (X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation)) != 0)
                        return CertificateIssue.CertREVOCATION_FAILURE;
                    if ((flags & (X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.ExplicitDistrust)) != 0)
                        return CertificateIssue.CertUNTRUSTEDROOT;
                    if ((flags & X509ChainStatusFlags.PartialChain) != 0)
                        return CertificateIssue.CertCHAINING;
                    if ((flags & (X509ChainStatusFlags.NotValidForUsage | X509ChainStatusFlags.CtlNotValidForUsage)) != 0)
                        return CertificateIssue.CertWRONG_USAGE;
                    if ((flags & X509ChainStatusFlags.InvalidBasicConstraints) != 0)
                        return CertificateIssue.CertPATHLENCONST;
                    if ((flags & X509ChainStatusFlags.HasNotSupportedCriticalExtension) != 0)
                        return CertificateIssue.CertCRITICAL;
                }
                return CertificateIssue.CertCHAINING;
            }

            return CertificateIssue.CertUNTRUSTEDCA;
        }
    }
}

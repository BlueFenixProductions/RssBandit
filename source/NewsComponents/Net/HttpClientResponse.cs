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
using System.IO;
using System.Net;
using System.Net.Http;

namespace NewsComponents.Net
{
    /// <summary>
    /// A <see cref="WebResponse"/> adapter wrapping a <see cref="HttpResponseMessage"/>.
    /// Returned by the synchronous request API (<see cref="SyncWebRequest"/>), so existing
    /// consumers keep working against the well known WebResponse shape (status code,
    /// headers, response stream) while the transfer happens over HttpClient internally.
    /// </summary>
    public sealed class HttpClientResponse : WebResponse
    {
        private readonly HttpResponseMessage _response;
        private readonly Uri _requestUri;
        private WebHeaderCollection _headers;
        private Stream _responseStream;
        private bool _disposed;

        internal HttpClientResponse(HttpResponseMessage response, Uri requestUri)
        {
            if (response == null)
                throw new ArgumentNullException("response");

            _response = response;
            _requestUri = requestUri;
        }

        /// <summary>
        /// Gets the HTTP response status code.
        /// </summary>
        public HttpStatusCode StatusCode
        {
            get { return _response.StatusCode; }
        }

        /// <summary>
        /// Gets the HTTP status description (reason phrase).
        /// </summary>
        public string StatusDescription
        {
            get { return _response.ReasonPhrase ?? String.Empty; }
        }

        /// <summary>
        /// Gets the Last-Modified header value converted to local time. If the server
        /// did not provide one, the current date/time is returned - this matches the
        /// old <see cref="HttpWebResponse.LastModified"/> behavior consumers relied on.
        /// </summary>
        public DateTime LastModified
        {
            get
            {
                DateTimeOffset? lastModified = _response.Content != null
                                                   ? _response.Content.Headers.LastModified
                                                   : null;
                return lastModified.HasValue ? lastModified.Value.LocalDateTime : DateTime.Now;
            }
        }

        /// <summary>
        /// Gets the Uri that actually responded (after any transparent rewrite).
        /// </summary>
        public override Uri ResponseUri
        {
            get
            {
                if (_response.RequestMessage != null && _response.RequestMessage.RequestUri != null)
                    return _response.RequestMessage.RequestUri;
                return _requestUri;
            }
        }

        /// <summary>
        /// Gets the content length (or -1, if not provided).
        /// </summary>
        public override long ContentLength
        {
            get
            {
                if (_response.Content != null && _response.Content.Headers.ContentLength.HasValue)
                    return _response.Content.Headers.ContentLength.Value;
                return -1L;
            }
        }

        /// <summary>
        /// Gets the content type (or an empty string, if not provided).
        /// </summary>
        public override string ContentType
        {
            get
            {
                if (_response.Content != null && _response.Content.Headers.ContentType != null)
                    return _response.Content.Headers.ContentType.ToString();
                return String.Empty;
            }
        }

        /// <summary>
        /// Headers are supported.
        /// </summary>
        public override bool SupportsHeaders
        {
            get { return true; }
        }

        /// <summary>
        /// Gets all response headers (response headers and content headers merged,
        /// like the old HttpWebResponse.Headers collection).
        /// </summary>
        public override WebHeaderCollection Headers
        {
            get
            {
                if (_headers == null)
                {
                    var headers = new WebHeaderCollection();
                    foreach (var header in _response.Headers)
                    {
                        foreach (string value in header.Value)
                            headers.Add(header.Key, value);
                    }
                    if (_response.Content != null)
                    {
                        foreach (var header in _response.Content.Headers)
                        {
                            foreach (string value in header.Value)
                                headers.Add(header.Key, value);
                        }
                    }
                    _headers = headers;
                }
                return _headers;
            }
        }

        /// <summary>
        /// Gets the wrapped <see cref="HttpResponseMessage"/>.
        /// </summary>
        public HttpResponseMessage ResponseMessage
        {
            get { return _response; }
        }

        /// <summary>
        /// Gets the response content stream (not buffered; not seekable).
        /// </summary>
        public override Stream GetResponseStream()
        {
            if (_responseStream == null)
            {
                _responseStream = _response.Content != null
                                      ? _response.Content.ReadAsStream()
                                      : Stream.Null;
            }
            return _responseStream;
        }

        /// <summary>
        /// Closes the response (and the underlying connection stream).
        /// </summary>
        public override void Close()
        {
            if (_disposed)
                return;
            _disposed = true;

            try
            {
                if (_responseStream != null)
                    _responseStream.Dispose();
            }
            catch
            {
                /* ignore */
            }
            _response.Dispose();
        }
    }
}

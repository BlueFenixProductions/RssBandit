#region CVS Version Header

/*
 * $Id$
 * Last modified by $Author$
 * Last modified at $Date$
 * $Revision$
 */

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;

using log4net;
using NewsComponents.Utils;
using RssBandit.Common;
using RssBandit.Common.Logging;

namespace NewsComponents.Net
{

    /// <summary>
    /// AsyncWebRequest class.
    /// </summary>
    public sealed class AsyncWebRequest: WebRequestBase
    {

        #region events

        /// <summary>
        /// Callback delegate used for OnAllRequestsComplete event.
        /// </summary>
        public delegate void RequestAllCompleteCallback();

        /// <summary>
        /// Event triggered, if all queued async. requests are done.
        /// </summary>
        public event RequestAllCompleteCallback OnAllRequestsComplete = null;

        /// <summary>
        /// Event triggered, if a not yet accepted CertificateIssue is raised by a web request.
        /// </summary>
        public static event EventHandler<CertificateIssueCancelEventArgs> OnCertificateIssue = null;

        #endregion

        #region private members

        /// <summary>
        /// Contains the url's as keys and the allowed (user interaction needed)
        /// CertificateIssue's within an ICollection as values.
        /// </summary>
        /// <remarks>That content should be maintained completely from within
        /// the OnCertificateIssue event.</remarks>
        private static Dictionary<string, IList<CertificateIssue>> _trustedCertificateIssues =
            new Dictionary<string, IList<CertificateIssue>>(5);


        private readonly Hashtable _queuedRequests;

        /// <summary>
        /// Scheduler which controls the maximum number of threads we use to perform concurrent HTTP requests
        /// </summary>
        private readonly PrioritizingTaskScheduler _scheduler;

        private readonly TaskFactory _taskFactory;

		private static readonly ILog Log = RssBandit.Common.Logging.Log.GetLogger(typeof(AsyncWebRequest));

        #endregion

        #region ctor's

        /// <summary>
        /// Constructor initialize a AsyncWebRequest instance
        /// </summary>
        public AsyncWebRequest()
        {
            _queuedRequests = Hashtable.Synchronized(new Hashtable(17));
            _scheduler = new PrioritizingTaskScheduler();
            _taskFactory = new TaskFactory(new CancellationTokenSource().Token,
                                           TaskCreationOptions.PreferFairness,
                                           TaskContinuationOptions.ExecuteSynchronously,
                                           _scheduler);
        }

        #endregion

        /// <summary>
        /// Gets the pending queued requests.
        /// </summary>
        public int PendingRequests
        {
            get
            {
                return _queuedRequests.Count;
            }
        }

        /// <summary>
        /// Contains the url's as keys and the allowed (user interaction needed)
        /// CertificateIssue's within an ICollection as values.
        /// </summary>
        /// <remarks>That content should be maintained completely from within
        /// the OnCertificateIssue event.</remarks>
        public static Dictionary<string, IList<CertificateIssue>> TrustedCertificateIssues
        {
            set
            {
                _trustedCertificateIssues = value;
            }
            get
            {
                return _trustedCertificateIssues;
            }
        }

        /// <summary>
        /// Used to make GET requests for multiple URLs asynchronously
        /// </summary>
        /// <param name="requests">The URLs to be fetched</param>
        /// <param name="webRequestStart">callback invoked when each GET request starts</param>
        /// <param name="webRequestComplete">callback invoked when each GET request completes</param>
        /// <param name="webRequestException">callback invoked when each GET request fails</param>
        /// <exception cref="NotSupportedException">The request scheme specified in address has not been registered.</exception>
        /// <exception cref="ArgumentNullException">The requestParameter is a null reference</exception>
        public void QueueRequestsAsync(List<RequestParameter> requests,
                                           RequestStartCallback webRequestStart,
                                           RequestCompleteCallback webRequestComplete,
                                           RequestExceptionCallback webRequestException)
        {
            const int priority = 10;  // needed for additional requests

            // Parallel options object specifies scheduler and max concurrent threads
            ParallelOptions options = new ParallelOptions() { TaskScheduler = _scheduler, MaxDegreeOfParallelism = _scheduler.MaximumConcurrencyLevel };

            // Create an instance of the RequestState and perform the HTTP request for each of the request parameters
            Parallel.ForEach(requests, options,
                request =>
                {
                    if (request != null && ! _queuedRequests.Contains(request.RequestUri.CanonicalizedUri()))
                    {
                        _queuedRequests.Add(request.RequestUri.CanonicalizedUri(), null);

                        var state = CreateState(request, priority);
                        state.WebRequestBeforeStart += webRequestStart;
                        state.WebRequestCompleted += webRequestComplete;
                        state.WebRequestException += webRequestException;

                        PerformHttpRequest(state);
                    }
                });

        }

        /// <summary>
        /// Creates the request state for a request parameter: HTTP(S) requests are
        /// performed natively over HttpClient (no WebRequest involved), file://
        /// keeps using the legacy WebRequest based stack.
        /// </summary>
        private RequestState CreateState(RequestParameter requestParameter, int priority)
        {
            if (requestParameter == null)
                throw new ArgumentNullException("requestParameter");

            if (IsHttpUri(requestParameter.RequestUri))
                return new RequestState(priority, requestParameter);

            var webRequest = PrepareRequest(requestParameter);
            return new RequestState(webRequest, priority, requestParameter);
        }

        private static bool IsHttpUri(Uri uri)
        {
            return uri != null &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Used to create a request for the legacy WebRequest based scheme
        /// (file://). HTTP(S) is handled natively over HttpClient.
        /// </summary>
        /// <param name="requestParameter">Could be modified for each subsequent request</param>
        internal WebRequest PrepareRequest(RequestParameter requestParameter)
        {
            if (requestParameter == null)
                throw new ArgumentNullException("requestParameter");

            // here are the exceptions caused:
#pragma warning disable SYSLIB0014
            WebRequest webRequest = WebRequest.Create(requestParameter.RequestUri);
#pragma warning restore SYSLIB0014

            FileWebRequest fileRequest = webRequest as FileWebRequest;

            if (fileRequest != null)
            {
                fileRequest.Timeout = DefaultTimeout;

                if (requestParameter.Credentials != null)
                {
                    fileRequest.Credentials = requestParameter.Credentials;
                }
            }
            else
            {
                throw new NotImplementedException("Unsupported WebRequest type: " + webRequest.GetType());
            }

            return webRequest;
        }

        /// <summary>
        /// Used to a queue an HTTP request for processing
        /// </summary>
        /// <param name="requestParameter"></param>
        /// <param name="webRequestComplete"></param>
        /// <param name="webRequestException"></param>
        /// <param name="webRequestStart"></param>
        /// <param name="priority"></param>
        /// <exception cref="NotSupportedException">The request scheme specified in address has not been registered.</exception>
        /// <exception cref="ArgumentNullException">The requestParameter is a null reference</exception>
        /// <exception cref="System.Security.SecurityException">The caller does not have permission to connect to the requested URI or a URI that the request is redirected to.</exception>
        internal RequestState QueueRequest(RequestParameter requestParameter,
                                           RequestStartCallback webRequestStart,
                                           RequestCompleteCallback webRequestComplete,
                                           RequestExceptionCallback webRequestException,
                                           int priority)
        {
            return
                DoQueueRequest(requestParameter, webRequestStart, webRequestComplete,
                             webRequestException, null, priority);
        }

        /// <summary>
        /// Used to a queue an HTTP request for processing
        /// </summary>
        /// <param name="requestParameter"></param>
        /// <param name="webRequestComplete"></param>
        /// <param name="webRequestException"></param>
        /// <param name="webRequestStart"></param>
        /// <param name="webRequestProgress"></param>
        /// <param name="priority"></param>
        /// <exception cref="NotSupportedException">The request scheme specified in address has not been registered.</exception>
        /// <exception cref="ArgumentNullException">The requestParameter is a null reference</exception>
        /// <exception cref="System.Security.SecurityException">The caller does not have permission to connect to the requested URI or a URI that the request is redirected to.</exception>
        internal RequestState QueueRequest(RequestParameter requestParameter,
                                           RequestStartCallback webRequestStart,
                                           RequestCompleteCallback webRequestComplete,
                                           RequestExceptionCallback webRequestException,
                                           RequestProgressCallback webRequestProgress,
                                           int priority)
        {
            return
                DoQueueRequest(requestParameter, webRequestStart, webRequestComplete,
                             webRequestException, webRequestProgress, priority);
        }

        /// <summary>
        /// Called for first web request.
        /// </summary>
        /// <param name="requestParameter">Could be modified for each subsequent request</param>
        /// <param name="webRequestComplete"></param>
        /// <param name="webRequestException"></param>
        /// <param name="webRequestStart"></param>
        /// <param name="webRequestProgress"></param>
        /// <param name="priority"></param>
        private RequestState DoQueueRequest(RequestParameter requestParameter,
                                           RequestStartCallback webRequestStart,
                                           RequestCompleteCallback webRequestComplete,
                                           RequestExceptionCallback webRequestException,
                                           RequestProgressCallback webRequestProgress,
                                           int priority)
        {
            if (requestParameter == null)
                throw new ArgumentNullException("requestParameter");

            if (_queuedRequests.Contains(requestParameter.RequestUri.CanonicalizedUri()))
                return null; // httpRequest already there

            _queuedRequests.Add(requestParameter.RequestUri.CanonicalizedUri(), null);

            RequestState state = CreateState(requestParameter, priority);

            state.WebRequestBeforeStart += webRequestStart;
            state.WebRequestCompleted += webRequestComplete;
            state.WebRequestException += webRequestException;
            state.WebRequestProgress += webRequestProgress;

            PerformHttpRequestAsync(state, priority);

            return state;
        }

        /// <summary>
        /// Called for subsequent requests.
        /// </summary>
        /// <param name="requestParameter">The request parameter.</param>
        /// <param name="priority">The priority.</param>
        /// <param name="prevState">State of the prev.</param>
        private void QueueRequestAgain(RequestParameter requestParameter,
                                           int priority, RequestState prevState)
        {
            if (requestParameter == null)
                throw new ArgumentNullException("requestParameter");

            if (prevState == null)
                throw new ArgumentNullException("prevState");

            RequestState state = prevState;

            IDisposable dispResponse = state.Response;
            if (dispResponse != null)
            {
                dispResponse.Dispose();
                state.Response = null;
            }

            if (state.HttpResponse != null)
            {
                state.HttpResponse.Dispose();
                state.HttpResponse = null;
            }

            if (state.ResponseStream != null)
            {
                // we don't want to get out of connections
                state.ResponseStream.Close();
            }

            if (state.Request != null)
            {
                state.Request.Credentials = null;

                // prevent NotImplementedExceptions:
                if (state.Request is HttpWebRequest)
                    state.Request.Abort();

                state.Request = null;
            }

            if (!IsHttpUri(requestParameter.RequestUri))
            {
                state.Request = PrepareRequest(requestParameter);
            }

            state.RequestParams = requestParameter;

            PerformHttpRequestAsync(state, priority);

        }


        /// <summary>
        /// Makes an asynchronous HTTP request using the provided RequestState object
        /// </summary>
        /// <param name="state">The HTTP request information</param>
        /// <param name="priority">The priority of the request</param>
        private void PerformHttpRequestAsync(RequestState state, int priority)
        {
            Task t = _taskFactory.StartNew(x =>
            {
                PerformHttpRequest((RequestState)x);
            }, state, CancellationToken.None, TaskCreationOptions.PreferFairness, _scheduler);

            //increase priority of the task if it needs to be performed quickly
            if (priority > 10)
                _scheduler.Prioritize(t);
        }

        /// <summary>
        /// Performs the HTTP request (synchron).
        /// </summary>
        /// <param name="state">The state.</param>
        private void PerformHttpRequest(RequestState state)
        {
            try
            {
                // next call returns true if the real request should be cancelled
                // (e.g. if no internet connection available)
                if (state.OnRequestBeforeStart())
                {
                    // signal this state to the worker class
                    this.RequestStartCancelled(state);
                    return;
                }
            }
            catch (Exception signalException)
            {
                Log.Error("Error during event dispatch of StartDownloadCallBack()", signalException);
            }

            state.StartTime = DateTime.Now;

            if (IsHttpUri(state.RequestParams.RequestUri))
            {
                PerformHttpClientRequest(state);
                return;
            }

            try
            {
                Log.Debug("calling GetResponse for " + state.Request.RequestUri);
                state.Response = state.Request.GetResponse();
            }
            catch (Exception responseException)
            {
                //For some reason the HttpWebResponse class throws an exception on 3xx responses
                WebException we = responseException as WebException;

                if ((we != null) && (we.Response != null))
                {
                    state.Response = we.Response;
                }
                else
                {
                    Log.Debug("GetResponse exception for " + state.Request.RequestUri, responseException);
                    state.OnRequestException(responseException);
                    this.FinalizeWebRequest(state);
                    return;
                }
            }

            ProcessResponse(state);

        }

        /// <summary>
        /// Performs a HTTP(S) request over a pooled HttpClient instance. Runs on a
        /// scheduler worker thread: the (genuinely async) send is awaited here in a
        /// blocking manner so the PrioritizingTaskScheduler keeps bounding the number
        /// of concurrent requests and the task priority semantics; the response body
        /// download then proceeds genuinely async (see ReadResponseAsync).
        /// </summary>
        private void PerformHttpClientRequest(RequestState state)
        {
            RequestParameter requestParameter = state.RequestParams;
            try
            {
                TimeSpan timeout = requestParameter.Timeout > TimeSpan.Zero
                                       ? requestParameter.Timeout
                                       : TimeSpan.FromMilliseconds(DefaultTimeout);
                if (requestParameter.ClientCertificate != null)
                    timeout += timeout; // double the timeout (SSL && Client Certs used!)

                HttpClient client = HttpClientCache.GetClient(requestParameter.Proxy,
                                                              requestParameter.Credentials,
                                                              requestParameter.RequestUri,
                                                              requestParameter.ClientCertificate);

                Log.Debug("sending request for " + requestParameter.RequestUri);

                using (HttpRequestMessage requestMessage = CreateRequestMessage(requestParameter))
                using (var timeoutSource = new CancellationTokenSource(timeout))
                {
                    try
                    {
                        TrustSelectedCertificatePolicy.CurrentRequestUri = requestParameter.RequestUri;
                        // We block this scheduler worker thread for the duration of the
                        // header phase (like the old HttpWebRequest.GetResponse() did):
                        state.HttpResponse = client.SendAsync(requestMessage,
                                                              HttpCompletionOption.ResponseHeadersRead,
                                                              timeoutSource.Token)
                                                   .GetAwaiter().GetResult();
                    }
                    catch (Exception sendException)
                    {
                        Exception translated = HttpClientCache.TranslateSendException(
                            sendException, requestParameter.RequestUri, timeoutSource.IsCancellationRequested);
                        Log.Debug("Send exception for " + requestParameter.RequestUri, sendException);
                        state.OnRequestException(translated);
                        FinalizeWebRequest(state);
                        return;
                    }
                    finally
                    {
                        TrustSelectedCertificatePolicy.CurrentRequestUri = null;
                    }
                }

                ProcessHttpClientResponse(state);
            }
            catch (Exception ex)
            {
                Log.Debug("PerformHttpClientRequest() exception: " + requestParameter.RequestUri + " :" + ex.Message);
                state.OnRequestException(state.InitialRequestUri, ex);
                FinalizeWebRequest(state);
            }
        }

        /// <summary>
        /// Builds the request message from the request parameter (conditional GET
        /// headers, user agent, cookies and custom headers travel per request).
        /// </summary>
        private static HttpRequestMessage CreateRequestMessage(RequestParameter requestParameter)
        {
            var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, requestParameter.RequestUri)
            {
                // negotiate HTTP/2 if available, fall back to HTTP/1.1:
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };

            request.Headers.TryAddWithoutValidation("User-Agent", FullUserAgent(requestParameter.UserAgent));

            if (requestParameter.Headers != null)
            {
                foreach (string header in requestParameter.Headers.AllKeys)
                {
                    request.Headers.TryAddWithoutValidation(header, requestParameter.Headers[header]);
                }
            }

            // due to the reported bug 893620 some web server fail with a server error 500
            // if we send DateTime.MinValue as IfModifiedSince. Some Unix derivates only know
            // about valid lowest DateTime around 1970, so we only send a header if above
            // our own minimum (1981):
            if (requestParameter.LastModified > MinValue)
            {
                request.Headers.IfModifiedSince = requestParameter.LastModified;
            }

            if (requestParameter.ETag != null)
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", requestParameter.ETag);
                request.Headers.TryAddWithoutValidation("A-IM", "feed");
            }

            if (requestParameter.Cookies != null)
            {
                var container = new CookieContainer();
                container.Add(requestParameter.Cookies);
                string cookieHeader = container.GetCookieHeader(requestParameter.RequestUri);
                if (!String.IsNullOrEmpty(cookieHeader))
                    request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }

            return request;
        }

        /// <summary>
        /// HttpResponseMessage processing - the HttpClient counterpart of ProcessResponse():
        /// handles success, not-modified, moved permanently/redirects, auth retries and
        /// failure statuses exactly the way the old HttpWebResponse handling did.
        /// </summary>
        private void ProcessHttpClientResponse(RequestState state)
        {
            try
            {
                HttpResponseMessage httpResponse = state.HttpResponse;
                Uri responseUri = (httpResponse.RequestMessage != null
                                       ? httpResponse.RequestMessage.RequestUri
                                       : null) ?? state.RequestParams.RequestUri;

                if (responseUri != state.RequestUri)
                {
                    Log.Debug(
                        String.Format("httpResponse.ResponseUri != state.RequestUri: \r\n'{0}'\r\n'{1}'",
                                      responseUri, state.RequestUri));
                }

                if (HttpStatusCode.OK == httpResponse.StatusCode ||
                    HttpExtendedStatusCode.IMUsed == (HttpExtendedStatusCode)httpResponse.StatusCode)
                {
                    // provide last request Uri and ETag:
                    state.RequestParams.ETag = GetETagHeader(httpResponse);
                    state.RequestParams.LastModified = GetLastModifiedHeader(httpResponse,
                                                                             state.RequestParams.LastModified);

                    state.ResponseStream = httpResponse.Content.ReadAsStream();
                    // genuinely async body download (frees this scheduler thread,
                    // like the old BeginRead/ReadCallback chain did):
                    _ = ReadResponseAsync(state);
                    // async read started, so we are done here:
                    Log.Debug("ProcessResponse() web response OK: " + state.RequestUri);

                    return;
                }

                if (httpResponse.StatusCode == HttpStatusCode.NotModified)
                {
                    string eTag = GetETagHeader(httpResponse);
                    // also if it was not modified, we receive a httpResponse.LastModified with current date!
                    // so we did not store it (is is just the same as last-retrived)
                    // provide last request Uri and ETag:
                    state.OnRequestCompleted(state.InitialRequestUri, state.RequestParams.RequestUri, eTag, MinValue,
                                             RequestResult.NotModified);
                    // cleanup:
                    FinalizeWebRequest(state);
                }
                else if ((httpResponse.StatusCode == HttpStatusCode.MovedPermanently)
                         || (httpResponse.StatusCode == HttpStatusCode.Moved))
                {
                    state.RetryCount++;
                    if (state.RetryCount > RequestState.MAX_RETRIES)
                    {
                        // there is no WebExceptionStatus.UnknownError in .NET 1.0 !!!
                        throw new WebException("Repeated HTTP httpResponse: " + httpResponse.StatusCode,
                                               null, WebExceptionStatus.RequestCanceled, null);
                    }

                    string url2 = GetLocationHeader(httpResponse);
                    state.MovedPermanently = true;
                    //Remove Url from queue
                    _queuedRequests.Remove(state.InitialRequestUri.CanonicalizedUri());

                    Log.Debug("ProcessResponse() Moved: '" + state.InitialRequestUri + " to " + url2);

                    // Enqueue the request with the new Url.
                    // We raise the queue priority a bit to get the retry request closer to the just
                    // finished one. So the user get better feedback, because the whole processing
                    // of one request (including the redirection/moved/... ) is visualized as one update
                    // action.


                    Uri req;
                    //Try absolute first
                    if (!Uri.TryCreate(url2, UriKind.Absolute, out req))
                    {
                        // Try relative
                        if (url2 == null || !Uri.TryCreate(responseUri, url2, out req))
                            throw new WebException(
                                string.Format(
                                    "Original resource temporary redirected. Request new resource at '{0}{1}' failed: ",
                                    responseUri, url2));
                    }

                    RequestParameter rqp = RequestParameter.Create(req, state.RequestParams);
                    QueueRequestAgain(rqp, state.Priority + 1, state);

                }
                else if (IsRedirect(httpResponse.StatusCode))
                {
                    state.RetryCount++;
                    if (state.RetryCount > RequestState.MAX_RETRIES)
                    {
                        // there is no WebExceptionStatus.UnknownError in .NET 1.0 !!!
                        throw new WebException("Repeated HTTP httpResponse: " + httpResponse.StatusCode,
                                               null, WebExceptionStatus.RequestCanceled, null);
                    }

                    string url2 = GetLocationHeader(httpResponse);
                    //Remove Url from queue
                    _queuedRequests.Remove(state.InitialRequestUri.CanonicalizedUri());

                    Log.Debug("ProcessResponse() Redirect: '" + state.InitialRequestUri + " to " + url2);
                    // Enqueue the request with the new Url.
                    // We raise the queue priority a bit to get the retry request closer to the just
                    // finished one. So the user get better feedback, because the whole processing
                    // of one request (including the redirection/moved/... ) is visualized as one update
                    // action.

                    Uri req;
                    //Try absolute first
                    if (!Uri.TryCreate(url2, UriKind.Absolute, out req))
                    {
                        // Try relative
                        if (url2 == null || !Uri.TryCreate(responseUri, url2, out req))
                            throw new WebException(
                                string.Format(
                                    "Original resource temporary redirected. Request new resource at '{0}{1}' failed: ",
                                    responseUri, url2));
                    }


                    RequestParameter rqp =
                        RequestParameter.Create(req, RebuildCredentials(state.RequestParams.Credentials, url2),
                                                state.RequestParams);
                    QueueRequestAgain(rqp, state.Priority + 1, state);

                }
                else if (IsUnauthorized(httpResponse.StatusCode))
                {
                    if (state.RequestParams.Credentials == null)
                    {
                        // no initial credentials, try with default credentials
                        state.RetryCount++;

                        //Remove Url from queue
                        _queuedRequests.Remove(state.InitialRequestUri.CanonicalizedUri());

                        // Enqueue the request with the new Url.
                        // We raise the queue priority a bit to get the retry request closer to the just
                        // finished one. So the user get better feedback, because the whole processing
                        // of one request (including the redirection/moved/... ) is visualized as one update
                        // action.
                        RequestParameter rqp =
                            RequestParameter.Create(CredentialCache.DefaultCredentials, state.RequestParams);
                        QueueRequestAgain(rqp, state.Priority + 1, state);

                    }
                    else
                    {
                        // failed with provided credentials

                        if (state.RequestParams.SetCookies)
                        {
                            // one more request without cookies

                            state.RetryCount++;

                            //Remove Url from queue
                            _queuedRequests.Remove(state.InitialRequestUri.CanonicalizedUri());

                            // Enqueue the request with the new Url.
                            // We raise the queue priority a bit to get the retry request closer to the just
                            // finished one. So the user get better feedback, because the whole processing
                            // of one request (including the redirection/moved/... ) is visualized as one update
                            // action.
                            RequestParameter rqp = RequestParameter.Create(false, state.RequestParams);
                            QueueRequestAgain(rqp, state.Priority + 1, state);

                        }
                        else
                        {
                            throw new ResourceAuthorizationException();
                        }
                    }
                }
                else if (IsAccessForbidden(httpResponse.StatusCode) &&
                         state.InitialRequestUri.Scheme == "https")
                {
                    throw new ClientCertificateRequiredException();
                }
                else if (httpResponse.StatusCode == HttpStatusCode.Gone)
                {
                    throw new ResourceGoneException();
                }
                else
                {
                    string statusDescription = httpResponse.ReasonPhrase;
                    if (String.IsNullOrEmpty(statusDescription))
                        statusDescription = httpResponse.StatusCode.ToString();

                    string htmlStatusMessage = null;
                    try
                    {
                        using (var reader = new StreamReader(httpResponse.Content.ReadAsStream()))
                            htmlStatusMessage = reader.ReadToEnd();
                    }
                    catch (Exception readEx)
                    {
                        Log.Debug("Could not read error response body for " + state.RequestUri, readEx);
                    }

                    if (String.IsNullOrEmpty(htmlStatusMessage))
							throw new WebException("Unexpected HTTP Response: " + statusDescription);

					if (htmlStatusMessage.Contains("<"))
		                    throw new WebException(htmlStatusMessage);

	                    throw new WebException(
							"<html><head><title>Unexpected HTTP Response</title></head><body><h2>Unexpected HTTP Response: " +
							statusDescription + "</h2><p>" + htmlStatusMessage + "</p></html>");
                }
            }
            catch (Exception ex)
            {
                Log.Debug("ProcessResponse() exception: " + state.RequestUri + " :" + ex.Message);
                state.OnRequestException(state.InitialRequestUri, ex);
                FinalizeWebRequest(state);
            }
        }

        /// <summary>
        /// Asynchronously reads the response content into the request state's buffer
        /// (memory stream), reporting progress with the same cadence as the old
        /// BeginRead/EndRead callback chain, then fires the completion callback.
        /// </summary>
        private async Task ReadResponseAsync(RequestState state)
        {
            Stream responseStream = state.ResponseStream;
            try
            {
                int read;
                while ((read = await responseStream
                                     .ReadAsync(state.ReadBuffer, 0, RequestState.BUFFER_SIZE)
                                     .ConfigureAwait(false)) > 0)
                {
                    state.BytesTransferred += read;
                    state.RequestData.Write(state.ReadBuffer, 0, read); // write buffer to mem stream

                    if (((state.BytesTransferred / RequestState.BUFFER_SIZE) % 10) == 0)
                    {
                        state.OnRequestProgress(state.InitialRequestUri, state.BytesTransferred);
                    }
                }

                // completed (stream yet deflated/unzipped, just reset pos.)
                state.ResponseStream = state.RequestData;
                state.ResponseStream.Seek(0, SeekOrigin.Begin);

                state.OnRequestCompleted(state.InitialRequestUri, state.RequestParams.RequestUri,
                                         state.RequestParams.ETag, state.RequestParams.LastModified,
                                         RequestResult.OK);
                // usual cleanup:
                responseStream.Close();
                state.RequestData.Close();
            }
            catch (Exception e)
            {
                Log.Error("ReadResponseAsync Exception raised", e);
                state.OnRequestException(state.RequestParams.RequestUri, e);
            }

            FinalizeWebRequest(state);
        }

        /// <summary>
        /// Gets the raw ETag response header (or null).
        /// </summary>
        private static string GetETagHeader(HttpResponseMessage response)
        {
            IEnumerable<string> values;
            if (response.Headers.TryGetValues("ETag", out values))
                return values.FirstOrDefault();
            return null;
        }

        /// <summary>
        /// Gets the raw Location response header (or null).
        /// </summary>
        private static string GetLocationHeader(HttpResponseMessage response)
        {
            IEnumerable<string> values;
            if (response.Headers.TryGetValues("Location", out values))
                return values.FirstOrDefault();
            return null;
        }

        /// <summary>
        /// Gets the Last-Modified header as local time. To keep the old
        /// HttpWebResponse.LastModified semantic: if the header is missing, the
        /// current date/time is returned; if it is present but unparsable, our own
        /// RFC2822 parser is tried (last chance) before keeping the previous value.
        /// </summary>
        private static DateTime GetLastModifiedHeader(HttpResponseMessage response, DateTime previousValue)
        {
            IEnumerable<string> values;
            string rawHeader = null;
            if (response.Content != null &&
                response.Content.Headers.TryGetValues("Last-Modified", out values))
            {
                rawHeader = values.FirstOrDefault();
            }

            if (String.IsNullOrEmpty(rawHeader))
            {
                // HttpWebResponse.LastModified returned DateTime.Now if the header was
                // missing - feeds without a Last-Modified header got their retrieval
                // time stored that way (used for the next conditional GET):
                return DateTime.Now;
            }

            DateTimeOffset? parsed = response.Content.Headers.LastModified;
            if (parsed.HasValue)
                return parsed.Value.LocalDateTime;

            // Built in header parser failed on provided date format
            // Try our own parser (last chance)
            try
            {
                return DateTimeExt.ParseRfc2822DateTime(rawHeader);
            }
            catch (FormatException)
            {
                /* ignore */
            }
            return previousValue;
        }

        /// <summary>
        /// Cancels the request
        /// </summary>
        /// <param name="state"></param>
        private void RequestStartCancelled(RequestState state)
        {
            if (state != null && !state.RequestFinalized)
            {
                Log.Info("RequestStart cancelled: " + state.RequestUri);
                state.OnRequestCompleted(state.RequestParams.ETag, state.RequestParams.LastModified,
                                         RequestResult.NotModified);
                _queuedRequests.Remove(state.InitialRequestUri.CanonicalizedUri());
                state.RequestFinalized = true;

                if (_queuedRequests.Count == 0)
                    RaiseOnAllRequestsComplete();
            }
        }

        /// <summary>
        /// Call it to cleanup any made request.
        /// </summary>
        /// <param name="state"></param>
        internal void FinalizeWebRequest(RequestState state)
        {
            if (state != null && !state.RequestFinalized)
            {
                Log.Debug("Request finalized. Request of '" + state.InitialRequestUri.CanonicalizedUri() + "' took " +
                           DateTime.Now.Subtract(state.StartTime) + " seconds");

                // ensure we close the resource so we do not get out of INet connections
                try
                {
                    if (state.ResponseStream != null)
                    {
                        state.ResponseStream.Close();
                    }

                    IDisposable dispResponse = state.Response;
                    if (dispResponse != null)
                    {
                        dispResponse.Dispose();
                        state.Response = null;
                    }

                    if (state.HttpResponse != null)
                    {
                        state.HttpResponse.Dispose();
                        state.HttpResponse = null;
                    }

                    if (state.Request != null)
                    {
                        state.Request.Credentials = null;

                        // prevent NotImplementedExceptions:
                        if (state.Request is HttpWebRequest)
                            state.Request.Abort();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("FinalizeWebRequest() caused exception", ex);
                }

                _queuedRequests.Remove(state.InitialRequestUri.CanonicalizedUri());
                state.RequestFinalized = true;

                if (_queuedRequests.Count == 0)
                    RaiseOnAllRequestsComplete();
            }
        }

        /// <summary>
        /// WebResponse processing (legacy WebRequest scheme: file://).
        /// </summary>
        private void ProcessResponse(RequestState state)
        {
            try
            {
                FileWebResponse fileResponse = state.Response as FileWebResponse;

                if (fileResponse != null)
                {
                    string reqFile = fileResponse.ResponseUri.LocalPath;

                    if (File.Exists(reqFile))
                    {
                        DateTime lwt = File.GetLastWriteTime(reqFile);
                        state.RequestParams.ETag = lwt.ToString();
                        state.RequestParams.LastModified = lwt;
                    }

                    state.ResponseStream = fileResponse.GetResponseStream();
                    state.ResponseStream.BeginRead(state.ReadBuffer, 0, RequestState.BUFFER_SIZE,
                                                   ReadCallback, state);
                    // async read started, so we are done here:
                    Log.Debug("ProcessResponse() file response OK: " + state.RequestUri);

                    return;
                }

                Debug.Assert(false,
                             "ProcessResponse(): unhandled WebResponse type: " +
                             state.Response.GetType());
                FinalizeWebRequest(state);
            }
            catch (ThreadAbortException)
            {
                FinalizeWebRequest(state);
                // ignore, just return
            }
            catch (Exception ex)
            {
                Log.Debug("ProcessResponse() exception: " + state.RequestUri + " :" + ex.Message);
                state.OnRequestException(state.InitialRequestUri, ex);
                FinalizeWebRequest(state);
            }
        }

        private static ICredentials RebuildCredentials(ICredentials credentials, string redirectUrl)
        {
            CredentialCache cc = credentials as CredentialCache;

            if (cc != null)
            {
                IEnumerator iterate = cc.GetEnumerator();
                while (iterate.MoveNext())
                {
                    NetworkCredential c = iterate.Current as NetworkCredential;
                    if (c != null)
                    {
                        // we just take the first one to recreate
                        string domainUser = c.Domain;
                        if (!string.IsNullOrEmpty(domainUser))
                            domainUser = domainUser + @"\";
                        domainUser = String.Concat(domainUser, c.UserName);
                        return FeedSource.CreateCredentialsFrom(redirectUrl, domainUser, c.Password);
                    }
                }
            }
            // give up/forward original credentials:
            return credentials;
        }

        /// <summary>
        /// Callback gets called (recursively) on subsequent response stream read requests
        /// (legacy WebRequest schemes only)
        /// </summary>
        /// <param name="result"></param>
        private void ReadCallback(IAsyncResult result)
        {
            RequestState state = null;
            if (result != null)
                state = result.AsyncState as RequestState;

            if (state == null)
                return;

            try
            {
                Stream responseStream = state.ResponseStream;
                int read = responseStream.EndRead(result);

                // fix at least one of the leaks in CLR 1.1 (and 1.0?)
                // see also http://dturini.blogspot.com/2004/06/on-past-few-days-im-dealing-with-some.html
                // and: http://support.microsoft.com/?kbid=831138
                if (Common.ClrVersion.Major < 2 && result.AsyncWaitHandle != null)
                    result.AsyncWaitHandle.Close();

                if (read > 0)
                {
                    state.BytesTransferred += read;
                    state.RequestData.Write(state.ReadBuffer, 0, read); // write buffer to mem stream, queue next read:
                    responseStream.BeginRead(state.ReadBuffer, 0, RequestState.BUFFER_SIZE,
                                             ReadCallback, state);

                    if (((state.BytesTransferred / RequestState.BUFFER_SIZE) % 10) == 0)
                    {
                        state.OnRequestProgress(state.InitialRequestUri, state.BytesTransferred);
                    }

                    // continue read:
                    return;
                }

                // completed (stream yet deflated/unzipped, just reset pos.)
                state.ResponseStream = state.RequestData;
                state.ResponseStream.Seek(0, SeekOrigin.Begin);

                state.OnRequestCompleted(state.InitialRequestUri, state.RequestParams.RequestUri,
                                         state.RequestParams.ETag, state.RequestParams.LastModified,
                                         RequestResult.OK);
                // usual cleanup:
                responseStream.Close();
                state.RequestData.Close();
            }
            catch (WebException e)
            {
                Log.Error("ReadCallBack WebException raised. Status: " + e.Status, e);
                state.OnRequestException(state.RequestParams.RequestUri, e);
            }
            catch (Exception e)
            {
                Log.Error("ReadCallBack Exception raised", e);
                state.OnRequestException(state.RequestParams.RequestUri, e);
            }

            FinalizeWebRequest(state);
        }

        private void RaiseOnAllRequestsComplete()
        {
            var handler = OnAllRequestsComplete;
            if (handler != null)
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    Log.Error("OnAllRequestsComplete() event impl. caused an error", ex);
                }
            }
        }

        internal static void RaiseOnCertificateIssue(object sender, CertificateIssueCancelEventArgs e)
        {
            string url = e.WebRequest.RequestUri.CanonicalizedUri();
            ICollection trusted = null;

            if (_trustedCertificateIssues != null)
            {
                lock (_trustedCertificateIssues)
                {
                    if (_trustedCertificateIssues.ContainsKey(url))
                        trusted = (ICollection)_trustedCertificateIssues[url];
                }
            }

            if (trusted != null && trusted.Count > 0)
            {
                if (trusted.Cast<CertificateIssue>().Any(trustedIssue => trustedIssue == e.CertificateIssue))
                {
                    e.Cancel = false; // is an yet accepted certificate isse
                    return;
                }
            }

            var handler = OnCertificateIssue;
            if (handler != null)
            {
                try
                {
                    handler(sender, e);
                }
                catch (Exception ex)
                {
                    Log.Error("OnCertificateIssue() event impl. caused an error", ex);
                }
            }
        }
    }

}

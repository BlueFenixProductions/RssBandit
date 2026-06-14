#region CVS Version Header

/*
 * $Id$
 * Last modified by $Author$
 * Last modified at $Date$
 * $Revision$
 */

#endregion

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using RssBandit.Common.Logging;

namespace NewsComponents.Net
{
    /// <summary>
    /// Downloads enclosure files over HTTP(S) by streaming the response body straight
    /// to disk, so arbitrarily large podcasts are never buffered in memory, with
    /// transparent resume of partially-downloaded files via HTTP range requests.
    /// <para>
    /// Replaced the COM-based BITS downloader on 2026-06-14: BITS was the only reason
    /// large enclosures could not go through managed HTTP (the old buffered path capped
    /// direct downloads at 15&#160;MB). The engine's pooled <see cref="HttpClientCache"/>
    /// handlers are reused for proxy, credential, client-certificate, decompression and
    /// certificate-trust handling. Because those handlers disable auto-redirect, this
    /// class follows redirects itself, the same way <see cref="AsyncWebRequest"/> does.
    /// </para>
    /// </summary>
    public sealed class HttpDownloader : IDownloader, IDisposable
    {
        #region constants / fields

        private static readonly ILog Logger = Log.GetLogger(typeof (HttpDownloader));

        /// <summary>Copy buffer size (80&#160;KiB) — large enough for efficient disk writes.</summary>
        private const int BufferSize = 81920;

        /// <summary>Raise a progress event roughly every quarter-megabyte transferred.</summary>
        private const long ProgressReportInterval = 256 * 1024;

        /// <summary>Upper bound on redirect hops while resolving an enclosure Url.</summary>
        private const int MaxRedirects = 10;

        /// <summary>
        /// Per-task cancellation sources. A single <see cref="HttpDownloader"/> instance is
        /// shared across registry-loaded tasks (see <c>DownloadRegistryManager.httpDownloader</c>),
        /// so cancellation must be tracked per <see cref="DownloadTask.TaskId"/> rather than in
        /// instance fields.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _active =
            new ConcurrentDictionary<Guid, CancellationTokenSource>();

        private bool _disposed;

        #endregion

        #region IDownloader events

        /// <summary>Notifies about the download progress for the update.</summary>
        public event EventHandler<DownloadTaskProgressEventArgs> DownloadProgress;

        /// <summary>Notifies that the downloading for a DownloadTask has started.</summary>
        public event EventHandler<TaskEventArgs> DownloadStarted;

        /// <summary>Notifies that the downloading for a DownloadTask has finished.</summary>
        public event EventHandler<TaskEventArgs> DownloadCompleted;

        /// <summary>Notifies that an error occurred while downloading the files for a DownloadTask.</summary>
        public event EventHandler<DownloadTaskErrorEventArgs> DownloadError;

        private void OnDownloadStarted(TaskEventArgs e)
        {
            DownloadStarted?.Invoke(this, e);
        }

        private void OnDownloadProgress(DownloadTaskProgressEventArgs e)
        {
            DownloadProgress?.Invoke(this, e);
        }

        private void OnDownloadCompleted(TaskEventArgs e)
        {
            DownloadCompleted?.Invoke(this, e);
        }

        private void OnDownloadError(DownloadTaskErrorEventArgs e)
        {
            DownloadError?.Invoke(this, e);
        }

        #endregion

        #region IDownloader implementation

        /// <summary>
        /// Synchronous download. Blocks until the file is fully downloaded, an error is
        /// raised, or <paramref name="maxWaitTime"/> elapses.
        /// </summary>
        /// <param name="task">The DownloadTask to process.</param>
        /// <param name="maxWaitTime">The maximum wait time (TimeSpan.Zero means no timeout).</param>
        public void Download(DownloadTask task, TimeSpan maxWaitTime)
        {
            if (CheckForResumeAndProceed(task))
                return;

            var cts = Register(task);
            if (maxWaitTime > TimeSpan.Zero)
                cts.CancelAfter(maxWaitTime);

            try
            {
                DownloadToFileAsync(task, cts.Token, cancelIsTimeout: true)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
            }
            finally
            {
                Unregister(task);
            }
        }

        /// <summary>
        /// Asynchronous download. Returns immediately; progress and completion are reported
        /// through the events.
        /// </summary>
        /// <param name="task">The DownloadTask to process.</param>
        public void BeginDownload(DownloadTask task)
        {
            if (CheckForResumeAndProceed(task))
                return;

            var cts = Register(task);
            CancellationToken token = cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await DownloadToFileAsync(task, token, cancelIsTimeout: false).ConfigureAwait(false);
                }
                finally
                {
                    Unregister(task);
                }
            });
        }

        /// <summary>
        /// Cancels an unfinished download. The partially downloaded file is kept on disk so a
        /// later attempt can resume it.
        /// </summary>
        /// <param name="task">The associated <see cref="DownloadTask"/>.</param>
        /// <returns>Always true.</returns>
        public bool CancelDownload(DownloadTask task)
        {
            if (task != null && _active.TryGetValue(task.TaskId, out CancellationTokenSource cts))
            {
                try { cts.Cancel(); }
                catch (ObjectDisposedException) { }
            }

            return true;
        }

        #endregion

        #region download core

        private async Task DownloadToFileAsync(DownloadTask task, CancellationToken token, bool cancelIsTimeout)
        {
            string targetFile = Path.Combine(task.DownloadFilesBase, task.DownloadItem.File.LocalName);
            string partialFile = targetFile + ".partial";

            try
            {
                OnDownloadStarted(new TaskEventArgs(task));

                var enclosureUri = new Uri(task.DownloadItem.Enclosure.Url);

                long resumeOffset = 0;
                if (File.Exists(partialFile))
                {
                    try { resumeOffset = new FileInfo(partialFile).Length; }
                    catch { resumeOffset = 0; }
                }

                using (HttpResponseMessage response =
                       await SendWithRedirectsAsync(task, enclosureUri, resumeOffset, token).ConfigureAwait(false))
                {
                    bool append;
                    if (response.StatusCode == HttpStatusCode.PartialContent)
                    {
                        // server honored our Range request: append to the existing .partial
                        append = true;
                    }
                    else
                    {
                        // 200 OK (no resume, or the server ignored the Range header): start over
                        response.EnsureSuccessStatusCode();
                        append = false;
                        resumeOffset = 0;
                    }

                    long bodyLength = response.Content.Headers.ContentLength ?? -1;
                    long totalSize = bodyLength >= 0
                                         ? bodyLength + (append ? resumeOffset : 0)
                                         : task.DownloadItem.Enclosure.Length;

                    long transferred = append ? resumeOffset : 0;
                    long sinceReport = 0;

                    using (Stream body = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                    using (var file = new FileStream(partialFile,
                                                     append ? FileMode.Append : FileMode.Create,
                                                     FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                    {
                        var buffer = new byte[BufferSize];
                        int read;
                        while ((read = await body.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                        {
                            await file.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                            transferred += read;
                            sinceReport += read;

                            if (sinceReport >= ProgressReportInterval)
                            {
                                sinceReport = 0;
                                OnDownloadProgress(new DownloadTaskProgressEventArgs(totalSize, transferred, 1, 0, task));
                            }
                        }

                        await file.FlushAsync(token).ConfigureAwait(false);
                    }

                    // final progress tick so the UI shows 100%
                    OnDownloadProgress(new DownloadTaskProgressEventArgs(totalSize, transferred, 1, 0, task));
                }

                // Atomically promote the completed .partial to the name the
                // BackgroundDownloadManager expects; it then moves it to TargetFolder.
                File.Move(partialFile, targetFile, overwrite: true);

                OnDownloadCompleted(new TaskEventArgs(task));
            }
            catch (OperationCanceledException) when (!cancelIsTimeout)
            {
                // user cancelled: keep the .partial file so the next attempt can resume
                Logger.InfoFormat("Enclosure download cancelled (partial kept for resume): {0}",
                                  task.DownloadItem.Enclosure.Url);
            }
            catch (OperationCanceledException ex)
            {
                OnDownloadError(new DownloadTaskErrorEventArgs(task,
                    new WebException(
                        string.Format("The enclosure download from '{0}' timed out.",
                                      task.DownloadItem.Enclosure.Url),
                        ex, WebExceptionStatus.Timeout, null)));
            }
            catch (Exception ex)
            {
                OnDownloadError(new DownloadTaskErrorEventArgs(task, ex));
            }
        }

        /// <summary>
        /// Issues the GET (with a Range header when resuming) and manually follows redirects,
        /// because the pooled <see cref="HttpClientCache"/> handlers have auto-redirect disabled.
        /// </summary>
        private static async Task<HttpResponseMessage> SendWithRedirectsAsync(DownloadTask task, Uri requestUri,
                                                                              long resumeOffset, CancellationToken token)
        {
            for (int hop = 0; ; hop++)
            {
                if (hop > MaxRedirects)
                    throw new WebException("Too many redirects while downloading " + requestUri);

                HttpClient client = HttpClientCache.GetClient(task.DownloadItem.Proxy, task.DownloadItem.Credentials,
                                                              requestUri, null);

                var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, requestUri);
                string userAgent = FeedSource.UserAgentString(string.Empty);
                if (!string.IsNullOrEmpty(userAgent))
                    request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                if (resumeOffset > 0)
                    request.Headers.Range = new RangeHeaderValue(resumeOffset, null);

                HttpResponseMessage response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

                int status = (int) response.StatusCode;
                if (status >= 300 && status < 400 && response.Headers.Location != null)
                {
                    Uri location = response.Headers.Location;
                    requestUri = location.IsAbsoluteUri ? location : new Uri(requestUri, location);
                    response.Dispose();
                    continue;
                }

                return response;
            }
        }

        private static bool CheckForResumeAndProceed(DownloadTask task)
        {
            return task != null && task.DownloadErrorResumeCount >= BackgroundDownloadManager.MaxDownloadErrorResumes;
        }

        private CancellationTokenSource Register(DownloadTask task)
        {
            var cts = new CancellationTokenSource();
            // replace (and tear down) any prior source still tracked for this task
            if (_active.TryRemove(task.TaskId, out CancellationTokenSource previous))
            {
                try { previous.Cancel(); }
                catch (ObjectDisposedException) { }
                previous.Dispose();
            }
            _active[task.TaskId] = cts;
            return cts;
        }

        private void Unregister(DownloadTask task)
        {
            if (_active.TryRemove(task.TaskId, out CancellationTokenSource cts))
                cts.Dispose();
        }

        #endregion

        #region IDisposable implementation

        /// <summary>
        /// Cancels any in-flight downloads and releases their cancellation sources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            foreach (var kvp in _active)
            {
                try { kvp.Value.Cancel(); }
                catch (ObjectDisposedException) { }
                kvp.Value.Dispose();
            }

            _active.Clear();
        }

        #endregion
    }
}

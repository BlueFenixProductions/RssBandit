using System;
using System.IO;
using System.Net;
using System.Threading;
using NewsComponents;
using NewsComponents.Net;
using NUnit.Framework;
using RssBandit.UnitTests;

namespace NewsComponents.UnitTests
{
    /// <summary>
    /// Exercises the managed streaming <see cref="HttpDownloader"/> that replaced the
    /// COM-based BITS downloader: a real enclosure is downloaded from the local test
    /// web server and its bytes are compared against the served file.
    /// </summary>
    [TestFixture]
    public class HttpDownloaderTests : WebServerTestFixture
    {
        private const string ROOT_URL = "http://127.0.0.1:8081/";
        private const string ENCLOSURE_PATH = "NewsHandlerTestFiles/LocalTestFeed.xml";

        private string _downloadDir;

        [SetUp]
        protected override void SetUp()
        {
            DeleteDirectory(UNPACK_DESTINATION);
            UnpackResourceDirectory("WebRoot");
            UnpackResourceDirectory("WebRoot.NewsHandlerTestFiles");
            base.SetUp();

            _downloadDir = Path.Combine(Path.GetTempPath(),
                "RssBanditHttpDownloaderTests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_downloadDir);
        }

        [TearDown]
        protected override void TearDown()
        {
            base.TearDown();
            try
            {
                if (Directory.Exists(_downloadDir))
                    Directory.Delete(_downloadDir, true);
            }
            catch { /* best-effort cleanup */ }
        }

        [Test]
        public void Download_streams_enclosure_to_disk()
        {
            DownloadTask task = CreateTask(ROOT_URL + ENCLOSURE_PATH);

            Exception error = null;
            var finished = new ManualResetEvent(false);

            using (var downloader = new HttpDownloader())
            {
                downloader.DownloadCompleted += (s, e) => finished.Set();
                downloader.DownloadError += (s, e) => { error = e.Exception; finished.Set(); };

                downloader.BeginDownload(task);

                Assert.IsTrue(finished.WaitOne(TimeSpan.FromSeconds(30)),
                    "the download did not finish within the timeout");
            }

            Assert.IsNull(error, "the download raised an error: " + error);

            string downloadedFile = Path.Combine(_downloadDir, task.DownloadItem.File.LocalName);
            Assert.IsTrue(File.Exists(downloadedFile),
                "expected the enclosure on disk at " + downloadedFile);

            string servedFile = Path.Combine(WEBROOT_PATH, "NewsHandlerTestFiles", "LocalTestFeed.xml");
            CollectionAssert.AreEqual(File.ReadAllBytes(servedFile), File.ReadAllBytes(downloadedFile),
                "the downloaded bytes differ from the served file");

            Assert.IsFalse(File.Exists(downloadedFile + ".partial"),
                "the .partial scratch file should be gone after a successful download");
        }

        [Test]
        public void Download_raises_error_for_missing_enclosure()
        {
            DownloadTask task = CreateTask(ROOT_URL + "NewsHandlerTestFiles/this-does-not-exist.bin");

            Exception error = null;
            bool completed = false;
            var finished = new ManualResetEvent(false);

            using (var downloader = new HttpDownloader())
            {
                downloader.DownloadCompleted += (s, e) => { completed = true; finished.Set(); };
                downloader.DownloadError += (s, e) => { error = e.Exception; finished.Set(); };

                downloader.BeginDownload(task);

                Assert.IsTrue(finished.WaitOne(TimeSpan.FromSeconds(30)),
                    "the download did not finish within the timeout");
            }

            Assert.IsFalse(completed, "a 404 enclosure must not report completion");
            Assert.IsNotNull(error, "a 404 enclosure must raise a download error");
        }

        private DownloadTask CreateTask(string url)
        {
            var info = new TestDownloadInfo(_downloadDir);
            var enclosure = new Enclosure("application/octet-stream", 0, url, "test enclosure");
            var item = new DownloadItem("test-feed", "test-item", enclosure, info);
            return new DownloadTask(item, info);
        }

        /// <summary>
        /// Minimal <see cref="IDownloadInfoProvider"/> that downloads straight into a
        /// throw-away directory with no proxy or credentials.
        /// </summary>
        private sealed class TestDownloadInfo : IDownloadInfoProvider
        {
            private readonly string _directory;

            public TestDownloadInfo(string directory)
            {
                _directory = directory;
            }

            public IWebProxy Proxy
            {
                get { return null; }
            }

            public string InitialDownloadLocation
            {
                get { return _directory; }
            }

            public string GetTargetFolder(DownloadItem item)
            {
                return _directory;
            }

            public ICredentials GetCredentials(DownloadItem item)
            {
                return null;
            }
        }
    }
}

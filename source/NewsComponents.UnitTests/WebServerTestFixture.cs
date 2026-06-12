using System;
using System.Collections.Generic;
using System.IO;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using WireMock.Types;
using WireMock.Util;

namespace RssBandit.UnitTests
{
	/// <summary>
	/// Base test fixture that hosts a local HTTP server (WireMock.Net) serving the
	/// unpacked WebRoot test resources. Replaces the ancient Cassini web server,
	/// which depended on System.Web and cannot run on modern .NET.
	/// </summary>
	/// <remarks>
	/// Files are read from disk per request (not stubbed up-front) because some
	/// tests write new files into the web root mid-test and then fetch them.
	/// <c>/FailWithStatus.aspx?code=NNN</c> responds with the given HTTP status,
	/// mirroring the legacy ASPX helper page.
	/// </remarks>
	public class WebServerTestFixture : BaseTestFixture
	{
		private WireMockServer _webServer;
		private readonly int _webServerPort = 8081;
		private string _webrootPath;

		/// <summary>
		/// Starts the local web server on port 8081 serving WEBROOT_PATH.
		/// Call this from within a method marked with the [SetUp] attribute.
		/// </summary>
		protected virtual void SetUp()
		{
			_webrootPath = WEBROOT_PATH;

			// "localhost" makes Kestrel bind both loopback interfaces, so tests can
			// reach the server as 127.0.0.1 (same-server cases) AND as localhost
			// (different-host cases, e.g. external-feed-link discovery)
			_webServer = WireMockServer.Start(new WireMockServerSettings
			{
				Urls = new[] { $"http://localhost:{_webServerPort}/" }
			});

			// legacy FailWithStatus.aspx behavior: return the status code given in the query
			_webServer
				.Given(Request.Create().WithPath("/FailWithStatus.aspx").UsingGet())
				.AtPriority(1)
				.RespondWith(Response.Create().WithCallback(request =>
				{
					int statusCode = 500;
					if (request.Query != null &&
					    request.Query.TryGetValue("code", out var codeValues) &&
					    codeValues.Count > 0)
					{
						int.TryParse(codeValues[0], out statusCode);
					}
					return new ResponseMessage { StatusCode = statusCode };
				}));

			// catch-all: serve static files from the unpacked web root, 404 when absent
			_webServer
				.Given(Request.Create().WithPath("/*").UsingGet())
				.AtPriority(100)
				.RespondWith(Response.Create().WithCallback(ServeWebRootFile));

			Console.WriteLine("Web server started on port {0} serving {1}", _webServerPort, _webrootPath);
		}

		/// <summary>
		/// Stops the web server. Call this from a method marked with the [TearDown] attribute.
		/// </summary>
		protected virtual void TearDown()
		{
			try
			{
				if (_webServer != null)
				{
					_webServer.Stop();
					_webServer.Dispose();
					_webServer = null;
					Console.WriteLine("Web server shutdown succeeds");
				}
			}
			catch { /* best-effort cleanup */ }
		}

		private ResponseMessage ServeWebRootFile(IRequestMessage request)
		{
			string relativePath = Uri.UnescapeDataString(request.Path ?? string.Empty)
				.TrimStart('/')
				.Replace('/', Path.DirectorySeparatorChar);

			string fullPath = Path.Combine(_webrootPath, relativePath);

			// no escaping the web root
			if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(_webrootPath), StringComparison.OrdinalIgnoreCase) ||
			    !File.Exists(fullPath))
			{
				return new ResponseMessage { StatusCode = 404 };
			}

			return new ResponseMessage
			{
				StatusCode = 200,
				Headers = new Dictionary<string, WireMockList<string>>
				{
					["Content-Type"] = new WireMockList<string>(GetContentType(fullPath))
				},
				BodyData = new BodyData
				{
					DetectedBodyType = BodyType.Bytes,
					BodyAsBytes = File.ReadAllBytes(fullPath)
				}
			};
		}

		private static string GetContentType(string path)
		{
			switch (Path.GetExtension(path).ToLowerInvariant())
			{
				case ".htm":
				case ".html":
					return "text/html";
				case ".xml":
				case ".rss":
				case ".rdf":
					return "text/xml";
				default:
					return "application/octet-stream";
			}
		}
	}
}

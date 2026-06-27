using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Xsl;
using NewsComponents.Feed;
using NewsComponents.Formatting;
using NUnit.Framework;
using RssBandit.UnitTests;

namespace NewsComponents.UnitTests
{
	/// <summary>
	/// Phase C1 Task 2 characterization test: proves the <see cref="NewsItemFormatter"/> relocated
	/// into the portable engine (<c>NewsComponents.Formatting</c>) plus its
	/// <see cref="DefaultNewsItemLocalizer"/> reproduce the head formatter's HTML byte-for-byte against
	/// the goldens captured in Task 1.
	/// </summary>
	/// <remarks>
	/// 8 cases = 4 deterministic fixtures (<see cref="NewsItemFormatterFixtures.BuildAll"/>) x 2
	/// templates (default + search). Each renders with the DEFAULT localizer (NOT the head's
	/// <c>SrNewsItemLocalizer</c>), masks the volatile local pubDate exactly as Task 1 did, normalizes
	/// newlines, and asserts equality against the embedded golden.
	/// </remarks>
	[TestFixture]
	public class NewsItemFormatterTests : BaseTestFixture
	{
		private CultureInfo _origCulture;
		private CultureInfo _origUICulture;

		[SetUp]
		public void PinInvariantCulture()
		{
			// Pin invariant culture so any culture-sensitive rendering matches Task 1's capture
			// (the head was captured under InvariantCulture). The default localizer returns the same
			// neutral English strings regardless, but we mirror Task 1 exactly.
			_origCulture = CultureInfo.CurrentCulture;
			_origUICulture = CultureInfo.CurrentUICulture;
			CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
			CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
		}

		[TearDown]
		public void RestoreCulture()
		{
			CultureInfo.CurrentCulture = _origCulture;
			CultureInfo.CurrentUICulture = _origUICulture;
		}

		/// <summary>4 fixtures x 2 templates = 8 byte-snapshot cases.</summary>
		private static IEnumerable<TestCaseData> Cases()
		{
			foreach (var (name, item) in NewsItemFormatterFixtures.BuildAll())
			{
				yield return new TestCaseData(name, item, string.Empty, "default")
					.SetName("NewsItemFormatter_" + name + "_default");
				yield return new TestCaseData(name, item, NewsItemFormatter.SearchTemplateId, "search")
					.SetName("NewsItemFormatter_" + name + "_search");
			}
		}

		[TestCaseSource(nameof(Cases))]
		public void RelocatedFormatterRendersByteIdenticalGolden(
			string name, INewsItem item, string templateId, string templateLabel)
		{
			// Default ctor registers the default template at String.Empty and the search template at
			// SearchTemplateId, and defaults Localizer to DefaultNewsItemLocalizer (neutral English).
			var formatter = new NewsItemFormatter();

			// Brand-new EMPTY argument list per render (the single-item template references no params).
			string html = formatter.ToHtml(templateId, item, new XsltArgumentList());

			// Mask the volatile, TZ-dependent local pubDate EXACTLY as Task 1 did.
			string renderedDate = item.Date.ToLocalTime().ToString("F", DateTimeFormatInfo.InvariantInfo);
			html = html.Replace(renderedDate, "__PUBDATE__");

			string expected = UnpackResource(
				"Expected.NewsItemFormatter." + name + "." + templateLabel + ".html");

			Assert.AreEqual(
				Normalize(expected), Normalize(html),
				"Relocated NewsItemFormatter output diverged from the Task 1 golden for '" +
				name + "." + templateLabel + "'.");
		}

		/// <summary>Normalizes newlines to LF so the comparison is line-ending agnostic.</summary>
		private static string Normalize(string s)
		{
			return s.Replace("\r\n", "\n").Replace("\r", "\n");
		}
	}
}

using System;
using System.Collections.Generic;
using NewsComponents.Feed;
using NewsComponents.Utils;

namespace NewsComponents.UnitTests
{
	/// <summary>
	/// Deterministic <see cref="NewsItem"/> fixtures SHARED between the Phase C1 golden-capture
	/// harness (which pins today's head <c>NewsItemFormatter</c> output) and the Phase C1 Task 2
	/// characterization test (which asserts the relocated core formatter reproduces the SAME goldens).
	/// </summary>
	/// <remarks>
	/// Determinism rules — DO NOT change without re-capturing the goldens:
	/// <list type="bullet">
	/// <item>Every item gets an EXPLICIT <c>Id</c> (a stable urn). Without it <see cref="NewsItem"/>
	/// falls back to a content hash (<c>title.GetHashCode()+content.GetHashCode()</c>) for its id, so
	/// the rendered <c>postid</c> would be non-deterministic across runs/runtimes.</item>
	/// <item>The id is deliberately DIFFERENT from the link, so the serialized <c>&lt;guid&gt;</c>
	/// element is emitted and the template's <c>postid</c> resolves to the explicit id.</item>
	/// <item>Dates are pinned to a fixed RFC2822 string (parsed to UTC by <see cref="DateTimeExt"/>).</item>
	/// <item>Links / ids / enclosure URLs are absolute so <c>HtmlHelper.ConvertToAbsoluteUrl</c> /
	/// <c>ExpandRelativeUrls</c> leave them untouched.</item>
	/// <item><c>FeedDetails</c> is a fixed <see cref="FeedInfo"/> so the NewsPaper channel/title is stable.</item>
	/// </list>
	/// Note: the rendered HTML embeds the volatile, time-zone-dependent local pubDate
	/// (<c>Date.ToLocalTime().ToString("F", InvariantInfo)</c>). The capture harness AND Task 2 must
	/// mask it identically by replacing that exact substring with the token <c>__PUBDATE__</c>.
	/// </remarks>
	public static class NewsItemFormatterFixtures
	{
		/// <summary>Fixed publication date for every fixture (RFC2822 string, parsed to UTC).</summary>
		public const string PinnedRfc2822Date = "Fri, 04 Apr 2003 10:41:37 GMT";

		private static FeedInfo CreateFeedDetails()
		{
			return new FeedInfo("", null, null, "FeedTitle", "http://feed.invalid/", "FeedDescription");
		}

		/// <summary>
		/// Builds the deterministic fixture set. Each tuple's <c>name</c> is the golden file stem.
		/// </summary>
		public static IEnumerable<(string name, INewsItem item)> BuildAll()
		{
			DateTime date = DateTimeExt.ParseRfc2822DateTime(PinnedRfc2822Date);

			// 1. plain - simple title + plain-text content + link.
			var plain = new NewsItem(
				null, "Plain Title", "http://example.invalid/plain",
				"This is plain text content.", date, "TestSubject",
				"urn:fixture:plain", null);
			plain.FeedDetails = CreateFeedDetails();
			yield return ("plain", plain);

			// 2. encoded - HTML markup body content (content:encoded style).
			var encoded = new NewsItem(
				null, "Encoded Title", "http://example.invalid/encoded",
				"<p>This is <b>HTML</b> content with a <a href=\"http://link.invalid/\">link</a>.</p>",
				date, "TestSubject", "urn:fixture:encoded", null);
			encoded.FeedDetails = CreateFeedDetails();
			yield return ("encoded", encoded);

			// 3. enclosure - has an Enclosure (podcast media).
			var enclosure = new NewsItem(
				null, "Enclosure Title", "http://example.invalid/enclosure",
				"Item with an enclosure.", date, "TestSubject",
				"urn:fixture:enclosure", null);
			enclosure.FeedDetails = CreateFeedDetails();
			enclosure.Enclosures = new List<IEnclosure>
			{
				new Enclosure("audio/mpeg", 123456, "http://podcast.invalid/episode1.mp3", "Episode 1")
			};
			yield return ("enclosure", enclosure);

			// 4. authored - dc:creator/author + flagged + read state.
			var authored = new NewsItem(
				null, "Authored Title", "http://example.invalid/authored",
				"Content by an author.", date, "TestSubject",
				"urn:fixture:authored", null);
			authored.FeedDetails = CreateFeedDetails();
			authored.Author = "Jane Author";
			authored.FlagStatus = Flagged.FollowUp;
			authored.BeenRead = true;
			yield return ("authored", authored);
		}
	}
}

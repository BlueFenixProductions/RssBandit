using System;
using System.Globalization;
using System.Text;
using Lucene.Net.Documents;
using NewsComponents.Utils;

#pragma warning disable CS0618 // Type or member is obsolete

namespace NewsComponents.Search
{ 
	/// <summary>
	/// A utility class for making Lucene Documents from a NewsItem.
	/// </summary>	
	public class LuceneNewsItemSearch {

		private LuceneNewsItemSearch(){}

		/// <summary>
		/// Creates a document for a NewsItem.
		/// </summary>
		internal static Document Document(INewsItem item) {
			
			// make a new, empty document
			Document doc = new Document();
			
			// NOTE:
			// Field.Keyword() are added and indexed (searchable), but not tokenized
			// Field.Text() are added, indexed (searchable) and tokenized  
			// Field.UnIndexed() are added only

			#region NewsItem fields/keywords

			// Add the uid and feed ID as a field, so that index can be 
			// incrementally maintained. This fields is stored with document,
			// are indexed, but not tokenized prior to indexing.
			// uid can be used to remove/refresh a single feed item,
			// while feedID can be used to remove the indexed docs for a feed
			// at once:
			doc.Add(Field.Keyword(LuceneSearch.IndexDocument.ItemID, UID(item)));
			doc.Add(Field.Keyword(LuceneSearch.IndexDocument.FeedID, item.Feed.id));
			
			doc.Add(Field.Keyword(LuceneSearch.Keyword.ItemLink, CheckNull(item.Link)));
			// remember, the item.Date is UTC here.
			// The format equals the old Lucene 2.9 DateTools.TimeToString(ticks, Resolution.MINUTE)
			// output ("yyyyMMddHHmm"), so term/prefix/range date queries keep working unchanged:
			doc.Add(Field.Keyword(LuceneSearch.Keyword.ItemDate,
				item.Date.ToString(DateIndexFormat, CultureInfo.InvariantCulture)));
			doc.Add(Field.Text(LuceneSearch.Keyword.ItemTitle, CheckNull(item.Title)));
			doc.Add(Field.Text(LuceneSearch.Keyword.ItemAuthor, CheckNull(item.Author)));
			doc.Add(Field.Text(LuceneSearch.Keyword.ItemTopic, CheckNull(item.Subject)));

			if (item.HasContent) 
			{
				StringBuilder content = new StringBuilder(HtmlHelper.StripAnyTags(item.Content));

				// Add the summary as an UnIndexed field, so that it is stored and returned
				// with hit documents for display.
				doc.Add(Field.UnIndexed(LuceneSearch.IndexDocument.ItemSummary, StringHelper.GetFirstWords(content.ToString(), 50)));

				// append the previous stripped outgoing links to get indexed/tokenized:
				foreach (var link in item.OutGoingLinks)
					content.AppendFormat(" {0}", link.Url);	

				// simple text content:
				doc.Add(Field.Text(LuceneSearch.IndexDocument.ItemContent, content.ToString()));

			} else {
				doc.Add(Field.Text(LuceneSearch.IndexDocument.ItemContent, CheckNull(item.Title)));
				doc.Add(Field.UnIndexed(LuceneSearch.IndexDocument.ItemSummary, StringHelper.GetFirstWords(item.Title, 50)));
			}

			#endregion

			#region Feed fields/keywords

			doc.Add(Field.Keyword(LuceneSearch.Keyword.FeedLink, CheckNull(item.FeedLink)));
			doc.Add(Field.Keyword(LuceneSearch.Keyword.FeedUrl, CheckNull(item.FeedDetails.Link)));
			doc.Add(Field.Text(LuceneSearch.Keyword.FeedTitle, CheckNull(item.FeedDetails.Title)));
			doc.Add(Field.Keyword(LuceneSearch.Keyword.FeedType, item.FeedDetails.Type.ToString()));
			// is not really required (thought to be used for scoped searches)
			//doc.Add(Field.Keyword(LuceneSearch.Keyword.FeedCategory, CheckNull(item.Feed.category)));
			doc.Add(Field.Text(LuceneSearch.Keyword.FeedDescription, CheckNull(item.FeedDetails.Description)));

			#endregion

			// return the document
			return doc;
		}
		
		// lucene 4.8:
		class Field
		{
			/// <summary>
			/// Field.Keyword() are stored and indexed (searchable), but not tokenized
			/// (old Lucene 2.x: Store.YES + Index.UN_TOKENIZED)
			/// </summary>
			/// <param name="name">The name.</param>
			/// <param name="value">The value.</param>
			/// <returns></returns>
			public static Lucene.Net.Documents.Field Keyword(string name, string value) {
				return new StringField(name, value, Lucene.Net.Documents.Field.Store.YES);
			}
			/// <summary>
			/// Field.Text() are stored, indexed (searchable) and tokenized
			/// (old Lucene 2.x: Store.YES + Index.TOKENIZED)
			/// </summary>
			/// <param name="name">The name.</param>
			/// <param name="value">The value.</param>
			/// <returns></returns>
			public static Lucene.Net.Documents.Field Text(string name, string value) {
				return new TextField(name, value, Lucene.Net.Documents.Field.Store.YES);
			}
			/// <summary>
			/// Field.UnIndexed() are stored only
			/// (old Lucene 2.x: Store.YES + Index.NO)
			/// </summary>
			/// <param name="name">The name.</param>
			/// <param name="value">The value.</param>
			/// <returns></returns>
			public static Lucene.Net.Documents.Field UnIndexed(string name, string value) {
				return new StoredField(name, value);
			}
		}

		/// <summary>
		/// The (invariant culture) date(time) format used to index item dates.
		/// It matches the old Lucene 2.9 DateTools.TimeToString(ticks, Resolution.MINUTE) format.
		/// </summary>
		internal const string DateIndexFormat = "yyyyMMddHHmm";

		/// <summary>
		/// Converts a Date to a string suitable for indexing.
		/// </summary>
		/// <remarks>The date is expected to be UTC (as item dates are).</remarks>
		public static System.String DateToString(System.DateTime date) {
			return date.ToString(DateIndexFormat, CultureInfo.InvariantCulture);
		}
		
		private static string CheckNull(string s) {
			if (s == null) return String.Empty;
			return s;
		}

		internal static char UrlPathSeparator = '/';
		internal static char UnicodeNullChar = '\u0000';
		
		public static string UID(INewsItem item) {
			string s = String.Concat(item.Feed.id, UnicodeNullChar, item.Id.Replace(UrlPathSeparator, UnicodeNullChar));
			return s;
		}
		
		public static string NewsItemIDFromUID(string uid) {
			return uid.Substring(1+uid.IndexOf(UnicodeNullChar)).Replace(UnicodeNullChar, UrlPathSeparator);
		}
		public static string FeedIDFromUID(string uid) {
			return uid.Substring(0, uid.IndexOf(UnicodeNullChar));
		}
	}
}

#pragma warning restore CS0618 // Type or member is obsolete

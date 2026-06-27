namespace NewsComponents.Formatting
{
	/// <summary>
	/// Supplies the localized strings that the news-item XSLT templates embed via the
	/// <c>urn:localization-extension</c> extension object (publisher / author / enclosure labels,
	/// toggle-state alt text, and the group/paging navigation text).
	/// </summary>
	/// <remarks>
	/// This seam keeps the portable engine self-sufficient (its <see cref="DefaultNewsItemLocalizer"/>
	/// returns the neutral English strings) while still letting the WinForms head inject an adapter
	/// over its full multi-culture <c>SR</c> string table.
	/// </remarks>
	public interface INewsItemLocalizer
	{
		/// <summary>Gets the localized "Related Links" text.</summary>
		string RelatedLinksText();

		/// <summary>Gets the localized "Previous" (page) text.</summary>
		string PreviousPageText();

		/// <summary>Gets the localized "Next" (page) text.</summary>
		string NextPageText();

		/// <summary>Gets the localized "Displaying page" text.</summary>
		string DisplayingPageText();

		/// <summary>Gets the localized "of" (page X of Y) text.</summary>
		string PageOfText();

		/// <summary>Gets the localized item publisher text.</summary>
		string ItemPublisherText();

		/// <summary>Gets the localized item author text.</summary>
		string ItemAuthorText();

		/// <summary>Gets the localized item date text.</summary>
		string ItemDateText();

		/// <summary>Gets the localized item enclosure text.</summary>
		string ItemEnclosureText();

		/// <summary>Gets the localized text to indicate toggle of flag state.</summary>
		string ToggleFlagStateText();

		/// <summary>Gets the localized text to indicate toggle of read state.</summary>
		string ToggleReadStateText();

		/// <summary>Gets the localized text to indicate toggle of watched state.</summary>
		string ToggleWatchStateText();
	}
}

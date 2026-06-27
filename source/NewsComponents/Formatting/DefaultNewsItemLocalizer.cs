namespace NewsComponents.Formatting
{
	/// <summary>
	/// Default <see cref="INewsItemLocalizer"/> returning neutral English strings.
	/// </summary>
	/// <remarks>
	/// Each value is copied VERBATIM from the WinForms head's neutral <c>SR.resx</c>
	/// <c>XsltDefaultTemplate_*</c> entries, so the portable engine reproduces the head's neutral
	/// (invariant-culture) HTML output byte-for-byte. DO NOT change these strings without re-capturing
	/// the Phase C1 golden HTML.
	/// </remarks>
	public sealed class DefaultNewsItemLocalizer : INewsItemLocalizer
	{
		/// <inheritdoc />
		public string RelatedLinksText() { return "Related Links"; }

		/// <inheritdoc />
		public string PreviousPageText() { return "Previous"; }

		/// <inheritdoc />
		public string NextPageText() { return "Next"; }

		/// <inheritdoc />
		public string DisplayingPageText() { return "Displaying page"; }

		/// <inheritdoc />
		public string PageOfText() { return "of"; }

		/// <inheritdoc />
		public string ItemPublisherText() { return "Publisher"; }

		/// <inheritdoc />
		public string ItemAuthorText() { return "Author"; }

		/// <inheritdoc />
		public string ItemDateText() { return "posted at"; }

		/// <inheritdoc />
		public string ItemEnclosureText() { return "Enclosure"; }

		/// <inheritdoc />
		public string ToggleFlagStateText() { return "Set or clear flag"; }

		/// <inheritdoc />
		public string ToggleReadStateText() { return "Mark read or unread"; }

		/// <inheritdoc />
		public string ToggleWatchStateText() { return "Watch or stop watching comments"; }
	}
}

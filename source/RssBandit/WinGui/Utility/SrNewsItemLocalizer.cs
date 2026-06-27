using NewsComponents.Formatting;
using RssBandit.Resources;

namespace RssBandit.WinGui.Utility
{
	/// <summary>
	/// WinForms-head <see cref="INewsItemLocalizer"/> adapter that resolves the news-item template
	/// strings from the head's localized <c>SR</c> string table, preserving the full multi-culture
	/// localization the head shipped before <c>NewsItemFormatter</c> moved into the portable engine.
	/// </summary>
	public sealed class SrNewsItemLocalizer : INewsItemLocalizer
	{
		/// <inheritdoc />
		public string RelatedLinksText() { return SR.XsltDefaultTemplate_RelatedLinks; }

		/// <inheritdoc />
		public string PreviousPageText() { return SR.XsltDefaultTemplate_Previous; }

		/// <inheritdoc />
		public string NextPageText() { return SR.XsltDefaultTemplate_Next; }

		/// <inheritdoc />
		public string DisplayingPageText() { return SR.XsltDefaultTemplate_Displaying_page; }

		/// <inheritdoc />
		public string PageOfText() { return SR.XsltDefaultTemplate_of; }

		/// <inheritdoc />
		public string ItemPublisherText() { return SR.XsltDefaultTemplate_ItemPublisher; }

		/// <inheritdoc />
		public string ItemAuthorText() { return SR.XsltDefaultTemplate_ItemAuthor; }

		/// <inheritdoc />
		public string ItemDateText() { return SR.XsltDefaultTemplate_ItemDate; }

		/// <inheritdoc />
		public string ItemEnclosureText() { return SR.XsltDefaultTemplate_ItemEnclosure; }

		/// <inheritdoc />
		public string ToggleFlagStateText() { return SR.XsltDefaultTemplate_ToggleFlagState; }

		/// <inheritdoc />
		public string ToggleReadStateText() { return SR.XsltDefaultTemplate_ToggleReadState; }

		/// <inheritdoc />
		public string ToggleWatchStateText() { return SR.XsltDefaultTemplate_ToggleWatchState; }
	}
}

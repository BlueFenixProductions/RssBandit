using System;

namespace RssBandit.AppServices.Core
{
	/// <summary>
	/// The per-feed and per-category settings accessor cluster extracted from the
	/// <c>FeedSource</c> god-object (Concern Q of the FeedSource decomposition; Slice 1).
	/// </summary>
	/// <remarks>
	/// Feeds are addressed by their URL, categories by their (separator-delimited) name.
	/// The contract is intentionally limited to portable primitives (<see cref="string"/>,
	/// <see cref="TimeSpan"/>, <see cref="bool"/>) so it can live in the contracts layer with
	/// no dependency on the NewsComponents engine types. Getters that fall through to an
	/// instance/static default which may itself be <c>null</c> (the stylesheet and the
	/// listview-layout column id) are annotated as nullable returns.
	/// </remarks>
	public interface IFeedAndCategorySettings
	{
		// -------- per-feed --------

		/// <summary>
		/// Sets the maximum amount of time an item should be kept in the cache for a particular
		/// feed. This overrides the value of the maxItemAge property.
		/// </summary>
		/// <param name="feedUrl">The feed.</param>
		/// <param name="age">The maximum amount of time items should be kept for the specified feed.</param>
		void SetMaxItemAge(string feedUrl, TimeSpan age);

		/// <summary>
		/// Gets the maximum amount of time an item is kept in the cache for a particular feed.
		/// </summary>
		/// <param name="feedUrl">The feed identifier.</param>
		TimeSpan GetMaxItemAge(string feedUrl);

		/// <summary>Sets the refresh rate for a feed.</summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="refreshRate">the new refresh rate</param>
		void SetRefreshRate(string feedUrl, int refreshRate);

		/// <summary>Gets the refresh rate for a feed.</summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <returns>the refresh rate</returns>
		int GetRefreshRate(string feedUrl);

		/// <summary>Sets the stylesheet for a feed.</summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="style">the new stylesheet</param>
		void SetStyleSheet(string feedUrl, string style);

		/// <summary>Gets the stylesheet for a feed.</summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <returns>the stylesheet</returns>
		string? GetStyleSheet(string feedUrl);

		/// <summary>Sets the listview layout ID for a feed.</summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="layout">the new listview layout</param>
		void SetFeedColumnLayoutID(string feedUrl, string layout);

		/// <summary>Gets the listview layout ID for a feed.</summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <returns>the listview layout</returns>
		string? GetFeedColumnLayoutID(string feedUrl);

		/// <summary>Sets whether to mark items as read on exiting the feed in the UI.</summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <param name="markitemsread">the new value for markitemsreadonexit</param>
		void SetMarkItemsReadOnExit(string feedUrl, bool markitemsread);

		/// <summary>Gets whether to mark items as read on exiting the feed in the UI.</summary>
		/// <param name="feedUrl">the URL of the feed</param>
		/// <returns>whether to mark items as read on exit</returns>
		bool GetMarkItemsReadOnExit(string feedUrl);

		// -------- per-category --------

		/// <summary>
		/// Sets the maximum amount of time an item should be kept in the cache for a particular
		/// category. This overrides the value of the maxItemAge property.
		/// </summary>
		/// <param name="category">The category.</param>
		/// <param name="age">The maximum amount of time items should be kept for the specified category.</param>
		void SetCategoryMaxItemAge(string category, TimeSpan age);

		/// <summary>Gets the maximum amount of time an item is kept in the cache for a particular category.</summary>
		/// <param name="category">The name of the category.</param>
		TimeSpan GetCategoryMaxItemAge(string category);

		/// <summary>Sets the refresh rate for a category.</summary>
		/// <param name="category">the name of the category</param>
		/// <param name="refreshRate">the new refresh rate</param>
		void SetCategoryRefreshRate(string category, int refreshRate);

		/// <summary>Gets the refresh rate for a category.</summary>
		/// <param name="category">the name of the category</param>
		/// <returns>the refresh rate</returns>
		int GetCategoryRefreshRate(string category);

		/// <summary>Sets the stylesheet for a category.</summary>
		/// <param name="category">the name of the category</param>
		/// <param name="style">the new stylesheet</param>
		void SetCategoryStyleSheet(string category, string style);

		/// <summary>Gets the stylesheet for a category.</summary>
		/// <param name="category">the name of the category</param>
		/// <returns>the stylesheet</returns>
		string? GetCategoryStyleSheet(string category);

		/// <summary>Sets the listview layout for a category.</summary>
		/// <param name="category">the name of the category</param>
		/// <param name="layout">the new listview layout</param>
		void SetCategoryFeedColumnLayoutID(string category, string layout);

		/// <summary>Gets the listview layout for a category.</summary>
		/// <param name="category">the name of the category</param>
		/// <returns>the listview layout</returns>
		string? GetCategoryFeedColumnLayoutID(string category);

		/// <summary>Sets whether to mark items as read on exiting the feed in the UI.</summary>
		/// <param name="category">the name of the category</param>
		/// <param name="markitemsread">the new value for markitemsreadonexit</param>
		void SetCategoryMarkItemsReadOnExit(string category, bool markitemsread);

		/// <summary>Gets whether to mark items as read on exiting the feed in the UI.</summary>
		/// <param name="category">the name of the category</param>
		/// <returns>whether to mark items as read on exit</returns>
		bool GetCategoryMarkItemsReadOnExit(string category);
	}
}

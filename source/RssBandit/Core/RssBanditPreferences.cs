#region Version Info Header
/*
 * $Id$
 * $HeadURL$
 * Last modified by $Author$
 * Last modified at $Date$
 * $Revision$
 */
#endregion

#region usings
using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Text;
using System.Collections.Generic;
using NewsComponents;
using NewsComponents.Utils;
using RssBandit.ViewModel;
using Logger = RssBandit.Common.Logging;
using RssBandit.WinGui.Utility;
using RssBandit.AppServices;
#endregion

namespace RssBandit 
{
	/// <summary>
	/// RssBanditPreferences manages 
	/// all the Bandit specific user preferences.
	/// </summary>
	[Serializable]
	public class RssBanditPreferences : BindableBase, IUserPreferences
	{
		#region bool instance variables
		/// <summary>
		/// To get rid of all the bool variables,
		/// we now use one store to track the bool states.
		/// </summary>
		[Flags,Serializable]
		private enum OptionalFlags:long
		{
			AllOff = 0,
			CustomProxy = 0x1,
			TakeIEProxySettings = 0x2,
			ByPassProxyOnLocal = 0x4,
			ProxyCustomCredentials = 0x8,
			UseRemoteStorage = 0x10,
			ReUseFirstBrowserTab = 0x20,
			NewsItemOpenLinkInDetailWindow = 0x40,
			MarkFeedItemsReadOnExit = 0x80,
			RefreshFeedsOnStartup = 0x100,
			AllowJavascriptInBrowser = 0x200,
			AllowJavaInBrowser = 0x400, 
			AllowActiveXInBrowser = 0x800,
			AllowBGSoundInBrowser = 0x1000, 
			AllowVideoInBrowser = 0x2000, 
			AllowImagesInBrowser = 0x4000,
			ShowNewItemsReceivedBalloon = 0x8000,
			BuildRelationCosmos = 0x10000,
			OpenNewTabsInBackground = 0x20000,
			DisableFavicons = 0x40000,
			AddPodcasts2ITunes = 0x80000,
			AddPodcasts2WMP    = 0x100000,
			AddPodcasts2Folder = 0x200000,
			SinglePodcastPlaylist = 0x400000,
			AllowAppEventSounds = 0x800000,
			ShowAllNewsItemsPerPage = 0x1000000,
			DisableAutoMarkItemsRead = 0x2000000,
			DownloadEnclosures = 0x4000000,
			EnclosureAlert = 0x8000000,
			CreateSubfoldersForEnclosures = 0x100000000,
		}

		private OptionalFlags allOptionalFlags;
		#endregion

		#region other instance variables

		private static readonly log4net.ILog _log = Logger.Log.GetLogger(typeof(RssBanditPreferences));
		
        //new 2.0.x
		private int refreshRate = FeedSource.DefaultRefreshRate;	
        private TextSize readingPaneTextSize = TextSize.Medium;

		//new 1.5.x
		private int numNewsItemsPerPage = 10;

		// new: 1.3.x
		private string userIdentityForComments = String.Empty;

		// old: 1.2.x; see RssBanditApplication.CheckAndMigrateSettingsAndPreferences() 
		private string referer = String.Empty;
		private string userName = String.Empty;
		private string userMailAddress = String.Empty;

		private string[] proxyBypassList = new string[]{};
		private string proxyAddress = String.Empty;
		private int proxyPort = 0;
		private string proxyUser = String.Empty;
		private string proxyPassword = String.Empty;

		private string remoteStorageUserName = String.Empty;
		private string remoteStoragePassword = String.Empty;
		private RemoteStorageProtocolType remoteStorageProtocol = RemoteStorageProtocolType.UNC;
		private string remoteStorageLocation = String.Empty;

		private string newsItemStylesheetFile = String.Empty;
		private HideToTray hideToTrayAction = HideToTray.OnMinimize;

		private Font normalFont;
		private Font unreadFont;
		private Font flagFont;
		private Font errorFont;
		private Font referrerFont;
		private Font newCommentsFont;
		private Color normalFontColor = FontColorHelper.DefaultNormalColor;
		private Color unreadFontColor = FontColorHelper.DefaultUnreadColor;
		private Color flagFontColor = FontColorHelper.DefaultHighlightColor;
		private Color errorFontColor = FontColorHelper.DefaultFailureColor;
		private Color referrerFontColor = FontColorHelper.DefaultReferenceColor;
		private Color newCommentsColor = FontColorHelper.DefaultNewCommentsColor;

		// general max item age: 90 days:
		private TimeSpan maxItemAge = TimeSpan.FromDays(90);	

		private BrowserBehaviorOnNewWindow browserBehaviorOnNewWindow = BrowserBehaviorOnNewWindow.OpenDefaultBrowser;
		private string browserCustomExecOnNewWindow = String.Empty;

		private DisplayFeedAlertWindow feedAlertWindow = DisplayFeedAlertWindow.AsConfiguredPerFeed;
		#endregion

		#region public properties

		/// <summary>
		/// Gets or sets the refresh rate in millisecs.
		/// </summary>
		/// <value>The refresh rate.</value>
		internal int RefreshRate
		{
			[DebuggerStepThrough]
			get { return refreshRate; }
			set
			{
				SetProperty(ref refreshRate, value);
			}
		}

        /// <summary>
        /// Gets/Sets the size of the text in the reading pane
        /// </summary>
        public TextSize ReadingPaneTextSize
        {
            [DebuggerStepThrough]
            get { return readingPaneTextSize; }
            set
            {
	            SetProperty(ref readingPaneTextSize, value);
            }	
        }

		/// <summary>
		/// Gets/Sets the number of news items to display per page in the newspaper view
		/// </summary>
		public int NumNewsItemsPerPage{
			[DebuggerStepThrough]
			get { return numNewsItemsPerPage; }
			set 
			{
				SetProperty(ref numNewsItemsPerPage, value);
			}		
		}


		/// <summary>
		/// Gets/Sets the user identity used to post feed comments.
		/// </summary>
		public string UserIdentityForComments {
			[DebuggerStepThrough]
			get { return userIdentityForComments; }
			set 
			{
				SetProperty(ref userIdentityForComments, value);
			}
		}

		#region kept for migration reasons only
		/// <summary>
		/// Obsolete. Do not use it anymore!
		/// Used only to migrate old values to the new structure UserIdentity.
		/// </summary>
		public string Referer 
		{
			[DebuggerStepThrough]
			get {	return referer;		}
			set {	referer = value;	}
		}

		/// <summary>
		/// Obsolete. Do not use it anymore!
		/// Used only to migrate old values to the new structure UserIdentity.
		/// </summary>
		public string UserName {
			[DebuggerStepThrough]
			get {	return userName;	}
			set {	userName = value;	}
		}

		/// <summary>
		/// Obsolete. Do not use it anymore!
		/// Used only to migrate old values to the new structure UserIdentity.
		/// </summary>
		public string UserMailAddress {
			[DebuggerStepThrough]
			get {	return userMailAddress;		}
			set {	userMailAddress = value;	}
		}
		#endregion

		/// <summary>
		/// Sets/Get a value that control if feeds should be refreshed from the original
		/// source on startup of the application.
		/// </summary>
		public bool FeedRefreshOnStartup 
		{			
			[DebuggerStepThrough]
			get {	return GetOption(OptionalFlags.RefreshFeedsOnStartup); }
			set {	
				SetOption(OptionalFlags.RefreshFeedsOnStartup, value);		
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Gets/Set a value to control if the application have to use a proxy to
		/// request feeds.
		/// </summary>
		public bool UseProxy {
			[DebuggerStepThrough]
			get {	return GetOption(OptionalFlags.CustomProxy);		}
			set {	
				SetOption(OptionalFlags.CustomProxy, value);
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// If <see cref="UseProxy">UseProxy</see> is set to true, this option is used
		/// to force a take over the proxy settings from and installed Internet Explorer.
		/// (Including automatic proxy configuration).
		/// </summary>
		public bool UseIEProxySettings {
			[DebuggerStepThrough]
			get { return GetOption(OptionalFlags.TakeIEProxySettings);	}
			set { 
				SetOption(OptionalFlags.TakeIEProxySettings, value);
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Gets/Set the value if the used proxy should bypass requests
		/// for local (intranet) servers.
		/// </summary>
		public bool BypassProxyOnLocal {
			[DebuggerStepThrough]
			get {	return GetOption(OptionalFlags.ByPassProxyOnLocal);		}
			set {	
				SetOption(OptionalFlags.ByPassProxyOnLocal, value);
				OnPropertyChanged();
			}
		}


		/// <summary>
		/// Gets/Sets the value that indicates whether a news item should be automatically 
		/// marked as read when viewed in the newspaper view
		/// </summary>
		public bool MarkItemsAsReadWhenViewed { 
			[DebuggerStepThrough]
			get {	return !GetOption(OptionalFlags.DisableAutoMarkItemsRead);		}
			set {	
				SetOption(OptionalFlags.DisableAutoMarkItemsRead, !value);
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Gets/Set the value that indicates whether a limited number of news items
		/// should be displayed per page in the newspaper view. 
		/// </summary>
		public bool LimitNewsItemsPerPage {
			[DebuggerStepThrough]
			get {	return !GetOption(OptionalFlags.ShowAllNewsItemsPerPage);		}
			set {	
				SetOption(OptionalFlags.ShowAllNewsItemsPerPage, !value);
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Sets/Get a list of servers/web addresses to bypass by the used proxy.
		/// </summary>
		public string[] ProxyBypassList 
		{
			[DebuggerStepThrough]
			get {	return proxyBypassList;			}
			set 
			{	
				SetProperty(ref proxyBypassList, value);
			}
		}

		/// <summary>
		/// Sets/Get the proxy address.
		/// </summary>
		public string ProxyAddress {
			[DebuggerStepThrough]
			get {	return proxyAddress;	}
			set 
			{
				SetProperty(ref proxyAddress, value);
			}
		}

		/// <summary>
		/// Sets/Get the proxy port number.
		/// </summary>
		public int ProxyPort {
			[DebuggerStepThrough]
			get {	return proxyPort;		}
			set 
			{
				SetProperty(ref proxyPort, value);
			}
		}

		/// <summary>
		/// Sets/Get a value indicating if the proxy have to use 
		/// custom credentials (proxy needs authentication).
		/// </summary>
		public bool ProxyCustomCredentials {
			[DebuggerStepThrough]
			get {	return GetOption(OptionalFlags.ProxyCustomCredentials);		}
			set {	
				SetOption(OptionalFlags.ProxyCustomCredentials, value);		
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Sets/Get the proxy custom credential user name.
		/// </summary>
		public string ProxyUser {
			[DebuggerStepThrough]
			get {	return proxyUser;		}
			set 
			{
				SetProperty(ref proxyUser, value);
			}
		}

		/// <summary>
		/// Sets/Get the proxy custom credential user password.
		/// </summary>
		public string ProxyPassword {
			[DebuggerStepThrough]
			get {	return proxyPassword;		}
			set 
			{
				SetProperty(ref proxyPassword, value);
			}
		}
		
		/// <summary>
		/// Sets/Get the global news item formatter stylesheet 
		/// (filename excluding path name)
		/// </summary>
		public string NewsItemStylesheetFile {
			[DebuggerStepThrough]
			get {	return newsItemStylesheetFile;		}
			set 
			{
				SetProperty(ref newsItemStylesheetFile, value);
			}
		}



		/// <summary>
		/// Sets/Get a value to control if the first opened web browser Tab should
		/// be reused or not.
		/// </summary>
		public bool ReuseFirstBrowserTab {
			[DebuggerStepThrough]
			get {	return GetOption(OptionalFlags.ReUseFirstBrowserTab);		}
			set {	
				SetOption(OptionalFlags.ReUseFirstBrowserTab, value);	
				OnPropertyChanged();
			}
		}	

		/// <summary>
		/// Sets/Get a value to control if the new browser tabs should be opened 
		/// in the background.
		/// </summary>
		public bool OpenNewTabsInBackground {
			[DebuggerStepThrough]
			get {	return GetOption(OptionalFlags.OpenNewTabsInBackground);		}
			set {	
				SetOption(OptionalFlags.OpenNewTabsInBackground, value);	
				OnPropertyChanged();
			}
		}	

		/// <summary>
		/// Gets or sets a value indicating whether to allow application
		/// event sounds.
		/// </summary>
		/// <value>
		/// 	<c>true</c> if [allow app event sounds]; otherwise, <c>false</c>.
		/// </value>
		public bool AllowAppEventSounds {
			[DebuggerStepThrough]
			get {	return GetOption(OptionalFlags.AllowAppEventSounds);		}
			set {	
				SetOption(OptionalFlags.AllowAppEventSounds, value);	
				OnPropertyChanged();
			}
		}	

		///// <summary>
		///// Gets or sets a value indicating whether to run bandit as windows user logon.
		///// It directly modifies the registry value within the "Run" section and
		///// don't get persisted into preferences file.
		///// </summary>
		///// <value>
		///// 	<c>true</c> if [run bandit as windows user logon]; otherwise, <c>false</c>.
		///// </value>
		//public bool RunBanditAsWindowsUserLogon {
		//	get { return Win32.Registry.RunAtStartup; }
		//	set {
		//		if (Win32.Registry.RunAtStartup != value)
		//		{
		//			Win32.Registry.RunAtStartup = value;
		//			OnPropertyChanged();
		//		}
		//	}
		//}
		
		/// <summary>
		/// Sets/Get a value to control whether there should be a single playlist 
		/// for podcasts files. If this value is false, then podcasts are added to 
		/// a playlist with the same name as the feed. 
		/// </summary>
		public bool SinglePodcastPlaylist {
			[DebuggerStepThrough]
			get {	return GetOption(OptionalFlags.SinglePodcastPlaylist);		}
			set {	
				SetOption(OptionalFlags.SinglePodcastPlaylist, value);	
				OnPropertyChanged();
			}
		}	

		/// <summary>
		/// Sets/Get a value to control if podcasts should be moved to a specified 
		/// podcasts folder. 
		/// </summary>
		public bool AddPodcasts2Folder {
			[DebuggerStepThrough]
			get {	return GetOption(OptionalFlags.AddPodcasts2Folder);		}
			set {	
				SetOption(OptionalFlags.AddPodcasts2Folder, value);	
				OnPropertyChanged();
			}
		}	

		/// <summary>
		/// Sets/Get a value to control if a playlist in Windows Media Player should be 
		/// created when an WMP-compatible podcast is successfully downloaded
		/// </summary>
		public bool AddPodcasts2WMP {
			[DebuggerStepThrough]
			get {	return GetOption(OptionalFlags.AddPodcasts2WMP);		}
			set {	
				SetOption(OptionalFlags.AddPodcasts2WMP, value);	
				OnPropertyChanged();
			}
		}	

		/// <summary>
		/// Sets/Get a value to control if a playlist in iTunes should be 
		/// created when an iTunes-compatible podcast is successfully downloaded
		/// </summary>
		public bool AddPodcasts2ITunes {
			[DebuggerStepThrough]
			get {	return GetOption(OptionalFlags.AddPodcasts2ITunes);		}
			set {	
				SetOption(OptionalFlags.AddPodcasts2ITunes, value);	
				OnPropertyChanged();
			}
		}	

		/// <summary>
		/// Sets/Get a value to control if the favicons are used as feed icons 
		/// in the tree view.
		/// </summary>
		public bool UseFavicons {
			[DebuggerStepThrough]
			get {	return !GetOption(OptionalFlags.DisableFavicons);		}
			set {	
				SetOption(OptionalFlags.DisableFavicons, !value);	
				OnPropertyChanged();
			}
		}	
		/// <summary>
		/// Sets/Get a value to control if unread feed items should be marked as read
		/// while leaving the feed through UI navigation (to another feed/category)
		/// </summary>
		public bool MarkItemsReadOnExit {
			[DebuggerStepThrough]
			get { return GetOption(OptionalFlags.MarkFeedItemsReadOnExit);	}
			set { 
				SetOption(OptionalFlags.MarkFeedItemsReadOnExit, value);	
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Sets/Get a value to control if an news item without a description
		/// should display the (web page) content of the link target instead (if true).
		/// </summary>
		public bool NewsItemOpenLinkInDetailWindow {
			[DebuggerStepThrough]
			get { return GetOption(OptionalFlags.NewsItemOpenLinkInDetailWindow);	}
			set { 
				SetOption(OptionalFlags.NewsItemOpenLinkInDetailWindow, value);	
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Sets/Get the user action <see cref="HideToTray">HideToTray</see> 
		/// when the application should minimize to the
		/// system tray area.
		/// </summary>
		public HideToTray HideToTrayAction {
			[DebuggerStepThrough]
			get {	return hideToTrayAction;		}
			set 
			{	
				SetProperty(ref hideToTrayAction, value);
			}
		}

		/// <summary>
		/// Normal font used to render items (listview) 
		/// and feeds (tree view)
		/// </summary>
		public Font NormalFont {
			[DebuggerStepThrough]
			get {	return normalFont;		}
			set 
			{
				SetProperty(ref normalFont, value);
			}
		}

		/// <summary>
		/// Normal font color used to render items (listview) 
		/// and feeds (tree view)
		/// </summary>
		public Color NormalFontColor {
			[DebuggerStepThrough]
			get {	return normalFontColor;		}
			set 
			{
				SetProperty(ref normalFontColor, value);
			}
		}

		/// <summary>
		/// Font used to highlight unread items (listview) 
		/// and feeds (tree view)
		/// </summary>
		public Font UnreadFont {
			[DebuggerStepThrough]
			get {	return unreadFont;		}
			set 
			{
				SetProperty(ref unreadFont, value);
			}
		}

		/// <summary>
		/// Color used to highlight unread items (listview) 
		/// and feeds (tree view)
		/// </summary>
		public Color UnreadFontColor {
			[DebuggerStepThrough]
			get {	return unreadFontColor;		}
			set 
			{
				SetProperty(ref unreadFontColor, value);
			}
		}

		/// <summary>
		/// Font used to render flagged items (listview) 
		/// </summary>
		public Font FlagFont {
			[DebuggerStepThrough]
			get {	return flagFont;		}
			set 
			{
				SetProperty(ref flagFont, value);
			}
		}
		
		/// <summary>
		/// Color used to render flagged items (listview) 
		/// </summary>
		public Color FlagFontColor {
			[DebuggerStepThrough]
			get {	return flagFontColor;		}
			set 
			{
				SetProperty(ref flagFontColor, value);
			}
		}

		/// <summary>
		/// Font used to render items that refer back to the users 
		/// default identity (listview) 
		/// </summary>
		public Font ReferrerFont {
			[DebuggerStepThrough]
			get {	return referrerFont;		}
			set 
			{
				SetProperty(ref referrerFont, value);
			}
		}

		/// <summary>
		/// Color used to render items that refer back to the users 
		/// default identity (listview) 
		/// </summary>
		public Color ReferrerFontColor {
			[DebuggerStepThrough]
			get {	return referrerFontColor;	}
			set 
			{
				SetProperty(ref referrerFontColor, value);
			}
		}

		/// <summary>
		/// Font used to render items that display an error message (listview) 
		/// </summary>
		public Font ErrorFont {
			[DebuggerStepThrough]
			get {	return errorFont;		}
			set 
			{
				SetProperty(ref errorFont, value);
			}
		}

		/// <summary>
		/// Color used to render items that display an error message (listview) 
		/// </summary>
		public Color ErrorFontColor {
			[DebuggerStepThrough]
			get {	return errorFontColor;		}
			set 
			{
				SetProperty(ref errorFontColor, value);
			}
		}

		/// <summary>
		/// Font used to render items that received new comments (watched) 
		/// </summary>
		public Font NewCommentsFont {
			[DebuggerStepThrough]
			get {	return newCommentsFont;		}
			set 
			{
				SetProperty(ref newCommentsFont, value);
			}
		}

		/// <summary>
		/// Color used to render items that received new comments (watched) 
		/// </summary>
		public Color NewCommentsFontColor {
			[DebuggerStepThrough]
			get {	return newCommentsColor;		}
			set 
			{
				SetProperty(ref newCommentsColor, value);
			}
		}
		
		/// <summary>
		/// Sets/Get the TimeSpan for the global maximum news item age.
		/// You have to use TimeSpan.MinValue for the unlimited item age.
		/// </summary>
		public TimeSpan MaxItemAge {
			[DebuggerStepThrough]
			get {	return maxItemAge;	}
			set 
			{
				SetProperty(ref maxItemAge, value);
			}
		}

		/// <summary>
		/// Sets/Get the value indicating if we have to use a remote storage
		/// for sync. states.
		/// </summary>
		public bool UseRemoteStorage {
			[DebuggerStepThrough]
			get { return GetOption(OptionalFlags.UseRemoteStorage); }
			set 
			{
				SetOption(OptionalFlags.UseRemoteStorage, value);
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Sets/Get the username that may be required to access
		/// the remote storage location.
		/// </summary>
		public string RemoteStorageUserName {
			[DebuggerStepThrough]
			get { return remoteStorageUserName; }
			set
			{
				SetProperty(ref remoteStorageUserName, value);
			}
		}

		/// <summary>
		/// Sets/Get the password that may be required to access the remote
		/// storage location.
		/// </summary>
		public string RemoteStoragePassword {
			[DebuggerStepThrough]
			get { return remoteStoragePassword; }
			set
			{
				SetProperty(ref remoteStoragePassword, value);
			}
		}

		/// <summary>
		/// Sets/Get the type of remote storage to use. <see cref="RemoteStorageProtocolType"/>
		/// </summary>
		public RemoteStorageProtocolType RemoteStorageProtocol {
			[DebuggerStepThrough]
			get { return remoteStorageProtocol; }
			set
			{
				SetProperty(ref remoteStorageProtocol, value);
			}
		}

		/// <summary>
		/// Sets/Get the remote storage location. Can vary dep. on
		/// the location type (ftp, share,...)
		/// </summary>
		public string RemoteStorageLocation {
			[DebuggerStepThrough]
			get { return remoteStorageLocation; }
			set 
			{
				SetProperty(ref remoteStorageLocation, value);
			}
		}

		/// <summary>
		/// Sets/Get a value that control if enclosures should be downloaded
		/// </summary>
		public bool DownloadEnclosures
		{
			[DebuggerStepThrough]
			get { return GetOption(OptionalFlags.DownloadEnclosures); }
			set
			{
				SetOption(OptionalFlags.DownloadEnclosures, value);
				OnPropertyChanged();
			}
		}
		/// <summary>
		/// Sets/Get a value that control if an alert should be displayed if enclosures are downloaded
		/// </summary>
		public bool EnclosureAlert
		{
			[DebuggerStepThrough]
			get { return GetOption(OptionalFlags.EnclosureAlert); }
			set
			{
				SetOption(OptionalFlags.EnclosureAlert, value);
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Gets or sets whether  podcasts and enclosures should be downloaded to a folder 
		/// named after the feed
		/// </summary>
		public bool CreateSubfoldersForEnclosures
		{
			get
			{
				return GetOption(OptionalFlags.CreateSubfoldersForEnclosures); 
			}

			set
			{
				SetOption(OptionalFlags.CreateSubfoldersForEnclosures, value);
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Sets/Get the behavior how to handle requests to open new
		/// window(s) while browsing
		/// </summary>
		public BrowserBehaviorOnNewWindow BrowserOnNewWindow {
			[DebuggerStepThrough]
			get { return browserBehaviorOnNewWindow; }
			set 
			{
				SetProperty(ref browserBehaviorOnNewWindow, value);
			}
		}

		/// <summary>
		/// Gets/Set the executable application to start if
		/// browser requires to open a new window.
		/// </summary>
		public string BrowserCustomExecOnNewWindow  {
			[DebuggerStepThrough]
			get { return browserCustomExecOnNewWindow; }
			set 
			{
				SetProperty(ref browserCustomExecOnNewWindow, value);
			}
		}

		/// <summary>
		/// Sets/Get if Javascript should be allowed to execute
		/// </summary>
		public bool BrowserJavascriptAllowed { 
			[DebuggerStepThrough]
			get { return GetOption(OptionalFlags.AllowJavascriptInBrowser); }
			set 
			{
				SetOption(OptionalFlags.AllowJavascriptInBrowser, value);
				OnPropertyChanged();
			}
		}
        		
		/// <summary>
		/// Sets/Get the DisplayFeedAlertWindow enumeration value
		/// </summary>
		public DisplayFeedAlertWindow ShowAlertWindow { 
			[DebuggerStepThrough]
			get { return feedAlertWindow; }
			set 
			{
				feedAlertWindow = value;
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Sets/Get if the system tray balloon tip should be displayed
		/// if new news items are received.
		/// </summary>
		public bool ShowNewItemsReceivedBalloon { 
			[DebuggerStepThrough]
			get { return GetOption(OptionalFlags.ShowNewItemsReceivedBalloon); }
			set 
			{
				SetOption(OptionalFlags.ShowNewItemsReceivedBalloon, value);
				OnPropertyChanged();
			}
		}

		/// <summary>
		/// Sets/Get if we build the relation cosmos (interlinkage of news items).
		/// </summary>
		public bool BuildRelationCosmos { 
			[DebuggerStepThrough]
			get { return true; /* we always want to do this given performance and usability improvements */ }
			set {
				/* do nothing */ 
			}
		}
		#endregion

		#region private OptionalFlags handling
		private bool GetOption(OptionalFlags flag)
		{
			return ((this.allOptionalFlags & flag) == flag);
		}

		private void SetOption(OptionalFlags flag, bool value)
		{
			if (value)
				this.allOptionalFlags |= flag;
			else
				this.allOptionalFlags = this.allOptionalFlags & ~flag;
		}
		#endregion

		#region ctor's
		public RssBanditPreferences()	{
			InitDefaults();
		}
		#endregion

		#region Init
		private void InitDefaults() {
			normalFont = FontColorHelper.DefaultNormalFont;
			unreadFont = FontColorHelper.DefaultUnreadFont;
			flagFont = FontColorHelper.DefaultHighlightFont;
			errorFont = FontColorHelper.DefaultFailureFont;
			referrerFont = FontColorHelper.DefaultReferenceFont;
			newCommentsFont = FontColorHelper.DefaultNewCommentsFont;

			// init default options to true:
			this.allOptionalFlags = DefaultOptionalFlags;
		}

		private static OptionalFlags DefaultOptionalFlags {
			get {
				OptionalFlags f = OptionalFlags.AllOff;
				f |= OptionalFlags.ByPassProxyOnLocal |
				//	OptionalFlags.ShowNewItemsReceivedBalloon |
					OptionalFlags.AllowImagesInBrowser |
					OptionalFlags.NewsItemOpenLinkInDetailWindow |
					OptionalFlags.ReUseFirstBrowserTab |
					OptionalFlags.AllowAppEventSounds  | 
                    OptionalFlags.BuildRelationCosmos| 
                    OptionalFlags.CreateSubfoldersForEnclosures|
                    OptionalFlags.RefreshFeedsOnStartup;
				return f;
			}
		}
		
		#endregion


		#region JSON persistence (current format)

		/// <summary>
		/// Maps this instance to the JSON persistence shape (<see cref="PreferencesDto"/>).
		/// Secrets are stored encrypted, fonts as FontConverter strings, colors as HTML
		/// color strings, enums and the optional flags as strings.
		/// </summary>
		internal PreferencesDto ToDto()
		{
			// new encryption key (version 21 and higher):
			EncryptionHelper.CompatibilityMode = false;
			return new PreferencesDto
			{
				PrefsVersion = 25,
				ProxyAddress = ProxyAddress,
				ProxyPort = ProxyPort,
				ProxyUserEncrypted = EncryptionHelper.Encrypt(ProxyUser),
				ProxyPasswordEncrypted = EncryptionHelper.Encrypt(ProxyPassword),
				ProxyBypassList = ProxyBypassList,
				NewsItemStylesheetFile = NewsItemStylesheetFile,
				HideToTrayAction = HideToTrayAction.ToString(),
				NormalFontString = SerializationInfoReader.ConvertFont(NormalFont),
				UnreadFontString = SerializationInfoReader.ConvertFont(UnreadFont),
				FlagFontString = SerializationInfoReader.ConvertFont(FlagFont),
				ErrorFontString = SerializationInfoReader.ConvertFont(ErrorFont),
				ReferrerFontString = SerializationInfoReader.ConvertFont(ReferrerFont),
				NewCommentsFontString = SerializationInfoReader.ConvertFont(NewCommentsFont),
				NormalFontColor = ColorTranslator.ToHtml(NormalFontColor),
				UnreadFontColor = ColorTranslator.ToHtml(UnreadFontColor),
				FlagFontColor = ColorTranslator.ToHtml(FlagFontColor),
				ErrorFontColor = ColorTranslator.ToHtml(ErrorFontColor),
				ReferrerFontColor = ColorTranslator.ToHtml(ReferrerFontColor),
				NewCommentsFontColor = ColorTranslator.ToHtml(NewCommentsFontColor),
				MaxItemAge = MaxItemAge,
				RemoteStorageUserNameEncrypted = EncryptionHelper.Encrypt(RemoteStorageUserName),
				RemoteStoragePasswordEncrypted = EncryptionHelper.Encrypt(RemoteStoragePassword),
				RemoteStorageProtocol = RemoteStorageProtocol.ToString(),
				RemoteStorageLocation = RemoteStorageLocation,
				BrowserOnNewWindow = BrowserOnNewWindow.ToString(),
				BrowserCustomExecOnNewWindow = BrowserCustomExecOnNewWindow,
				ShowAlertWindow = ShowAlertWindow.ToString(),
				UserIdentityForComments = UserIdentityForComments,
				AllOptionalFlags = this.allOptionalFlags.ToString(),
				NumNewsItemsPerPage = NumNewsItemsPerPage,
				ReadingPaneTextSize = ReadingPaneTextSize.ToString(),
				RefreshRate = RefreshRate
			};
		}

		/// <summary>
		/// Creates a <see cref="RssBanditPreferences"/> instance from the JSON
		/// persistence shape, applying the same null/empty tolerance and fallback
		/// defaults as the legacy <see cref="SerializationInfo"/> constructor.
		/// </summary>
		internal static RssBanditPreferences FromDto(PreferencesDto dto)
		{
			if (dto == null)
				throw new ArgumentNullException(nameof(dto));

			var p = new RssBanditPreferences();	// runs InitDefaults()

			// JSON prefs are always written with the new encryption key (version >= 21):
			EncryptionHelper.CompatibilityMode = false;

			// booleans all live inside the flags enum:
			OptionalFlags flags;
			if (String.IsNullOrEmpty(dto.AllOptionalFlags) || !Enum.TryParse(dto.AllOptionalFlags, out flags))
				flags = DefaultOptionalFlags;
			p.allOptionalFlags = flags;

			p.ProxyAddress = dto.ProxyAddress ?? String.Empty;
			p.ProxyPort = dto.ProxyPort;
			p.ProxyUser = EncryptionHelper.Decrypt(dto.ProxyUserEncrypted ?? String.Empty);
			p.ProxyPassword = EncryptionHelper.Decrypt(dto.ProxyPasswordEncrypted ?? String.Empty);
			p.ProxyBypassList = dto.ProxyBypassList ?? new string[] { };

			p.NewsItemStylesheetFile = dto.NewsItemStylesheetFile ?? String.Empty;
			p.HideToTrayAction = ParseEnum(dto.HideToTrayAction, HideToTray.OnMinimize);

			p.NormalFont = ParseFont(dto.NormalFontString, FontColorHelper.DefaultNormalFont);
			p.UnreadFont = ParseFont(dto.UnreadFontString, FontColorHelper.DefaultUnreadFont);
			p.FlagFont = ParseFont(dto.FlagFontString, FontColorHelper.DefaultHighlightFont);
			p.ErrorFont = ParseFont(dto.ErrorFontString, FontColorHelper.DefaultFailureFont);
			p.ReferrerFont = ParseFont(dto.ReferrerFontString, FontColorHelper.DefaultReferenceFont);
			p.NewCommentsFont = ParseFont(dto.NewCommentsFontString, FontColorHelper.DefaultNewCommentsFont);

			p.NormalFontColor = ParseColor(dto.NormalFontColor, FontColorHelper.DefaultNormalColor);
			p.UnreadFontColor = ParseColor(dto.UnreadFontColor, FontColorHelper.DefaultUnreadColor);
			p.FlagFontColor = ParseColor(dto.FlagFontColor, FontColorHelper.DefaultHighlightColor);
			p.ErrorFontColor = ParseColor(dto.ErrorFontColor, FontColorHelper.DefaultFailureColor);
			p.ReferrerFontColor = ParseColor(dto.ReferrerFontColor, FontColorHelper.DefaultReferenceColor);
			p.NewCommentsFontColor = ParseColor(dto.NewCommentsFontColor, FontColorHelper.DefaultNewCommentsColor);

			p.MaxItemAge = dto.MaxItemAge;

			p.RemoteStorageUserName = EncryptionHelper.Decrypt(dto.RemoteStorageUserNameEncrypted ?? String.Empty);
			p.RemoteStoragePassword = EncryptionHelper.Decrypt(dto.RemoteStoragePasswordEncrypted ?? String.Empty);
			p.RemoteStorageProtocol = ParseEnum(dto.RemoteStorageProtocol, RemoteStorageProtocolType.Unknown);
			p.RemoteStorageLocation = dto.RemoteStorageLocation ?? String.Empty;
			// dasBlog_1_3 is not anymore supported:
			if (p.UseRemoteStorage && p.RemoteStorageProtocol == RemoteStorageProtocolType.dasBlog_1_3)
			{
				p.UseRemoteStorage = false;
			}

			p.BrowserOnNewWindow = ParseEnum(dto.BrowserOnNewWindow, BrowserBehaviorOnNewWindow.OpenDefaultBrowser);
			p.BrowserCustomExecOnNewWindow = dto.BrowserCustomExecOnNewWindow ?? String.Empty;
			p.ShowAlertWindow = ParseEnum(dto.ShowAlertWindow, DisplayFeedAlertWindow.AsConfiguredPerFeed);
			p.UserIdentityForComments = dto.UserIdentityForComments ?? String.Empty;
			p.NumNewsItemsPerPage = dto.NumNewsItemsPerPage;
			p.ReadingPaneTextSize = ParseEnum(dto.ReadingPaneTextSize, TextSize.Medium);
			p.RefreshRate = dto.RefreshRate;

			return p;
		}

		/// <summary>
		/// Parses a font string created by <see cref="SerializationInfoReader.ConvertFont"/>
		/// (inverse operation, same logic as <see cref="SerializationInfoReader.GetFont"/>).
		/// </summary>
		private static Font ParseFont(string fontString, Font defaultValue)
		{
			if (String.IsNullOrEmpty(fontString))
				return defaultValue;
			try
			{
				var converter = new FontConverter();
				return converter.ConvertFromString(null, CultureInfo.InvariantCulture, fontString) as Font ?? defaultValue;
			}
			catch (Exception ex)
			{
				_log.Warn("Could not parse stored font value '" + fontString + "', using default", ex);
				return defaultValue;
			}
		}

		/// <summary>
		/// Parses an HTML color string created by <see cref="ColorTranslator.ToHtml"/>.
		/// </summary>
		private static Color ParseColor(string htmlColor, Color defaultValue)
		{
			if (String.IsNullOrEmpty(htmlColor))
				return defaultValue;
			try
			{
				return ColorTranslator.FromHtml(htmlColor);
			}
			catch (Exception ex)
			{
				_log.Warn("Could not parse stored color value '" + htmlColor + "', using default", ex);
				return defaultValue;
			}
		}

		/// <summary>
		/// Parses an enum stored as string, falling back to the given default
		/// (same tolerance as the legacy SerializationInfoReader.Get).
		/// </summary>
		private static T ParseEnum<T>(string value, T defaultValue) where T : struct, Enum
		{
			T parsed;
			if (!String.IsNullOrEmpty(value) && Enum.TryParse(value, out parsed))
				return parsed;
			return defaultValue;
		}

		#endregion

		
		#region helper classes
		private class EncryptionHelper {
			private static TripleDESCryptoServiceProvider _des;
			private static bool _compatibilityMode = false;

			private EncryptionHelper(){}

			static EncryptionHelper() {
				_des = new TripleDESCryptoServiceProvider();
				_des.Key = _calcHash();
				_des.Mode = CipherMode.ECB;
			}

			/// <summary>
			/// Just to enable read of old encrypted values by 
			/// providing the value 'true'.
			/// </summary>
			internal static bool CompatibilityMode { 
				get { return _compatibilityMode; }
				set {
					if (value != _compatibilityMode)
						_des.Key = _calcHash();
					_compatibilityMode = value;
				}
			}

			public static string Decrypt(string str) {
				byte[] base64;
				byte[] bytes;
				string ret;

				if (str == null)
					ret = null;
				else {
					if (str.Length == 0)
						ret = String.Empty;
					else {
						try {
							base64 = Convert.FromBase64String(str);
							bytes = _des.CreateDecryptor().TransformFinalBlock(base64, 0, base64.GetLength(0));
							ret = Encoding.Unicode.GetString(bytes);
						}
						catch (Exception e) {
							_log.Debug("Exception in Decrypt", e);
							ret = String.Empty;
						}
					}
				}
				return ret;
			}

			public static string Encrypt(string str) {
				byte[] inBytes;
				byte[] bytes;
				string ret;

				if (str == null)
					ret = null;
				else {
					if (str.Length == 0)
						ret = String.Empty;
					else {
						try {
							inBytes = Encoding.Unicode.GetBytes(str);
							bytes = _des.CreateEncryptor().TransformFinalBlock(inBytes, 0, inBytes.GetLength(0));
							ret = Convert.ToBase64String(bytes);
						}
						catch (Exception e) {
							_log.Debug("Exception in Encrypt", e);
							ret = String.Empty;
						}
					}
				}
				return ret;
			}

			private static byte[] _calcHash() 
			{
				// for FIPS compliance we just return the hash we formerly calculated.
				// This is for backward compatibility, so users do not loose all their
				// feed/feedsource/ftp/ etc. credentials...
				byte[] h = new byte[16];
				if (_compatibilityMode)
				{
					h[0] = 120;
					h[1] = 40;
					h[2] = 4;
					h[3] = 105;
					h[4] = 228;
					h[5] = 255;
					h[6] = 178;
					h[7] = 45;
					h[8] = 118;
					h[9] = 90;
					h[10] = 179;
					h[11] = 178;
					h[12] = 149;
					h[13] = 150;
					h[14] = 125;
					h[15] = 185;
				}
				else
				{
					h[0] = 33;
					h[1] = 97;
					h[2] = 12;
					h[3] = 205;
					h[4] = 28;
					h[5] = 181;
					h[6] = 25;
					h[7] = 20;
					h[8] = 55;
					h[9] = 214;
					h[10] = 222;
					h[11] = 35;
					h[12] = 111;
					h[13] = 239;
					h[14] = 96;
					h[15] = 42;
				}
				return h;

				//string salt = null;
				//if (_compatibilityMode) {
				//    // use the old days salt string.
				//    // this is not just a query: it will also create the folder :-(
				//    salt = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				//} else {
				//    // so here is a possibly better way to init the salt without
				//    // query the file system:
				//    salt = "B*A!N_D:I;T,P1E0P%P$E+R";
				//}

				//byte[] b = Encoding.Unicode.GetBytes(salt);
				//int bLen = b.GetLength(0);
				
				//// just to make the key somewhat "invisible" in Anakrino, we use the random class.
				//// the seed (a prime number) makes it repro
				//Random r = new Random(1500450271);	
				//// result array
				//byte[] res = new Byte[500];
				//int i = 0;
				
				//for (i = 0; i < bLen && i < 500; i++)
				//    res[i] = (byte)(b[i] ^ r.Next(30, 127));
				
				//// padding:
				//while (i < 500) {
				//    res[i] = (byte)r.Next(30, 127);
				//    i++;
				//}

				//MD5CryptoServiceProvider csp = new MD5CryptoServiceProvider();
				//byte[] cspr = csp.ComputeHash(res);
				//return cspr;
			}



		}
		#endregion
	}

	/// <summary>
	/// JSON persistence shape for <see cref="RssBanditPreferences"/> (the current
	/// preferences format, replacing the legacy SOAP/BinaryFormatter formats).
	/// Property initializers carry the same fallback defaults the legacy
	/// <see cref="SerializationInfo"/> reader used for missing values.
	/// Secrets (proxy/remote storage credentials) are stored encrypted.
	/// </summary>
	public class PreferencesDto
	{
		public int PrefsVersion { get; set; } = 25;
		public string ProxyAddress { get; set; } = String.Empty;
		public int ProxyPort { get; set; } = 8080;
		public string ProxyUserEncrypted { get; set; } = String.Empty;
		public string ProxyPasswordEncrypted { get; set; } = String.Empty;
		public string[] ProxyBypassList { get; set; } = new string[] { };
		public string NewsItemStylesheetFile { get; set; } = String.Empty;
		public string HideToTrayAction { get; set; }
		public string NormalFontString { get; set; }
		public string UnreadFontString { get; set; }
		public string FlagFontString { get; set; }
		public string ErrorFontString { get; set; }
		public string ReferrerFontString { get; set; }
		public string NewCommentsFontString { get; set; }
		public string NormalFontColor { get; set; }
		public string UnreadFontColor { get; set; }
		public string FlagFontColor { get; set; }
		public string ErrorFontColor { get; set; }
		public string ReferrerFontColor { get; set; }
		public string NewCommentsFontColor { get; set; }
		public TimeSpan MaxItemAge { get; set; } = TimeSpan.FromDays(90);
		public string RemoteStorageUserNameEncrypted { get; set; } = String.Empty;
		public string RemoteStoragePasswordEncrypted { get; set; } = String.Empty;
		public string RemoteStorageProtocol { get; set; }
		public string RemoteStorageLocation { get; set; } = String.Empty;
		public string BrowserOnNewWindow { get; set; }
		public string BrowserCustomExecOnNewWindow { get; set; } = String.Empty;
		public string ShowAlertWindow { get; set; }
		public string UserIdentityForComments { get; set; } = String.Empty;
		public string AllOptionalFlags { get; set; }
		public int NumNewsItemsPerPage { get; set; } = 10;
		public string ReadingPaneTextSize { get; set; }
		public int RefreshRate { get; set; } = FeedSource.DefaultRefreshRate;
	}
}

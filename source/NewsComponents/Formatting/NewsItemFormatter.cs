#region Version Info Header
/*
 * $Id$
 * $HeadURL$
 * Last modified by $Author$
 * Last modified at $Date$
 * $Revision$
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using System.IO;

using NewsComponents;
using NewsComponents.Feed;
using NewsComponents.Resources;
using RssBandit.Common.Logging;
using RssBandit.Exceptions;

namespace NewsComponents.Formatting
{
	/// <summary>
	/// NewsItemFormatter manages stylesheets and news item 
	/// transformation/formatting to HTML.
	/// </summary>
	public class NewsItemFormatter {

		public event EventHandler<FeedExceptionEventArgs> TransformError;
		public event EventHandler<ExceptionEventArgs> StylesheetError;
		public event EventHandler<ExceptionEventArgs> StylesheetValidationError;

		static private readonly string _defaultTmpl;

		static private readonly string _searchTmpl;      

		public const string SearchTemplateId = ":*<>?"; 

		//the item of the table of XSLT stylesheets
        struct StylesheetDescriptor
        {
            /// <summary>
            /// The XSL Compiled Transform object
            /// </summary>
            public XslCompiledTransform CompiledTransform;
            /// <summary>
            /// The full path to the stylesheet used.
            /// Can e used to resolve relative images, etc.
            /// </summary>
            public string Location;
        }

        private readonly IDictionary<string, StylesheetDescriptor> stylesheetTable = new Dictionary<string, StylesheetDescriptor>(17); 

		static NewsItemFormatter()
		{
			_defaultTmpl = LoadEmbeddedTemplate("DefaultTemplate.xslt");
			_searchTmpl = LoadEmbeddedTemplate("SearchResultsTemplate.xslt");
		}

		/// <summary>
		/// Loads an embedded XSLT template resource as a string, reproducing byte-for-byte how the
		/// WinForms head formerly read these templates from its ResX (which referenced them via
		/// ResXFileRef with a ;Windows-1252 encoding suffix).
		/// </summary>
		/// <remarks>
		/// The head decoded these files as Windows-1252. Code page 1252 (<c>Encoding.GetEncoding(1252)</c>)
		/// is not registered on the portable net10.0 target without the System.Text.Encoding.CodePages
		/// package, so we use the in-box <see cref="Encoding.Latin1"/> instead: both templates are pure
		/// ASCII (no byte greater than 0x7F), and Latin1 is a 1:1 byte-to-code-point map that decodes
		/// such content identically to Windows-1252 (the two encodings differ only in the 0x80-0x9F range
		/// these files never use). The Phase C1 byte-snapshot tests prove the resulting HTML is identical
		/// to the head's golden capture.
		/// </remarks>
		private static string LoadEmbeddedTemplate(string fileName)
		{
			using (Stream stream = Resource.Manager.GetStream("Resources." + fileName))
			using (StreamReader reader = new StreamReader(stream, Encoding.Latin1))
			{
				return reader.ReadToEnd();
			}
		}

		/// <summary>
		/// Gets or sets the localizer that supplies the localized strings the XSLT templates embed
		/// (publisher / author / enclosure labels, toggle-state alt text, paging text). Defaults to
		/// <see cref="DefaultNewsItemLocalizer"/> (neutral English, matching the head's neutral SR
		/// values). The WinForms head injects an adapter over its full multi-culture string table.
		/// </summary>
		public INewsItemLocalizer Localizer { get; set; } = new DefaultNewsItemLocalizer();

		public NewsItemFormatter():this(String.Empty, _defaultTmpl) {
			this.AddXslStyleSheet(SearchTemplateId, _searchTmpl);
		}
		public NewsItemFormatter(string xslStyleSheetName, string xslStyleSheet) {
			this.AddXslStyleSheet(xslStyleSheetName, xslStyleSheet);
		}


		/// <summary>
		/// Tests whether a particular stylesheet is contained within the item formatter
		/// </summary>
		/// <param name="name">The name of the stylesheet</param>
		/// <returns>Tests whether the </returns>
		public bool ContainsXslStyleSheet(string name){
		 return this.stylesheetTable.ContainsKey(name); 
		}

		/// <summary>
		/// Set/Get the currently used XSLT stylesheet to be used to format
		/// an NewsItem for the detail display.
		/// </summary>
		/// <exception cref="XmlException"></exception>
		/// <exception cref="XsltException"></exception>
		public void AddXslStyleSheet(string name, string stylesheet){

            XslCompiledTransform transform = new XslCompiledTransform();

			try {
					
				if(name == null){
					name = String.Empty;
				}

				if (string.IsNullOrEmpty(stylesheet)) {
					stylesheet = DefaultNewsItemTemplate;
				}	
					
				//FeedDemon styles use an $IMAGEDIR$ magic variable which we should account for				
				stylesheet = stylesheet.Replace("$IMAGEDIR$", "https://templates.invalid/images/"); 

                transform.Load(new XmlTextReader(new StringReader(stylesheet))); 	
				
				if(this.stylesheetTable.ContainsKey(name)){
					this.stylesheetTable.Remove(name); 
				}

				this.stylesheetTable.Add(name, new StylesheetDescriptor {Location = name, CompiledTransform  = transform}); 
				
			}catch (XsltCompileException e)
			{
				this.OnStylesheetError(this, new ExceptionEventArgs(e, ComponentsText.ExceptionNewsItemFormatterStylesheetCompile));
			}
			catch (XsltException e)
			{
				this.OnStylesheetError(this, new ExceptionEventArgs(e, ComponentsText.ExceptionNewsItemFormatterInvalidStylesheet));
			}
			catch (XmlException e)
			{
				this.OnStylesheetError(this, new ExceptionEventArgs(e, ComponentsText.ExceptionNewsItemFormatterStylesheetMessage));
			}
			catch (Exception e)
			{
				Log.Error("AddXslStyleSheet() caused unexpected error", e);
			}
		}


		/// <summary>
		/// Transform a NewsItem, FeedInfo or FeedInfoList using the specified stylesheet
		/// </summary>
		/// <param name="stylesheet">The stylesheet to use for transformation</param>
		/// <param name="transformTarget">The object to transform</param>
		/// <param name="xslArgs"></param>
		/// <returns>The results of the transformation</returns>
		public virtual string ToHtml(string stylesheet, object transformTarget, XsltArgumentList xslArgs) 
		{

			string link = String.Empty, content = String.Empty;
			
			if (transformTarget == null) 
				return "<html><head><title>empty</title></head><body></body></html>";

			// use a streamed output to get the "disable-output-escaping" 
			// working:
			StringWriter swr = new StringWriter();

			try	{
				XPathDocument doc; 
				
				if(transformTarget is INewsItem){
					INewsItem item = (INewsItem) transformTarget;
					link = item.FeedLink;
					content = item.Content;
					doc = new XPathDocument(new XmlTextReader(new StringReader(item.ToString(NewsItemSerializationFormat.NewsPaper, false))), XmlSpace.Preserve);
                }else if (transformTarget is IFeedDetails){
					IFeedDetails feed = (IFeedDetails) transformTarget;
					link = feed.Link;
					doc = new XPathDocument(new XmlTextReader(new StringReader(feed.ToString(NewsItemSerializationFormat.NewsPaper, false))), XmlSpace.Preserve);				
				}else if(transformTarget is FeedInfoList){
					FeedInfoList feeds = (FeedInfoList) transformTarget;
					doc = new XPathDocument(new XmlTextReader(new StringReader(feeds.ToString())), XmlSpace.Preserve);				
				}else{
					throw new ArgumentException("transformTarget"); 
				}

				XslCompiledTransform transform; 
				
				if(this.stylesheetTable.ContainsKey(stylesheet)){
                    transform = this.stylesheetTable[stylesheet].CompiledTransform;
				}else{
                    transform = this.stylesheetTable[String.Empty].CompiledTransform;
				}	
				
				// support simple localizations (some common predefined strings to display):
				xslArgs.AddExtensionObject("urn:localization-extension", new LocalizerExtensionObject(Localizer));
				transform.Transform(doc, xslArgs, swr);

			} catch (ThreadAbortException) {
				// ignored
			}	catch (Exception e)	{
				this.OnTransformationError(this, new FeedExceptionEventArgs(e, link, ComponentsText.ExceptionNewsItemTransformation));
				return content;	// try to display unformatted simple text
			}
		
			return swr.ToString();
		}


		public static string DefaultNewsItemTemplate	{
			get {	return _defaultTmpl; }
		}

		protected void OnTransformationError(object sender, FeedExceptionEventArgs e) {
			if (TransformError != null)
				foreach (EventHandler<FeedExceptionEventArgs> eh in TransformError.GetInvocationList())
					eh.BeginInvoke(sender, e, null, null);
		}

		protected void OnStylesheetError(object sender, ExceptionEventArgs e) {
			if (StylesheetError != null)
				foreach (EventHandler<ExceptionEventArgs> eh in StylesheetError.GetInvocationList())
					eh.BeginInvoke(this, e, null, null);
		}

		protected void OnStylesheetValidationError(object sender, ExceptionEventArgs e) {
			if (StylesheetValidationError != null)
				foreach (EventHandler<ExceptionEventArgs> eh in StylesheetValidationError.GetInvocationList())
					eh.BeginInvoke(this, e, null, null);
		}

		#region LocalizerExtensionObject class

		/// <summary>
		/// Xslt Transformation extension to provide localized strings 
		/// to Xslt templates
		/// </summary>
		public class LocalizerExtensionObject
		{
			private readonly INewsItemLocalizer _loc;

			/// <summary>
			/// Initializes a new instance of the <see cref="LocalizerExtensionObject"/> class.
			/// </summary>
			/// <param name="localizer">The localizer that supplies the localized strings.</param>
			public LocalizerExtensionObject(INewsItemLocalizer localizer)
			{
				_loc = localizer;
			}

			/// <summary>
			/// Gets the localized related links text.
			/// </summary>
			/// <returns></returns>
			public string RelatedLinksText(){
				return _loc.RelatedLinksText();
			}

			/// <summary>
			/// Returns the localized previous text.
			/// </summary>
			/// <returns></returns>
			public string PreviousPageText(){
				return _loc.PreviousPageText();
			}

			/// <summary>
			/// Returns the localized next text.
			/// </summary>
			/// <returns></returns>
			public string NextPageText(){
				return _loc.NextPageText();
			}

			/// <summary>
			/// Returns the localized displaying page text.
			/// </summary>
			/// <returns></returns>
			public string DisplayingPageText(){
				return _loc.DisplayingPageText();
			}

			/// <summary>
			/// Returns the localized of text.
			/// </summary>
			/// <returns></returns>
			public string PageOfText(){
				return _loc.PageOfText();
			}

			/// <summary>
			/// Gets the localized item publisher text.
			/// </summary>
			/// <returns></returns>
			public string ItemPublisherText() {
				return _loc.ItemPublisherText();
			}

			/// <summary>
			/// Gets the localized item author text.
			/// </summary>
			/// <returns></returns>
			public string ItemAuthorText() {
				return _loc.ItemAuthorText();
			}

			/// <summary>
			/// Gets the localized item date text.
			/// </summary>
			/// <returns></returns>
			public string ItemDateText() {
				return _loc.ItemDateText();
			}

			/// <summary>
			/// Gets the localized item enclosure text.
			/// </summary>
			/// <returns></returns>
			public string ItemEnclosureText() {
				return _loc.ItemEnclosureText();
			}
			/// <summary>
			/// Gets the localized text to indicate toggle of flag states.
			/// </summary>
			/// <returns></returns>
			public string ToggleFlagStateText() {
				return _loc.ToggleFlagStateText();
			}

			/// <summary>
			/// Gets the localized text to indicate toggle of read state.
			/// </summary>
			/// <returns></returns>
			public string ToggleReadStateText() {
				return _loc.ToggleReadStateText();
			}

			/// <summary>
			/// Gets the localized text to indicate toggle of watched state.
			/// </summary>
			/// <returns></returns>
			public string ToggleWatchStateText() {
				return _loc.ToggleWatchStateText();
			}
		}

		#endregion
	}

	
}

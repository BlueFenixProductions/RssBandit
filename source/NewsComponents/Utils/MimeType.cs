#region CVS Version Header

/*
 * $Id$
 * Last modified by $Author$
 * Last modified at $Date$
 * $Revision$
 */

#endregion

using System;
using Microsoft.Win32;

namespace NewsComponents.Utils
{
    /// <summary>
    /// Represents a MIME Type (e.g. <c>audio/mpeg</c>).
    /// See also http://www.ietf.org/rfc/rfc2045.txt
    /// </summary>
    /// <remarks>
    /// The content-sniffing helpers (<c>CreateFrom</c> over a file/stream/byte[],
    /// <c>MatchContentOf</c>) and the extension&#8594;MIME creation tables
    /// (<c>CreateFromFileExt</c>, <c>CreateFromRegisteredApps</c>) were removed on
    /// 2026-06-14: they were unused in the build, and the only one that needed native
    /// code relied on <c>urlmon!FindMimeFromData</c>. What remains is a small managed
    /// value type built from a Content-Type string, with a registry-backed
    /// MIME&#8594;extension lookup.
    /// </remarks>
    [Serializable]
    public class MimeType : IEquatable<MimeType>
    {
        #region Private Members

        private string msType;
        private string msSubType;

        #endregion

        #region Constructors

        /// <summary>Constructor.</summary>
        public MimeType()
        {
            msType = msSubType = String.Empty;
        }

        /// <summary>Constructor.</summary>
        /// <param name="contentType">Full MIME Content-Type string.</param>
        public MimeType(string contentType)
        {
            SplitTypeAndSubType(contentType, out msType, out msSubType);
        }

        /// <summary>Constructor.</summary>
        /// <param name="type">discrete-type or composite-type</param>
        /// <param name="subType">sub-Type</param>
        public MimeType(string type, string subType)
        {
            msType = (type ?? String.Empty);
            msSubType = (subType ?? String.Empty);
        }

        #endregion

        #region Public Statics

        /// <summary>
        /// Gets an empty MimeType instance.
        /// </summary>
        public static readonly MimeType Empty = new MimeType();

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets/Sets the MIME type (discrete-type or composite-type)
        /// </summary>
        public string Type
        {
            get { return msType; }
            set { msType = value; }
        }

        /// <summary>
        /// Gets/Sets the MIME sub-Type.
        /// </summary>
        public string SubType
        {
            get { return msSubType; }
            set { msSubType = value; }
        }

        /// <summary>
        /// Gets/Sets the Full MIME Content-Type string.
        /// </summary>
        public string ContentType
        {
            get { return msType + "/" + msSubType; }
            set { SplitTypeAndSubType(value, out msType, out msSubType); }
        }

        /// <summary>
        /// Gets the file extension registered for this MimeType instance.
        /// </summary>
        /// <returns>File extension (string) if found/available, else null</returns>
        public string GetFileExtension()
        {
            if (string.IsNullOrEmpty(msType))
                return null;
            return WindowsRegistry.GetMimeTypeOption(ContentType, "Extension");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current MimeType.
        /// </summary>
        public override string ToString()
        {
            return ContentType;
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        public bool Equals(MimeType other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return ContentType.Equals(other.ContentType);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current MimeType.
        /// </summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as MimeType);
        }

        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        public override int GetHashCode()
        {
            if (ContentType != null)
                return ContentType.GetHashCode();
            return base.GetHashCode();
        }

        #endregion

        #region Private support functions

        private static void SplitTypeAndSubType(string contentType, out string sType, out string sSubType)
        {
            // this construct ensures it works also with contentType == null, and not containing any "/":
            string[] sArray = String.Concat(contentType, "/").Split(new char[] {'/'});
            sType = sArray[0].Trim();
            sSubType = sArray[1].Trim();
        }

        /// <summary>
        /// Wrap the windows registry access needed for MimeType.
        /// </summary>
        private static class WindowsRegistry
        {
            /// <summary>
            /// Gets the registered option (e.g. "Extension") for a given MIME type.
            /// </summary>
            /// <param name="mimeType">The MIME type to look up.</param>
            /// <param name="option">The value name to read (e.g. "Extension").</param>
            /// <returns>Non empty string if found, else null</returns>
            /// <permission cref="Microsoft.Win32.Registry">Read access to HKEY_CLASSES_ROOT\MIME</permission>
            public static string GetMimeTypeOption(string mimeType, string option)
            {
                if (option == null || mimeType == null)
                    return null;

                // HKEY_CLASSES_ROOT lookup is Windows-only (CA1416); no MIME registry on other heads.
                if (!OperatingSystem.IsWindows())
                    return null;

                RegistryKey typeKey = Registry.ClassesRoot.OpenSubKey(@"MIME\Database\Content Type\" + mimeType, false);
                if (typeKey == null)
                    return null;

                string val = typeKey.GetValue(option) as string;
                typeKey.Close();
                return val;
            }
        }

        #endregion
    }
}

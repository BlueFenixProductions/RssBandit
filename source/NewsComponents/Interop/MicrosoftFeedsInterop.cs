#region Copyright
/*
Hand-written COM interop for the Windows RSS Platform (msfeeds.dll).

Replaces the tlbimp-generated "Microsoft.Feeds.Interop" COMReference so that the
project can be built with the dotnet CLI (which cannot run ResolveComReference,
error MSB4803).

Authoritative sources used to derive this file (Phase A, 2026-06-12):
 - %ProgramFiles(x86)%\Windows Kits\10\Include\10.0.26100.0\um\msfeeds.idl
     (interface definitions, IIDs/CLSIDs, member declaration order == vtable order)
 - %ProgramFiles(x86)%\Windows Kits\10\Include\10.0.26100.0\um\msfeedsid.h
     (DISPIDs)
Type library: "Microsoft Feeds 2.0 Object Library" {9CDCD9C9-BC40-41C6-89C5-230466DB0BD0}.

IMPORTANT: member order inside each interface MUST match the IDL declaration order
(these are dual interfaces called early-bound through the vtable). Do not reorder
and do not insert members in the middle.

Intentionally omitted (unused by RSS Bandit): IFeed2, IFeedItem2, IFeedEvents,
FeedFolderWatcher/FeedWatcher coclasses and the IUnknown-based IXFeed* interfaces.

The classic tlbimp event pattern (casting the watcher RCW to IFeedFolderEvents_Event)
relied on .NET Framework TCE adapter generation which does not exist on modern .NET.
IFeedFolderEvents_Event is therefore a small wrapper class that wires the connection
point via System.Runtime.InteropServices.ComEventsHelper using the source-interface
IID and DISPIDs.
*/
#endregion

using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace Microsoft.Feeds.Interop
{
    #region enums (msfeeds.idl)

    public enum FEEDS_BACKGROUNDSYNC_ACTION
    {
        FBSA_DISABLE = 0,
        FBSA_ENABLE = 1,
        FBSA_RUNNOW = 2,
    }

    public enum FEEDS_BACKGROUNDSYNC_STATUS
    {
        FBSS_DISABLED = 0,
        FBSS_ENABLED = 1,
    }

    public enum FEEDS_EVENTS_SCOPE
    {
        FES_ALL = 0,
        FES_SELF_ONLY = 1,
        FES_SELF_AND_CHILDREN_ONLY = 2,
    }

    public enum FEEDS_EVENTS_MASK
    {
        FEM_FOLDEREVENTS = 0x00000001,
        FEM_FEEDEVENTS = 0x00000002,
    }

    public enum FEEDS_XML_SORT_PROPERTY
    {
        FXSP_NONE = 0,
        FXSP_PUBDATE = 1,
        FXSP_DOWNLOADTIME = 2,
    }

    public enum FEEDS_XML_SORT_ORDER
    {
        FXSO_NONE = 0,
        FXSO_ASCENDING = 1,
        FXSO_DESCENDING = 2,
    }

    public enum FEEDS_XML_FILTER_FLAGS
    {
        FXFF_ALL = 0x00000000,
        FXFF_UNREAD = 0x00000001,
        FXFF_READ = 0x00000002,
    }

    public enum FEEDS_XML_INCLUDE_FLAGS
    {
        FXIF_NONE = 0x00000000,
        FXIF_CF_EXTENSIONS = 0x00000001,
    }

    public enum FEEDS_DOWNLOAD_STATUS
    {
        FDS_NONE = 0,
        FDS_PENDING = 1,
        FDS_DOWNLOADING = 2,
        FDS_DOWNLOADED = 3,
        FDS_DOWNLOAD_FAILED = 4,
    }

    public enum FEEDS_SYNC_SETTING
    {
        FSS_DEFAULT = 0,
        FSS_INTERVAL = 1,
        FSS_MANUAL = 2,
        FSS_SUGGESTED = 3,
    }

    public enum FEEDS_DOWNLOAD_ERROR
    {
        FDE_NONE = 0,
        FDE_DOWNLOAD_FAILED = 1,
        FDE_INVALID_FEED_FORMAT = 2,
        FDE_NORMALIZATION_FAILED = 3,
        FDE_PERSISTENCE_FAILED = 4,
        FDE_DOWNLOAD_BLOCKED = 5,
        FDE_CANCELED = 6,
        FDE_UNSUPPORTED_AUTH = 7,
        FDE_BACKGROUND_DOWNLOAD_DISABLED = 8,
        FDE_NOT_EXIST = 9,
        FDE_UNSUPPORTED_MSXML = 10,
        FDE_UNSUPPORTED_DTD = 11,
        FDE_DOWNLOAD_SIZE_LIMIT_EXCEEDED = 12,
        FDE_ACCESS_DENIED = 13,
        FDE_AUTH_FAILED = 14,
        FDE_INVALID_AUTH = 15,
    }

    public enum FEEDS_EVENTS_ITEM_COUNT_FLAGS
    {
        FEICF_READ_ITEM_COUNT_CHANGED = 0x00000001,
        FEICF_UNREAD_ITEM_COUNT_CHANGED = 0x00000002,
    }

    #endregion

    #region IFeedsManager

    [ComImport]
    [Guid("A74029CC-1F1A-4906-88F0-810638D86591")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFeedsManager
    {
        [DispId(0x1000)]
        object RootFolder
        {
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(0x1001)]
        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool IsSubscribed([In, MarshalAs(UnmanagedType.BStr)] string feedUrl);

        [DispId(0x1002)]
        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool ExistsFeed([In, MarshalAs(UnmanagedType.BStr)] string feedPath);

        [DispId(0x1003)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object GetFeed([In, MarshalAs(UnmanagedType.BStr)] string feedPath);

        [DispId(0x1008)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object GetFeedByUrl([In, MarshalAs(UnmanagedType.BStr)] string feedUrl);

        [DispId(0x1004)]
        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool ExistsFolder([In, MarshalAs(UnmanagedType.BStr)] string folderPath);

        [DispId(0x1005)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object GetFolder([In, MarshalAs(UnmanagedType.BStr)] string folderPath);

        [DispId(0x1006)]
        void DeleteFeed([In, MarshalAs(UnmanagedType.BStr)] string feedPath);

        [DispId(0x1007)]
        void DeleteFolder([In, MarshalAs(UnmanagedType.BStr)] string folderPath);

        [DispId(0x1009)]
        void BackgroundSync([In] FEEDS_BACKGROUNDSYNC_ACTION action);

        [DispId(0x100a)]
        FEEDS_BACKGROUNDSYNC_STATUS BackgroundSyncStatus { get; }

        [DispId(0x100b)]
        int DefaultInterval { get; set; }

        [DispId(0x100c)]
        void AsyncSyncAll();

        [DispId(0x100d)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string Normalize([In, MarshalAs(UnmanagedType.BStr)] string feedXmlIn);

        [DispId(0x100e)]
        int ItemCountLimit { get; }
    }

    #endregion

    #region IFeedsEnum

    [ComImport]
    [Guid("E3CD0028-2EED-4C60-8FAE-A3225309A836")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFeedsEnum
    {
        [DispId(0x2000)]
        int Count { get; }

        [DispId(0x2001)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object Item([In] int index);

        [DispId(-4)]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(EnumVariantMarshaler))]
        IEnumerator GetEnumerator();
    }

    #region IEnumVARIANT -> IEnumerator marshaling
    // .NET Framework shipped EnumeratorToEnumVariantMarshaler for this; modern .NET removed it
    // from the ref pack, so we carry our own minimal equivalent for the _NewEnum (DISPID -4) slot.

    [ComImport]
    [Guid("00020404-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEnumVARIANT
    {
        [PreserveSig]
        int Next(int celt, [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 0)] object[] rgVar, IntPtr pceltFetched);

        [PreserveSig]
        int Skip(int celt);

        [PreserveSig]
        int Reset();

        IEnumVARIANT Clone();
    }

    internal sealed class EnumVariantEnumerator : IEnumerator
    {
        private readonly IEnumVARIANT enumVariant;
        private object current;

        internal EnumVariantEnumerator(IEnumVARIANT enumVariant)
        {
            this.enumVariant = enumVariant;
        }

        public object Current => current;

        public bool MoveNext()
        {
            object[] fetched = new object[1];
            // pceltFetched may be null when celt == 1; S_OK means an element was returned.
            if (enumVariant.Next(1, fetched, IntPtr.Zero) == 0)
            {
                current = fetched[0];
                return true;
            }
            current = null;
            return false;
        }

        public void Reset()
        {
            enumVariant.Reset();
            current = null;
        }
    }

    internal sealed class EnumVariantMarshaler : ICustomMarshaler
    {
        private static readonly EnumVariantMarshaler Instance = new EnumVariantMarshaler();

        public static ICustomMarshaler GetInstance(string cookie)
        {
            return Instance;
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            if (pNativeData == IntPtr.Zero)
                return null;
            return new EnumVariantEnumerator((IEnumVARIANT)Marshal.GetObjectForIUnknown(pNativeData));
        }

        public IntPtr MarshalManagedToNative(object managedObj)
        {
            throw new NotSupportedException();
        }

        public void CleanUpNativeData(IntPtr pNativeData)
        {
            // GetObjectForIUnknown added its own reference; release the one handed to us.
            Marshal.Release(pNativeData);
        }

        public void CleanUpManagedData(object managedObj)
        {
        }

        public int GetNativeDataSize()
        {
            return -1;
        }
    }

    #endregion

    #endregion

    #region IFeedFolder

    [ComImport]
    [Guid("81F04AD1-4194-4D7D-86D6-11813CEC163C")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFeedFolder
    {
        [DispId(0x3000)]
        object Feeds
        {
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(0x3001)]
        object Subfolders
        {
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(0x3002)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object CreateFeed([In, MarshalAs(UnmanagedType.BStr)] string feedName, [In, MarshalAs(UnmanagedType.BStr)] string feedUrl);

        [DispId(0x3003)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object CreateSubfolder([In, MarshalAs(UnmanagedType.BStr)] string folderName);

        [DispId(0x3004)]
        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool ExistsFeed([In, MarshalAs(UnmanagedType.BStr)] string feedName);

        [DispId(0x3005)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object GetFeed([In, MarshalAs(UnmanagedType.BStr)] string feedName);

        [DispId(0x3006)]
        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool ExistsSubfolder([In, MarshalAs(UnmanagedType.BStr)] string folderName);

        [DispId(0x3007)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object GetSubfolder([In, MarshalAs(UnmanagedType.BStr)] string folderName);

        [DispId(0x3008)]
        void Delete();

        [DispId(0x3009)]
        string Name
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x300a)]
        void Rename([In, MarshalAs(UnmanagedType.BStr)] string folderName);

        [DispId(0x300b)]
        string Path
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x300c)]
        void Move([In, MarshalAs(UnmanagedType.BStr)] string newParentPath);

        [DispId(0x300d)]
        object Parent
        {
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(0x300e)]
        bool IsRoot
        {
            [return: MarshalAs(UnmanagedType.VariantBool)]
            get;
        }

        [DispId(0x300f)]
        int TotalUnreadItemCount { get; }

        [DispId(0x3010)]
        int TotalItemCount { get; }

        [DispId(0x3011)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object GetWatcher([In] FEEDS_EVENTS_SCOPE scope, [In] FEEDS_EVENTS_MASK mask);
    }

    #endregion

    #region IFeedFolderEvents (source dispinterface)

    [ComImport]
    [Guid("20A59FA6-A844-4630-9E98-175F70B4D55B")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFeedFolderEvents
    {
        [DispId(0x7000)]
        void Error();

        [DispId(0x7001)]
        void FolderAdded([In, MarshalAs(UnmanagedType.BStr)] string path);

        [DispId(0x7002)]
        void FolderDeleted([In, MarshalAs(UnmanagedType.BStr)] string path);

        [DispId(0x7003)]
        void FolderRenamed([In, MarshalAs(UnmanagedType.BStr)] string path, [In, MarshalAs(UnmanagedType.BStr)] string oldPath);

        [DispId(0x7004)]
        void FolderMovedFrom([In, MarshalAs(UnmanagedType.BStr)] string path, [In, MarshalAs(UnmanagedType.BStr)] string oldPath);

        [DispId(0x7005)]
        void FolderMovedTo([In, MarshalAs(UnmanagedType.BStr)] string path, [In, MarshalAs(UnmanagedType.BStr)] string oldPath);

        [DispId(0x7006)]
        void FolderItemCountChanged([In, MarshalAs(UnmanagedType.BStr)] string path, [In] int itemCountType);

        [DispId(0x7007)]
        void FeedAdded([In, MarshalAs(UnmanagedType.BStr)] string path);

        [DispId(0x7008)]
        void FeedDeleted([In, MarshalAs(UnmanagedType.BStr)] string path);

        [DispId(0x7009)]
        void FeedRenamed([In, MarshalAs(UnmanagedType.BStr)] string path, [In, MarshalAs(UnmanagedType.BStr)] string oldPath);

        [DispId(0x700a)]
        void FeedUrlChanged([In, MarshalAs(UnmanagedType.BStr)] string path);

        [DispId(0x700b)]
        void FeedMovedFrom([In, MarshalAs(UnmanagedType.BStr)] string path, [In, MarshalAs(UnmanagedType.BStr)] string oldPath);

        [DispId(0x700c)]
        void FeedMovedTo([In, MarshalAs(UnmanagedType.BStr)] string path, [In, MarshalAs(UnmanagedType.BStr)] string oldPath);

        [DispId(0x700d)]
        void FeedDownloading([In, MarshalAs(UnmanagedType.BStr)] string path);

        [DispId(0x700e)]
        void FeedDownloadCompleted([In, MarshalAs(UnmanagedType.BStr)] string path, [In] FEEDS_DOWNLOAD_ERROR error);

        [DispId(0x700f)]
        void FeedItemCountChanged([In, MarshalAs(UnmanagedType.BStr)] string path, [In] int itemCountType);
    }

    #endregion

    #region IFeed

    [ComImport]
    [Guid("F7F915D8-2EDE-42BC-98E7-A5D05063A757")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFeed
    {
        [DispId(0x4000)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string Xml([In] int count, [In] FEEDS_XML_SORT_PROPERTY sortProperty, [In] FEEDS_XML_SORT_ORDER sortOrder, [In] FEEDS_XML_FILTER_FLAGS filterFlags, [In] FEEDS_XML_INCLUDE_FLAGS includeFlags);

        [DispId(0x4001)]
        string Name
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x4002)]
        void Rename([In, MarshalAs(UnmanagedType.BStr)] string name);

        [DispId(0x4003)]
        string Url
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
            [param: MarshalAs(UnmanagedType.BStr)]
            set;
        }

        [DispId(0x4004)]
        string LocalId
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x4005)]
        string Path
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x4006)]
        void Move([In, MarshalAs(UnmanagedType.BStr)] string newParentPath);

        [DispId(0x4007)]
        object Parent
        {
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(0x4008)]
        DateTime LastWriteTime { get; }

        [DispId(0x4009)]
        void Delete();

        [DispId(0x400a)]
        void Download();

        [DispId(0x400b)]
        void AsyncDownload();

        [DispId(0x400c)]
        void CancelAsyncDownload();

        [DispId(0x400e)]
        FEEDS_SYNC_SETTING SyncSetting { get; set; }

        [DispId(0x400d)]
        int Interval { get; set; }

        [DispId(0x400f)]
        DateTime LastDownloadTime { get; }

        [DispId(0x4010)]
        string LocalEnclosurePath
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x4011)]
        object Items
        {
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(0x4012)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object GetItem([In] int itemId);

        [DispId(0x4013)]
        string Title
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x4014)]
        string Description
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x4015)]
        string Link
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x4016)]
        string Image
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x4017)]
        DateTime LastBuildDate { get; }

        [DispId(0x4018)]
        DateTime PubDate { get; }

        [DispId(0x4019)]
        int Ttl { get; }

        [DispId(0x401a)]
        string Language
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x401b)]
        string Copyright
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x4026)]
        int MaxItemCount { get; set; }

        [DispId(0x401c)]
        bool DownloadEnclosuresAutomatically
        {
            [return: MarshalAs(UnmanagedType.VariantBool)]
            get;
            [param: MarshalAs(UnmanagedType.VariantBool)]
            set;
        }

        [DispId(0x401d)]
        FEEDS_DOWNLOAD_STATUS DownloadStatus { get; }

        [DispId(0x401e)]
        FEEDS_DOWNLOAD_ERROR LastDownloadError { get; }

        [DispId(0x401f)]
        void Merge([In, MarshalAs(UnmanagedType.BStr)] string feedXml, [In, MarshalAs(UnmanagedType.BStr)] string feedUrl);

        [DispId(0x4020)]
        string DownloadUrl
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x4021)]
        bool IsList
        {
            [return: MarshalAs(UnmanagedType.VariantBool)]
            get;
        }

        [DispId(0x4022)]
        void MarkAllItemsRead();

        [DispId(0x4023)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object GetWatcher([In] FEEDS_EVENTS_SCOPE scope, [In] FEEDS_EVENTS_MASK mask);

        [DispId(0x4024)]
        int UnreadItemCount { get; }

        [DispId(0x4025)]
        int ItemCount { get; }
    }

    #endregion

    #region IFeedItem

    [ComImport]
    [Guid("0A1E6CAD-0A47-4DA2-A13D-5BAAA5C8BD4F")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFeedItem
    {
        [DispId(0x5000)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string Xml([In] FEEDS_XML_INCLUDE_FLAGS includeFlags);

        [DispId(0x5001)]
        string Title
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x5002)]
        string Link
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x5003)]
        string Guid
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x5004)]
        string Description
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x5005)]
        DateTime PubDate { get; }

        [DispId(0x5006)]
        string Comments
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x5007)]
        string Author
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x5008)]
        object Enclosure
        {
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(0x5009)]
        bool IsRead
        {
            [return: MarshalAs(UnmanagedType.VariantBool)]
            get;
            [param: MarshalAs(UnmanagedType.VariantBool)]
            set;
        }

        [DispId(0x500a)]
        int LocalId { get; }

        [DispId(0x500b)]
        object Parent
        {
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(0x500c)]
        void Delete();

        [DispId(0x500d)]
        string DownloadUrl
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x500e)]
        DateTime LastDownloadTime { get; }

        [DispId(0x500f)]
        DateTime Modified { get; }
    }

    #endregion

    #region IFeedEnclosure

    [ComImport]
    [Guid("361C26F7-90A4-4E67-AE09-3A36A546436A")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IFeedEnclosure
    {
        [DispId(0x6000)]
        string Url
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x6001)]
        string Type
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x6002)]
        int Length { get; }

        [DispId(0x6003)]
        void AsyncDownload();

        [DispId(0x6004)]
        void CancelAsyncDownload();

        [DispId(0x6005)]
        FEEDS_DOWNLOAD_STATUS DownloadStatus { get; }

        [DispId(0x6006)]
        FEEDS_DOWNLOAD_ERROR LastDownloadError { get; }

        [DispId(0x6007)]
        string LocalPath
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x6008)]
        object Parent
        {
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(0x6009)]
        string DownloadUrl
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x600a)]
        string DownloadMimeType
        {
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(0x600b)]
        void RemoveFile();

        [DispId(0x600c)]
        void SetFile([In, MarshalAs(UnmanagedType.BStr)] string downloadUrl, [In, MarshalAs(UnmanagedType.BStr)] string downloadFilePath, [In, MarshalAs(UnmanagedType.BStr)] string downloadMimeType, [In, MarshalAs(UnmanagedType.BStr)] string enclosureFilename);
    }

    #endregion

    #region FeedsManager coclass

    /// <summary>CLSID_FeedsManager — instantiate via <c>new FeedsManager()</c>.</summary>
    [ComImport]
    [Guid("FAEB54C4-F66F-4806-83A0-805299F5E3AD")]
    public class FeedsManagerClass
    {
    }

    /// <summary>tlbimp-style coclass interface so existing <c>new FeedsManager()</c> call sites keep working.</summary>
    [ComImport]
    [Guid("A74029CC-1F1A-4906-88F0-810638D86591")]
    [CoClass(typeof(FeedsManagerClass))]
    public interface FeedsManager : IFeedsManager
    {
    }

    #endregion

    #region IFeedFolderEvents_Event (ComEventsHelper-based replacement for the tlbimp TCE pattern)

    /// <summary>Handles a general error raised by the watched feed folder.</summary>
    public delegate void IFeedFolderEvents_ErrorEventHandler();
    /// <summary>Handles a subfolder being added under the watched folder.</summary>
    public delegate void IFeedFolderEvents_FolderAddedEventHandler(string path);
    /// <summary>Handles a subfolder being deleted under the watched folder.</summary>
    public delegate void IFeedFolderEvents_FolderDeletedEventHandler(string path);
    /// <summary>Handles a subfolder being renamed under the watched folder.</summary>
    public delegate void IFeedFolderEvents_FolderRenamedEventHandler(string path, string oldPath);
    /// <summary>Handles a subfolder being moved out of the watched folder.</summary>
    public delegate void IFeedFolderEvents_FolderMovedFromEventHandler(string path, string oldPath);
    /// <summary>Handles a subfolder being moved into the watched folder.</summary>
    public delegate void IFeedFolderEvents_FolderMovedToEventHandler(string path, string oldPath);
    /// <summary>Handles a change of the unread/item count of a subfolder (see FEEDS_EVENTS_ITEM_COUNT_FLAGS).</summary>
    public delegate void IFeedFolderEvents_FolderItemCountChangedEventHandler(string path, int itemCountType);
    /// <summary>Handles a feed being added to the watched folder.</summary>
    public delegate void IFeedFolderEvents_FeedAddedEventHandler(string path);
    /// <summary>Handles a feed being deleted from the watched folder.</summary>
    public delegate void IFeedFolderEvents_FeedDeletedEventHandler(string path);
    /// <summary>Handles a feed being renamed within the watched folder.</summary>
    public delegate void IFeedFolderEvents_FeedRenamedEventHandler(string path, string oldPath);
    /// <summary>Handles a change of a feed's URL within the watched folder.</summary>
    public delegate void IFeedFolderEvents_FeedUrlChangedEventHandler(string path);
    /// <summary>Handles a feed being moved out of the watched folder.</summary>
    public delegate void IFeedFolderEvents_FeedMovedFromEventHandler(string path, string oldPath);
    /// <summary>Handles a feed being moved into the watched folder.</summary>
    public delegate void IFeedFolderEvents_FeedMovedToEventHandler(string path, string oldPath);
    /// <summary>Handles the start of a feed download.</summary>
    public delegate void IFeedFolderEvents_FeedDownloadingEventHandler(string path);
    /// <summary>Handles the completion of a feed download, including its error status.</summary>
    public delegate void IFeedFolderEvents_FeedDownloadCompletedEventHandler(string path, FEEDS_DOWNLOAD_ERROR error);
    /// <summary>Handles a change of the unread/item count of a feed (see FEEDS_EVENTS_ITEM_COUNT_FLAGS).</summary>
    public delegate void IFeedFolderEvents_FeedItemCountChangedEventHandler(string path, int itemCountType);

    /// <summary>
    /// Connection-point event wrapper for a watcher object returned by
    /// <see cref="IFeedFolder.GetWatcher"/>. Wrap the returned RCW:
    /// <c>var fw = new IFeedFolderEvents_Event(folder.GetWatcher(scope, mask));</c>
    /// </summary>
    public sealed class IFeedFolderEvents_Event
    {
        private static readonly Guid SourceIid = new Guid("20A59FA6-A844-4630-9E98-175F70B4D55B"); // IID_IFeedFolderEvents

        private readonly object watcher;

        public IFeedFolderEvents_Event(object watcher)
        {
            if (watcher == null)
                throw new ArgumentNullException(nameof(watcher));
            this.watcher = watcher;
        }

        public event IFeedFolderEvents_ErrorEventHandler Error
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x7000, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x7000, value); }
        }

        public event IFeedFolderEvents_FolderAddedEventHandler FolderAdded
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x7001, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x7001, value); }
        }

        public event IFeedFolderEvents_FolderDeletedEventHandler FolderDeleted
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x7002, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x7002, value); }
        }

        public event IFeedFolderEvents_FolderRenamedEventHandler FolderRenamed
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x7003, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x7003, value); }
        }

        public event IFeedFolderEvents_FolderMovedFromEventHandler FolderMovedFrom
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x7004, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x7004, value); }
        }

        public event IFeedFolderEvents_FolderMovedToEventHandler FolderMovedTo
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x7005, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x7005, value); }
        }

        public event IFeedFolderEvents_FolderItemCountChangedEventHandler FolderItemCountChanged
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x7006, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x7006, value); }
        }

        public event IFeedFolderEvents_FeedAddedEventHandler FeedAdded
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x7007, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x7007, value); }
        }

        public event IFeedFolderEvents_FeedDeletedEventHandler FeedDeleted
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x7008, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x7008, value); }
        }

        public event IFeedFolderEvents_FeedRenamedEventHandler FeedRenamed
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x7009, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x7009, value); }
        }

        public event IFeedFolderEvents_FeedUrlChangedEventHandler FeedUrlChanged
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x700a, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x700a, value); }
        }

        public event IFeedFolderEvents_FeedMovedFromEventHandler FeedMovedFrom
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x700b, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x700b, value); }
        }

        public event IFeedFolderEvents_FeedMovedToEventHandler FeedMovedTo
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x700c, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x700c, value); }
        }

        public event IFeedFolderEvents_FeedDownloadingEventHandler FeedDownloading
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x700d, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x700d, value); }
        }

        public event IFeedFolderEvents_FeedDownloadCompletedEventHandler FeedDownloadCompleted
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x700e, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x700e, value); }
        }

        public event IFeedFolderEvents_FeedItemCountChangedEventHandler FeedItemCountChanged
        {
            add { ComEventsHelper.Combine(watcher, SourceIid, 0x700f, value); }
            remove { ComEventsHelper.Remove(watcher, SourceIid, 0x700f, value); }
        }
    }

    #endregion
}

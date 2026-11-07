using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using RavenneToolsMenu.Core;

namespace RavenneToolsMenu.Detection;

// ---- Interfaces COM du Shell (interoper manuelle, signatures minimales) ----

[ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IServiceProviderCOM
{
    [PreserveSig] int QueryService(ref Guid guidService, ref Guid riid, out nint ppvObject);
}

[ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellBrowser
{
    [PreserveSig] int GetWindow(out nint phwnd);
    [PreserveSig] int ContextSensitiveHelp(int fEnterMode);
    [PreserveSig] int InsertMenusSB(nint hmenuShared, ref nint lpMenuWidths);
    [PreserveSig] int SetMenuSB(nint hmenuShared, nint holemenuRes, nint hwndActiveObject);
    [PreserveSig] int RemoveMenusSB(nint hmenuShared);
    [PreserveSig] int SetStatusTextSB([MarshalAs(UnmanagedType.LPWStr)] string? text);
    [PreserveSig] int EnableModelessSB(int fEnable);
    [PreserveSig] int TranslateAcceleratorSB(ref MSG pmsg, ushort wID);
    [PreserveSig] int BrowseObject(nint pidl, uint wFlags);
    [PreserveSig] int GetViewStateStream(uint grfMode, out nint ppStrm);
    [PreserveSig] int GetControlWindow(uint id, out nint phwnd);
    [PreserveSig] int SendControlMsg(uint id, uint uMsg, nint wParam, nint lParam, out nint pret);
    [PreserveSig] int QueryActiveShellView(out IShellView? ppshv);
    [PreserveSig] int OnViewWindowActive(IShellView psv);
    [PreserveSig] int SetToolbarItems(nint lpButtons, uint nButtons, uint uFlags);
}

[ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellView
{
    [PreserveSig] int GetWindow(out nint phwnd);
    [PreserveSig] int ContextSensitiveHelp(int fEnterMode);
    [PreserveSig] int TranslateAccelerator(ref MSG pmsg);
    [PreserveSig] int EnableModeless(int fEnable);
    [PreserveSig] int UIActivate(uint uState);
    [PreserveSig] int Refresh();
    [PreserveSig] int CreateViewWindow(nint psvPrevious, nint prcView, nint psb, nint prcViewOut, out nint hwndView);
    [PreserveSig] int DestroyViewWindow();
    [PreserveSig] int GetCurrentInfo(ref int lpfs);
    [PreserveSig] int AddPropertySheetPages(uint dwReserved, nint pfn, nint lparam);
    [PreserveSig] int SaveViewState();
    [PreserveSig] int SelectItem(nint pidlItem, uint uFlags);
    [PreserveSig] int GetItemObject(uint uItem, ref Guid riid, out nint ppv);
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public nint hwnd;
    public uint message;
    public nint wParam, lParam;
    public uint time;
    public int ptX, ptY;
}

/// <summary>
/// Lecture de la sélection courante de l'Explorateur Windows (fichiers avec chemins complets),
/// via les interfaces COM du Shell (IShellWindows → IShellBrowser → IShellView → IDataObject/CF_HDROP).
/// </summary>
internal static class ExplorerSelection
{
    private static readonly Guid CLSID_ShellWindows = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    private static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid IID_IShellBrowser = new("000214E2-0000-0000-C000-000000000046");
    private static readonly Guid IID_IDataObject = new("0000010E-0000-0000-C000-000000000046");
    private const uint SVGIO_SELECTION = 0x1;

    public static void TryGetPaths(nint hwnd, List<string> paths)
    {
        try
        {
            var type = Type.GetTypeFromCLSID(CLSID_ShellWindows);
            if (type is null) return;
            var shellWindows = (System.Collections.IEnumerable)Activator.CreateInstance(type)!;

            foreach (var obj in shellWindows)
            {
                long browserHwnd;
                try
                {
                    var raw = obj!.GetType().InvokeMember("HWND",
                        System.Reflection.BindingFlags.GetProperty, null, obj, null);
                    browserHwnd = raw switch { long v => v, int i => i, _ => throw new InvalidOperationException() };
                }
                catch { continue; }
                if ((nint)browserHwnd != hwnd) continue;

                GetSelectionFromBrowser(obj, paths);
                return; // une seule fenêtre correspond
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Sélection Explorateur illisible : " + ex.Message);
        }
    }

    private static void GetSelectionFromBrowser(object browser, List<string> paths)
    {
        nint pUnk = Marshal.GetIUnknownForObject(browser);
        try
        {
            var sidServiceProvider = SID_STopLevelBrowser;
            var iidServiceProvider = new Guid("6D5140C1-7436-11CE-8034-00AA006009FA");
            if (Marshal.QueryInterface(pUnk, ref iidServiceProvider, out nint pSp) != 0) return;
            try
            {
                var sp = (IServiceProviderCOM)Marshal.GetObjectForIUnknown(pSp);
                var sidBrowser = SID_STopLevelBrowser;
                var iidBrowser = IID_IShellBrowser;
                if (sp.QueryService(ref sidBrowser, ref iidBrowser, out nint pSb) != 0) return;
                try
                {
                    var shellBrowser = (IShellBrowser)Marshal.GetObjectForIUnknown(pSb);
                    if (shellBrowser.QueryActiveShellView(out var view) != 0 || view is null) return;
                    var iidDataObject = IID_IDataObject;
                if (view.GetItemObject(SVGIO_SELECTION, ref iidDataObject, out nint pDo) != 0) return;
                try
                {
                    ReadHdrop((System.Runtime.InteropServices.ComTypes.IDataObject)Marshal.GetObjectForIUnknown(pDo), paths);
                }
                finally { Marshal.Release(pDo); }
                }
                finally { Marshal.Release(pSb); }
            }
            finally { Marshal.Release(pSp); }
        }
        finally { Marshal.Release(pUnk); }
    }

    private static void ReadHdrop(System.Runtime.InteropServices.ComTypes.IDataObject dataObject, List<string> paths)
    {
        var fmt = new FORMATETC { cfFormat = 15 /*CF_HDROP*/, ptd = nint.Zero, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
        dataObject.GetData(ref fmt, out var medium);
        try
        {
            if (medium.tymed != TYMED.TYMED_HGLOBAL || medium.unionmember == 0) return;
            uint count = Win32.DragQueryFileW(medium.unionmember, 0xFFFFFFFF, null, 0);
            var sb = new StringBuilder(1024);
            for (uint i = 0; i < count && i < 64; i++)
            {
                if (Win32.DragQueryFileW(medium.unionmember, i, sb, sb.Capacity) > 0 && sb.Length > 0)
                    paths.Add(sb.ToString());
            }
        }
        finally
        {
            ReleaseStgMedium(ref medium);
        }
    }

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);
}

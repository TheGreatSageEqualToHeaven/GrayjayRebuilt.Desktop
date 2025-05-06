// DotCef, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// DotCef.DialogWindows

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace DotCef;

[SupportedOSPlatform("windows")]
public static class DialogWindows
{
    private const int MAX_PATH = 260;

    private const int OFN_PATHMUSTEXIST = 2048;

    private const int OFN_FILEMUSTEXIST = 4096;

    private const int OFN_ALLOWMULTISELECT = 512;

    private const int OFN_EXPLORER = 524288;

    private const int OFN_OVERWRITEPROMPT = 2;

    private const int OFN_NOCHANGEDIR = 8;

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetOpenFileName([In] [Out] ref OPENFILENAME ofn);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CommDlgExtendedError();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHBrowseForFolder(ref BROWSEINFO lpbi);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SHGetPathFromIDList(nint pidl, StringBuilder pszPath);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(nint ptr);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetSaveFileName([In] [Out] ref OPENFILENAME ofn);

    public static unsafe string[] PickFiles(bool multiple, params (string, string)[] filters)
    {
        var ofn = new OPENFILENAME
        {
            lStructSize = Marshal.SizeOf<OPENFILENAME>(),
            lpstrFile = Marshal.AllocHGlobal(2048)
        };
        Unsafe.InitBlockUnaligned((void*)ofn.lpstrFile, 0, 2048u);
        try
        {
            ofn.nMaxFile = 1024;
            ofn.Flags = 6144;
            if (multiple) ofn.Flags |= 524800;
            var stringBuilder = new StringBuilder();
            for (var i = 0; i < filters.Length; i++)
            {
                var tuple = filters[i];
                var item = tuple.Item1;
                var item2 = tuple.Item2;
                var stringBuilder2 = stringBuilder;
                var handler = new StringBuilder.AppendInterpolatedStringHandler(2, 2, stringBuilder2);
                handler.AppendFormatted(item);
                handler.AppendLiteral("\0");
                handler.AppendFormatted(item2);
                handler.AppendLiteral("\0");
                stringBuilder2.Append(ref handler);
            }

            stringBuilder.Append("\0");
            ofn.lpstrFilter = stringBuilder.ToString();
            var list = new List<string>();
            if (GetOpenFileName(ref ofn))
            {
                if (multiple)
                {
                    var lpstrFile = ofn.lpstrFile;
                    var text = Marshal.PtrToStringUni(lpstrFile);
                    lpstrFile += (text.Length + 1) * 2;
                    var value = Marshal.PtrToStringUni(lpstrFile);
                    if (string.IsNullOrEmpty(value))
                        list.Add(text);
                    else
                        while (true)
                        {
                            value = Marshal.PtrToStringUni(lpstrFile);
                            if (string.IsNullOrEmpty(value)) break;
                            list.Add(Path.Combine(text, value));
                            lpstrFile += (value.Length + 1) * 2;
                        }
                }
                else
                {
                    list.Add(Marshal.PtrToStringUni(ofn.lpstrFile));
                }
            }
            else
            {
                CommDlgExtendedError();
            }

            return list.ToArray();
        }
        finally
        {
            Marshal.FreeHGlobal(ofn.lpstrFile);
        }
    }

    public static string PickDirectory()
    {
        var lpbi = new BROWSEINFO
        {
            lpszTitle = "Select Directory"
        };
        var num = SHBrowseForFolder(ref lpbi);
        if (num != IntPtr.Zero)
        {
            var stringBuilder = new StringBuilder(260);
            if (SHGetPathFromIDList(num, stringBuilder))
            {
                CoTaskMemFree(num);
                return stringBuilder.ToString();
            }

            CoTaskMemFree(num);
        }

        return "";
    }

    public static string SaveFile(string defaultName, List<(string description, string extension)> filters)
    {
        var ofn = new OPENFILENAME
        {
            lStructSize = Marshal.SizeOf<OPENFILENAME>(),
            hwndOwner = IntPtr.Zero,
            lpstrFile = Marshal.AllocHGlobal(2048)
        };
        try
        {
            ofn.nMaxFile = 1024;
            ofn.lpstrFilter = "";
            ofn.nFilterIndex = 1;
            ofn.lpstrFileTitle = IntPtr.Zero;
            ofn.nMaxFileTitle = 0;
            ofn.lpstrInitialDir = IntPtr.Zero;
            ofn.Flags = 10;
            if (!string.IsNullOrEmpty(defaultName))
            {
                var bytes = Encoding.Unicode.GetBytes(defaultName);
                Marshal.Copy(length: Math.Min(bytes.Length, 1023), source: bytes, startIndex: 0,
                    destination: ofn.lpstrFile);
            }

            new StringBuilder(1024).Append(defaultName);
            var stringBuilder = new StringBuilder();
            foreach (var filter in filters)
            {
                var item = filter.description;
                var item2 = filter.extension;
                var stringBuilder2 = stringBuilder;
                var handler = new StringBuilder.AppendInterpolatedStringHandler(4, 2, stringBuilder2);
                handler.AppendFormatted(item);
                handler.AppendLiteral("\0*.");
                handler.AppendFormatted(item2);
                handler.AppendLiteral("\0");
                stringBuilder2.Append(ref handler);
            }

            stringBuilder.Append("\0");
            ofn.lpstrFilter = stringBuilder.ToString();
            var result = "";
            if (GetSaveFileName(ref ofn)) result = Marshal.PtrToStringUni(ofn.lpstrFile);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(ofn.lpstrFile);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;

        public nint hwndOwner;

        public nint hInstance;

        public string lpstrFilter;

        public string lpstrCustomFilter;

        public int nMaxCustFilter;

        public int nFilterIndex;

        public nint lpstrFile;

        public int nMaxFile;

        public nint lpstrFileTitle;

        public int nMaxFileTitle;

        public nint lpstrInitialDir;

        public string lpstrTitle;

        public int Flags;

        public short nFileOffset;

        public short nFileExtension;

        public string lpstrDefExt;

        public nint lCustData;

        public nint lpfnHook;

        public string lpTemplateName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BROWSEINFO
    {
        public nint hwndOwner;

        public nint pidlRoot;

        public nint pszDisplayName;

        public string lpszTitle;

        public uint ulFlags;

        public nint lpfn;

        public nint lParam;

        public int iImage;
    }
}
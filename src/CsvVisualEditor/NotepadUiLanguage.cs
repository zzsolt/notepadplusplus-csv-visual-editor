namespace CsvVisualEditor;

using CsvVisualEditor.Localization;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>Reads the documented narrow-char Notepad++ language API after NPPN_READY.</summary>
internal static class NotepadUiLanguage
{
    internal const uint GetNativeLanguageFilename = 1024 + 1000 + 116;
    private const int MaximumFilenameBytes = 1024;

    internal static string ReadFilename(IntPtr host) => ReadFilename((size, buffer) =>
        SendMessage(host, GetNativeLanguageFilename, size, buffer));

    internal static string ReadFilename(Func<IntPtr, IntPtr, IntPtr> request)
    {
        var requested = request(IntPtr.Zero, IntPtr.Zero).ToInt64();
        if (requested <= 0 || requested > MaximumFilenameBytes) return string.Empty;
        var capacity = checked((int)requested + 1);
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            for (var index = 0; index < capacity; index++) Marshal.WriteByte(buffer, index, 0);
            var received = request((IntPtr)capacity, buffer).ToInt64();
            if (received != requested || received < 0 || received >= capacity) return string.Empty;
            var bytes = new byte[(int)received];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            if (bytes.Any(static value => value == 0 || value > 127)) return string.Empty;
            return Encoding.ASCII.GetString(bytes);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}

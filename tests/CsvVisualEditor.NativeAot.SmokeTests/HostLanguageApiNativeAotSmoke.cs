namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CsvVisualEditor.Localization;

internal static class HostLanguageApiNativeAotSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        foreach (var entry in L10n.Languages)
        {
            var bytes = Encoding.ASCII.GetBytes(entry.NativeFilename);
            var calls = 0;
            var result = NotepadUiLanguage.ReadFilename((size, address) =>
            {
                calls++;
                if (address == IntPtr.Zero) return (IntPtr)bytes.Length;
                if (size.ToInt64() != bytes.Length + 1)
                    throw new InvalidOperationException("Host language buffer must include the terminator.");
                Marshal.Copy(bytes, 0, address, bytes.Length);
                Marshal.WriteByte(address, bytes.Length, 0);
                return (IntPtr)bytes.Length;
            });
            if (calls != 2 || L10n.ResolveNativeLanguage(result) != entry.Code)
                throw new InvalidOperationException("The documented narrow-character host language protocol must resolve the complete inventory.");
        }
        foreach (var count in new long[] { 0, -1, 1025, long.MaxValue })
        {
            var calls = 0;
            var result = NotepadUiLanguage.ReadFilename((_, _) => { calls++; return (IntPtr)count; });
            if (calls != 1 || result.Length != 0) throw new InvalidOperationException("Invalid/unsupported host language responses must fall back safely.");
        }
        if (NotepadUiLanguage.ReadFilename((_, address) => address == IntPtr.Zero ? (IntPtr)8 : (IntPtr)9) != "")
            throw new InvalidOperationException("A changed length between host queries must be rejected.");
        if (NotepadUiLanguage.ReadFilename((_, address) =>
        {
            if (address == IntPtr.Zero) return (IntPtr)3;
            Marshal.Copy(new byte[] { 255, 254, 253 }, 0, address, 3);
            return (IntPtr)3;
        }) != "") throw new InvalidOperationException("Language identifiers must be ASCII filenames, not encoded display names.");
        Console.WriteLine("Host language API: all inventory filenames and invalid-length/encoding fallbacks PASS.");
    }
}

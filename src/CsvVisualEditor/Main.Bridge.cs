namespace CsvVisualEditor;

using Npp.DotNet.Plugin;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

partial class Main
{
    #region Notepad++ unmanaged bridge - do not edit
    [UnmanagedCallersOnly(EntryPoint = "setInfo", CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe void SetInfo(NppData* notepadPlusData)
    {
        PluginData.NppData = *notepadPlusData;
        Instance.OnSetInfo();
    }

    [UnmanagedCallersOnly(EntryPoint = "beNotified", CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe void BeNotified(ScNotification* notification)
    {
        Instance.OnBeNotified(*notification);
    }

    [UnmanagedCallersOnly(EntryPoint = "messageProc", CallConvs = [typeof(CallConvCdecl)])]
    internal static NativeBool MessageProc(uint msg, UIntPtr wParam, IntPtr lParam)
    {
        return Instance.OnMessageProc(msg, wParam, lParam);
    }

    [UnmanagedCallersOnly(EntryPoint = "getFuncsArray", CallConvs = [typeof(CallConvCdecl)])]
    internal static IntPtr GetFuncsArray(IntPtr nbF) => IDotNetPlugin.OnGetFuncsArray(nbF);

    [UnmanagedCallersOnly(EntryPoint = "getName", CallConvs = [typeof(CallConvCdecl)])]
    internal static IntPtr GetName() => IDotNetPlugin.OnGetName();

    [UnmanagedCallersOnly(EntryPoint = "isUnicode", CallConvs = [typeof(CallConvCdecl)])]
    internal static NativeBool IsUnicode() => IDotNetPlugin.OnIsUnicode();
    #endregion
}

namespace CsvVisualEditor;

using CsvVisualEditor.Core;
using Npp.DotNet.Plugin;
using System.Runtime.InteropServices;

/// <summary>
/// Expands only the untouched Notepad++ default right-dock width on first display.
/// The host's existing user-sized layout is preserved.
/// </summary>
internal static class NotepadDockWidthAdjuster
{
    private const uint DmmMoveSplitter = 0x500B;
    private const string DockingManagerClassName = "dockingManager";
    private const string VerticalDockSplitterClassName = "wedockspliter";
    private const int MaximumLogicalSplitterDistance = 48;

    public static bool TryExpandInitialRightDock(Control dockedControl)
    {
        ArgumentNullException.ThrowIfNull(dockedControl);

        if (!dockedControl.IsHandleCreated)
        {
            return false;
        }

        var notepadHandle = PluginData.NppData.NppHandle;
        if (notepadHandle == IntPtr.Zero)
        {
            return false;
        }

        var clientTabHandle = GetParent(dockedControl.Handle);
        var dockContainerHandle = GetParent(clientTabHandle);
        if (clientTabHandle == IntPtr.Zero || dockContainerHandle == IntPtr.Zero)
        {
            return false;
        }

        if (!GetWindowRect(dockContainerHandle, out var containerRect) ||
            !GetWindowRect(notepadHandle, out var notepadWindowRect) ||
            !GetClientRect(notepadHandle, out var notepadClientRect))
        {
            return false;
        }

        var notepadCenterX = notepadWindowRect.Left + (notepadWindowRect.Width / 2);
        var containerCenterX = containerRect.Left + (containerRect.Width / 2);
        if (containerCenterX <= notepadCenterX)
        {
            return false;
        }

        var targetWidth = DockPanelSizingPolicy.CalculateTargetWidth(
            notepadClientRect.Width,
            containerRect.Width,
            dockedControl.DeviceDpi);
        var requestedIncrease = targetWidth - containerRect.Width;
        if (requestedIncrease <= 0)
        {
            return false;
        }

        var dockingManagerHandle = FindWindowEx(
            notepadHandle,
            IntPtr.Zero,
            DockingManagerClassName,
            null);
        if (dockingManagerHandle == IntPtr.Zero)
        {
            return false;
        }

        var splitterHandle = FindRightDockSplitter(
            notepadHandle,
            containerRect,
            dockedControl.DeviceDpi);
        if (splitterHandle == IntPtr.Zero)
        {
            return false;
        }

        SendMessage(
            dockingManagerHandle,
            DmmMoveSplitter,
            requestedIncrease,
            splitterHandle);

        return GetWindowRect(dockContainerHandle, out var resizedRect) &&
               resizedRect.Width > containerRect.Width;
    }

    private static IntPtr FindRightDockSplitter(
        IntPtr notepadHandle,
        NativeRect containerRect,
        int dpi)
    {
        var bestHandle = IntPtr.Zero;
        var bestDistance = int.MaxValue;
        var childAfter = IntPtr.Zero;

        while (true)
        {
            childAfter = FindWindowEx(
                notepadHandle,
                childAfter,
                VerticalDockSplitterClassName,
                null);
            if (childAfter == IntPtr.Zero)
            {
                break;
            }

            if (!IsWindowVisible(childAfter) ||
                !GetWindowRect(childAfter, out var splitterRect))
            {
                continue;
            }

            var verticalOverlap = Math.Min(containerRect.Bottom, splitterRect.Bottom) -
                                  Math.Max(containerRect.Top, splitterRect.Top);
            if (verticalOverlap <= 0)
            {
                continue;
            }

            var distance = Math.Abs(containerRect.Left - splitterRect.Right);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestHandle = childAfter;
            }
        }

        var maximumDistance = (int)Math.Round(
            MaximumLogicalSplitterDistance * (dpi / 96d),
            MidpointRounding.AwayFromZero);

        return bestDistance <= maximumDistance ? bestHandle : IntPtr.Zero;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(
        IntPtr parentHandle,
        IntPtr childAfter,
        string? className,
        string? windowName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(
        IntPtr windowHandle,
        out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(
        IntPtr windowHandle,
        out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr windowHandle,
        uint message,
        nint wParam,
        nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;

        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }
}

namespace CsvVisualEditor.NativeAot.SmokeTests;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class CellKeyboardNativeAotSmoke
{
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

    [ModuleInitializer]
    internal static void Run()
    {
        using var form = new Form { Size = new Size(600, 300) };
        using var grid = new CsvDataGridView
        {
            Dock = DockStyle.Fill, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.RowHeaderSelect
        };
        grid.Columns.Add("CsvColumn0", "Value");
        grid.Rows.Add("old");
        form.Controls.Add(grid);
        form.Show();
        Application.DoEvents();
        grid.CurrentCell = grid.Rows[0].Cells[0];
        Require(grid.BeginEdit(true), "The real grid editing control must open.");
        var editor = (TextBox)grid.EditingControl!;
        editor.Clear();
        editor.Focus();

        // Deliberately omit managed keydown preprocessing: native modeless hosts
        // can deliver translated character messages directly to a cell editor.
        const string typed = "  a b\\c  ";
        foreach (var c in typed)
            SendMessage(editor.Handle, 0x0102, (IntPtr)c, (IntPtr)1);
        Require(editor.Text == typed, "Native character delivery must preserve every Space and literal backslash exactly once.");
        editor.Select(2, 1);
        SendMessage(editor.Handle, 0x0102, (IntPtr)' ', (IntPtr)1);
        Require(editor.Text == "    b\\c  ", "A typed Space must replace only the selection.");
        Require(grid.EndEdit(), "The text editor must commit to the grid normally.");
        Require(grid.Rows[0].Cells[0].Value as string == "    b\\c  ", "Grid commit must not trim spaces or escape backslashes.");

        Require(grid.BeginEdit(true), "Reused text editor must open again.");
        editor = (TextBox)grid.EditingControl!;
        editor.ReadOnly = true;
        var before = editor.Text;
        SendMessage(editor.Handle, 0x0102, (IntPtr)' ', (IntPtr)1);
        Require(editor.Text == before, "Read-only editor must not accept a Space.");
        grid.CancelEdit();
        form.Close();
        Console.WriteLine("Cell keyboard: native Space without managed keydown, repeated/edge spaces, replacement, backslash and read-only control PASS.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

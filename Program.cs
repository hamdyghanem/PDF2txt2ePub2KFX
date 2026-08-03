namespace NileFusion.BookConverter;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Enable visual styles for WinForms
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

namespace ImageToIcon
{
    using System;
    using System.IO;
    using System.Windows.Forms;
    using SilDev;
    using SilDev.Ini.Legacy;

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Ini.SetFile(Path.ChangeExtension(PathEx.LocalPath, ".ini"));
            Log.AllowLogging(Ini.FilePath);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}

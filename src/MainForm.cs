namespace ImageToIcon;

using Properties;
using SilDev;
using SilDev.Drawing;
using SilDev.Forms;
using SilDev.Ini.Legacy;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static SilDev.WinApi;
using SharpImage = SixLabors.ImageSharp.Image;
using SharpPngEncoder = SixLabors.ImageSharp.Formats.Png.PngEncoder;

public partial class MainForm : Form
{
    private static readonly List<string> TmpFiles = new();
    private static string _fileName;
    private bool _isCliMode;

    public MainForm()
    {
        InitializeComponent();

        if (TryRunCliMode())
        {
            Close();
            return;
        }

        SuspendLayout();

        var dpi = Desktop.GetDpi();
        if (dpi > 96f)
        {
            base.MinimumSize = new(base.MinimumSize.Width, LocalGetDpiDimension(base.MinimumSize.Height, dpi));
            Size = base.MinimumSize;
        }

        if (Desktop.AppsUseDarkTheme)
        {
            this.EnableDarkMode();
            this.ChangeColorMode(ControlExColorMode.DarkDarkDark);
            base.BackColor = Color.FromArgb(32, 32, 32);
        }

        ResumeLayout(false);
        return;

        static int LocalGetDpiDimension(int i, float dpi, bool upscale = false) =>
            (int)Math.Round(i / (upscale ? dpi * 96f - dpi : dpi * 96f + dpi) / 1.5f);
    }

    private static Image LoadImageFromPath(string path)
    {
        try
        {
            if (!path.EndsWithEx(".webp"))
                return new Bitmap(path);
            var tmpFile = FileEx.GetUniqueTempPath();
            using (var webp = SharpImage.Load(path))
            {
                using var png = new FileStream(tmpFile, FileMode.Create);
                webp.Save(png, new SharpPngEncoder());
            }
            TmpFiles.Add(tmpFile);
            return new Bitmap(tmpFile);
        }
        catch (Exception ex) when (ex.IsCaught())
        {
            Log.Write(ex);
            return default;
        }
    }

    private static Image ValidateImageSize(Image image, Size size) =>
        !image.Size.Equals(size) ? image.Redraw(size.Width, size.Height) : image;

    private void MainForm_Load(object sender, EventArgs e)
    {
        Extended.Checked = Ini.Read("Settings", nameof(Extended), false);

        var file = EnvironmentEx.CommandLineArgs(false, false).FirstOrDefault(IsImageFile);
        var image = LoadImageFromPath(file);
        UpdateImages(image ?? Resources.Symbol, true);
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        TmpFiles.ForEach(x => CmdExec.WaitForExitThenDelete(x, ProcessEx.CurrentName));
        if (Extended.Checked != Ini.Read("Settings", nameof(Extended), false))
            Ini.WriteDirect("Settings", nameof(Extended), Extended.Checked);
    }

    private void Extended_CheckedChanged(object sender, EventArgs e)
    {
        if (sender is CheckBox)
            UpdateImages(ImgPanel.Controls.Cast<Control>().Select(x => x.BackgroundImage).FirstOrDefault());
    }

    private void OpenBtn_Click(object sender, EventArgs e) =>
        UpdateImages(OpenImageFileDialog());

    private void SaveBtn_Click(object sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog();
        dialog.FileName = _fileName;
        dialog.Filter = @"Icon files (*.ico)|*.ico";
        if (dialog.ShowDialog() != DialogResult.OK)
            return;
        var images = ImgPanel.Controls.Cast<Control>().Select(x => (Image)x.BackgroundImage.Clone());
        IconFactory.Save(images, dialog.FileName);
        MessageBoxEx.Show(this, "File successfully saved!", Text, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
    }

    private bool IsImageFile(string str) 
    {
        if (!FileEx.Exists(str))
        {
            if (_isCliMode && PathEx.IsValidPath(str))
                Console.WriteLine($"Skipped: '{str}' not found.\n");
            return false;
        }
        if (str.EndsWithEx(
            ".bmp", ".dib", ".rle",
            ".jpg", ".jepg", ".jpe", ".jfif",
            ".tif", ".tiff",
            ".png",
            ".webp"))
            return true;

        if (_isCliMode) 
            Console.WriteLine($"Skipped: '{str}' is an unsupported type.\n");
        return false;
    }

    private IEnumerable<Image> GetImages(Image image)
    {
        if (image == null)
            return default;

        var size = image.Width;
        var sizes = IconFactory.GetSizes(Extended.Checked ? IconFactorySizeOption.Additional : IconFactorySizeOption.Application).ToArray();
        return sizes.Where(x => size >= x).Select(x => image.Redraw(x, x));
    }

    private void UpdateImages(Image image, bool centerWindow = false)
    {
        if (image == null)
            return;
        
        var images = GetImages(image);

        SuspendLayout();
        ImgPanel.SuspendLayout();
        ImgPanel.Controls.Clear();
        var color = Color.DodgerBlue;
        foreach (var label in from img in images
                              let bgImg = img
                              let bgSize = bgImg.Width
                              select new Label
                              {
                                  BackgroundImage = bgImg,
                                  BackgroundImageLayout = ImageLayout.Stretch,
                                  AllowDrop = true,
                                  ForeColor = color,
                                  Font = new Font("Calibri", 5.75f, FontStyle.Bold, GraphicsUnit.Point, 0),
                                  Margin = new Padding(2, 2, 2, 2),
                                  Size = img.Size,
                                  Text = bgSize.ToString(),
                                  TextAlign = ContentAlignment.TopRight
                              })
        {
            label.DragEnter += LocalLabelDragEnter;
            label.DragDrop += LocalLabelDragDrop;
            label.MouseEnter += LocalLabelMouseEnter;
            label.MouseLeave += LocalLabelMouseLeave;
            label.Click += LocalLabelClick;
            ControlEx.DrawBorder(label, color, ControlExBorderStyle.Dashed);
            ImgPanel.Controls.Add(label);
        }

        var width = SystemInformation.BorderSize.Width * 2 +
                    SystemInformation.FrameBorderSize.Width * 2 +
                    SystemInformation.SizingBorderWidth * 2 +
                    LayoutPanel.Margin.Horizontal +
                    LayoutPanel.Padding.Horizontal +
                    ImgPanel.Margin.Horizontal +
                    ImgPanel.Padding.Horizontal +
                    ImgPanel.Controls.OfType<Label>().Sum(control => control.Width +
                                                                     control.Margin.Horizontal +
                                                                     control.Padding.Horizontal);
        Width = width > MinimumSize.Width ? width : MinimumSize.Width;
        if (Log.DebugMode > 0)
            Log.Write($"New width: {width}");
        if (centerWindow)
            NativeHelper.CenterWindow(Handle);

        ImgPanel.ResumeLayout();
        ResumeLayout();

        Icon = image.ToIcon();
        return;

        static void LocalLabelDragEnter(object sender, DragEventArgs e)
        {
            if (sender is Label)
                e.Effect = DragDropEffects.Copy;
        }

        static void LocalLabelDragDrop(object sender, DragEventArgs e)
        {
            if (sender is not Label owner || e.Data.GetData(DataFormats.FileDrop, false) is not string[] paths)
                return;
            var newBgImg = LoadImageFromPath(paths.FirstOrDefault());
            if (newBgImg != null)
                owner.BackgroundImage = ValidateImageSize(newBgImg, owner.Size);
        }

        void LocalLabelMouseEnter(object sender, EventArgs e)
        {
            if (sender is not Label owner)
                return;
            toolTip.SetToolTip(owner, $"{owner.Text}x{owner.Text}");
            owner.Cursor = Cursors.Hand;
        }

        void LocalLabelMouseLeave(object sender, EventArgs e)
        {
            if (sender is not Label owner)
                return;
            toolTip.RemoveAll();
            owner.Cursor = Cursors.Default;
        }

        void LocalLabelClick(object sender, EventArgs e)
        {
            if (sender is not Label owner)
                return;
            var newBgImg = OpenImageFileDialog();
            if (newBgImg != null)
                owner.BackgroundImage = ValidateImageSize(newBgImg, owner.Size);
        }
    }

    private Image OpenImageFileDialog()
    {
        using var dialog = new OpenFileDialog();
        dialog.CheckFileExists = true;
        dialog.CheckPathExists = true;
        dialog.Multiselect = false;
        var imageEncoders = ImageCodecInfo.GetImageEncoders();
        var extensions = new List<string>();
        for (var i = 0; i < imageEncoders.Length; i++)
        {
            extensions.Add(imageEncoders[i].FilenameExtension.ToLower());
            var description = imageEncoders[i].CodecName.Substring(8).Replace("Codec", "Files").Trim();
            var pattern = extensions[extensions.Count - 1];
            dialog.Filter = $@"{dialog.Filter}{(i > 0 ? "|" : string.Empty)}{description} ({pattern})|{pattern}";
        }
        extensions.Add("*.webp");
        dialog.Filter = $@"{dialog.Filter}{"|"}{"WEBP Files"} ({"*.webp"})|{"*.webp"}";
        dialog.Filter = $@"{dialog.Filter}|Image Files ({extensions.Join(";")})|{extensions.Join(";")}";
        dialog.FilterIndex = extensions.Count + 1;
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return default;
        var selectedFile = dialog.FileName;
        try
        {
            var bmp = LoadImageFromPath(selectedFile);
            _fileName = Path.GetFileNameWithoutExtension(selectedFile);
            return bmp;
        }
        catch (Exception ex) when (ex.IsCaught())
        {
            return null;
        }
    }

    private bool TryRunCliMode()
    {
        var args = EnvironmentEx.CommandLineArgs(false, false).Distinct().ToList();
        if (!args.Any(s => s.EqualsEx("--cli", "/cli", "--help", "/?")))
            return false;

        _isCliMode = true;
        ConsoleWindow.Allocate();

        if (args.Any(s => s.EqualsEx("--help", "/?")))
        {
            Console.WriteLine(Resources.CliHelp.FormatInvariant(ProcessEx.CurrentName));
            return true;
        }

        var outArg = args.FirstOrDefault(s => s.StartsWithEx("--o=", "/o="));
        if (string.IsNullOrWhiteSpace(outArg))
        {
            Console.WriteLine("Error: Missing output path.");
            Console.WriteLine(Resources.CliHelp.FormatInvariant(ProcessEx.CurrentName));
            return true;
        }

        var outDir = outArg.Substring(outArg.IndexOf('=') + 1).Trim('"');
        if (!Directory.Exists(outDir))
        {
            var envDir = PathEx.Combine(outDir);
            if (!Directory.Exists(envDir))
            { 
                Console.WriteLine($"Error: Output path '{outDir}' does not exist.");
                return true;
            }
            outDir = envDir;
        }

        var files = args.Where(IsImageFile);
        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine($"Skipped: '{file}' not found.\n");
                continue;
            }

            Console.WriteLine($"Processing '{file}' . . .");

            var image = LoadImageFromPath(file);
            if (image == null)
            {
                Console.WriteLine("Skipped: Invalid image format.\n");
                continue;
            }

            var fileName = Path.GetFileNameWithoutExtension(file);
            var outPath = PathEx.Combine(outDir, fileName + ".ico");

            if (FileEx.Exists(outPath))
                outPath = PathEx.Combine(outDir, $"{fileName}___{DateTime.Now:yyyyMMddHHmmssfff}.ico");

            Console.WriteLine("Creating images . . .");
            var images = GetImages(image);

            Console.WriteLine("Saving icon . . .");
            IconFactory.Save(images, outPath);

            Console.WriteLine($"Done: '{outPath}' saved.\n");
        }

        return true;
    }

}
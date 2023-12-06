namespace ImageToIcon
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;
    using Properties;
    using SilDev;
    using SilDev.Drawing;
    using SilDev.Forms;
    using SilDev.Ini.Legacy;

    public partial class MainForm : Form
    {
        private static string _fileName;

        public sealed override Size MaximumSize
        {
            get => base.MaximumSize;
            set => base.MaximumSize = value;
        }

        public sealed override Size MinimumSize
        {
            get => base.MinimumSize;
            set => base.MinimumSize = value;
        }

        public sealed override Color BackColor
        {
            get => base.BackColor;
            set => base.BackColor = value;
        }

        public MainForm()
        {
            InitializeComponent();

            SuspendLayout();

            var dpi = Desktop.GetDpi();
            if (dpi > 96f)
            {
                MinimumSize = new(MinimumSize.Width, GetDpiDimension(MinimumSize.Height, dpi));
                Size = MinimumSize;
            }

            if (Desktop.AppsUseDarkTheme)
            {
                this.EnableDarkMode();
                this.ChangeColorMode(ControlExColorMode.DarkDarkDark);
                BackColor = Color.FromArgb(32, 32, 32);
            }

            ResumeLayout(false);
        }

        private static int GetDpiDimension(int i, float dpi, bool upscale = false)
        {
            if (upscale)
                return (int)Math.Round(i / 96f * dpi - dpi / 1.5f);
            return (int)Math.Round(i / dpi * 96f + dpi / 1.5f);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Extended.Checked = Ini.Read("Settings", nameof(Extended), false);
            UpdateImages(Resources.Symbol, true);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Extended.Checked == Ini.Read("Settings", nameof(Extended), false))
                return;
            Ini.WriteDirect("Settings", nameof(Extended), Extended.Checked);
        }

        private void Extended_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is not CheckBox)
                return;
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

        private void Label_MouseEnter(object sender, EventArgs e)
        {
            if (sender is not Label owner)
                return;
            toolTip.SetToolTip(owner, $"{owner.Text}x{owner.Text}");
            owner.Cursor = Cursors.Hand;
        }

        private void Label_MouseLeave(object sender, EventArgs e)
        {
            if (sender is not Label owner)
                return;
            toolTip.RemoveAll();
            owner.Cursor = Cursors.Default;
        }

        private void Label_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is not Label)
                return;
            e.Effect = DragDropEffects.Copy;
        }

        private void Label_DragDrop(object sender, DragEventArgs e)
        {
            if (sender is not Label owner || e.Data.GetData(DataFormats.FileDrop, false) is not string[] paths)
                return;
            try
            {
                var bmp = new Bitmap(paths.First());
                owner.BackgroundImage = bmp.Redraw(owner.Width);
            }
            catch (Exception ex) when (ex.IsCaught())
            {
                Log.Write(ex);
            }
        }

        private void Label_Click(object sender, EventArgs e)
        {
            if (sender is not Label owner)
                return;
            var newBgImg = OpenImageFileDialog();
            if (newBgImg == null)
                return;
            owner.BackgroundImage = !newBgImg.Size.Equals(owner.Size) ? newBgImg.Redraw(owner.Width, owner.Height) : newBgImg;
        }

        private void UpdateImages(Image image, bool centerWindow = false)
        {
            if (image == null)
                return;

            var size = image.Width;
            var sizes = IconFactory.GetSizes(Extended.Checked ? IconFactorySizeOption.Additional : IconFactorySizeOption.Application).ToArray();
            var images = sizes.Where(x => size >= x).Select(x => image.Redraw(x, x));

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
                label.MouseEnter += Label_MouseEnter;
                label.MouseLeave += Label_MouseLeave;
                label.DragEnter += Label_DragEnter;
                label.DragDrop += Label_DragDrop;
                label.Click += Label_Click;
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
                WinApi.NativeHelper.CenterWindow(Handle);

            ImgPanel.ResumeLayout();
            ResumeLayout();

            Icon = image.ToIcon();
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
                dialog.Filter = string.Format(@"{0}{1}{2} ({3})|{3}", dialog.Filter, i > 0 ? "|" : string.Empty, description, pattern);
            }
            dialog.Filter = string.Format(@"{0}|Image Files ({1})|{1}", dialog.Filter, extensions.Join(";"));
            dialog.FilterIndex = imageEncoders.Length + 1;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return default;
            try
            {
                var bmp = new Bitmap(dialog.FileName);
                _fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                return bmp;
            }
            catch (Exception ex) when (ex.IsCaught())
            {
                return null;
            }
        }
    }
}

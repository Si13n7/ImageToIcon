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

    public partial class MainForm : Form
    {
        private static string _fileName;

        public MainForm() =>
            InitializeComponent();

        private void MainForm_Load(object sender, EventArgs e)
        {
            Extended.Checked = Ini.Read("Settings", nameof(Extended), false);
            UpdateImages(Resources.Symbol);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Extended.Checked == Ini.Read("Settings", nameof(Extended), false))
                return;
            Ini.WriteDirect("Settings", nameof(Extended), Extended.Checked);
        }

        private void Extended_CheckedChanged(object sender, EventArgs e)
        {
            if (!(sender is CheckBox owner))
                return;
            var image = ImgPanel.Controls.Cast<Control>().Select(x => x.BackgroundImage).FirstOrDefault();
            UpdateImages(image);
            Size = owner.Checked ? MaximumSize : MinimumSize;
        }

        private void OpenBtn_Click(object sender, EventArgs e) =>
            UpdateImages(OpenImageFileDialog());

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog { FileName = _fileName, Filter = @"Icon files (*.ico)|*.ico" };
            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            var images = ImgPanel.Controls.Cast<Control>().Select(x => (Image)x.BackgroundImage.Clone());
            IconFactory.Save(images, dialog.FileName);
            MessageBoxEx.Show(this, "File successfully saved!", Text, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        private void UpdateImages(Image image)
        {
            if (image == null)
                return;

            var draw = image;
            var size = image.Width;
            var sizes = IconFactory.GetSizes(Extended.Checked ? IconFactorySizeOption.Additional : IconFactorySizeOption.Application);
            var images = sizes.Where(x => size >= x).Select(x => draw.Redraw(x, x));

            SuspendLayout();
            ImgPanel.SuspendLayout();
            ImgPanel.Controls.Clear();
            var color = Color.DodgerBlue;
            foreach (var img in images)
            {
                var bgImg = img;
                var bgSize = bgImg.Width;
                var label = new Label
                {
                    BackgroundImage = bgImg,
                    BackgroundImageLayout = ImageLayout.Stretch,
                    ForeColor = color,
                    Font = new Font("Calibri", 5.75f, FontStyle.Bold, GraphicsUnit.Point, 0),
                    Margin = new Padding(2, 2, 2, 2),
                    Size = img.Size,
                    Text = bgSize.ToString(),
                    TextAlign = ContentAlignment.TopRight
                };
                label.MouseEnter += delegate { label.Cursor = Cursors.Hand; };
                label.MouseLeave += delegate { label.Cursor = Cursors.Default; };
                label.Click += delegate
                {
                    var newBgImg = OpenImageFileDialog();
                    if (newBgImg == null)
                        return;
                    label.BackgroundImage = !newBgImg.Size.Equals(label.Size) ? newBgImg.Redraw(label.Width, label.Height) : newBgImg;
                };
                ControlEx.DrawBorder(label, color, ControlExBorderStyle.Dashed);
                ImgPanel.Controls.Add(label);
            }
            ImgPanel.ResumeLayout();
            ResumeLayout();

            Icon = image.ToIcon();
        }

        private Image OpenImageFileDialog()
        {
            using var dialog = new OpenFileDialog { CheckFileExists = true, CheckPathExists = true, Multiselect = false };
            var imageEncoders = ImageCodecInfo.GetImageEncoders();
            var extensions = new List<string>();
            for (var i = 0; i < imageEncoders.Length; i++)
            {
                extensions.Add(imageEncoders[i].FilenameExtension.ToLower());
                var description = imageEncoders[i].CodecName.Substring(8).Replace("Codec", "Files").Trim();
                var pattern = extensions[extensions.Count - 1];
                dialog.Filter = string.Format("{0}{1}{2} ({3})|{3}", dialog.Filter, i > 0 ? "|" : string.Empty, description, pattern);
            }
            dialog.Filter = string.Format("{0}|Image Files ({1})|{1}", dialog.Filter, extensions.Join(";"));
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

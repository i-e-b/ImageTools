using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ImageTools.ImageStorageFileFormats;
using static System.Drawing.Brushes;

namespace ImageViewer
{
    public partial class Form1 : Form
    {
        private string? _baseFolder;
        private List<string> _files = new List<string>();
        private int _fileIndex;
        private Bitmap? _imageCache;
        private string _message = "";
        private readonly Brush _background = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.Azure, Color.Aqua);
        private bool _drag;
        private readonly object _loadLock = new object();
        private float _x, _y;
        private int _mx, _my;
        private float _scale = 1.0f;

        public Form1(string[]? args)
        {
            InitializeComponent();

            _baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (args?.Length > 0)
            {
                _baseFolder = Path.GetDirectoryName(args[0]);
                RefreshImageSet(args[0]);
            }
            else
            {
                RefreshImageSet();
            }

            MouseWheel += ChangeZoom;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            
            e.Graphics.FillRectangle(_background, 0, 0, Width, Height);

            lock (_loadLock)
            {
                e.Graphics.ScaleTransform(_scale,_scale);
                if (_imageCache != null) e.Graphics.DrawImage(_imageCache, new PointF(_x, _y));
                e.Graphics.ScaleTransform(1/_scale,1/_scale);
            }
            
            e.Graphics.DrawString(_message, Font, Black, 10, 10);
        }

        private void pickImgBtn_Click(object sender, EventArgs e)
        {
            var folderPicker = new FolderBrowserDialog
            {
                Description = "Pick a base folder",
                ShowNewFolderButton = false
            };

            var result = folderPicker.ShowDialog();
            switch (result)
            {
                case DialogResult.OK:
                case DialogResult.Yes:
                    _baseFolder = folderPicker.SelectedPath;
                    RefreshImageSet();
                    return;
                default:
                    return;
            }
        }

        private void RefreshImageSet(string? initial = null)
        {
            if (string.IsNullOrWhiteSpace(_baseFolder!)) return;
            _files = Directory
                .EnumerateFiles(_baseFolder!)
                .Where(NotKnownBadFiles)
                .ToList();
            
            _fileIndex = Math.Max(0, _files.IndexOf(initial??""));
            ReloadImage();
        }

        private bool NotKnownBadFiles(string arg)
        {
            if (arg.EndsWith("\\desktop.ini", StringComparison.OrdinalIgnoreCase)) return false;
            
            return true;
        }

        private void ReloadImage()
        {
            if (_files.Count < 1)
            {
                Text = "No files in "+_baseFolder;
                return;
            }

            // Clear out the old image
            _imageCache?.Dispose();
            _imageCache = null;
            _message = "Loading";
            _x = 0;
            _y = 0;
            Invalidate();
            
            // Try loading the file as an image, and cache to a bitmap.
            // If it fails, we'll write the file name and a message instead.
            Text = _files[_fileIndex] ?? "";
            
            ThreadPool.QueueUserWorkItem(x => {
                try
                {
                    var bmp = ImageTools.Utilities.Load.FromFile(_files[_fileIndex] ?? "");
                    lock (_loadLock)
                    {
                        _imageCache?.Dispose();
                        _imageCache = bmp;
                        _message = "";
                    }
                }
                catch (Exception ex)
                {
                    _message = ex.ToString();
                }

                Invalidate();
            });
        }

        private void leftBtn_Click(object sender, EventArgs e) => DecrementSelectedFile();
        private void rightBtn_Click(object sender, EventArgs e) => IncrementSelectedFile();

        private void IncrementSelectedFile()
        {
            _fileIndex++;
            if (_fileIndex >= _files.Count) _fileIndex = _files.Count - 1;
            else ReloadImage();
        }
        
        private void DecrementSelectedFile()
        {
            _fileIndex--;
            if (_fileIndex < 0) _fileIndex = 0;
            else ReloadImage();
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Clicks > 1)
            {
                var newScale = _scale > 1.0f ? 1.0f : 4.0f;
                UpdateScaleAdjusted(e.X, e.Y, newScale);
                Invalidate();
                return;
            }

            _mx = e.X; _my = e.Y;
            _drag = true;
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e) => _drag = false;

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_drag) return;
            _x += (e.X - _mx) / _scale;
            _y += (e.Y - _my) / _scale;
            _mx = e.X; _my = e.Y;
            PinScrollToScreen();
            Invalidate();
        }

        private void UpdateScaleAdjusted(float mouseX, float mouseY, float newScale)
        {
            var oldScale = _scale;
            
            // compensate for centre point
            // Should zoom over relative mouse point if possible
            
            // the _x,_y offset is scaled, so we want to find out
            // where the mouse is in relation to it before the scale
            // and then update _x,_y so that point is still under the
            // mouse after the scale
            
            // page xy at old scale
            var px = -_x + (mouseX / oldScale);
            var py = -_y + (mouseY / oldScale);
            
            // where would we be on the page at the new scale?
            var ax = -_x + (mouseX / newScale);
            var ay = -_y + (mouseY / newScale);

            _x += ax - px;
            _y += ay - py;
            _scale = newScale;
            PinScrollToScreen();
        }

        private void ChangeZoom(object sender, MouseEventArgs e)
        {
            if (e.Delta == 0) return;
            
            var speed = (ModifierKeys & Keys.Control) != 0 ? 0.1f : 0.01f;
            var newScale = _scale + Math.Sign(e.Delta) * speed;
                
            if (newScale < 0.01f) newScale = 0.01f;
            if (newScale > 10.0f) newScale = 10.0f;
            
            UpdateScaleAdjusted(e.X, e.Y, newScale);
            
            Text = (_scale * 100).ToString("0.0");
            Invalidate();
        }
        
        private void PinScrollToScreen()
        {
            if (_imageCache == null) return;
            
            
            var imgWidth = _imageCache.Width;
            var halfScreenWidth = (Width / 2.0f) / _scale;
            var imgHeight = _imageCache.Height;
            var halfScreenHeight = (Height / 2.0f) / _scale;
            
            _x = Math.Min(halfScreenWidth,Math.Max(-imgWidth + halfScreenWidth,_x));
            _y = Math.Min(halfScreenHeight,Math.Max(-imgHeight + halfScreenHeight,_y));
        }

        private void resetZoomBtn_Click(object sender, EventArgs e)
        {
            _x = _y = 0;
            _scale = 1.0f;
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left:
                    DecrementSelectedFile();
                    return false;

                case Keys.Right:
                    IncrementSelectedFile();
                    return false;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void editButton_Click(object sender, EventArgs e)
        {
            // C:\Program Files\paint.net\PaintDotNet.exe
            
            if (string.IsNullOrWhiteSpace(_files[_fileIndex]!)) return;
            
            // TODO: refine this so we can find other install locations
            Process.Start(@"C:\Program Files\paint.net\PaintDotNet.exe", _files[_fileIndex]!);
            
            //MessageBox.Show("Not implemented");
        }

        private void exportButton_Click(object sender, EventArgs e)
        {
            if (_imageCache == null) return;
            
            var fileDialog = new SaveFileDialog
            {
                Title = "Save file as...",
                Filter = "*.wfi|WFI files",
                DefaultExt = ".wfi",
                FileName = Path.GetFileNameWithoutExtension(_files[_fileIndex] ?? ""),
                SupportMultiDottedExtensions = true,
                AddExtension = true,
                OverwritePrompt = true
            };

            var result = fileDialog.ShowDialog();
            switch (result)
            {
                case DialogResult.OK:
                case DialogResult.Yes:
                    _imageCache?.SaveWaveletImageFormat(fileDialog.FileName!);
                    RefreshImageSet();
                    return;
                default:
                    return;
            }
        }
    }
}
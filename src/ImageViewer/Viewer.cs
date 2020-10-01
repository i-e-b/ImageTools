using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
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
        private int _x, _y;
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

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            
            e.Graphics.FillRectangle(_background, 0, 0, Width, Height);

            lock (_loadLock)
            {
                e.Graphics.ScaleTransform(_scale,_scale);
                if (_imageCache != null) e.Graphics.DrawImage(_imageCache, new PointF(_x / _scale, _y / _scale));
                e.Graphics.ScaleTransform(1/_scale,1/_scale);
            }
            
            e.Graphics.DrawString(_message, Font, Black, 10, pickImgBtn?.Bottom??0 + 10);
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

        private void leftBtn_Click(object sender, EventArgs e)
        {
            _fileIndex--;
            if (_fileIndex < 0) _fileIndex = 0;
            else ReloadImage();
        }

        private void rightBtn_Click(object sender, EventArgs e)
        {
            _fileIndex++;
            if (_fileIndex >= _files.Count) _fileIndex = _files.Count - 1;
            else ReloadImage();
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            _mx = e.X; _my = e.Y;
            _drag = true;
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e) => _drag = false;

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_drag) return;
            _x += e.X - _mx;
            _y += e.Y - _my;
            _mx = e.X; _my = e.Y;
            Invalidate();
        }

        private void ChangeZoom(object sender, MouseEventArgs e)
        {
            if (e.Delta == 0) return;
            
            _scale += e.Delta * 0.0005f; // TODO: this is terrible.
                
            if (_scale < 0.01f) _scale = 0.01f;
            if (_scale > 10.0f) _scale = 10.0f;
                
            // TODO: compensate for centre point
                
            Text = (_scale * 100).ToString("0.0");
            Invalidate();
        }
    }
}
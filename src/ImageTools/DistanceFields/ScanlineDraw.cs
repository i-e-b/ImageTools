using System.Drawing;
using System.Drawing.Drawing2D;
using ImageTools.GeneralTypes;

namespace ImageTools.DistanceFields
{

    /// <summary>
    /// Does the same stuff as SdfDraw, for comparison.
    /// This tends to be a few times faster, and slightly worse looking
    /// </summary>
    public static class ScanlineDraw
    {
        private const double Epsilon = float.Epsilon; // no, that's not an error
        
#region Tables
        private static readonly byte[] _gammaAdjust = new byte[256];
        /// <summary>
        /// Set up tables
        /// </summary>
        static ScanlineDraw()
        {
            SetGamma(2.2);
        }

        public static void SetGamma(double gamma)
        {
            var m = 0.0;
            var n = 255 / Math.Pow(255, 1/gamma);
            for (int i = 0; i < 256; i++)
            {
                var x = Math.Pow(i, 1/gamma) * n;
                m = Math.Max(x,m);
                if (x > 255) x = 255;
                _gammaAdjust![i] = (byte)x;
            }
        }
#endregion

        /// <summary>
        /// Coverage based anti-aliased line fixed to 1-pixel total.
        /// This gives a very rough effect, but is quite fast.
        /// </summary>
        public static void DrawLine(ByteImage img, int x1, int y1, int x2, int y2, uint color)
        {
            if (img == null) return;

            byte r = (byte) ((color >> 16) & 0xff);
            byte g = (byte) ((color >> 8) & 0xff);
            byte b = (byte) ((color) & 0xff);
            CoverageLine(img, x1, y1, x2, y2, r, g, b);
        }

        /// <summary>
        /// Fill a polygon using a specific winding rule
        /// </summary>
        public static void FillPolygon(ByteImage img, PointF[] points, uint color, FillMode mode)
        {
            if (img == null) return;
            Action<IEnumerable<ScanlineSegment>, ICollection<PixelSpan>, int> rule = mode switch
            {
                FillMode.Alternate => OddEvenSpans,
                FillMode.Winding => NonZeroWindingSpans,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            var segments = ToSortedLineSegments(points, img.Bounds.Top, img.Bounds.Bottom);
            var spans = GetPolygonSpans(segments, rule);
            if (spans == null) return;
            
            byte r = (byte) ((color >> 16) & 0xff);
            byte g = (byte) ((color >> 8) & 0xff);
            byte b = (byte) ((color) & 0xff);

            foreach (var span in spans)
            {
                img.SetSpan(span, r, g, b);
                //img.SetPixel(span!.Left, span.Y, 255,0,255);
                //img.SetPixel(span!.Right, span.Y, 255,0,255);
            }
        }
        
        /// <summary>
        /// Fill a polygon using a specific winding rule
        /// </summary>
        public static void FillPolygon(ByteImage img, Contour[] contours, uint color, FillMode mode)
        {
            if (img == null) return;
            Action<IEnumerable<ScanlineSegment>, ICollection<PixelSpan>, int> rule = mode switch
            {
                FillMode.Alternate => OddEvenSpans,
                FillMode.Winding => NonZeroWindingSpans,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            var segments = ToSortedLineSegments(contours, img.Bounds.Top, img.Bounds.Bottom);
            var spans = GetPolygonSpans(segments, rule);
            if (spans == null) return;
            
            byte r = (byte) ((color >> 16) & 0xff);
            byte g = (byte) ((color >> 8) & 0xff);
            byte b = (byte) ((color) & 0xff);

            foreach (var span in spans)
            {
                img.SetSpan(span, r, g, b);
                //img.SetPixel(span!.Left, span.Y, 255,0,255);
                //img.SetPixel(span!.Right, span.Y, 255,0,255);
            }
        }


        private static IEnumerable<PixelSpan> GetPolygonSpans(BucketList segments, Action<IEnumerable<ScanlineSegment>, ICollection<PixelSpan>, int> fillRule)
        {
            var spans = new List<PixelSpan>();
            var buckets = segments!.SegmentsList;
            if (buckets == null) return spans;

            foreach (var bucket in buckets)
            {
                var activeList = new List<ScanlineSegment>();
                for (var y = bucket!.TopBound; y <= bucket.BottomBound; y++) // each scan line
                {
                    // find segments that affect this line
                    activeList.Clear();
                    for (int i = 0; i < bucket.Segments!.Count; i++)
                    {
                        var s = bucket.Segments[i];
                        s!.PositionAtY(y);
                        activeList.Add(s);
                    }

                    activeList.Sort(SegmentSortHorizontal);

                    // build pixel spans from the segments
                    fillRule!(activeList, spans, y);
                }
            }
            return spans;
        }

        private static void OddEvenSpans(IEnumerable<ScanlineSegment> rowSegments, ICollection<PixelSpan> spans, int y)
        {
            var oddState = 0;
            double left = 0;
            foreach (var span in rowSegments!) // run across the scan line, find left and right edges of drawn area
            {
                if (oddState == 0) left = span!.X;
                else spans!.Add(new PixelSpan {
                    Y = y,
                    
                    Left = (int) left,
                    LeftFraction = _gammaAdjust![LeftFractional(left)],
                    
                    Right = (int) span!.X,
                    RightFraction = _gammaAdjust![RightFractional(span.X)]
                });

                oddState = 1 - oddState;
            }
        }
        
        private static void NonZeroWindingSpans(IEnumerable<ScanlineSegment> rowSegments, ICollection<PixelSpan> spans, int y)
        {
            var windingCount = 0;
            double left = 0;
            foreach (var span in rowSegments!) // run across the scan line, find left and right edges of drawn area
            {
                if (windingCount == 0) left = span!.X;
                if (span!.Clockwise) windingCount--;
                else windingCount++;

                if (windingCount == 0)
                {
                    spans!.Add(new PixelSpan {
                        Y = y,
                    
                        Left = (int) left,
                        LeftFraction = _gammaAdjust![LeftFractional(left)],
                    
                        Right = (int) span.X,
                        RightFraction = _gammaAdjust![RightFractional(span.X)]
                    });
                }
            }
        }

        private static byte LeftFractional(double real) => (byte)((1.0 - (real - (int)real)) * 255);
        private static byte RightFractional(double real) => (byte)((real - (int)real) * 255);
        
        private static BucketList ToSortedLineSegments(PointF[] points, int topLimit, int bottomLimit)
        {
            var segments = new List<ScanlineSegment>();
            if (points == null || points.Length < 2) return new BucketList(segments, topLimit, bottomLimit);
            
            for (int i = 0; i < points.Length; i++)
            {
                var j = (i + 1) % points.Length;
                
                if (Horizontal(points[i], points[j])) continue; // doesn't contribute to scan lines
                segments.Add(new ScanlineSegment(points[i], points[j]));
            }
            
            return new BucketList(segments, topLimit, bottomLimit);
        }
        
        private static BucketList ToSortedLineSegments(Contour[] contours, int topLimit, int bottomLimit)
        {
            var segments = new List<ScanlineSegment>();
            if (contours == null || contours.Length < 1) return new BucketList(segments, topLimit, bottomLimit);
            for (int i = 0; i < contours.Length; i++)
            {
                var contour = contours[i];
                var segCount = contour!.PairCount();
                for (int j = 0; j < segCount; j++)
                {
                    var seg = contour.Pair(j);
                    var pA = seg!.A.ToPointF();
                    var pB = seg.B.ToPointF();
                    if (Horizontal(pA, pB)) continue; // doesn't contribute to scan lines
                    
                    segments.Add(new ScanlineSegment(pA, pB));
                }
            }
            
            return new BucketList(segments, topLimit, bottomLimit);
        }

        private static int SegmentSortHorizontal(ScanlineSegment a, ScanlineSegment b) => a!.X.CompareTo(b!.X);
        
        private static bool Horizontal(PointF a, PointF b) => Math.Abs(a.Y - b.Y) <= Epsilon;

        /// <summary>
        /// Single pixel, scanline based anti-aliased line
        /// </summary>
        private static void CoverageLine(
            ByteImage img, // target buffer
            int x0, int y0, int x1, int y1, // line coords
            byte r, byte g, byte b // draw color
        )
        {
            if (img?.PixelBytes == null || img.RowBytes < 1) return;
            int dx = x1 - x0, sx = (dx < 0) ? -1 : 1;
            int dy = y1 - y0, sy = (dy < 0) ? -1 : 1;

            // dx and dy always positive. sx & sy hold the sign
            if (dx < 0) dx = -dx;
            if (dy < 0) dy = -dy;

            // adjust for the 1-pixel offset our paired-pixels approximation gives
            if (sy < 0)
            {
                y0++;
                y1++;
            }

            if (sx < 0)
            {
                x0++;
                x1++;
            }

            int pairOffset = (dx > dy ? img.RowBytes : 4); // paired pixel for AA, as byte offset from main pixel.

            int coverAdj = (dx + dy) / 2; // to adjust `err` so it's centred over 0
            int err = 0; // running error
            int ds = (dx >= dy ? -sy : sx); // error sign
            int errOff = (dx > dy ? dx + dy : 0); // error adjustment

            for (;;)
            {
                // rough approximation of coverage, based on error
                int v = (err + coverAdj - errOff) * ds;
                if (v > 127) v = 127;
                if (v < -127) v = -127;
                var leftCover = 128 + v; // 'left' coverage,  0..255
                var rightCover = 128 - v; // 'right' coverage, 0..255

                // Gamma adjust (not doing this results in really ugly lines)
                leftCover = _gammaAdjust![leftCover];
                var antiLeft = 255 - leftCover;
                rightCover = _gammaAdjust![rightCover];
                var antiRight = 255 - leftCover;
                
                // set primary pixel, mixing original colour with target colour
                var pixelOffset = (y0 * img.RowBytes) + (x0 * 4); // target pixel as byte offset from base

                //                                          [              existing colour mix               ]   [   line colour mix   ]
                img.PixelBytes[pixelOffset + 0] = (byte) (((img.PixelBytes[pixelOffset + 0] * antiRight) >> 8) + ((b * rightCover) >> 8));
                img.PixelBytes[pixelOffset + 1] = (byte) (((img.PixelBytes[pixelOffset + 1] * antiRight) >> 8) + ((g * rightCover) >> 8));
                img.PixelBytes[pixelOffset + 2] = (byte) (((img.PixelBytes[pixelOffset + 2] * antiRight) >> 8) + ((r * rightCover) >> 8));

                pixelOffset += pairOffset; // switch to the 'other' pixel

                img.PixelBytes[pixelOffset + 0] = (byte) (((img.PixelBytes[pixelOffset + 0] * antiLeft) >> 8) + ((b * leftCover) >> 8));
                img.PixelBytes[pixelOffset + 1] = (byte) (((img.PixelBytes[pixelOffset + 1] * antiLeft) >> 8) + ((g * leftCover) >> 8));
                img.PixelBytes[pixelOffset + 2] = (byte) (((img.PixelBytes[pixelOffset + 2] * antiLeft) >> 8) + ((r * leftCover) >> 8));


                // end of line check
                if (x0 == x1 && y0 == y1) break;

                int e2 = err;
                if (e2 > -dx)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dy)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

    }
    
    public class BucketList
    {
        public class RangeAndSegments
        {
            public int TopBound;
            public int BottomBound;
            public readonly List<ScanlineSegment> Segments = new List<ScanlineSegment>();
        }

        public List<RangeAndSegments> SegmentsList { get; }

        public BucketList(List<ScanlineSegment> source, int topLimit, int bottomLimit)
        {
            if (source == null) throw new Exception();
            SegmentsList = new List<RangeAndSegments>();

            // find all the integer Y values where a segment starts or ends.
            // for each span between these, filter the segments into that 'bucket'
            var switches = new SortedSet<int>();
            foreach (var segment in source)
            {
                switches.Add(Math.Max(topLimit, segment!.TopScan));
                switches.Add(Math.Min(bottomLimit, segment.BottomScan));
            }

            var switchPoints = switches.ToList();
            for (var i = 0; i < switchPoints.Count - 1; i++)
            {
                var range = new RangeAndSegments
                {
                    TopBound = switchPoints[i],
                    BottomBound = switchPoints[i + 1]
                };

                range.Segments!.AddRange(source.Where(s =>
                     s!.TopScan <= range.TopBound && s.BottomScan >= range.BottomBound // fully spans this bucket
                ));
                
                SegmentsList.Add(range);
            }
        }
    }
    
    public class ScanlineSegment
    {
        private const double Epsilon = float.Epsilon; // no, that's not an error
        
        public readonly int TopScan;
        public readonly int BottomScan;
        
        public readonly double Top;
        public readonly double Bottom;
        public readonly double TopX;
        public readonly double BottomX;
        public readonly double Dy;
        public readonly double Dx;
        
        public double X;

        public ScanlineSegment(PointF a, PointF b)
        {
            if (Math.Abs(a.Y - b.Y) < Epsilon) Clockwise = a.X < b.X;
            else Clockwise = a.Y > b.Y;

            if (a.Y <= b.Y)
            {
                Top  = a.Y; Bottom  = b.Y;
                TopX = a.X; BottomX = b.X;
            }
            else
            {
                Top  = b.Y; Bottom  = a.Y;
                TopX = b.X; BottomX = a.X;
            }
            Dy = Bottom-Top;
            Dx = (BottomX - TopX) / Dy;
            
            TopScan = (int)Math.Round(Top);
            BottomScan = (int)Math.Round(Bottom);
            
            X = int.MinValue;
        }

        public bool Clockwise { get; }

        public void PositionAtY(double y)
        {
            var dy = y - Top;
            X = dy * Dx + TopX;
        }
    }
}
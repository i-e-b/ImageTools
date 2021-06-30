using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace ImageTools.DistanceFields
{
    /// <summary>
    /// Does the same stuff as SdfDraw, for comparison.
    /// This tends to be around 10x faster, and about 5x worse looking
    /// </summary>
    public static class ScanlineDraw
    {
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
            Action<IEnumerable<Segment>, ICollection<PixelSpan>, int> rule = mode switch
            {
                FillMode.Alternate => OddEvenSpans,
                FillMode.Winding => NonZeroWindingSpans,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            var spans = GetPolygonSpans(img, points, rule);
            if (spans == null) return;
            
            byte r = (byte) ((color >> 16) & 0xff);
            byte g = (byte) ((color >> 8) & 0xff);
            byte b = (byte) ((color) & 0xff);

            foreach (var span in spans)
            {
                img.SetSpan(span, r, g, b);
            }
        }
        
        private static IEnumerable<PixelSpan> GetPolygonSpans(ByteImage img, PointF[] points, Action<IEnumerable<Segment>, ICollection<PixelSpan>, int> fillRule)
        {
            var segments = ToSortedLineSegments(points);
            var spans = new List<PixelSpan>();
            if (segments!.Count < 2) return spans;
            
            var top = Math.Max(0, (int)segments.Min(s=>s.Top));
            var bottom = Math.Min(img!.Bounds.Height, (int)segments.Max(s=>s.Bottom) + 1);
            var skip = 0; // segments we can skip because we're below their lower bound

            var activeList = new List<Segment>();
            for (var y = top; y < bottom; y++) // each scan line
            {
                while (segments[skip].Bottom < y) skip++;

                // find segments that affect this line
                activeList.Clear();
                for (int i = skip; i < segments.Count; i++)
                {
                    var s = segments[i];
                    if (s.Top > y) continue;
                    s.PositionAtY(y);
                    activeList.Add(s);
                }
                activeList.Sort(SegmentSortHorizontal);
                
                // build pixel spans from the segments
                fillRule!(activeList, spans, y);
            }
            return spans;
        }

        private static void OddEvenSpans(IEnumerable<Segment> rowSegments, ICollection<PixelSpan> spans, int y)
        {
            var windingCount = 0;
            double left = 0;
            foreach (var span in rowSegments!) // run across the scan line, find left and right edges of drawn area
            {
                if (windingCount == 0) left = span.Pos;
                else spans!.Add(new PixelSpan {
                    Y = y,
                    
                    Left = (int) left,
                    LeftFraction = _gammaAdjust![LeftFractional(left)],
                    
                    Right = (int) span.Pos,
                    RightFraction = _gammaAdjust![RightFractional(span.Pos)]
                });

                windingCount = 1 - windingCount;
            }
        }
        
        private static void NonZeroWindingSpans(IEnumerable<Segment> rowSegments, ICollection<PixelSpan> spans, int y)
        {
            var windingCount = 0;
            double left = 0;
            foreach (var span in rowSegments!) // run across the scan line, find left and right edges of drawn area
            {
                if (windingCount == 0) left = span.Pos;
                if (span.Clockwise) windingCount--;
                else windingCount++;

                if (windingCount == 0)
                {
                    spans!.Add(new PixelSpan {
                        Y = y,
                    
                        Left = (int) left,
                        LeftFraction = _gammaAdjust![LeftFractional(left)],
                    
                        Right = (int) span.Pos,
                        RightFraction = _gammaAdjust![RightFractional(span.Pos)]
                    });
                }
            }
        }

        private static byte LeftFractional(double real) => (byte)((1.0 - (real - (int)real)) * 255);
        private static byte RightFractional(double real) => (byte)((real - (int)real) * 255);
        
        private static List<Segment> ToSortedLineSegments(PointF[] points)
        {
            var outp = new List<Segment>();
            if (points == null || points.Length < 2) return outp;
            for (int i = 0; i < points.Length; i++)
            {
                var j = (i + 1) % points.Length;
                
                if (Horizontal(points[i], points[j])) continue; // doesn't contribute to scan lines
                outp.Add(new Segment(points[i], points[j]));
            }
            outp.Sort(SegmentSortVertical);
            return outp;
        }

        private static int SegmentSortHorizontal(Segment x, Segment y) => x.Pos.CompareTo(y.Pos);
        private static int SegmentSortVertical(Segment x, Segment y) => x.Bottom.CompareTo(y.Bottom);
        
        private static bool Horizontal(PointF a, PointF b) => Math.Abs(a.Y - b.Y) < 0.00001;

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

        internal struct Segment
        {
            public readonly double Top;
            public readonly double Bottom;
            public readonly double TopX;
            public readonly double BottomX;
            public readonly double Dy;
            public readonly double Dx;
        
            public double Pos;

            public Segment(PointF a, PointF b)
            {
                if (Math.Abs(a.Y - b.Y) < 0.0001) Clockwise = a.X < b.X;
                else Clockwise = a.Y > b.Y;

                if (a.Y < b.Y)
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
            
                Pos = int.MinValue;
            }

            public bool Clockwise { get; set; }

            public Segment PositionAtY(double y)
            {
                var dy = y - Top;
                Pos = dy * Dx + TopX;
                return this;
            }
        }
    }
}
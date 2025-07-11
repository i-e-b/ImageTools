using System.Drawing;
using System.Drawing.Drawing2D;
using ImageTools.GeneralTypes;

namespace ImageTools.DistanceFields
{
    public class SdfDraw:GlslHelper
    {
        /// <summary>
        /// Distance based anti-aliased line with thickness.
        /// This gives a much nicer result than coverage-based, but is slower
        /// </summary>
        public static void DrawLine(ByteImage img, double thickness, double x1, double y1, double x2, double y2, uint color)
        {
            if (img == null) return;
            SplitColor(color, out var r, out var g, out var b);
            
            // work out maximum rect we have to cover
            var p1 = new Vector2(x1,y1);
            var p2 = new Vector2(x2,y2);
            
            MinMaxRange(out var minX, out var minY, out var maxX, out var maxY);
            ExpandRange(p1, ref minX, ref minY, ref maxX, ref maxY);
            ExpandRange(p2, ref minX, ref minY, ref maxX, ref maxY);
            ReduceMinMaxToBounds(img, ref minX, ref maxX, ref minY, ref maxY);
            
            var realWidth = thickness / 2;
            
            // Line as a distance function
            double DistanceFunc(Vector2 p) => OrientedBox(p, p1, p2, realWidth);

            // Do a general rendering of the function
            RenderDistanceFunction(img, minY, maxY, minX, maxX, DistanceFunc, b, g, r);
        }

        /// <summary>
        /// Distance based fill of polygon
        /// </summary>
        public static void FillPolygon(ByteImage img, PointF[] points, uint color, FillMode mode)
        {
            // Basic setup
            if (points == null) return;
            if (points.Length < 3) return; // area is empty
            SplitColor(color, out var r, out var g, out var b);
            
            // Get bounds, and cast points to vectors
            MinMaxRange(out var minX, out var minY, out var maxX, out var maxY);
            var vectors = points.Select(p =>
            {
                ExpandRange(p, ref minX, ref minY, ref maxX, ref maxY);
                return new Vector2(p);
            }).ToArray();
            ReduceMinMaxToBounds(img, ref minX, ref maxX, ref minY, ref maxY);

            // Pick a distance function based on the fill rule
            Func<Vector2, double> distanceFunc = mode switch
            {
                FillMode.Alternate => p => PolygonDistanceAlternate(p, vectors),
                FillMode.Winding => p => PolygonDistanceWinding(p, vectors),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
            
            // Do a general rendering of the function
            RenderDistanceFunction(img, minY, maxY, minX, maxX, distanceFunc, b, g, r);
        }

        /// <summary>
        /// Distance based fill of polygon
        /// </summary>
        public static void FillPolygon(ByteImage img, Contour[] contours, uint color, FillMode mode)
        {
            // basic setup
            if (contours == null) return;
            if (contours.Length < 1) return; // area is empty
            SplitColor(color, out var r, out var g, out var b);
            
            // Get bounds, and extract point pairs
            MinMaxRange(out var minX, out var minY, out var maxX, out var maxY);
            var allPairs = new List<VecSegment2>();
            foreach (var contour in contours)
            {
                var pairCount = contour!.PairCount();
                for (int i = 0; i < pairCount; i++)
                {
                    var pair = contour.Pair(i);
                    allPairs.Add(pair);
                    
                    ExpandRange(pair!.A, ref minX, ref minY, ref maxX, ref maxY);
                    ExpandRange(pair!.B, ref minX, ref minY, ref maxX, ref maxY);
                }
            }
            ReduceMinMaxToBounds(img, ref minX, ref maxX, ref minY, ref maxY);
            var pairs = allPairs.ToArray();
            
            // Pick a distance function based on the fill rule
            Func<Vector2, double> distanceFunc = mode switch
            {
                FillMode.Alternate => p => PolygonDistanceAlternate(p, pairs),
                FillMode.Winding => p => PolygonDistanceWinding(p, pairs),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
            
            // Do a general rendering of the function
            RenderDistanceFunction(img, minY, maxY, minX, maxX, distanceFunc, b, g, r);
        }

        /// <summary>
        /// Draw a set of lines, using `z` co-ord as line radius
        /// </summary>
        public static void DrawPressureCurve(ByteImage img, uint color, Vector3[] curve)
        {
            // Basic setup
            if (curve == null) return;
            if (curve.Length < 2) return; // line is a point. TODO: handle this
            SplitColor(color, out var r, out var g, out var b);
            
            // Determine bounds
            MinMaxRange(out var minX, out var minY, out var maxX, out var maxY);
            foreach (var point in curve)
            {
                ExpandRange(point, ref minX, ref minY, ref maxX, ref maxY);
                minX = Math.Min(minX, (int)(point.X - point.Z));
                maxX = Math.Max(maxX, (int)(point.X + point.Z));
                minY = Math.Min(minY, (int)(point.Y - point.Z));
                maxY = Math.Max(maxY, (int)(point.Y + point.Z));
            }
            ReduceMinMaxToBounds(img, ref minX, ref maxX, ref minY, ref maxY);
            
            
            // Build a distance function (we do all the line segments
            // at once so the blending works correctly)
            double DistanceFunc(Vector2 p)
            {
                var d = double.MaxValue;
                var lim = curve.Length - 1;
                for (int i = 0; i < lim; i++)
                {
                    d = Math.Min(d, UnevenCapsule(p, curve[i], curve[i + 1])); // 'min' in sdf is equivalent to logical 'or' in bitmaps
                }

                return d;
            }

            // Do a general rendering of the function
            RenderDistanceFunction(img, minY, maxY, minX, maxX, DistanceFunc, b, g, r);
        }
        
        /// <summary>
        /// Fill an ellipse ring around a limited angle sweep.
        /// If thickness >= radius, the ring will be a filled pie segment.
        /// If startAngle == endAngle, the ring will be an ellipse
        /// </summary>
        public static void FillPartialRing(ByteImage img, uint color, double x1, double y1, double x2, double y2, double startAngle, double clockwiseAngle, double thickness)
        {
            // Basic setup
            if (img == null) return;
            if (clockwiseAngle == 0) return; // no section
            SplitColor(color, out var r, out var g, out var b);
            
            // Determine bounds
            MinMaxRange(out var minX, out var minY, out var maxX, out var maxY);
            ExpandRange(x1, y1, ref minX, ref minY, ref maxX, ref maxY);
            ExpandRange(x2, y2, ref minX, ref minY, ref maxX, ref maxY);
            ReduceMinMaxToBounds(img, ref minX, ref maxX, ref minY, ref maxY);
            
            // Normalise to clockwise
            var endAngle = startAngle + clockwiseAngle;
            if (clockwiseAngle < 0.0)
            {
                var tmp = startAngle;
                startAngle += clockwiseAngle;
                endAngle = tmp;
                if (startAngle < 0) startAngle = (startAngle + 360) % 360;
            }
            
            
            // position and size vectors
            var c = Vector2.Centre(x1,y1,x2,y2);
            var rect = Vector2.RectangleVector(x1,y1,x2,y2); // vector from centre to a vertex of the rect
            var length = rect.MaxComponent();
            var degToRad = Math.PI / 180.0;
            var startRadians = (startAngle % 360) * degToRad; // 0 is centre-right
            var endRadians = (endAngle % 360) * degToRad; // this is clockwise from start angle (i.e. 360 always gives a complete ellipse)

            var sX = Math.Cos(startRadians);
            var sY = Math.Sin(startRadians);
            var p0 = new Vector2(sX, sY);
            
            var eX = Math.Cos(endRadians);
            var eY = Math.Sin(endRadians);
            var p3 = new Vector2(eX, eY);
            
            var pp0 = p0 * length + c;
            var pp3 = p3 * length + c;
            
            var vc = (pp0 + ((pp3-pp0)/2)) - c;
            var el = (length - vc.Length()) + 1;
            vc *= el;
            vc *= 2;
            var pp1 = pp0 + vc;
            var pp2 = pp3 + vc;
            
            // We start with a complete ellipse, then
            // - If the arc is less than half a turn, we intersect with the wedge
            // - If the arc is half a turn or more, we subtract the wedge
            // The wedge is the triangle between c,p1,p2; plus a rectangle p1,p2 extended away from c.
            
            // Build a distance function (we do all the line segments
            // at once so the blending works correctly)
            double DistanceFuncLessThanHalf(Vector2 p)
            {
                var dp = p - c;
                var ellipse = sdaEllipseV4(dp, rect);
                
                var ring = Subtract(ellipse + thickness, ellipse);
                var wedge = PolygonDistanceAlternate(p, new[]{c, pp0, pp1, pp2, pp3});
                
                return Intersect(ring, wedge);
            }
            double DistanceFuncMoreThanHalf(Vector2 p)
            {
                var dp = p - c;
                var ellipse = sdaEllipseV4(dp, rect);
                
                var ring = Subtract(ellipse + thickness, ellipse);
                var wedge = PolygonDistanceAlternate(p, new[]{c, pp0, pp1, pp2, pp3});
                
                return Subtract(wedge, ring);
            }

            // Do a general rendering of the function
            if (clockwiseAngle < 180)
            {
                RenderDistanceFunction(img, minY, maxY, minX, maxX, DistanceFuncLessThanHalf, b, g, r);
            }
            else
            {
                RenderDistanceFunction(img, minY, maxY, minX, maxX, DistanceFuncMoreThanHalf, b, g, r);
            }
        }

        #region SDF combining
        private static double Union( double d1, double d2 ) => Math.Min(d1,d2);
        private static double Subtract( double thing, double from ) => Math.Max(-thing,from);
        private static double Intersect( double d1, double d2 ) => Math.Max(d1,d2);
        #endregion
        
        #region Distance functions
        // square ends
        private static double OrientedBox(Vector2 samplePoint, Vector2 a, Vector2 b, double thickness)
        {
            var l = (b - a).Length();
            var d = (b - a) / l;
            var q = (samplePoint - (a + b) * 0.5);
            q = new Matrix2(d.X, -d.Y, d.Y, d.X) * q;
            q = q.Abs() - (new Vector2(l, thickness)) * 0.5;
            return q.Max(0.0).Length() + Math.Min(Math.Max(q.X, q.Y), 0.0);    
        }
        
        private static double sdCircle( Vector2 p, double r )
        {
            return length(p) - r;
        }
        
        private static double sdTriangle( in Vector2 p, in Vector2 p0, in Vector2 p1, in Vector2 p2 )
        {
            var e0 = p1 - p0;
            var e1 = p2 - p1;
            var e2 = p0 - p2;

            var v0 = p - p0;
            var v1 = p - p1;
            var v2 = p - p2;

            var pq0 = v0 - e0*clamp( dot(v0,e0)/dot(e0,e0), 0.0, 1.0 );
            var pq1 = v1 - e1*clamp( dot(v1,e1)/dot(e1,e1), 0.0, 1.0 );
            var pq2 = v2 - e2*clamp( dot(v2,e2)/dot(e2,e2), 0.0, 1.0 );
    
            double s = e0.X*e2.Y - e0.Y*e2.X;
            var d = min( min( vec2( dot( pq0, pq0 ), s*(v0.X*e0.Y-v0.Y*e0.X) ),
                vec2( dot( pq1, pq1 ), s*(v1.X*e1.Y-v1.Y*e1.X) )),
             vec2( dot( pq2, pq2 ), s*(v2.X*e2.Y-v2.Y*e2.X) ));

            return -sqrt(d.X)*sign(d.Y);
        }

        // Approximate ellipse (squashed circle)
        private static double sdEllipse2(Vector2 p, Vector2 r)
        {
            var k1 = length(p/r);
            return (k1-1.0)*min(r.X,r.Y);
        }
        
        private static double sdaEllipseV3( in Vector2 p, in Vector2 r )
        {
            var k1 = length(p/r);
            return length(p)*(1.0-1.0/k1);
        }

        // Blend 2 different approximations to get a pretty good result
        private static double sdaEllipseV4( in Vector2 p, in Vector2 r )
        {
            var under = sdEllipse2(p,r);
            var over = sdaEllipseV3(p,r);
            if (over < 0) return under;
            return (under + over)/2;
        }

        // This is only *very* approximate
        // it tends to blur out sharp edges
        private static double sdEllipse( in Vector2 p, in Vector2 r )
        {
            var k1 = length(p/r);
            var k2 = length(p/(r*r));
            var d = k1*(k1-1.0)/k2; 
            
            if (d > 1) return Math.Sqrt(d);
            if (d < -1) return -Math.Sqrt(-d);
            return d; // adjust for overshoot
        }
        
        private static double sdEllipseExact( Vector2 p,  Vector2 ab )
        {
            p = abs(p); if( p.X > p.Y ) {p=p.YX;ab=ab.YX;}
            var l = ab.Y*ab.Y - ab.X*ab.X;
            //if (l == 0.0) return 1.0; // ???
            var m = ab.X*p.X/l;      var m2 = m*m; 
            var n = ab.Y*p.Y/l;      var n2 = n*n; 
            var c = (m2+n2-1.0)/3.0; var c3 = c*c*c;
            var q = c3 + m2*n2*2.0;
            var d = c3 + m2*n2;
            var g = m + m*n2;
            double co;
            if( d<0.0 )
            {
                var h = acos(q/c3)/3.0;
                var s = cos(h);
                var t = sin(h)*sqrt(3.0);
                var rx = sqrt( -c*(s + t + 2.0) + m2 );
                var ry = sqrt( -c*(s - t + 2.0) + m2 );
                co = (ry+sign(l)*rx+abs(g)/(rx*ry)- m)/2.0;
            }
            else
            {
                var h = 2.0*m*n*sqrt( d );
                var s = sign(q+h)*pow(abs(q+h), 1.0/3.0);
                var u = sign(q-h)*pow(abs(q-h), 1.0/3.0);
                var rx = -s - u - c*4.0 + 2.0*m2;
                var ry = (s - u)*sqrt(3.0);
                var rm = sqrt( rx*rx + ry*ry );
                co = (ry/sqrt(rm-rx)+2.0*g/rm-m)/2.0;
            }
            var r = ab * vec2(co, sqrt(1.0-co*co));
            return length(r-p) * sign(p.Y-r.Y);
        }
        
        // line with a linearly varying line width
        private static double UnevenCapsule(Vector2 samplePoint, Vector3 va, Vector3 vb)
        {
            var pa = va.SplitXY_Z(out var ra);
            var pb = vb.SplitXY_Z(out var rb);
            
            // rotate to standard form
            samplePoint  -= pa;
            pb -= pa;
            var h = Vector2.Dot(pb,pb);
            var q = new Vector2(Vector2.Dot(samplePoint, new Vector2(pb.Y, -pb.X)), Vector2.Dot(samplePoint, pb)) / h;
    
            //-----------
    
            q.X = Math.Abs(q.X);
    
            var b = ra-rb;
            var c = new Vector2(Math.Sqrt(h-b*b),b);
    
            var k = Vector2.Cross(c,q);
            var m = Vector2.Dot(c,q);
            var n = Vector2.Dot(q,q);
    
            if( k < 0.0 )  return Math.Sqrt(h*(n             )) - ra;
            if( k > c.X ) return Math.Sqrt(h*(n+1.0-2.0*q.Y)) - rb;
            return                     m                        - ra;
        }

        // https://www.shadertoy.com/view/wdBXRW
        private static double PolygonDistanceAlternate(Vector2 p, Vector2[] v)
        {
            var num = v!.Length;
            var d = Vector2.Dot(p - v[0], p - v[0]);
            var s = 1.0;
            for (int i = 0, j = num - 1; i < num; j = i, i++)
            {
                // distance
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Clamp(Vector2.Dot(w, e) / Vector2.Dot(e, e), 0.0, 1.0);
                d = Math.Min(d, Vector2.Dot(b, b));

                // winding number
                var cond = new BoolVec3(p.Y >= v[i].Y,
                    p.Y < v[j].Y,
                    e.X * w.Y > e.Y * w.X);
                if (cond.All() || cond.None()) s = -s;
            }

            return s * Math.Sqrt(d);
        }
        
        // Same as above, but input is disconnected line segments
        private static double PolygonDistanceAlternate(Vector2 p, VecSegment2[] v)
        {
            var num = v!.Length;
            var d = Vector2.Dot(p - v[0]!.A, p - v[0].A);
            var s = 1.0;
            for (int i = 0; i < num; i++)
            {
                // distance
                var e = v[i]!.B - v[i].A;
                var w = p - v[i].A;
                var b = w - e * Clamp(Vector2.Dot(w, e) / Vector2.Dot(e, e), 0.0, 1.0);
                d = Math.Min(d, Vector2.Dot(b, b));

                // winding number
                var cond = new BoolVec3(p.Y >= v[i].A.Y,
                    p.Y < v[i].B.Y,
                    e.X * w.Y > e.Y * w.X);
                if (cond.All() || cond.None()) s = -s;
            }

            return s * Math.Sqrt(d);
        }

        // https://www.shadertoy.com/view/WdSGRd
        private static double PolygonDistanceWinding(Vector2 p, Vector2[] poly)
        {
            var length = poly!.Length;
            var e = new Vector2[length];
            var v = new Vector2[length];
            var pq = new Vector2[length];
            
            // data
            for (int i = 0; i < length; i++)
            {
                int i2 = (int) ((float) (i + 1) % length); //i+1
                e[i] = poly[i2] - poly[i];
                v[i] = p - poly[i];
                pq[i] = v[i] - e[i] * Clamp(Vector2.Dot(v[i], e[i]) / Vector2.Dot(e[i], e[i]), 0.0, 1.0);
            }

            //distance
            var d = Vector2.Dot(pq[0], pq[0]);
            for (int i = 1; i < length; i++)
            {
                d = Math.Min(d, Vector2.Dot(pq[i], pq[i]));
            }

            //winding number
            // from http://geomalgorithms.com/a03-_inclusion.html
            var wn = 0;
            for (var i = 0; i < length; i++)
            {
                var i2 = (int) ((float) (i + 1) % length);
                var cond1 = 0.0 <= v[i].Y;
                var cond2 = 0.0 > v[i2].Y;
                var val3 = Vector2.Cross(e[i], v[i]); //isLeft
                wn += cond1 && cond2 && val3 > 0.0 ? 1 : 0; // have  a valid up intersect
                wn -= !cond1 && !cond2 && val3 < 0.0 ? 1 : 0; // have  a valid down intersect
            }

            var s = wn == 0 ? 1.0 : -1.0; // flip distance if we're inside
            return Math.Sqrt(d) * s;
        }
        
        // Same as above, but input is disconnected line segments
        private static double PolygonDistanceWinding(Vector2 p, VecSegment2[] poly)
        {
            var length = poly!.Length;
            var e = new Vector2[length];
            var v = new Vector2[length];
            var v2 = new Vector2[length];
            var pq = new Vector2[length];
            
            // data
            for (int i = 0; i < length; i++)
            {
                e[i] = poly[i]!.B - poly[i].A;
                v[i] = p - poly[i].A;
                v2[i] = p - poly[i].B;
                pq[i] = v[i] - e[i] * Clamp(Vector2.Dot(v[i], e[i]) / Vector2.Dot(e[i], e[i]), 0.0, 1.0);
            }

            //distance
            var d = Vector2.Dot(pq[0], pq[0]);
            for (int i = 1; i < length; i++)
            {
                d = Math.Min(d, Vector2.Dot(pq[i], pq[i]));
            }

            //winding number
            // from http://geomalgorithms.com/a03-_inclusion.html
            var wn = 0;
            for (int i = 0; i < length; i++)
            {
                var cond1 = 0.0 <= v[i].Y;
                var cond2 = 0.0 > v2[i].Y;
                var val3 = Vector2.Cross(e[i], v[i]); //isLeft
                wn += cond1 && cond2 && val3 > 0.0 ? 1 : 0; // have  a valid up intersect
                wn -= !cond1 && !cond2 && val3 < 0.0 ? 1 : 0; // have  a valid down intersect
            }

            var s = wn == 0 ? 1.0 : -1.0; // flip distance if we're inside
            return Math.Sqrt(d) * s;
        }
        #endregion
    }
}
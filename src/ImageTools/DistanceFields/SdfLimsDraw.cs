using ImageTools.GeneralTypes;

namespace ImageTools.DistanceFields
{
    /// <summary>
    /// L-infinity metric space.
    /// Based on https://www.iquilezles.org/www/articles/distfunctions2dlinf/distfunctions2dlinf.htm
    /// This seems to give simpler distance functions than euclidean SDFs, but causes more sample
    /// points to be inspected
    /// </summary>
    public class SdfLimsDraw:GlslHelper
    {
        /// <summary>
        /// Draw a rotated rectangle. x1,y1 is the centre, rx,ry is a corner vertex
        /// </summary>
        public static void DrawRectangle(ByteImage img, uint color, double x1, double y1, double rx, double ry, double ang )
        {
            if (img == null) return;
            SplitColor(color, out var r, out var g, out var b);
            
            // work out maximum rect we have to cover
            var c = new Vector2(x1,y1);
            var pr = new Vector2(rx,ry);
            var w = vec2(cos(ang),sin(ang));
            
            MinMaxRange(out var minX, out var minY, out var maxX, out var maxY);
            ExpandRange(c, pr, ref minX, ref minY, ref maxX, ref maxY);
            ReduceMinMaxToBounds(img, ref minX, ref maxX, ref minY, ref maxY);
            
            // Rectangle as a distance function
            double DistanceFunc(Vector2 p) => OrientedBox(p, c, pr, w);

            // Do a general rendering of the function
            RenderDistanceFunction(img, minY, maxY, minX, maxX, DistanceFunc, b, g, r);
        }
        /// <summary>
        /// Draw an axis-aligned oval
        /// </summary>
        public static void DrawOval(ByteImage img, uint color, double x1, double y1, double x2, double y2)
        {
            if (img == null) return;
            SplitColor(color, out var r, out var g, out var b);
            
            // work out maximum rect we have to cover
            var c1 = Vector2.LowestPair(x1,y1, x2,y2);
            var c2 = Vector2.HighestPair(x1,y1, x2,y2);
            
            var cx = Vector2.Centre(x1,y1,x2,y2);
            var rp = Vector2.RectangleVector(x1,y1,x2,y2);
            
            MinMaxRange(out var minX, out var minY, out var maxX, out var maxY);
            ExpandRange(c1, ref minX, ref minY, ref maxX, ref maxY);
            ExpandRange(c2, ref minX, ref minY, ref maxX, ref maxY);
            ReduceMinMaxToBounds(img, ref minX, ref maxX, ref minY, ref maxY);
            
            // Rectangle as a distance function
            double DistanceFunc(Vector2 p) => sdEllipse(p, cx, rp);

            // Do a general rendering of the function
            RenderDistanceFunction(img, minY, maxY, minX, maxX, DistanceFunc, b, g, r);
        }
        
        // p = sample point, c = centre, r = bounding box corner point
        private static double  sdEllipse( Vector2 p, Vector2 c, Vector2 r )
        {
            p -= c;
            p = abs(p);
            p = max(p,(p-r).YX); // idea by oneshade
    
            var m = dot(r,r);
            var d = p.Y-p.X;
            return p.X - (r.Y*sqrt(m-d*d)-r.X*d) * r.X/m;
        }
        
        // p = sample point, c = centre, r = corner point, w = rotation vector 
        private static double OrientedBox(Vector2 p, Vector2 c, Vector2 r, Vector2 w )
        {
            p -= c;
            var q = w.XYYX * p.XXYY; // centre?
            var s = w.XYYX * r.XXYY; // midline right?

            return max( 
                // rotated rectangle
                max(abs(q.X+q.Z)-r.X, abs(q.W-q.Y)-r.Y ) / 
                max(abs(w.X-w.Y),     abs(w.X+w.Y)),
                // axis aligned bbox
                max(abs(p.X)-max(abs(s.X-s.Z),abs(s.X+s.Z)), 
                    abs(p.Y)-max(abs(s.Y+s.W),abs(s.Y-s.W)) ) );
        }
    }
}
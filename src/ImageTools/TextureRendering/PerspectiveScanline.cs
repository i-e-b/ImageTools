
// ReSharper disable InconsistentNaming
// ReSharper disable InconsistentNaming
// ReSharper disable JoinDeclarationAndInitializer
namespace ImageTools.TextureRendering
{
    /// <summary>
    /// A point in 3D euclidean space with 2D texture co-ordinates
    /// </summary>
    public class Point3DTexture2D
    {
        // ReSharper disable UnassignedField.Global
        public float x, y, z, u, v;
        // ReSharper restore UnassignedField.Global
    }

    /// <summary>
    /// Class for storing 3D textured triangles, with methods to
    /// render onto a target bitmap
    /// </summary>
    /// <remarks>
    /// Ported from http://www.lysator.liu.se/~mikaelk/doc/perspectivetexture/
    /// based on an article written for 3DReview in 1997 by Mikael Kalms.
    /// </remarks>
    public class PerspectiveTriangle
    {
        public readonly Point3DTexture2D P1;
        public readonly Point3DTexture2D P2;
        public readonly Point3DTexture2D P3;
        public readonly ByteImage Texture;

        public PerspectiveTriangle(ByteImage texture, Point3DTexture2D p1, Point3DTexture2D p2, Point3DTexture2D p3)
        {
            Texture = texture;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        // ReSharper disable IdentifierTypo
        private float dizdx, duizdx, dvizdx, dizdy, duizdy, dvizdy;
        private float dizdxn, duizdxn, dvizdxn;
        private float xa, xb, iza, uiza, viza;

        private float dxdya, dxdyb, dizdya, duizdya, dvizdya;
        // ReSharper restore InconsistentNaming

        // Subdivision span-size
        const int SubDivShift = 4;
        const int SubDivSize = 1 << SubDivShift;


        private void swapfloat(ref float x, ref float y)
        {
            (x, y) = (y, x);
        }

        public void Draw(ByteImage target)
        {
            drawtpolyperspdivsubtri(target);
        }

        private void drawtpolyperspdivsubtri(ByteImage target)
        {
            var poly = this;
            float x1, y1, x2, y2, x3, y3;
            float iz1, uiz1, viz1, iz2, uiz2, viz2, iz3, uiz3, viz3;
            float dxdy1 = 0.0f, dxdy2 = 0.0f, dxdy3 = 0.0f;
            float denom;
            float dy;
            int y1i, y2i, y3i;

            // Shift XY coordinate system (+0.5, +0.5) to match the subpixel strategy
            //  technique

            x1 = poly.P1!.x + 0.5f;
            y1 = poly.P1 .y + 0.5f;
            x2 = poly.P2!.x + 0.5f;
            y2 = poly.P2 .y + 0.5f;
            x3 = poly.P3!.x + 0.5f;
            y3 = poly.P3 .y + 0.5f;

            // Calculate alternative 1/Z, U/Z and V/Z values which will be
            //  interpolated

            iz1 = 1.0f / poly.P1.z;
            iz2 = 1.0f / poly.P2.z;
            iz3 = 1.0f / poly.P3.z;
            uiz1 = poly.P1.u * iz1;
            viz1 = poly.P1.v * iz1;
            uiz2 = poly.P2.u * iz2;
            viz2 = poly.P2.v * iz2;
            uiz3 = poly.P3.u * iz3;
            viz3 = poly.P3.v * iz3;

            // Sort the vertices in increasing Y order

            if (y1 > y2)
            {
                swapfloat(ref x1, ref x2);
                swapfloat(ref y1, ref y2);
                swapfloat(ref iz1, ref iz2);
                swapfloat(ref uiz1, ref uiz2);
                swapfloat(ref viz1, ref viz2);
            }

            if (y1 > y3)
            {
                swapfloat(ref x1, ref x3);
                swapfloat(ref y1, ref y3);
                swapfloat(ref iz1, ref iz3);
                swapfloat(ref uiz1, ref uiz3);
                swapfloat(ref viz1, ref viz3);
            }

            if (y2 > y3)
            {
                swapfloat(ref x2, ref x3);
                swapfloat(ref y2, ref y3);
                swapfloat(ref iz2, ref iz3);
                swapfloat(ref uiz2, ref uiz3);
                swapfloat(ref viz2, ref viz3);
            }

            y1i = (int)y1;
            y2i = (int)y2;
            y3i = (int)y3;

            // Skip poly if it's too thin to cover any pixels at all

            if ((y1i == y2i && y1i == y3i)
                || ((int)x1 == (int)x2 && (int)x1 == (int)x3))
                return;

            // Calculate horizontal and vertical increments for UV axes (these
            //  calculations are certainly not optimal, although they're stable
            //  (handles any dy being 0)

            denom = ((x3 - x1) * (y2 - y1) - (x2 - x1) * (y3 - y1));

            if (denom == 0) // Skip poly if it's an infinitely thin line
                return;

            denom = 1.0f / denom; // Reciprocal for speeding up
            dizdx = ((iz3 - iz1) * (y2 - y1) - (iz2 - iz1) * (y3 - y1)) * denom;
            duizdx = ((uiz3 - uiz1) * (y2 - y1) - (uiz2 - uiz1) * (y3 - y1)) * denom;
            dvizdx = ((viz3 - viz1) * (y2 - y1) - (viz2 - viz1) * (y3 - y1)) * denom;
            dizdy = ((iz2 - iz1) * (x3 - x1) - (iz3 - iz1) * (x2 - x1)) * denom;
            duizdy = ((uiz2 - uiz1) * (x3 - x1) - (uiz3 - uiz1) * (x2 - x1)) * denom;
            dvizdy = ((viz2 - viz1) * (x3 - x1) - (viz3 - viz1) * (x2 - x1)) * denom;

            // Horizontal increases for 1/Z, U/Z and V/Z which step one full span
            //  ahead

            dizdxn = dizdx * SubDivSize;
            duizdxn = duizdx * SubDivSize;
            dvizdxn = dvizdx * SubDivSize;

            // Calculate X-slopes along the edges

            if (y2 > y1)
                dxdy1 = (x2 - x1) / (y2 - y1);
            if (y3 > y1)
                dxdy2 = (x3 - x1) / (y3 - y1);
            if (y3 > y2)
                dxdy3 = (x3 - x2) / (y3 - y2);

            // Determine which side of the poly the longer edge is on

            var side = dxdy2 > dxdy1;

            if ((int)y1 == (int)y2)
                side = x1 > x2;
            if ((int)y2 == (int)y3)
                side = x3 > x2;

            if (!side) // Longer edge is on the left side
            {
                // Calculate slopes along left edge

                dxdya = dxdy2;
                dizdya = dxdy2 * dizdx + dizdy;
                duizdya = dxdy2 * duizdx + duizdy;
                dvizdya = dxdy2 * dvizdx + dvizdy;

                // Perform subpixel pre-stepping along left edge

                dy = 1 - (y1 - y1i);
                xa = x1 + dy * dxdya;
                iza = iz1 + dy * dizdya;
                uiza = uiz1 + dy * duizdya;
                viza = viz1 + dy * dvizdya;

                if (y1i < y2i) // Draw upper segment if possibly visible
                {
                    // Set right edge X-slope and perform subpixel pre-
                    //  stepping

                    xb = x1 + dy * dxdy1;
                    dxdyb = dxdy1;

                    drawtpolyperspdivsubtriseg(target, y1i, y2i);
                }

                if (y2i < y3i) // Draw lower segment if possibly visible
                {
                    // Set right edge X-slope and perform subpixel pre-
                    //  stepping

                    xb = x2 + (1 - (y2 - y2i)) * dxdy3;
                    dxdyb = dxdy3;

                    drawtpolyperspdivsubtriseg(target, y2i, y3i);
                }
            }
            else // Longer edge is on the right side
            {
                // Set right edge X-slope and perform subpixel pre-stepping

                dxdyb = dxdy2;
                dy = 1 - (y1 - y1i);
                xb = x1 + dy * dxdyb;

                if (y1i < y2i) // Draw upper segment if possibly visible
                {
                    // Set slopes along left edge and perform subpixel
                    //  pre-stepping

                    dxdya = dxdy1;
                    dizdya = dxdy1 * dizdx + dizdy;
                    duizdya = dxdy1 * duizdx + duizdy;
                    dvizdya = dxdy1 * dvizdx + dvizdy;
                    xa = x1 + dy * dxdya;
                    iza = iz1 + dy * dizdya;
                    uiza = uiz1 + dy * duizdya;
                    viza = viz1 + dy * dvizdya;

                    drawtpolyperspdivsubtriseg(target, y1i, y2i);
                }

                if (y2i < y3i) // Draw lower segment if possibly visible
                {
                    // Set slopes along left edge and perform subpixel
                    //  pre-stepping

                    dxdya = dxdy3;
                    dizdya = dxdy3 * dizdx + dizdy;
                    duizdya = dxdy3 * duizdx + duizdy;
                    dvizdya = dxdy3 * dvizdx + dvizdy;
                    dy = 1 - (y2 - y2i);
                    xa = x2 + dy * dxdya;
                    iza = iz2 + dy * dizdya;
                    uiza = uiz2 + dy * duizdya;
                    viza = viz2 + dy * dvizdya;

                    drawtpolyperspdivsubtriseg(target, y2i, y3i);
                }
            }
        }

        private void drawtpolyperspdivsubtriseg(ByteImage target, int y1, int y2)
        {
            // TODO: bounds checks
            while (y1 < y2) // Loop through all lines in segment
            {
                float iz, uiz, viz;
                int u1, v1, u2, v2, u, v, du, dv;
                var x1 = (int)xa;
                var x2 = (int)xb;

                // Perform sub-texel pre-stepping on 1/Z, U/Z and V/Z

                var dx = 1 - (xa - x1);
                iz = iza + dx * dizdx;
                uiz = uiza + dx * duizdx;
                viz = viza + dx * dvizdx;

                //scr = &screen[y1 * 320 + x1];
                var cursor = target!.GetCursor(x1, y1);

                // Calculate UV for the first pixel

                var z = 65536 / iz;
                u2 = (int)(uiz * z);
                v2 = (int)(viz * z);

                // Length of line segment

                var xcount = x2 - x1;

                while (xcount >= SubDivSize) // Draw all full-length
                {
                    //  spans
                    // Step 1/Z, U/Z and V/Z to the next span

                    iz += dizdxn;
                    uiz += duizdxn;
                    viz += dvizdxn;

                    u1 = u2;
                    v1 = v2;

                    // Calculate UV at the beginning of next span

                    z = 65536 / iz;
                    u2 = (int)(uiz * z);
                    v2 = (int)(viz * z);

                    u = u1;
                    v = v1;

                    // Calculate linear UV slope over span

                    du = (u2 - u1) >> SubDivShift;
                    dv = (v2 - v1) >> SubDivShift;

                    var x = SubDivSize;
                    while (x-->0) // Draw span
                    {
                        // Copy pixel from texture to screen

                        //*(scr++) = texture[((((int)v) & 0xff0000) >> 8) + ((((int)u) & 0xff0000) >> 16)]; // I think this assumes 256 pixel texture
                        var int_u = (u & 0xff0000) >> 16;
                        var int_v = (v & 0xff0000) >> 16;
                        Texture!.GetPixel(int_u, int_v, out var r, out var g, out var b);
                        cursor!.Set(r,g,b); cursor.Advance();
                        
                        // Step horizontally along UV axes

                        u += du;
                        v += dv;
                    }

                    xcount -= SubDivSize; // One span less
                }

                if (xcount != 0) // Draw last, non-full-length span
                {
                    // Step 1/Z, U/Z and V/Z to end of span

                    iz += dizdx * xcount;
                    uiz += duizdx * xcount;
                    viz += dvizdx * xcount;

                    u1 = u2;
                    v1 = v2;

                    // Calculate UV at end of span

                    z = 65536 / iz;
                    u2 = (int)(uiz * z);
                    v2 = (int)(viz * z);

                    u = u1;
                    v = v1;


                    // Calculate linear UV slope over span

                    du = (u2 - u1) / xcount;
                    dv = (v2 - v1) / xcount;

                    while (xcount-->0) // Draw span
                    {
                        // Copy pixel from texture to screen

                        //*(scr++) = texture[((((int)v) & 0xff0000) >> 8) + ((((int)u) & 0xff0000) >> 16)];
                        var int_u = (u & 0xff0000) >> 16;
                        var int_v = (v & 0xff0000) >> 16;
                        Texture!.GetPixel(int_u, int_v, out var r, out var g, out var b);
                        cursor!.Set(r,g,b); cursor.Advance();

                        // Step horizontally along UV axes

                        u += du;
                        v += dv;
                    }
                }

                // Step vertically along both edges

                xa += dxdya;
                xb += dxdyb;
                iza += dizdya;
                uiza += duizdya;
                viza += dvizdya;

                y1++;
            }
        }
    }
}
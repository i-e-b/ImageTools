namespace ImageTools.GeneralTypes
{
    public struct Vector3
    {
        public double Dx, Dy, Dz;

        public Vector3(double x, double y, double z) { Dx = x; Dy = y; Dz = z; }

        public Vector2 SplitXY_Z(out double z)
        {
            z = Dz;
            return new Vector2(Dx, Dy);
        }
    }
}
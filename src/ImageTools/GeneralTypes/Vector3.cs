namespace ImageTools.GeneralTypes
{
    public struct Vector3
    {
        public double X, Y, Z;

        public Vector3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public Vector2 SplitXY_Z(out double z)
        {
            z = Z;
            return new Vector2(X, Y);
        }
    }
}
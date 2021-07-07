namespace ImageTools.GeneralTypes
{
    /// <summary>
    /// Three booleans
    /// </summary>
    public struct BoolVec3
    {
        public bool A, B, C;

        public BoolVec3(bool a, bool b, bool c)
        {
            A = a;
            B = b;
            C = c;
        }

        public bool All() => A && B && C;

        public bool None() => !A && !B && !C;
    }
}
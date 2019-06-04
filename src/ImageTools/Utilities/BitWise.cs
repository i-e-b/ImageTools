namespace ImageTools.Utilities
{
    public static class BitWise {
        
        public static uint NextPow2(uint c) {
            c--;
            c |= c >> 1;
            c |= c >> 2;
            c |= c >> 4;
            c |= c >> 8;
            c |= c >> 16;
            return ++c;
        }
    }
}
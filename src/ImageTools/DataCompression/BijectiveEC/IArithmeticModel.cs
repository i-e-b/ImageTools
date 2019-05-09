namespace ImageTools.DataCompression.BijectiveEC
{
    public interface IArithmeticModel
    {
        void Encode(byte symbol, IArithmeticEncoder dest);
        byte Decode(IArithmeticDecoder src);
    };
}
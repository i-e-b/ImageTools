namespace ImageTools.DataCompression.BijectiveEC
{
    internal interface IByteStream
    {
        
        //you MUST call AtEnd() before calling Get() or Peek()
        bool AtEnd();

        byte Get();
        byte Peek();


        uint Read(byte[] dest, uint len);
    }
}
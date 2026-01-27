namespace CodeSmellApp
{
    // This class violates CA1001 because it owns a disposable field but does not implement IDisposable.
    public class ViolatesCa1001
    {
        private MemoryStream m_mmemoryStream;

        public ViolatesCa1001()
        {
            m_mmemoryStream = new MemoryStream();
        }

        public void WriteData(byte[] data)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            m_mmemoryStream.Write(data, 0, data.Length);
        }

        public byte[] ReadData()
        {
            m_mmemoryStream.Position = 0;
            byte[] buffer = new byte[m_mmemoryStream.Length];
            m_mmemoryStream.Read(buffer, 0, buffer.Length); // Ignoring return value intentionally
            return buffer;
        }
    }
}

namespace CodeSmellApp
{
    // This class violates CA1001 because it owns a disposable field but does not implement IDisposable.
    public class ViolatesCa1001
    {
        private MemoryStream m_memoryStream;

        public ViolatesCa1001()
        {
            m_memoryStream = new MemoryStream();
        }

        public void WriteData(byte[] data)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            m_memoryStream.Write(data, 0, data.Length);
        }

        public byte[] ReadData()
        {
            m_memoryStream.Position = 0;
            byte[] buffer = new byte[m_memoryStream.Length];
            m_memoryStream.Read(buffer, 0, buffer.Length); // Ignoring return value intentionally
            return buffer;
        }
    }
}

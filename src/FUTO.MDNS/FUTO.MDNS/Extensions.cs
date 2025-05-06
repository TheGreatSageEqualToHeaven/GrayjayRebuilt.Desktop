namespace FUTO.MDNS;

using System.Text;

public static class Extensions
{
    public static string ToByteDump(this byte[] bytes)
    {
        StringBuilder result = new StringBuilder();
        for (int i = 0; i < bytes.Length; i++)
        {
            result.AppendFormat("{0:X2} ", bytes[i]);

            if ((i + 1) % 16 == 0 || i == bytes.Length - 1)
            {
                int padding = 3 * (16 - (i % 16 + 1));
                if (i == bytes.Length - 1 && (i + 1) % 16 != 0)
                    result.Append(' ', padding);

                result.Append("; ");
                int start = i - (i % 16);
                int end = Math.Min(i, bytes.Length - 1);
                for (int j = start; j <= end; j++)
                {
                    char ch = (bytes[j] >= 32 && bytes[j] <= 127) ? (char)bytes[j] : '.';
                    result.Append(ch);
                }

                if (i != bytes.Length - 1)
                    result.AppendLine();
            }
        }

        return result.ToString();
    }

    public static string ReadDomainName(this byte[] data)
    {
        int position = 0;
        return ReadDomainName(data, ref position);
    }

    public static string ReadDomainName(this byte[] data, ref int position, int depth = 0)
    {
        if (depth > 16)
            throw new Exception("Exceeded maximum recursion depth in DNS packet. Possible circular reference.");

        var domainParts = new List<string>();
        bool endOfName = false;

        while (!endOfName)
        {
            byte length = data[position];
            if ((length & 0b11000000) == 0b11000000)
            {
                int offset = ((length & 0b111111) << 8) | data[position + 1];
                int savedPosition = position + 2;
                position = offset;

                string part = ReadDomainName(data, ref position, depth + 1);
                domainParts.Add(part);
                position = savedPosition;
                break;
            }
            else if (length == 0)
            {
                position++;
                endOfName = true;
            }
            else
            {
                position++;
                var part = Encoding.UTF8.GetString(data.AsSpan().Slice(position, length));
                domainParts.Add(part);
                position += length;
            }
        }

        return string.Join(".", domainParts);
    }
}
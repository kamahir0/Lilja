using System;
using System.Text;

namespace Lilja.Repository
{
    public static class RepositoryFileName
    {
        public static string Encode<TKey>(TKey key)
        {
            if (key is null)
            {
                return "_null";
            }

            var text = key.ToString() ?? string.Empty;
            if (text.Length == 0)
            {
                return "_empty";
            }

            var bytes = Encoding.UTF8.GetBytes(text);
            return "k_" + Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        public static string Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
            {
                return string.Empty;
            }

            if (encoded == "_null")
            {
                return "null";
            }

            if (encoded == "_empty")
            {
                return string.Empty;
            }

            if (encoded.StartsWith("k_"))
            {
                var base64 = encoded.Substring(2)
                    .Replace('-', '+')
                    .Replace('_', '/');

                int mod = base64.Length % 4;
                if (mod > 0)
                {
                    base64 += new string('=', 4 - mod);
                }

                try
                {
                    var bytes = Convert.FromBase64String(base64);
                    return Encoding.UTF8.GetString(bytes);
                }
                catch
                {
                    return encoded;
                }
            }

            return encoded;
        }
    }
}

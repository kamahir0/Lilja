using System;
using System.Text;

namespace Lilja.Repository
{
    public static class RepositoryFileName
    {
        public static string Encode<TKey>(TKey key)
        {
            var text = key?.ToString() ?? string.Empty;
            if (text.Length == 0)
            {
                return "_";
            }

            var bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }
}

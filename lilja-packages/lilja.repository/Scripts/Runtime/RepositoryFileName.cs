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
    }
}

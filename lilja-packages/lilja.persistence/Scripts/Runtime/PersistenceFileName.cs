using System.Text;

namespace Lilja.Persistence
{
    public static class PersistenceFileName
    {
        public static string Encode(object key)
        {
            return Encode(key?.ToString() ?? string.Empty);
        }

        public static string Encode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "_";
            }

            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if ((c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '-' ||
                    c == '_')
                {
                    builder.Append(c);
                    continue;
                }

                builder.Append('_');
            }

            return builder.Length == 0 ? "_" : builder.ToString();
        }
    }
}

using System.IO;
using System.Text;

namespace Lilja.Repository
{
    /// <summary>
    /// 書き込み成功後に一時ファイルで置き換えることで、ファイルを書き込みます。
    /// </summary>
    public static class AtomicFileWriter
    {
        /// <summary>
        /// アトミックな置換操作を使って UTF-8 テキストをファイルへ書き込みます。
        /// </summary>
        /// <param name="filePath">出力先パス。</param>
        /// <param name="content">書き込むテキスト。</param>
        public static void WriteAllText(string filePath, string content)
        {
            var tempPath = GetTempPath(filePath);
            EnsureDirectory(filePath);

            try
            {
                File.WriteAllText(tempPath, content, new UTF8Encoding(false));
                ReplaceFile(tempPath, filePath);
            }
            finally
            {
                DeleteIfExists(tempPath);
            }
        }

        /// <summary>
        /// アトミックな置換操作を使ってバイナリ内容をファイルへ書き込みます。
        /// </summary>
        /// <param name="filePath">出力先パス。</param>
        /// <param name="bytes">書き込むバイト列。</param>
        public static void WriteAllBytes(string filePath, byte[] bytes)
        {
            var tempPath = GetTempPath(filePath);
            EnsureDirectory(filePath);

            try
            {
                File.WriteAllBytes(tempPath, bytes);
                ReplaceFile(tempPath, filePath);
            }
            finally
            {
                DeleteIfExists(tempPath);
            }
        }

        /// <summary>
        /// ファイルが存在する場合に削除します。
        /// </summary>
        /// <param name="filePath">削除するファイル。</param>
        public static void DeleteIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private static void ReplaceFile(string tempPath, string filePath)
        {
            if (!File.Exists(filePath))
            {
                File.Move(tempPath, filePath);
                return;
            }

            var backupPath = GetBackupPath(filePath);
            DeleteIfExists(backupPath);
            File.Replace(tempPath, filePath, backupPath, true);
            DeleteIfExists(backupPath);
        }

        private static void EnsureDirectory(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string GetTempPath(string filePath)
        {
            return filePath + ".tmp";
        }

        private static string GetBackupPath(string filePath)
        {
            return filePath + ".bak";
        }
    }
}

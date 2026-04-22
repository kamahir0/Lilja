using System.IO;
using System.Text;

namespace Lilja.Repository
{
    /// <summary>
    /// Writes files by replacing the destination with a temporary file once the write succeeds.
    /// </summary>
    public static class AtomicFileWriter
    {
        /// <summary>
        /// Writes UTF-8 text to a file using an atomic replace operation.
        /// </summary>
        /// <param name="filePath">The destination path.</param>
        /// <param name="content">The text to write.</param>
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
        /// Writes binary content to a file using an atomic replace operation.
        /// </summary>
        /// <param name="filePath">The destination path.</param>
        /// <param name="bytes">The bytes to write.</param>
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
        /// Deletes a file when it exists.
        /// </summary>
        /// <param name="filePath">The file to remove.</param>
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

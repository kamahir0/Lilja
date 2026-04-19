using System.IO;

namespace Lilja.Repository
{
    /// <summary>
    /// Atomicなファイル書き込みユーティリティ。
    /// 一時ファイル書き込み → File.Replace/Moveパターンでデータ破損を防止する。
    /// </summary>
    public static class AtomicFileWriter
    {
        /// <summary>
        /// テキストをAtomicに書き込む。
        /// </summary>
        /// <param name="filePath">書き込み先ファイルパス。</param>
        /// <param name="content">書き込む内容。</param>
        public static void WriteAllText(string filePath, string content)
        {
            var tempPath = filePath + ".tmp";
            var backupPath = filePath + ".bak";

            File.WriteAllText(tempPath, content);
            ReplaceFile(tempPath, filePath, backupPath);
        }

        /// <summary>
        /// バイト列をAtomicに書き込む。
        /// </summary>
        /// <param name="filePath">書き込み先ファイルパス。</param>
        /// <param name="bytes">書き込むバイト列。</param>
        public static void WriteAllBytes(string filePath, byte[] bytes)
        {
            var tempPath = filePath + ".tmp";
            var backupPath = filePath + ".bak";

            File.WriteAllBytes(tempPath, bytes);
            ReplaceFile(tempPath, filePath, backupPath);
        }

        /// <summary>
        /// 一時ファイルで対象ファイルをAtomicに置換する。
        /// </summary>
        private static void ReplaceFile(string tempPath, string destPath, string backupPath)
        {
            // バックアップファイルが残っていたら削除
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            if (File.Exists(destPath))
            {
                // 元ファイルが存在する場合: Atomic replace
                File.Replace(tempPath, destPath, backupPath);

                // バックアップを削除
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            else
            {
                // 元ファイルが未存在の場合: リネームで配置
                var directory = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Move(tempPath, destPath);
            }
        }

        /// <summary>
        /// 対象ファイルが存在すれば削除する。
        /// </summary>
        public static void DeleteIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}

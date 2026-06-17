using System;
using System.IO;
using UnityEditor;

namespace Lilja.CustomProjectWindow
{
    public enum SaveMode
    {
        UserSettingsFile,
        EditorPrefs
    }

    internal interface ISettingsStorage
    {
        bool Exists();
        string Load();
        void Save(string json);
        DateTime GetLastWriteTimeUtc();
    }

    internal sealed class EditorPrefsStorage : ISettingsStorage
    {
        private readonly string _key;
        public EditorPrefsStorage(string key) => _key = key;

        public bool Exists() => EditorPrefs.HasKey(_key);
        public string Load() => EditorPrefs.GetString(_key, string.Empty);
        public void Save(string json) => EditorPrefs.SetString(_key, json);
        public DateTime GetLastWriteTimeUtc() => DateTime.MinValue;
    }

    internal sealed class UserSettingsFileStorage : ISettingsStorage
    {
        private readonly string _filePath;
        public UserSettingsFileStorage(string filePath) => _filePath = filePath;

        public bool Exists() => File.Exists(_filePath);

        public string Load()
        {
            if (!Exists()) return string.Empty;
            return File.ReadAllText(_filePath);
        }

        public void Save(string json)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_filePath, json);
        }

        public DateTime GetLastWriteTimeUtc()
        {
            if (!Exists()) return DateTime.MinValue;
            return File.GetLastWriteTimeUtc(_filePath);
        }
    }
}

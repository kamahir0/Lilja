using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lilja.DevKit.PackageManagement
{
    /// <summary>
    /// Liljaパッケージ作成ウィンドウ
    /// 設定はProjectSettings/LiljaPackageCreatorSettings.jsonに保存される
    /// </summary>
    public class PackageCreatorWindow : EditorWindow
    {
        #region Constants

        private const string SettingsPath = "ProjectSettings/LiljaPackageCreatorSettings.json";
        private const string WindowTitle = "📦 Lilja Package Creator";

        // EditorPrefs Keys
        private const string KeyPrefix = "Lilja.DevKit.PackageCreator.";
        private const string KeyLiljaPackagesDirectory = KeyPrefix + "LiljaPackagesDirectory";
        private const string KeyPackageBaseName = KeyPrefix + "PackageBaseName";
        private const string KeyAuthorName = KeyPrefix + "AuthorName";
        private const string KeyAuthorUrl = KeyPrefix + "AuthorUrl";
        private const string KeyAuthorEmail = KeyPrefix + "AuthorEmail";
        private const string KeyWithImport = KeyPrefix + "WithImport";
        private const string KeyUseAnalyzer = KeyPrefix + "UseAnalyzer";

        #endregion

        #region Serializable Settings

        /// <summary>
        /// JSON保存用の設定クラス
        /// </summary>
        [System.Serializable]
        private class Settings
        {
            // ReSharper disable All
            // Project Settings (Git管理対象)
            public string organizationName = "kamahir0";

            // Local Settings (EditorPrefs管理対象 - Git管理外)
            public string liljaPackagesDirectory = string.Empty;
            public string packageBaseName = "NewPackage";
            public string displayNameOverride = string.Empty;
            public string packageNameOverride = string.Empty;

            // Author情報（任意）
            public string authorName = string.Empty;
            public string authorUrl = string.Empty;
            public string authorEmail = string.Empty;

            // 作成後の動作
            public bool withImport = true;

            // オプション生成
            public bool useAnalyzer = false;
            // ReSharper restore All
        }

        /// <summary>
        /// ディスク保存用（OrganizationNameのみ）
        /// </summary>
        [System.Serializable]
        private class ProjectSettingsData
        {
            // ReSharper disable All
            public string organizationName;
            // ReSharper restore All
        }

        #endregion

        #region Fields

        private Settings _settings;
        private bool _showAuthorSection;

        #endregion

        #region Menu Item

        [MenuItem("Lilja/DevKit/Package Creator Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<PackageCreatorWindow>(WindowTitle);
            window.minSize = new Vector2(450, 320);
        }

        #endregion

        #region Unity Callbacks

        private void OnEnable()
        {
            LoadSettings();
        }

        private void OnDisable()
        {
            SaveSettings();
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                LoadSettings();
            }

            // タイトル
            GUILayout.Label(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 1. lilja-packagesディレクトリ指定
            DrawLiljaPackagesDirectoryField();
            EditorGUILayout.Space();

            // 2. OrganizationName入力
            DrawOrganizationNameField();
            EditorGUILayout.Space();

            // 3. パッケージ名入力
            DrawPackageNameField();
            EditorGUILayout.Space();

            // 4. Author情報（折りたたみ）
            DrawAuthorField();
            EditorGUILayout.Space();

            // 5. オプション
            DrawOptionsField();
            EditorGUILayout.Space(20);

            // 6. 作成ボタン
            DrawCreateButton();
        }

        #endregion

        #region GUI Drawing Methods

        private void DrawLiljaPackagesDirectoryField()
        {
            EditorGUILayout.LabelField("lilja-packages Directory", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _settings.liljaPackagesDirectory = EditorGUILayout.TextField(_settings.liljaPackagesDirectory);

                if (GUILayout.Button("📂", GUILayout.Width(30)))
                {
                    string path = EditorUtility.OpenFolderPanel(
                        "Select lilja-packages Directory",
                        _settings.liljaPackagesDirectory,
                        string.Empty
                    );

                    if (!string.IsNullOrEmpty(path))
                    {
                        _settings.liljaPackagesDirectory = path;
                        SaveSettings();
                    }
                }
            }

            if (string.IsNullOrEmpty(_settings.liljaPackagesDirectory))
            {
                EditorGUILayout.HelpBox(
                    "Please select the lilja-packages directory",
                    MessageType.Warning
                );
            }
        }

        private void DrawOrganizationNameField()
        {
            EditorGUILayout.LabelField("Organization", EditorStyles.boldLabel);
            _settings.organizationName = EditorGUILayout.TextField("Organization Name", _settings.organizationName);

            // スコーププレビュー
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Package Scope", $"com.{_settings.organizationName}");
            EditorGUI.EndDisabledGroup();
        }

        private void DrawPackageNameField()
        {
            EditorGUILayout.LabelField("Package Name", EditorStyles.boldLabel);

            // PackageBaseName 入力
            EditorGUI.BeginChangeCheck();
            string newBaseName = EditorGUILayout.TextField("Base Name (PascalCase)", _settings.packageBaseName);
            if (EditorGUI.EndChangeCheck())
            {
                _settings.packageBaseName = newBaseName;
                SaveSettings();
            }

            // 自動生成される名前
            string autoDisplayName = PackageCreator.GenerateDisplayName(_settings.packageBaseName);
            string autoPackageName =
                PackageCreator.GeneratePackageName(_settings.organizationName, _settings.packageBaseName);

            // DisplayName (Override可能)
            DrawOverrideTextField("DisplayName", autoDisplayName, ref _settings.displayNameOverride);

            // PackageName (Override可能)
            DrawOverrideTextField("Package Name", autoPackageName, ref _settings.packageNameOverride);
        }

        private void DrawOverrideTextField(string label, string autoValue, ref string overrideValue)
        {
            bool isOverridden = !string.IsNullOrEmpty(overrideValue);
            string displayValue = isOverridden ? overrideValue : autoValue;

            Color originalColor = GUI.color;
            if (!isOverridden)
            {
                // Auto値表示時は少し透過させてDisabledっぽく見せる
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
            }

            string newValue = EditorGUILayout.TextField(label, displayValue);

            GUI.color = originalColor;

            if (newValue != displayValue)
            {
                // 変更があった場合
                if (string.IsNullOrEmpty(newValue))
                {
                    // 空にされた -> Autoに戻す
                    overrideValue = string.Empty;
                }
                else if (newValue == autoValue)
                {
                    // Auto値と同じ -> Override不要
                    overrideValue = string.Empty;
                }
                else
                {
                    // 変更あり -> Override設定
                    overrideValue = newValue;
                }

                SaveSettings();
            }
        }

        private void DrawAuthorField()
        {
            _showAuthorSection = EditorGUILayout.Foldout(_showAuthorSection, "Author Info (Optional)", true);

            if (_showAuthorSection)
            {
                EditorGUI.indentLevel++;
                _settings.authorName = EditorGUILayout.TextField("Name", _settings.authorName);
                _settings.authorUrl = EditorGUILayout.TextField("URL", _settings.authorUrl);
                _settings.authorEmail = EditorGUILayout.TextField("Email", _settings.authorEmail);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawOptionsField()
        {
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            _settings.withImport = EditorGUILayout.Toggle(
                "With Import",
                _settings.withImport
            );

            _settings.useAnalyzer = EditorGUILayout.Toggle(
                "Use Analyzer",
                _settings.useAnalyzer
            );
        }

        private void DrawCreateButton()
        {
            bool canCreate = !string.IsNullOrEmpty(_settings.liljaPackagesDirectory) &&
                             !string.IsNullOrEmpty(_settings.packageBaseName);

            EditorGUI.BeginDisabledGroup(!canCreate);
            if (GUILayout.Button("Create Package", GUILayout.Height(40)))
            {
                CreatePackage();
            }

            EditorGUI.EndDisabledGroup();
        }

        #endregion

        #region Package Creation

        private void CreatePackage()
        {
            // パラメータを構築
            var parameters = new PackageCreatorParameters
            {
                LiljaPackagesDirectory = _settings.liljaPackagesDirectory,
                OrganizationName = _settings.organizationName,
                PackageBaseName = _settings.packageBaseName,
                AuthorName = _settings.authorName,
                AuthorUrl = _settings.authorUrl,
                AuthorEmail = _settings.authorEmail,
                UseAnalyzer = _settings.useAnalyzer,
                DisplayNameOverride = _settings.displayNameOverride,
                PackageNameOverride = _settings.packageNameOverride
            };

            // 出力先パス計算
            string kebabName = PackageCreator.ConvertToKebabCase(_settings.packageBaseName);
            string directoryName = $"lilja.{kebabName}";
            string targetPath = Path.Combine(_settings.liljaPackagesDirectory, directoryName);

            // ディレクトリ存在チェック
            if (Directory.Exists(targetPath))
            {
                EditorDialog.DisplayAlertDialog(
                    "Error",
                    $"Directory already exists:\n{targetPath}",
                    "OK",
                    DialogIconType.Error
                );
                return;
            }

            // パッケージ作成実行
            string createdPath = PackageCreator.Create(parameters);

            if (!string.IsNullOrEmpty(createdPath))
            {
                // インポート設定が有効な場合はmanifest.jsonに追加
                if (_settings.withImport)
                {
                    // srcフォルダ内にpackage.jsonがあるため、srcフォルダをインポート対象とする
                    PackageImporter.Import(Path.Combine(createdPath, "src"));
                }

                EditorDialog.DisplayAlertDialog(
                    "Success",
                    $"Package created successfully:\n{createdPath}",
                    "OK",
                    DialogIconType.Info
                );
            }
        }

        #endregion

        #region Settings Persistence

        private void LoadSettings()
        {
            // 1. Load Project Settings (JSON)
            if (File.Exists(SettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsPath);
                    _settings = JsonUtility.FromJson<Settings>(json);
                }
                catch
                {
                    _settings = new Settings();
                }
            }
            else
            {
                _settings = new Settings();
            }

            // 2. Load Local Settings (EditorPrefs)
            _settings.liljaPackagesDirectory =
                EditorPrefs.GetString(KeyLiljaPackagesDirectory, _settings.liljaPackagesDirectory);
            _settings.packageBaseName = EditorPrefs.GetString(KeyPackageBaseName, "NewPackage");
            _settings.authorName = EditorPrefs.GetString(KeyAuthorName, "");
            _settings.authorUrl = EditorPrefs.GetString(KeyAuthorUrl, "");
            _settings.authorEmail = EditorPrefs.GetString(KeyAuthorEmail, "");
            _settings.withImport = EditorPrefs.GetBool(KeyWithImport, true);
            _settings.useAnalyzer = EditorPrefs.GetBool(KeyUseAnalyzer, false);
            _settings.displayNameOverride = EditorPrefs.GetString(KeyPrefix + "DisplayNameOverride", "");
            _settings.packageNameOverride = EditorPrefs.GetString(KeyPrefix + "PackageNameOverride", "");
        }

        private void SaveSettings()
        {
            if (_settings == null)
            {
                return;
            }

            // 1. Save Project Settings (JSON) - Only serializable fields (organizationName) are saved via DTO
            try
            {
                var projectSettings = new ProjectSettingsData
                {
                    organizationName = _settings.organizationName
                };
                string json = JsonUtility.ToJson(projectSettings, true);
                File.WriteAllText(SettingsPath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save LiljaPackageCreator settings: {e.Message}");
            }

            // 2. Save Local Settings (EditorPrefs)
            EditorPrefs.SetString(KeyLiljaPackagesDirectory, _settings.liljaPackagesDirectory);
            EditorPrefs.SetString(KeyPackageBaseName, _settings.packageBaseName);
            EditorPrefs.SetString(KeyAuthorName, _settings.authorName);
            EditorPrefs.SetString(KeyAuthorUrl, _settings.authorUrl);
            EditorPrefs.SetString(KeyAuthorEmail, _settings.authorEmail);
            EditorPrefs.SetBool(KeyWithImport, _settings.withImport);
            EditorPrefs.SetBool(KeyUseAnalyzer, _settings.useAnalyzer);
            EditorPrefs.SetString(KeyPrefix + "DisplayNameOverride", _settings.displayNameOverride);
            EditorPrefs.SetString(KeyPrefix + "PackageNameOverride", _settings.packageNameOverride);
        }

        #endregion
    }
}

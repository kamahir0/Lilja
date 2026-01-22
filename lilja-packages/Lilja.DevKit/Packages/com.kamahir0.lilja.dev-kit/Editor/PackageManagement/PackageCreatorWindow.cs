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

        #endregion

        #region Serializable Settings

        /// <summary>
        /// JSON保存用の設定クラス
        /// </summary>
        [System.Serializable]
        private class Settings
        {
            public string liljaPackagesDirectory = "";
            public string organizationName = "kamahir0";
            public string packageBaseName = "NewPackage";

            // Author情報（任意）
            public string authorName = "";
            public string authorUrl = "";
            public string authorEmail = "";

            // 作成後の動作
            public bool withImport = true;
        }

        #endregion

        #region Fields

        private Settings _settings;
        private bool _showAuthorSection = false;

        #endregion

        #region Menu Item

        [MenuItem("Window/Lilja/Package Creator")]
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
                        ""
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

            // 自動生成される名前のプレビュー（編集不可）
            string displayName = PackageCreator.GenerateDisplayName(_settings.packageBaseName);
            string packageName =
                PackageCreator.GeneratePackageName(_settings.organizationName, _settings.packageBaseName);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("DisplayName (Auto)", displayName);
            EditorGUILayout.TextField("Package Name (Auto)", packageName);
            EditorGUI.EndDisabledGroup();
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
                AuthorEmail = _settings.authorEmail
            };

            // 出力先パス計算
            string displayName = PackageCreator.GenerateDisplayName(_settings.packageBaseName);
            string targetPath = Path.Combine(_settings.liljaPackagesDirectory, displayName);

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
                    // パッケージディレクトリ（package.jsonがある場所）を取得
                    string packageName = PackageCreator.GeneratePackageName(
                        _settings.organizationName,
                        _settings.packageBaseName
                    );
                    string packageDir = Path.Combine(createdPath, "Packages", packageName);
                    PackageImporter.Import(packageDir);
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
        }

        private void SaveSettings()
        {
            if (_settings == null)
            {
                return;
            }

            try
            {
                string json = JsonUtility.ToJson(_settings, true);
                File.WriteAllText(SettingsPath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save LiljaPackageCreator settings: {e.Message}");
            }
        }

        #endregion
    }
}

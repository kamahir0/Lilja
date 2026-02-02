#if UNITY_EDITOR
using System.IO;
using ScreenManagementSample.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScreenManagementSample.Editor
{
    /// <summary>
    /// サンプルRPGのアセット一括生成エディタスクリプト（3D対応版）
    /// </summary>
    public static class SampleAssetGenerator
    {
        private const string ScenesPath = "Assets/ScreenManagementTest/Sample/Scenes";
        private const string ResourcesPath = "Assets/ScreenManagementTest/Sample/Resources";

        // 3Dカラーパレット
        private static readonly Color PlayerColor = new Color(0.2f, 0.6f, 1f, 1f); // 青
        private static readonly Color EnemyColor = new Color(1f, 0.3f, 0.3f, 1f); // 赤
        private static readonly Color GridColor = new Color(0.3f, 0.3f, 0.35f, 1f); // グレー
        private static readonly Color AccentColor = new Color(1f, 0.8f, 0.2f, 1f); // ゴールド

        [MenuItem("Sample RPG/Generate Assets")]
        public static void GenerateAssets()
        {
            // ディレクトリの中身をクリア
            ClearDirectory(ScenesPath);
            ClearDirectory(ResourcesPath);

            // ディレクトリ作成
            EnsureDirectory(ScenesPath);
            EnsureDirectory($"{ResourcesPath}/Overlay");
            EnsureDirectory($"{ResourcesPath}/Common");

            // シーン生成（Bootを最後に生成し、生成完了時にBootが開かれた状態にする）
            GenerateTitleScene();
            GenerateMapScene();
            GenerateBattleScene();
            GenerateGameOverScene();
            GenerateBootScene();

            // プレハブ生成
            GenerateMenuPrefab();
            GenerateSkillSelectPrefab();
            GenerateItemSelectPrefab();
            GenerateTargetSelectPrefab();
            GenerateFadePrefab();

            AssetDatabase.Refresh();
            Debug.Log("[SampleAssetGenerator] アセット生成が完了しました");
        }

        private static void ClearDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (!file.EndsWith(".meta"))
                    {
                        AssetDatabase.DeleteAsset(file.Replace("\\", "/"));
                    }
                }
            }
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        #region シーン生成

        private static void GenerateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Boot コンポーネントのみ（UIは不要、すぐにアンロードされるため）
            var bootGo = new GameObject("Boot");
            bootGo.AddComponent<ScreenManagementSample.Boot>();

            EditorSceneManager.SaveScene(scene, $"{ScenesPath}/Boot.unity");
        }

        private static void GenerateTitleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvas = CreateCanvas("Canvas");
            CreateEventSystem();
            Create3DCamera(new Vector3(0, 2, -5), new Vector3(15, 0, 0));

            // ライト
            CreateDirectionalLight();

            // TitleView
            var titleViewGo = new GameObject("TitleView", typeof(RectTransform));
            titleViewGo.transform.SetParent(canvas.transform, false);
            var titleView = titleViewGo.AddComponent<TitleView>();

            // 3D装飾オブジェクト（回転するキューブ）
            var decorationGo = CreatePrimitive3D("Decoration", PrimitiveType.Cube, AccentColor, new Vector3(0, 1, 0));
            decorationGo.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            // タイトルテキスト
            var titleText = CreateText("TitleText", "Sample RPG", 96, canvas.transform);
            SetRectTransform(titleText.GetComponent<RectTransform>(), new Vector2(0.5f, 0.85f), new Vector2(800, 200));

            // スタートボタン
            var startButton = CreateButton("StartButton", "ゲームスタート", canvas.transform);
            SetRectTransform(startButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.15f), new Vector2(400, 120));

            // TitleViewにボタンを設定
            var serializedObject = new SerializedObject(titleView);
            serializedObject.FindProperty("_startButton").objectReferenceValue = startButton;
            serializedObject.FindProperty("_decorationModel").objectReferenceValue = decorationGo.transform;
            serializedObject.FindProperty("_rotationSpeed").floatValue = 30f;
            serializedObject.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, $"{ScenesPath}/Title.unity");
        }

        private static void GenerateMapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvas = CreateCanvas("Canvas");
            CreateEventSystem();
            Create3DCamera(new Vector3(3, 8, -3), new Vector3(60, 0, 0));

            // ライト
            CreateDirectionalLight();

            // MapView
            var mapViewGo = new GameObject("MapView", typeof(RectTransform));
            mapViewGo.transform.SetParent(canvas.transform, false);
            var mapView = mapViewGo.AddComponent<MapView>();

            // 3Dグリッド生成
            var gridContainer = new GameObject("GridContainer");
            const int gridSize = 5;
            const float tileSize = 1.5f;

            for (int x = 0; x < gridSize; x++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    var tile = CreatePrimitive3D($"Tile_{x}_{z}", PrimitiveType.Cube, GridColor,
                        new Vector3(x * tileSize, -0.25f, z * tileSize));
                    tile.transform.localScale = new Vector3(tileSize - 0.1f, 0.5f, tileSize - 0.1f);
                    tile.transform.SetParent(gridContainer.transform);
                }
            }

            // プレイヤーモデル（Capsule）
            var playerGo = CreatePrimitive3D("Player", PrimitiveType.Capsule, PlayerColor, new Vector3(0, 0.5f, 0));
            playerGo.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            // 位置表示
            var posText = CreateText("PositionText", "位置: (0, 0)", 48, canvas.transform);
            SetRectTransform(posText.GetComponent<RectTransform>(), new Vector2(0.5f, 0.95f), new Vector2(400, 80));

            // HP表示
            var hpText = CreateText("HpText", "HP: 100/100", 48, canvas.transform);
            SetRectTransform(hpText.GetComponent<RectTransform>(), new Vector2(0.5f, 0.9f), new Vector2(400, 80));

            // 移動ボタン
            var upButton = CreateButton("UpButton", "↑", canvas.transform);
            SetRectTransform(upButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.25f), new Vector2(120, 120));

            var downButton = CreateButton("DownButton", "↓", canvas.transform);
            SetRectTransform(downButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.05f), new Vector2(120, 120));

            var leftButton = CreateButton("LeftButton", "←", canvas.transform);
            SetRectTransform(leftButton.GetComponent<RectTransform>(), new Vector2(0.4f, 0.15f), new Vector2(120, 120));

            var rightButton = CreateButton("RightButton", "→", canvas.transform);
            SetRectTransform(rightButton.GetComponent<RectTransform>(), new Vector2(0.6f, 0.15f), new Vector2(120, 120));

            // メニューボタン
            var menuButton = CreateButton("MenuButton", "メニュー", canvas.transform);
            SetRectTransform(menuButton.GetComponent<RectTransform>(), new Vector2(0.9f, 0.95f), new Vector2(240, 100));

            // インタラクトボタン（初期状態では非表示）
            var interactButton = CreateButton("InteractButton", "調べる", canvas.transform);
            SetRectTransform(interactButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.4f), new Vector2(240, 100));
            interactButton.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.3f, 1f);
            interactButton.gameObject.SetActive(false);

            // MapViewに設定
            var serializedObject = new SerializedObject(mapView);
            serializedObject.FindProperty("_positionText").objectReferenceValue = posText.GetComponent<Text>();
            serializedObject.FindProperty("_hpText").objectReferenceValue = hpText.GetComponent<Text>();
            serializedObject.FindProperty("_upButton").objectReferenceValue = upButton;
            serializedObject.FindProperty("_downButton").objectReferenceValue = downButton;
            serializedObject.FindProperty("_leftButton").objectReferenceValue = leftButton;
            serializedObject.FindProperty("_rightButton").objectReferenceValue = rightButton;
            serializedObject.FindProperty("_menuButton").objectReferenceValue = menuButton;
            serializedObject.FindProperty("_interactButton").objectReferenceValue = interactButton;
            serializedObject.FindProperty("_playerModel").objectReferenceValue = playerGo.transform;
            serializedObject.FindProperty("_gridContainer").objectReferenceValue = gridContainer.transform;
            serializedObject.FindProperty("_tileSize").floatValue = tileSize;
            serializedObject.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, $"{ScenesPath}/Map.unity");
        }

        private static void GenerateBattleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvas = CreateCanvas("Canvas");
            CreateEventSystem();
            Create3DCamera(new Vector3(0, 3, -6), new Vector3(20, 0, 0));

            // ライト
            CreateDirectionalLight();

            // 床
            var floor = CreatePrimitive3D("Floor", PrimitiveType.Cube, new Color(0.2f, 0.25f, 0.2f),
                new Vector3(0, -0.5f, 0));
            floor.transform.localScale = new Vector3(10f, 1f, 8f);

            // BattleView
            var battleViewGo = new GameObject("BattleView", typeof(RectTransform));
            battleViewGo.transform.SetParent(canvas.transform, false);
            var battleView = battleViewGo.AddComponent<BattleView>();

            // プレイヤーモデル（Capsule）
            var playerGo = CreatePrimitive3D("PlayerBattle", PrimitiveType.Capsule, PlayerColor,
                new Vector3(-2, 0.5f, 0));
            playerGo.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

            // 敵コンテナ（動的生成用）
            var enemyContainer = new GameObject("EnemyContainer");
            enemyContainer.transform.position = Vector3.zero;

            // 敵プレハブを生成（Resources/Commonに保存）
            var enemyPrefab = CreatePrimitive3D("EnemyPrefab", PrimitiveType.Sphere, EnemyColor, Vector3.zero);
            enemyPrefab.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            PrefabUtility.SaveAsPrefabAsset(enemyPrefab, $"{ResourcesPath}/Common/EnemyPrefab.prefab");
            var enemyPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ResourcesPath}/Common/EnemyPrefab.prefab");
            Object.DestroyImmediate(enemyPrefab);

            // プレイヤーステータス
            var playerStatus = CreateText("PlayerStatusText", "プレイヤー\nHP: 100/100", 40, canvas.transform);
            SetRectTransform(playerStatus.GetComponent<RectTransform>(), new Vector2(0.15f, 0.2f), new Vector2(400, 160));

            // 敵ステータス（複数敵表示用に拡大）
            var enemyStatus = CreateText("EnemyStatusText", "敵A (HP:30/30)\n敵B (HP:30/30)\n敵C (HP:30/30)", 32, canvas.transform);
            SetRectTransform(enemyStatus.GetComponent<RectTransform>(), new Vector2(0.85f, 0.75f), new Vector2(500, 240));

            // メッセージ
            var message = CreateText("MessageText", "敵が現れた！", 48, canvas.transform);
            SetRectTransform(message.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(800, 120));

            // コマンドボタン（4つ横並び）
            var attackButton = CreateButton("AttackButton", "たたかう", canvas.transform);
            SetRectTransform(attackButton.GetComponent<RectTransform>(), new Vector2(0.2f, 0.1f), new Vector2(240, 100));

            var skillButton = CreateButton("SkillButton", "スキル", canvas.transform);
            SetRectTransform(skillButton.GetComponent<RectTransform>(), new Vector2(0.4f, 0.1f), new Vector2(240, 100));

            var itemButton = CreateButton("ItemButton", "アイテム", canvas.transform);
            SetRectTransform(itemButton.GetComponent<RectTransform>(), new Vector2(0.6f, 0.1f), new Vector2(240, 100));

            var defendButton = CreateButton("DefendButton", "防御", canvas.transform);
            SetRectTransform(defendButton.GetComponent<RectTransform>(), new Vector2(0.8f, 0.1f), new Vector2(240, 100));

            // BattleViewに設定
            var serializedObject = new SerializedObject(battleView);
            serializedObject.FindProperty("_playerStatusText").objectReferenceValue = playerStatus.GetComponent<Text>();
            serializedObject.FindProperty("_enemyStatusText").objectReferenceValue = enemyStatus.GetComponent<Text>();
            serializedObject.FindProperty("_messageText").objectReferenceValue = message.GetComponent<Text>();
            serializedObject.FindProperty("_attackButton").objectReferenceValue = attackButton;
            serializedObject.FindProperty("_skillButton").objectReferenceValue = skillButton;
            serializedObject.FindProperty("_itemButton").objectReferenceValue = itemButton;
            serializedObject.FindProperty("_defendButton").objectReferenceValue = defendButton;
            serializedObject.FindProperty("_playerBattleModel").objectReferenceValue = playerGo.transform;
            serializedObject.FindProperty("_enemyContainer").objectReferenceValue = enemyContainer.transform;
            serializedObject.FindProperty("_enemyPrefab").objectReferenceValue = enemyPrefabAsset;
            serializedObject.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, $"{ScenesPath}/Battle.unity");
        }

        private static void GenerateGameOverScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvas = CreateCanvas("Canvas");
            CreateEventSystem();
            Create3DCamera(new Vector3(0, 2, -5), new Vector3(15, 0, 0));

            // ライト
            CreateDirectionalLight();

            // GameOverView
            var gameOverViewGo = new GameObject("GameOverView", typeof(RectTransform));
            gameOverViewGo.transform.SetParent(canvas.transform, false);
            var gameOverView = gameOverViewGo.AddComponent<GameOverView>();

            // 落下するキューブコンテナ
            var fallingContainer = new GameObject("FallingCubes");
            var cubeColors = new[] { Color.red, Color.gray, Color.black, new Color(0.4f, 0.2f, 0.2f) };

            for (int i = 0; i < 8; i++)
            {
                var cube = CreatePrimitive3D($"FallingCube_{i}", PrimitiveType.Cube,
                    cubeColors[i % cubeColors.Length],
                    new Vector3(Random.Range(-3f, 3f), Random.Range(2f, 8f), Random.Range(-1f, 1f)));
                cube.transform.localScale = Vector3.one * Random.Range(0.3f, 0.8f);
                cube.transform.rotation = Random.rotation;
                cube.transform.SetParent(fallingContainer.transform);
            }

            // ゲームオーバーテキスト
            var gameOverText = CreateText("GameOverText", "GAME OVER", 96, canvas.transform);
            SetRectTransform(gameOverText.GetComponent<RectTransform>(), new Vector2(0.5f, 0.7f), new Vector2(800, 200));
            gameOverText.GetComponent<Text>().color = Color.red;

            // タイトルへ戻るボタン
            var titleButton = CreateButton("TitleButton", "タイトルへ", canvas.transform);
            SetRectTransform(titleButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.2f), new Vector2(400, 120));

            // GameOverViewに設定
            var serializedObject = new SerializedObject(gameOverView);
            serializedObject.FindProperty("_titleButton").objectReferenceValue = titleButton;
            serializedObject.FindProperty("_fallingCubesContainer").objectReferenceValue = fallingContainer.transform;
            serializedObject.FindProperty("_fallSpeed").floatValue = 3f;
            serializedObject.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, $"{ScenesPath}/GameOver.unity");
        }

        #endregion

        #region プレハブ生成

        private static void GenerateMenuPrefab()
        {
            // Canvas
            var canvasGo = new GameObject("Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // MenuView
            var menuView = canvasGo.AddComponent<MenuView>();

            // 背景
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);

            // パネル
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            SetRectTransform(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(800, 600));
            panel.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 1f);

            // ステータステキスト
            var statusText = CreateText("StatusText", "ステータス", 40, panel.transform);
            SetRectTransform(statusText.GetComponent<RectTransform>(), new Vector2(0.5f, 0.7f), new Vector2(700, 300));

            // 閉じるボタン
            var closeButton = CreateButton("CloseButton", "閉じる", panel.transform);
            SetRectTransform(closeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.15f), new Vector2(300, 100));

            // MenuViewに設定
            var serializedObject = new SerializedObject(menuView);
            serializedObject.FindProperty("_statusText").objectReferenceValue = statusText.GetComponent<Text>();
            serializedObject.FindProperty("_closeButton").objectReferenceValue = closeButton;
            serializedObject.ApplyModifiedProperties();

            // プレハブとして保存
            PrefabUtility.SaveAsPrefabAsset(canvasGo, $"{ResourcesPath}/Overlay/Menu.prefab");
            Object.DestroyImmediate(canvasGo);
        }

        private static void GenerateSkillSelectPrefab()
        {
            // Canvas
            var canvasGo = new GameObject("SkillSelect", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // SkillSelectView
            var skillSelectView = canvasGo.AddComponent<SkillSelectView>();

            // 背景（半透明）
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);

            // パネル
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            SetRectTransform(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(800, 700));
            panel.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.25f, 1f);

            // タイトル
            var titleText = CreateText("TitleText", "スキル選択", 56, panel.transform);
            SetRectTransform(titleText.GetComponent<RectTransform>(), new Vector2(0.5f, 0.9f), new Vector2(600, 100));

            // ボタンコンテナ（動的ボタン用）
            var buttonContainer = new GameObject("ButtonContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            buttonContainer.transform.SetParent(panel.transform, false);
            var containerRect = buttonContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.1f, 0.25f);
            containerRect.anchorMax = new Vector2(0.9f, 0.8f);
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;
            var layout = buttonContainer.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // スキルボタンプレハブ（非表示）
            var skillButtonPrefab = CreateButton("SkillButtonPrefab", "スキル", panel.transform);
            SetRectTransform(skillButtonPrefab.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(500, 100));
            skillButtonPrefab.gameObject.SetActive(false);

            // 戻るボタン
            var backButton = CreateButton("BackButton", "もどる", panel.transform);
            SetRectTransform(backButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.08f), new Vector2(300, 80));
            backButton.GetComponent<Image>().color = new Color(0.4f, 0.25f, 0.25f, 1f);

            // 説明テキスト
            var descText = CreateText("DescriptionText", "スキルを選んでください", 36, panel.transform);
            SetRectTransform(descText.GetComponent<RectTransform>(), new Vector2(0.5f, -0.05f), new Vector2(700, 80));

            // SkillSelectViewに設定
            var serializedObject = new SerializedObject(skillSelectView);
            serializedObject.FindProperty("_buttonContainer").objectReferenceValue = buttonContainer.transform;
            serializedObject.FindProperty("_skillButtonPrefab").objectReferenceValue = skillButtonPrefab;
            serializedObject.FindProperty("_backButton").objectReferenceValue = backButton;
            serializedObject.FindProperty("_descriptionText").objectReferenceValue = descText.GetComponent<Text>();
            serializedObject.ApplyModifiedProperties();

            // プレハブとして保存
            PrefabUtility.SaveAsPrefabAsset(canvasGo, $"{ResourcesPath}/Overlay/SkillSelect.prefab");
            Object.DestroyImmediate(canvasGo);
        }

        private static void GenerateItemSelectPrefab()
        {
            // Canvas
            var canvasGo = new GameObject("ItemSelect", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // ItemSelectView
            var itemSelectView = canvasGo.AddComponent<ItemSelectView>();

            // 背景（半透明）
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);

            // パネル
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            SetRectTransform(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(800, 700));
            panel.GetComponent<Image>().color = new Color(0.15f, 0.2f, 0.15f, 1f);

            // タイトル
            var titleText = CreateText("TitleText", "アイテム選択", 56, panel.transform);
            SetRectTransform(titleText.GetComponent<RectTransform>(), new Vector2(0.5f, 0.9f), new Vector2(600, 100));

            // ボタンコンテナ（動的ボタン用）
            var buttonContainer = new GameObject("ButtonContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            buttonContainer.transform.SetParent(panel.transform, false);
            var containerRect = buttonContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.1f, 0.25f);
            containerRect.anchorMax = new Vector2(0.9f, 0.8f);
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;
            var layout = buttonContainer.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // アイテムボタンプレハブ（非表示）
            var itemButtonPrefab = CreateButton("ItemButtonPrefab", "アイテム", panel.transform);
            SetRectTransform(itemButtonPrefab.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(500, 100));
            itemButtonPrefab.gameObject.SetActive(false);

            // 戻るボタン
            var backButton = CreateButton("BackButton", "もどる", panel.transform);
            SetRectTransform(backButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.08f), new Vector2(300, 80));
            backButton.GetComponent<Image>().color = new Color(0.4f, 0.25f, 0.25f, 1f);

            // 説明テキスト
            var descText = CreateText("DescriptionText", "アイテムを選んでください", 36, panel.transform);
            SetRectTransform(descText.GetComponent<RectTransform>(), new Vector2(0.5f, -0.05f), new Vector2(700, 80));

            // ItemSelectViewに設定
            var serializedObject = new SerializedObject(itemSelectView);
            serializedObject.FindProperty("_buttonContainer").objectReferenceValue = buttonContainer.transform;
            serializedObject.FindProperty("_itemButtonPrefab").objectReferenceValue = itemButtonPrefab;
            serializedObject.FindProperty("_backButton").objectReferenceValue = backButton;
            serializedObject.FindProperty("_descriptionText").objectReferenceValue = descText.GetComponent<Text>();
            serializedObject.ApplyModifiedProperties();

            // プレハブとして保存
            PrefabUtility.SaveAsPrefabAsset(canvasGo, $"{ResourcesPath}/Overlay/ItemSelect.prefab");
            Object.DestroyImmediate(canvasGo);
        }

        private static void GenerateTargetSelectPrefab()
        {
            // Canvas
            var canvasGo = new GameObject("TargetSelect", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // TargetSelectView
            var targetSelectView = canvasGo.AddComponent<TargetSelectView>();

            // 背景（半透明）
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);

            // パネル
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            SetRectTransform(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(700, 600));
            panel.GetComponent<Image>().color = new Color(0.2f, 0.15f, 0.15f, 1f);

            // タイトル
            var titleText = CreateText("TitleText", "ターゲット選択", 56, panel.transform);
            SetRectTransform(titleText.GetComponent<RectTransform>(), new Vector2(0.5f, 0.9f), new Vector2(600, 100));

            // ボタンコンテナ（動的ボタン用）
            var buttonContainer = new GameObject("ButtonContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            buttonContainer.transform.SetParent(panel.transform, false);
            var containerRect = buttonContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.1f, 0.25f);
            containerRect.anchorMax = new Vector2(0.9f, 0.8f);
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;
            var layout = buttonContainer.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // ターゲットボタンプレハブ（非表示）
            var targetButtonPrefab = CreateButton("TargetButtonPrefab", "敵A (HP:30/30)", panel.transform);
            SetRectTransform(targetButtonPrefab.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(500, 90));
            targetButtonPrefab.gameObject.SetActive(false);

            // 戻るボタン
            var backButton = CreateButton("BackButton", "もどる", panel.transform);
            SetRectTransform(backButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.08f), new Vector2(300, 80));
            backButton.GetComponent<Image>().color = new Color(0.4f, 0.25f, 0.25f, 1f);

            // 説明テキスト
            var descText = CreateText("DescriptionText", "ターゲットを選んでください", 36, panel.transform);
            SetRectTransform(descText.GetComponent<RectTransform>(), new Vector2(0.5f, -0.05f), new Vector2(700, 80));

            // TargetSelectViewに設定
            var serializedObject2 = new SerializedObject(targetSelectView);
            serializedObject2.FindProperty("_buttonContainer").objectReferenceValue = buttonContainer.transform;
            serializedObject2.FindProperty("_targetButtonPrefab").objectReferenceValue = targetButtonPrefab;
            serializedObject2.FindProperty("_backButton").objectReferenceValue = backButton;
            serializedObject2.FindProperty("_descriptionText").objectReferenceValue = descText.GetComponent<Text>();
            serializedObject2.ApplyModifiedProperties();

            // プレハブとして保存
            PrefabUtility.SaveAsPrefabAsset(canvasGo, $"{ResourcesPath}/Overlay/TargetSelect.prefab");
            Object.DestroyImmediate(canvasGo);
        }

        private static void GenerateFadePrefab()
        {
            var fadeGo = new GameObject("SampleFade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = fadeGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var scaler = fadeGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var canvasGroup = fadeGo.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // 黒背景
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(fadeGo.transform, false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bg.GetComponent<Image>().color = Color.black;

            // SampleFadeコンポーネント
            var fade = fadeGo.AddComponent<SampleFade>();
            var serializedObject = new SerializedObject(fade);
            serializedObject.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            serializedObject.FindProperty("_fadeDuration").floatValue = 0.3f;
            serializedObject.ApplyModifiedProperties();

            // プレハブとして保存
            PrefabUtility.SaveAsPrefabAsset(fadeGo, $"{ResourcesPath}/Common/SampleFade.prefab");
            Object.DestroyImmediate(fadeGo);
        }

        #endregion

        #region 3Dヘルパー

        private static GameObject CreatePrimitive3D(string name, PrimitiveType primitiveType, Color color, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            go.transform.position = position;

            // マテリアル設定（URP/Built-in両対応）
            var renderer = go.GetComponent<Renderer>();

            // マテリアルをアセットとして保存・読み込み（プレハブ化時にリンクが切れないように）
            var materialName = $"Mat_{name}";
            var materialPath = $"{ResourcesPath}/Common/{materialName}.mat";

            // 既存のマテリアルがあればロード、なければ作成
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = CreateLitMaterial(color); // URP Litを使用
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                // 既存マテリアルの色を更新
                UpdateMaterialColor(material, color);
            }

            renderer.sharedMaterial = material;

            return go;
        }

        /// <summary>
        /// URP/Built-in両対応のLitマテリアルを作成
        /// </summary>
        private static Material CreateLitMaterial(Color color)
        {
            // URPのLitシェーダーを優先的に探す
            var shader = Shader.Find("Universal Render Pipeline/Lit");

            // 見つからなければBuilt-inのStandardを試す
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            // 最終フォールバック：Diffuse
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            var material = new Material(shader);
            UpdateMaterialColor(material, color);

            return material;
        }

        private static void UpdateMaterialColor(Material material, Color color)
        {
            // シェーダーに応じて色を設定
            if (material.HasProperty("_BaseColor"))
            {
                // URP Lit
                // Note: URP Litの場合はWorkflow Modeなどに依存する場合があるが、基本は_BaseColor
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                // Built-in Standard / Unlit
                material.SetColor("_Color", color);
            }
        }

        private static void Create3DCamera(Vector3 position, Vector3 rotation)
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            camera.orthographic = false; // Perspective
            camera.fieldOfView = 60f;
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = position;
            cameraGo.transform.eulerAngles = rotation;
        }

        private static void CreateDirectionalLight()
        {
            var lightGo = new GameObject("Directional Light", typeof(Light));
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.9f);
            light.intensity = 1f;
            lightGo.transform.position = new Vector3(0, 3, 0);
            lightGo.transform.eulerAngles = new Vector3(50, -30, 0);
        }

        #endregion

        #region UIヘルパー

        private static Canvas CreateCanvas(string name)
        {
            var canvasGo = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            return canvas;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static GameObject CreateText(string name, string text, int fontSize, Transform parent)
        {
            var textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(parent, false);

            var textComp = textGo.GetComponent<Text>();
            textComp.text = text;
            textComp.fontSize = fontSize;
            textComp.color = Color.white;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return textGo;
        }

        private static Button CreateButton(string name, string label, Transform parent, int fontSize = 48)
        {
            var buttonGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);

            var image = buttonGo.GetComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.35f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(buttonGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var textComp = textGo.GetComponent<Text>();
            textComp.text = label;
            textComp.fontSize = fontSize;
            textComp.color = Color.white;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return buttonGo.GetComponent<Button>();
        }

        private static void SetRectTransform(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        #endregion
    }
}
#endif

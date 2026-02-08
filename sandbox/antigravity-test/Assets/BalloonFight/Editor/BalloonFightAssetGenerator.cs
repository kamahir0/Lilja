using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

namespace BalloonFight.Editor
{
    /// <summary>
    /// バルーンファイト（BALLOON TRIPモード）のアセットを一括生成するエディタスクリプト
    /// </summary>
    public static class BalloonFightAssetGenerator
    {
        private const string RootPath = "Assets/BalloonFight";
        private const string MaterialsPath = RootPath + "/Materials";
        private const string PrefabsPath = RootPath + "/Prefabs";
        private const string ScenesPath = RootPath + "/Scenes";

        [MenuItem("Tools/BalloonFight/Generate All Assets (BALLOON TRIP)")]
        public static void GenerateAllAssets()
        {
            if (!EditorUtility.DisplayDialog(
                "アセット生成確認",
                "BALLOON TRIPモード用のアセットを生成します。\n既存のBalloonFightアセットは削除されます。よろしいですか？",
                "はい", "キャンセル"))
            {
                return;
            }

            // タグを作成
            CreateRequiredTags();

            // 古いアセットを削除
            DeleteOldAssets();

            // フォルダ作成
            CreateFolders();

            // アセット生成
            var materials = CreateMaterials();
            CreatePrefabs(materials);
            CreateBootScene(materials);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BalloonFight] BALLOON TRIP用アセットの生成が完了しました！");
            EditorUtility.DisplayDialog("完了", "アセットの生成が完了しました。\nBootシーンを開いて再生してください。", "OK");
        }

        private static void CreateRequiredTags()
        {
            // TagManagerを取得
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProperty = tagManager.FindProperty("tags");

            string[] requiredTags = { "PlayerBalloon", "Spark", "Collectible", "Sea", "Player" };

            foreach (string tagName in requiredTags)
            {
                bool found = false;
                for (int i = 0; i < tagsProperty.arraySize; i++)
                {
                    if (tagsProperty.GetArrayElementAtIndex(i).stringValue == tagName)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    tagsProperty.InsertArrayElementAtIndex(tagsProperty.arraySize);
                    tagsProperty.GetArrayElementAtIndex(tagsProperty.arraySize - 1).stringValue = tagName;
                    Debug.Log($"[BalloonFight] タグ '{tagName}' を作成しました");
                }
            }

            tagManager.ApplyModifiedProperties();
        }

        private static void DeleteOldAssets()
        {
            if (AssetDatabase.IsValidFolder(RootPath))
            {
                // Scriptsフォルダは残す
                string scriptsPath = RootPath + "/Scripts";
                string editorPath = RootPath + "/Editor";
                
                // Materials, Prefabs, Scenesを削除
                if (AssetDatabase.IsValidFolder(MaterialsPath))
                    AssetDatabase.DeleteAsset(MaterialsPath);
                if (AssetDatabase.IsValidFolder(PrefabsPath))
                    AssetDatabase.DeleteAsset(PrefabsPath);
                if (AssetDatabase.IsValidFolder(ScenesPath))
                    AssetDatabase.DeleteAsset(ScenesPath);
            }
            AssetDatabase.Refresh();
        }

        private static void CreateFolders()
        {
            if (!AssetDatabase.IsValidFolder(RootPath))
                AssetDatabase.CreateFolder("Assets", "BalloonFight");
            if (!AssetDatabase.IsValidFolder(MaterialsPath))
                AssetDatabase.CreateFolder(RootPath, "Materials");
            if (!AssetDatabase.IsValidFolder(PrefabsPath))
                AssetDatabase.CreateFolder(RootPath, "Prefabs");
            if (!AssetDatabase.IsValidFolder(ScenesPath))
                AssetDatabase.CreateFolder(RootPath, "Scenes");
        }

        private static MaterialSet CreateMaterials()
        {
            var set = new MaterialSet();

            // プレイヤー用
            set.PlayerBody = CreateMaterial("PlayerBody", new Color(0.2f, 0.6f, 1f));
            set.PlayerBalloon = CreateMaterial("PlayerBalloon", new Color(1f, 0.4f, 0.4f));
            set.BalloonString = CreateMaterial("BalloonString", new Color(0.9f, 0.9f, 0.9f));

            // オブジェクト用
            set.Spark = CreateMaterial("Spark", new Color(1f, 1f, 0.2f), true); // 発光
            set.Collectible = CreateMaterial("Collectible", new Color(1f, 0.6f, 0.2f)); // オレンジ色の風船
            
            // 環境用
            set.Sea = CreateMaterial("Sea", new Color(0.1f, 0.2f, 0.8f));
            set.Star = CreateMaterial("Star", new Color(1f, 1f, 1f), true); // 発光
            set.Background = CreateMaterial("Background", new Color(0.05f, 0.05f, 0.1f));

            return set;
        }

        private static Material CreateMaterial(string name, Color color, bool emission = false)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.5f);
            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.5f);
            }
            
            string path = $"{MaterialsPath}/{name}.mat";
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void CreatePrefabs(MaterialSet materials)
        {
            CreatePlayerPrefab(materials);
            CreateSparkPrefab(materials);
            CreateCollectiblePrefab(materials);
            CreateSeaPrefab(materials);
            CreateStarPrefab(materials);
        }

        private static void CreatePlayerPrefab(MaterialSet materials)
        {
            // ルートオブジェクト
            var player = new GameObject("Player");
            player.tag = "Player";
            var rb = player.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            rb.mass = 1f;
            rb.linearDamping = 0.5f;

            // 体
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform);
            body.transform.localPosition = Vector3.zero;
            // スケール縮小: 0.6 -> 0.4
            body.transform.localScale = new Vector3(0.4f, 0.35f, 0.4f);
            body.GetComponent<Renderer>().sharedMaterial = materials.PlayerBody;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            // コライダー
            var bodyCollider = player.AddComponent<CapsuleCollider>();
            // コライダーサイズ縮小: 1f -> 0.7f, radius: 0.3 -> 0.2
            bodyCollider.height = 0.7f;
            bodyCollider.radius = 0.2f;

            // 風船
            CreateBalloon(player.transform, "Balloon1", new Vector3(-0.15f, 0.8f, 0), materials.PlayerBalloon, materials.BalloonString, true);
            CreateBalloon(player.transform, "Balloon2", new Vector3(0.15f, 0.8f, 0), materials.PlayerBalloon, materials.BalloonString, true);

            // PlayerControllerスクリプト
            var playerControllerType = System.Type.GetType("BalloonFight.PlayerController, Assembly-CSharp");
            if (playerControllerType != null)
            {
                player.AddComponent(playerControllerType);
            }

            // 保存
            var path = $"{PrefabsPath}/Player.prefab";
            PrefabUtility.SaveAsPrefabAsset(player, path);
            Object.DestroyImmediate(player);
        }

        private static void CreateSparkPrefab(MaterialSet materials)
        {
            var spark = new GameObject("Spark");
            spark.tag = "Spark";
            
            // 本体
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(spark.transform);
            // スケール縮小: 0.8 -> 0.5
            body.transform.localScale = Vector3.one * 0.5f;
            body.GetComponent<Renderer>().sharedMaterial = materials.Spark;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            // コライダー（Trigger）
            var collider = spark.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            // 半径縮小: 0.4 -> 0.25
            collider.radius = 0.25f;

            // Sparkスクリプト
            var sparkType = System.Type.GetType("BalloonFight.Spark, Assembly-CSharp");
            if (sparkType != null)
            {
                spark.AddComponent(sparkType);
            }

            PrefabUtility.SaveAsPrefabAsset(spark, $"{PrefabsPath}/Spark.prefab");
            Object.DestroyImmediate(spark);
        }

        private static void CreateCollectiblePrefab(MaterialSet materials)
        {
            var balloon = new GameObject("CollectibleBalloon");
            balloon.tag = "Collectible";

            // 風船モデル
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(balloon.transform);
            // スケール縮小: 0.6 -> 0.4
            body.transform.localScale = new Vector3(0.4f, 0.5f, 0.4f);
            body.GetComponent<Renderer>().sharedMaterial = materials.Collectible;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            // 紐
            var str = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            str.transform.SetParent(balloon.transform);
            str.transform.localPosition = new Vector3(0, -0.3f, 0);
            str.transform.localScale = new Vector3(0.03f, 0.3f, 0.03f);
            str.GetComponent<Renderer>().sharedMaterial = materials.BalloonString;
            Object.DestroyImmediate(str.GetComponent<Collider>());

            // コライダー
            var collider = balloon.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            // 半径縮小: 0.4 -> 0.25
            collider.radius = 0.25f;

            // Collectibleスクリプト
            var collecType = System.Type.GetType("BalloonFight.Collectible, Assembly-CSharp");
            if (collecType != null)
            {
                balloon.AddComponent(collecType);
            }

            PrefabUtility.SaveAsPrefabAsset(balloon, $"{PrefabsPath}/CollectibleBalloon.prefab");
            Object.DestroyImmediate(balloon);
        }

        private static void CreateSeaPrefab(MaterialSet materials)
        {
            var sea = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sea.name = "Sea";
            sea.tag = "Sea";
            sea.transform.localScale = new Vector3(1, 1, 1); // スクリプトでサイズ調整
            sea.GetComponent<Renderer>().sharedMaterial = materials.Sea;
            
            // コライダーはCubeについているものをそのまま
            sea.GetComponent<BoxCollider>().isTrigger = true;

            PrefabUtility.SaveAsPrefabAsset(sea, $"{PrefabsPath}/Sea.prefab");
            Object.DestroyImmediate(sea);
        }

        private static void CreateStarPrefab(MaterialSet materials)
        {
            var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            star.name = "Star";
            star.transform.localScale = Vector3.one * 0.2f;
            star.GetComponent<Renderer>().sharedMaterial = materials.Star;
            Object.DestroyImmediate(star.GetComponent<Collider>());

            PrefabUtility.SaveAsPrefabAsset(star, $"{PrefabsPath}/Star.prefab");
            Object.DestroyImmediate(star);
        }

        private static void CreateBalloon(Transform parent, string name, Vector3 localPos, Material balloonMat, Material stringMat, bool isPlayer)
        {
            var balloonHolder = new GameObject(name);
            balloonHolder.transform.SetParent(parent);
            balloonHolder.transform.localPosition = localPos;

            var balloon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            balloon.name = "BalloonSphere";
            balloon.transform.SetParent(balloonHolder.transform);
            balloon.transform.localPosition = Vector3.zero;
            // スケール縮小: 0.5 -> 0.3
            balloon.transform.localScale = new Vector3(0.3f, 0.35f, 0.3f);
            balloon.GetComponent<Renderer>().sharedMaterial = balloonMat;
            
            var balloonCollider = balloon.GetComponent<SphereCollider>();
            balloonCollider.isTrigger = true;
            balloon.tag = isPlayer ? "PlayerBalloon" : "EnemyBalloon"; // EnemyBalloonタグは残しておく

            var str = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            str.name = "String";
            str.transform.SetParent(balloonHolder.transform);
            str.transform.localPosition = new Vector3(0, -0.2f, 0);
            str.transform.localScale = new Vector3(0.02f, 0.2f, 0.02f);
            str.GetComponent<Renderer>().sharedMaterial = stringMat;
            Object.DestroyImmediate(str.GetComponent<Collider>());
        }



        private static void CreateBootScene(MaterialSet materials)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // カメラ
            var cameraObj = new GameObject("Main Camera");
            var camera = cameraObj.AddComponent<Camera>();
            cameraObj.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            // GameManager
            var gameManager = new GameObject("GameManager");
            var gameManagerType = System.Type.GetType("BalloonFight.GameManager, Assembly-CSharp");
            if (gameManagerType != null)
            {
                gameManager.AddComponent(gameManagerType);
            }

            // ScrollSystem
            var scrollSystem = new GameObject("ScrollSystem");
            var scrollType = System.Type.GetType("BalloonFight.ScrollSystem, Assembly-CSharp");
            if (scrollType != null)
            {
                scrollSystem.AddComponent(scrollType);
            }

            // プレイヤーをインスタンス化
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Player.prefab");
            if (playerPrefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                // カメラより少し右、画面中央付近に配置
                player.transform.position = new Vector3(0, 0, 0);
            }

            // シーン保存
            string scenePath = $"{ScenesPath}/Boot.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private class MaterialSet
        {
            public Material PlayerBody;
            public Material PlayerBalloon;
            public Material BalloonString;
            public Material Spark;
            public Material Collectible;
            public Material Sea;
            public Material Star;
            public Material Background;
        }
    }
}

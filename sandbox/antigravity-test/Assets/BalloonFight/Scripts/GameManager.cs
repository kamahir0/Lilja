using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BalloonFight
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("ゲーム設定")]
        [SerializeField] private float scrollSpeed = 1.5f;

        // プレハブ参照（インスペクタで設定できない場合のためにResources.LoadやAssetDatabase.Loadを使用）
        private GameObject sparkPrefab;
        private GameObject collectiblePrefab;
        private GameObject seaPrefab;
        private GameObject starPrefab;
        private GameObject sharkPrefab;

        private bool isGameOver = false;
        private int score = 0;
        private float distanceTraveled = 0;
        
        // 生成管理
        private float nextSpawnX = 5f;
        private Transform cameraTransform;

        public bool IsGameOver => isGameOver;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            cameraTransform = Camera.main.transform;
            LoadPrefabs();
            
            // 海を生成してカメラの子にする（追従用）
            if (seaPrefab != null)
            {
                var sea = Instantiate(seaPrefab);
                sea.transform.position = new Vector3(cameraTransform.position.x, -6f, 0); // 低めの位置
                sea.transform.localScale = new Vector3(30, 2, 5);
                sea.transform.SetParent(cameraTransform);
            }
        }

        private void LoadPrefabs()
        {
            // エディタ実行時のみ有効なパス読み込み（ビルド後はResourcesフォルダが必要だが今回はエディタ前提）
#if UNITY_EDITOR
            sparkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BalloonFight/Prefabs/Spark.prefab");
            collectiblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BalloonFight/Prefabs/CollectibleBalloon.prefab");
            seaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BalloonFight/Prefabs/Sea.prefab");
            starPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BalloonFight/Prefabs/Star.prefab");
            sharkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BalloonFight/Prefabs/Shark.prefab");
#endif
        }

        public void SpawnShark(Vector3 targetPos)
        {
            if (sharkPrefab == null) return;
            
            // 水面下（Y=-5付近）から生成
            Vector3 spawnPos = new Vector3(targetPos.x, -5f, 0);
            var sharkObj = Instantiate(sharkPrefab, spawnPos, Quaternion.identity);
            
            var shark = sharkObj.GetComponent<Shark>();
            if (shark != null)
            {
                var player = Object.FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    shark.Attack(player.transform);
                }
            }
        }

        private void Update()
        {
            if (isGameOver)
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                }
                return;
            }

            // スクロール処理
            Vector3 pos = cameraTransform.position;
            pos.x += scrollSpeed * Time.deltaTime;
            cameraTransform.position = pos;
            
            distanceTraveled = pos.x;

            // オブジェクト生成
            GenerateObjects();
        }

        private void GenerateObjects()
        {
            // カメラの右端より少し先に生成
            float spawnX = cameraTransform.position.x + 10f;

            if (spawnX > nextSpawnX)
            {
                // ランダムに生成パターン決定
                float rand = Random.value;

                if (rand < 0.4f)
                {
                    // スパーク生成
                    SpawnSpark(spawnX);
                }
                else if (rand < 0.7f)
                {
                    // 風船生成
                    SpawnCollectible(spawnX);
                }
                else
                {
                    // 背景の星
                    if (starPrefab != null)
                    {
                        Instantiate(starPrefab, new Vector3(spawnX, Random.Range(-2f, 6f), 5f), Quaternion.identity);
                    }
                }

                nextSpawnX += Random.Range(2f, 5f);
            }
        }

        private void SpawnSpark(float x)
        {
            if (sparkPrefab == null) return;
            float y = Random.Range(-2f, 5f);
            Instantiate(sparkPrefab, new Vector3(x, y, 0), Quaternion.identity);
        }

        private void SpawnCollectible(float x)
        {
            if (collectiblePrefab == null) return;
            float y = Random.Range(-2f, 5f);
            Instantiate(collectiblePrefab, new Vector3(x, y, 0), Quaternion.identity);
        }

        public void AddScore(int value)
        {
            score += value;
        }

        public void OnPlayerDied()
        {
            if (isGameOver) return;
            isGameOver = true;
            Debug.Log($"Game Over! Score: {score}");
        }

        private void OnGUI()
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 30;
            style.normal.textColor = Color.white;
            
            // スコア表示
            GUI.Label(new Rect(20, 20, 300, 50), $"SCORE: {score}", style);

            if (isGameOver)
            {
                style.fontSize = 60;
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = new Color(1f, 0.3f, 0.3f);
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "GAME OVER\nPress R", style);
            }
        }
    }
}

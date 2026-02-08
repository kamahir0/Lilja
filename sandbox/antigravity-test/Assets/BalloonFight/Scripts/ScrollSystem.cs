using UnityEngine;

namespace BalloonFight
{
    public class ScrollSystem : MonoBehaviour
    {
        [SerializeField] private float scrollSpeed = 3f;
        [SerializeField] private float waterLevelY = -4.5f;
        [SerializeField] private float starInterval = 8f;

        private Transform cameraTransform;
        private float nextStarX;
        private GameObject seaObject; // 海のプレハブをシーン生成時に配置していないため、動的に生成・管理する

        private void Start()
        {
            cameraTransform = Camera.main.transform;
            nextStarX = cameraTransform.position.x;
            
            // 海を生成（カメラに追従させる）
            LoadSea();
        }

        private void Update()
        {
            if (GameManager.Instance.IsGameOver) return;

            // カメラを移動
            Vector3 pos = cameraTransform.position;
            pos.x += scrollSpeed * Time.deltaTime;
            cameraTransform.position = pos;

            // 海を追従
            if (seaObject != null)
            {
                Vector3 seaPos = seaObject.transform.position;
                seaPos.x = cameraTransform.position.x;
                seaObject.transform.position = seaPos;
            }

            // 背景（星）の生成
            if (cameraTransform.position.x + 15f > nextStarX)
            {
                GenerateStar(nextStarX);
                nextStarX += Random.Range(2f, 5f);
            }
        }

        private void LoadSea()
        {
            var seaPrefab = Resources.Load<GameObject>("BalloonFight/Prefabs/Sea");
            // Prefabが無い場合に備えて動的生成も考慮（本来はAssetGeneratorで作る）
            // ここではResources.Loadは使えない（Assets以下のパス指定が必要、かつResourcesフォルダに入れていないため）
            // エディタスクリプトで生成したプレハブをGameManager等から参照して渡す設計にするのがUnity流だが、
            // 簡易的にプリミティブ生成で代用するか、GameManagerのGenerateStageで生成されたものを管理する。
            
            // 下記GameManagerで生成管理する形に変更するため、ここではスクロールのみ担当
        }

        void GenerateStar(float x)
        {
            // 簡易的な星生成。本来はGameManagerで管理すべきだが、視覚効果なのでここで。
            // GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // ... (省略)
        }
    }
}

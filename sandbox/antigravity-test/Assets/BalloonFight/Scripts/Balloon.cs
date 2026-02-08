using UnityEngine;

namespace BalloonFight
{
    /// <summary>
    /// 風船コンポーネント。衝突判定を担当。
    /// </summary>
    public class Balloon : MonoBehaviour
    {
        private PlayerController playerController;

        private void Start()
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (playerController == null || playerController.IsDead) return;

            if (other.CompareTag("Spark"))
            {
                // スパーク（障害物）に当たったら死亡
                playerController.Die();
            }
            else if (other.CompareTag("Sea"))
            {
                // 海に落ちたら死亡
                playerController.Die();
            }
            else if (other.CompareTag("Collectible"))
            {
                // コレクタブル（風船）を取得
                var collectible = other.GetComponent<Collectible>();
                if (collectible != null)
                {
                    collectible.Collect();
                }
            }
        }
    }
}

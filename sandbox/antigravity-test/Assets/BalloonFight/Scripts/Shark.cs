using UnityEngine;

namespace BalloonFight
{
    public class Shark : MonoBehaviour
    {
        [SerializeField] private float jumpForce = 12f;
        private Rigidbody rb;
        private bool hasEaten = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public void Attack(Transform target)
        {
            // 水面下から飛び出す
            // プレイヤーのX座標に合わせてジャンプ
            Vector3 jumpDir = (target.position - transform.position).normalized;
            jumpDir.y = 1f; // 上方向重視
            jumpDir.x *= 0.5f; // 横方向は少し抑えめに
            
            rb.AddForce(jumpDir * jumpForce, ForceMode.Impulse);
            
            // 回転演出（口を開けているように見せるため上を向く）
            transform.rotation = Quaternion.Euler(-45, 0, 0);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasEaten) return;

            if (other.CompareTag("Player"))
            {
                hasEaten = true;
                // プレイヤーを食べる
                var player = other.GetComponent<PlayerController>();
                if (player != null)
                {
                    player.EatenByShark();
                }
                
                // 自身も少しして消える
                Destroy(gameObject, 2f);
            }
        }

        private void FixedUpdate()
        {
            // 落下時に頭を下に向ける
            if (rb.linearVelocity.y < 0)
            {
                float angle = Mathf.LerpAngle(transform.rotation.eulerAngles.x, 45, Time.fixedDeltaTime * 2f);
                transform.rotation = Quaternion.Euler(angle, 0, 0);
            }
        }
    }
}

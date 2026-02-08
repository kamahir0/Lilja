using UnityEngine;

namespace BalloonFight
{
    public class PlayerController : MonoBehaviour
    {
        [Header("移動設定")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float flapForce = 8f;
        [SerializeField] private float flapCooldown = 0.15f;
        [SerializeField] private float maxVelocity = 10f;

        private Rigidbody rb;
        private float lastFlapTime;
        private bool isDead = false;
        private Camera mainCamera;

        public bool IsDead => isDead;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (isDead) return;

            HandleMovement();
            HandleFlap();
            CheckScreenBounds();
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            
            // 慣性を残しつつ制御
            Vector3 force = new Vector3(horizontal * moveSpeed * 2f, 0, 0);
            rb.AddForce(force);

            // 速度制限（水平のみ）
            Vector3 velocity = rb.linearVelocity;
            if (Mathf.Abs(velocity.x) > moveSpeed)
            {
                velocity.x = Mathf.Sign(velocity.x) * moveSpeed;
                rb.linearVelocity = velocity;
            }

            // 向き
            if (horizontal != 0)
            {
                // スプライト反転の代わりに回転で表現
                // transform.rotation = Quaternion.Euler(0, horizontal > 0 ? 0 : 180, 0);
                // 3Dモデルなので回転させると風船の位置関係が変わるため、今回は回転させない（あるいはモデル構造を変える必要がある）
                // 簡易的に現状維持
            }
        }

        private void HandleFlap()
        {
            if (Input.GetKeyDown(KeyCode.Space) && Time.time - lastFlapTime > flapCooldown)
            {
                // Y軸の速度をリセットしてから力を加える（操作感向上のため）
                Vector3 vel = rb.linearVelocity;
                vel.y = Mathf.Max(vel.y, 0); 
                rb.linearVelocity = vel;

                rb.AddForce(Vector3.up * flapForce, ForceMode.Impulse);
                lastFlapTime = Time.time;
            }
        }

        private void CheckScreenBounds()
        {
            if (mainCamera == null) return;

            Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);

            // 画面左端から出たら死亡
            if (viewportPos.x < 0)
            {
                Die();
            }
            // 画面下端から出たら死亡（海に落ちた）
            if (viewportPos.y < 0)
            {
                Die();
            }

            // 画面右端・上端は壁扱い（Clamp）
            if (viewportPos.x > 1)
            {
                Vector3 pos = transform.position;
                pos.x = mainCamera.ViewportToWorldPoint(new Vector3(1, viewportPos.y, viewportPos.z)).x;
                transform.position = pos;
                
                Vector3 vel = rb.linearVelocity;
                vel.x = Mathf.Min(vel.x, 0);
                rb.linearVelocity = vel;
            }
            if (viewportPos.y > 1)
            {
                Vector3 pos = transform.position;
                pos.y = mainCamera.ViewportToWorldPoint(new Vector3(viewportPos.x, 1, viewportPos.z)).y;
                transform.position = pos;
                
                Vector3 vel = rb.linearVelocity;
                vel.y = Mathf.Min(vel.y, 0);
                rb.linearVelocity = vel;
            }
        }

        public void Die()
        {
            if (isDead) return;
            isDead = true;
            rb.constraints = RigidbodyConstraints.None; // 回転も許可
            rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
            
            GameManager.Instance.OnPlayerDied();
        }

        public void OnTouchSea()
        {
            if (isDead) return;
            
            // まだ死なずに、動きを止めてサメを待つ
            // 溺れる演出（水面で停止）
            isDead = true; // 操作不能にする
            rb.linearVelocity = Vector3.zero;
            rb.useGravity = false; // 沈まないようにする
            
            // サメを呼ぶ
            GameManager.Instance.SpawnShark(transform.position);
        }

        public void EatenByShark()
        {
            // 食べられたので消える
            gameObject.SetActive(false);
            GameManager.Instance.OnPlayerDied();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isDead) return;

            if (other.CompareTag("Spark"))
            {
                Die();
            }
            else if (other.CompareTag("Sea"))
            {
                OnTouchSea();
            }
            else if (other.CompareTag("Collectible"))
            {
                var collectible = other.GetComponent<Collectible>();
                if (collectible != null)
                {
                    collectible.Collect();
                }
            }
        }
    }
}

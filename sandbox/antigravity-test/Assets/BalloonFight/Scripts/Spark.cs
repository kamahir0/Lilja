using UnityEngine;

namespace BalloonFight
{
    public class Spark : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float moveRange = 3f;
        
        private Vector3 startPos;
        private bool isMoving = false;

        private void Start()
        {
            startPos = transform.position;
            // 50%の確率で動くスパークにする
            isMoving = Random.value > 0.5f;
            
            // ランダムに少し位置をずらす（点滅表現の代わり）
        }

        private void Update()
        {
            if (isMoving)
            {
                float offset = Mathf.PingPong(Time.time * moveSpeed, moveRange * 2) - moveRange;
                transform.position = startPos + new Vector3(offset, 0, 0);
            }

            // カメラの後ろに行ったら消える
            if (transform.position.x < Camera.main.transform.position.x - 15f)
            {
                Destroy(gameObject);
            }
        }
    }
}

using UnityEngine;

namespace BalloonFight
{
    public class Collectible : MonoBehaviour
    {
        [SerializeField] private int scoreValue = 100;

        public void Collect()
        {
            GameManager.Instance.AddScore(scoreValue);
            Destroy(gameObject);
        }

        private void Update()
        {
            // カメラの後ろに行ったら消える
            if (transform.position.x < Camera.main.transform.position.x - 15f)
            {
                Destroy(gameObject);
            }
        }
    }
}

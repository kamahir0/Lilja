using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using ScreenManagementSample.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// バトル画面のView（MVP - 表示のみ担当）
    /// 複数敵表示に対応（動的生成）
    /// DOTweenによるリッチなアニメーション
    /// </summary>
    public class BattleView : MonoBehaviour
    {
        [Header("コマンドボタン")] [SerializeField] private Button _attackButton;
        [SerializeField] private Button _skillButton;
        [SerializeField] private Button _itemButton;
        [SerializeField] private Button _defendButton;

        [Header("ステータス表示")] [SerializeField] private Text _playerStatusText;
        [SerializeField] private Text _enemyStatusText;
        [SerializeField] private Text _messageText;

        [Header("3D")] [SerializeField] private Transform _playerBattleModel;
        [SerializeField] private Transform _enemyContainer;
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private float _attackMoveDistance = 1.5f;
        [SerializeField] private float _animationDuration = 0.2f;
        [SerializeField] private float _shakeIntensity = 0.3f;
        [SerializeField] private float _shakeDuration = 0.3f;

        [Header("撃破演出")] [SerializeField] private float _defeatDuration = 0.5f;
        [SerializeField] private float _defeatRotation = 360f;

        private Vector3 _playerOriginalPosition;
        private readonly List<GameObject> _enemyObjects = new List<GameObject>();
        private readonly List<Vector3> _enemyOriginalPositions = new List<Vector3>();

        // 敵配置位置（最大3体まで対応）
        private static readonly Vector3[] EnemySpawnPositions =
        {
            new Vector3(2, 0.6f, 0),
            new Vector3(3, 0.6f, 1.2f),
            new Vector3(3, 0.6f, -1.2f)
        };

        /// <summary> たたかうボタン </summary>
        public Button AttackButton => _attackButton;

        /// <summary> スキルボタン </summary>
        public Button SkillButton => _skillButton;

        /// <summary> アイテムボタン </summary>
        public Button ItemButton => _itemButton;

        /// <summary> 防御ボタン </summary>
        public Button DefendButton => _defendButton;

        private void Awake()
        {
            if (_playerBattleModel != null)
            {
                _playerOriginalPosition = _playerBattleModel.position;
            }

            // ボタンのクリックフィードバックを設定
            SetupButtonFeedback(_attackButton);
            SetupButtonFeedback(_skillButton);
            SetupButtonFeedback(_itemButton);
            SetupButtonFeedback(_defendButton);
        }

        /// <summary>
        /// ボタンにクリック時のバウンスフィードバックを設定
        /// </summary>
        private void SetupButtonFeedback(Button button)
        {
            if (button == null) return;

            button.onClick.AddListener(() =>
            {
                button.transform
                    .DOPunchScale(Vector3.one * 0.1f, 0.15f, 8, 0.5f)
                    .SetLink(button.gameObject);
            });
        }

        #region 敵オブジェクト管理

        /// <summary>
        /// 敵オブジェクトを敵の数だけ生成
        /// </summary>
        public void InitializeEnemyObjects(int enemyCount)
        {
            // 管理リストのクリア
            ClearEnemyObjects();

            if (_enemyPrefab == null || _enemyContainer == null) return;

            for (int i = 0; i < enemyCount && i < EnemySpawnPositions.Length; i++)
            {
                var position = EnemySpawnPositions[i];
                var enemyGo = Instantiate(_enemyPrefab, position, Quaternion.identity, _enemyContainer);

                // スケールイン演出
                enemyGo.transform.localScale = Vector3.zero;
                enemyGo.SetActive(true);
                enemyGo.transform
                    .DOScale(Vector3.one, 0.3f)
                    .SetEase(Ease.OutBack)
                    .SetDelay(i * 0.1f)
                    .SetLink(enemyGo);

                // 名前を識別しやすくする
                enemyGo.name = $"EnemyObject_{i}";
                _enemyObjects.Add(enemyGo);
                _enemyOriginalPositions.Add(position);
            }
        }

        /// <summary>
        /// 敵オブジェクトをクリア
        /// </summary>
        public void ClearEnemyObjects()
        {
            foreach (var obj in _enemyObjects)
            {
                if (obj != null)
                {
                    DOTween.Kill(obj.transform);
                    Destroy(obj);
                }
            }

            _enemyObjects.Clear();
            _enemyOriginalPositions.Clear();
        }

        /// <summary>
        /// 指定インデックスの敵オブジェクトを非表示にする（撃破演出付き）
        /// </summary>
        public async UniTask PlayDefeatAnimationAsync(int index, CancellationToken cancellationToken)
        {
            if (index < 0 || index >= _enemyObjects.Count || _enemyObjects[index] == null)
            {
                return;
            }

            var enemyObj = _enemyObjects[index];

            // 撃破演出：スケール縮小 + 回転 + 上昇
            var sequence = DOTween.Sequence()
                .Join(enemyObj.transform.DOScale(Vector3.zero, _defeatDuration).SetEase(Ease.InBack))
                .Join(enemyObj.transform.DORotate(new Vector3(0, _defeatRotation, 0), _defeatDuration, RotateMode.FastBeyond360))
                .Join(enemyObj.transform.DOMoveY(enemyObj.transform.position.y + 1f, _defeatDuration).SetEase(Ease.OutQuad))
                .SetLink(enemyObj)
                .OnComplete(() => enemyObj.SetActive(false));

            await sequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }

        /// <summary>
        /// 指定インデックスの敵オブジェクトを非表示にする
        /// </summary>
        public void HideEnemyObject(int index)
        {
            if (index >= 0 && index < _enemyObjects.Count && _enemyObjects[index] != null)
            {
                _enemyObjects[index].SetActive(false);
            }
        }

        /// <summary>
        /// 敵オブジェクトの表示状態を更新（IsAliveに基づく）
        /// </summary>
        public void UpdateEnemyObjectsVisibility(Enemy[] enemies)
        {
            for (int i = 0; i < enemies.Length && i < _enemyObjects.Count; i++)
            {
                if (_enemyObjects[i] != null)
                {
                    _enemyObjects[i].SetActive(enemies[i].IsAlive);
                }
            }
        }

        #endregion

        #region UI表示（即座に反映）

        /// <summary>
        /// プレイヤーステータスを設定
        /// </summary>
        public void SetPlayerStatus(string name, int currentHp, int maxHp)
        {
            if (_playerStatusText != null)
            {
                _playerStatusText.text = $"{name}\nHP: {currentHp}/{maxHp}";
            }
        }

        /// <summary>
        /// 複数敵のステータスを設定
        /// </summary>
        public void SetEnemiesStatus(Enemy[] enemies)
        {
            if (_enemyStatusText == null) return;

            var sb = new StringBuilder();
            foreach (var enemy in enemies)
            {
                var status = enemy.IsAlive ? $"HP:{enemy.CurrentHp}/{enemy.MaxHp}" : "倒れた";
                sb.AppendLine($"{enemy.Name} ({status})");
            }

            _enemyStatusText.text = sb.ToString().TrimEnd();
        }

        /// <summary>
        /// メッセージを設定（フェードイン付き）
        /// </summary>
        public void SetMessage(string message)
        {
            if (_messageText != null)
            {
                _messageText.text = message;

                // テキストのパンチスケール
                _messageText.transform
                    .DOPunchScale(Vector3.one * 0.05f, 0.2f, 6, 0.5f)
                    .SetLink(_messageText.gameObject);
            }
        }

        /// <summary>
        /// 全コマンドボタンの有効/無効を設定
        /// </summary>
        public void SetCommandButtonsInteractable(bool interactable)
        {
            if (_attackButton != null) _attackButton.interactable = interactable;
            if (_skillButton != null) _skillButton.interactable = interactable;
            if (_itemButton != null) _itemButton.interactable = interactable;
            if (_defendButton != null) _defendButton.interactable = interactable;
        }

        /// <summary>
        /// アイテムボタンの有効/無効を設定（アイテムがない場合など）
        /// </summary>
        public void SetItemButtonInteractable(bool interactable)
        {
            if (_itemButton != null)
            {
                _itemButton.interactable = interactable;
            }
        }

        #endregion

        #region アニメーション

        /// <summary>
        /// プレイヤーの攻撃アニメーション再生（DOTween版）
        /// </summary>
        public async UniTask PlayPlayerAttackAnimationAsync(CancellationToken cancellationToken)
        {
            if (_playerBattleModel == null) return;

            var targetPosition = _playerOriginalPosition + Vector3.right * _attackMoveDistance;

            // 前進（OutBackで勢いよく）
            await _playerBattleModel
                .DOMove(targetPosition, _animationDuration)
                .SetEase(Ease.OutBack)
                .SetLink(_playerBattleModel.gameObject)
                .AsyncWaitForCompletion()
                .AsUniTask()
                .AttachExternalCancellation(cancellationToken);

            // 戻る（InBackで引き戻し感）
            await _playerBattleModel
                .DOMove(_playerOriginalPosition, _animationDuration)
                .SetEase(Ease.InBack)
                .SetLink(_playerBattleModel.gameObject)
                .AsyncWaitForCompletion()
                .AsUniTask()
                .AttachExternalCancellation(cancellationToken);
        }

        /// <summary>
        /// 敵の攻撃アニメーション再生（インデックス指定、DOTween版）
        /// </summary>
        public async UniTask PlayEnemyAttackAnimationAsync(int enemyIndex, CancellationToken cancellationToken)
        {
            if (enemyIndex < 0 || enemyIndex >= _enemyObjects.Count) return;

            var enemyObj = _enemyObjects[enemyIndex];
            var originalPosition = _enemyOriginalPositions[enemyIndex];

            if (enemyObj == null || !enemyObj.activeSelf) return;

            var targetPosition = originalPosition + Vector3.left * _attackMoveDistance;

            // 前進（OutBackで勢いよく）
            await enemyObj.transform
                .DOMove(targetPosition, _animationDuration)
                .SetEase(Ease.OutBack)
                .SetLink(enemyObj)
                .AsyncWaitForCompletion()
                .AsUniTask()
                .AttachExternalCancellation(cancellationToken);

            // 戻る（InBackで引き戻し感）
            await enemyObj.transform
                .DOMove(originalPosition, _animationDuration)
                .SetEase(Ease.InBack)
                .SetLink(enemyObj)
                .AsyncWaitForCompletion()
                .AsUniTask()
                .AttachExternalCancellation(cancellationToken);
        }

        /// <summary>
        /// ダメージ時の揺れアニメーション（DOTween版）
        /// </summary>
        public async UniTask PlayDamageAnimationAsync(bool isPlayer, int targetIndex, CancellationToken cancellationToken)
        {
            Transform target;
            Vector3 originalPosition;

            if (isPlayer)
            {
                target = _playerBattleModel;
                originalPosition = _playerOriginalPosition;
            }
            else
            {
                if (targetIndex < 0 || targetIndex >= _enemyObjects.Count) return;
                var enemyObj = _enemyObjects[targetIndex];
                if (enemyObj == null || !enemyObj.activeSelf) return;
                target = enemyObj.transform;
                originalPosition = _enemyOriginalPositions[targetIndex];
            }

            if (target == null) return;

            // DOShakePositionで自然な揺れを表現
            await target
                .DOShakePosition(_shakeDuration, _shakeIntensity, 15, 90f, false, true, ShakeRandomnessMode.Harmonic)
                .SetLink(target.gameObject)
                .AsyncWaitForCompletion()
                .AsUniTask()
                .AttachExternalCancellation(cancellationToken);

            // 元の位置に戻す（シェイク後のズレ補正）
            target.position = originalPosition;
        }

        /// <summary>
        /// HPが減少した時のUI揺れ演出
        /// </summary>
        public void PlayHpDamageEffect()
        {
            if (_playerStatusText != null)
            {
                _playerStatusText.transform
                    .DOShakePosition(0.2f, 5f, 20, 90f, false, true)
                    .SetLink(_playerStatusText.gameObject);
            }
        }

        #endregion

        private void OnDestroy()
        {
            // すべてのTweenをクリーンアップ
            DOTween.Kill(transform);
        }
    }
}




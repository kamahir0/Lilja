using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations; // これが必要

public class AddressableTester : MonoBehaviour
{
    [SerializeField] private string _address = "lilja_1";

    private SpriteRenderer _spriteRenderer;

    private AsyncOperationHandle<Sprite> _handle;

    private void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // 1. ロード開始
        _handle = Addressables.LoadAssetAsync<Sprite>(_address);

        // 2. 完了時のコールバックを登録
        _handle.Completed += handle =>
        {
            // 3. 結果を適用
            _spriteRenderer.sprite = handle.Result;
            Debug.Log("スプライトをロードしました");

            StartCoroutine(UnloadSprite());
        };
    }

    private IEnumerator UnloadSprite()
    {
        yield return new WaitForSeconds(2);
        Addressables.Release(_handle);
        Debug.Log("スプライトをアンロードしました");
    }
}
using System.Collections;
using UnityEngine;

public class ResourcesTester : MonoBehaviour
{
    [SerializeField] private string _path = "lilja_1";

    private SpriteRenderer _spriteRenderer;

    private void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        StartCoroutine(Do());
    }

    private IEnumerator Do()
    {
        var sprite = Resources.Load<Sprite>(_path);
        _spriteRenderer.sprite = sprite;
        Debug.Log("スプライトをロードしました");

        yield return new WaitForSeconds(1);
        _spriteRenderer.sprite = null;
        Resources.UnloadAsset(sprite);
        Debug.Log("スプライトをアンロードしました");
    }
}

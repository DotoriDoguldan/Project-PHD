using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 씬 전환 암전판. "참조는 인스펙터 연결" 규칙의 예외 — 씬을 넘어 살아남아야 해서
/// SoundManager 처럼 처음 쓸 때 스스로 만들어진다(DontDestroyOnLoad).
/// 전환 중에는 화면 전체를 덮어 입력도 함께 막는다.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    // 배경(BG_Gradient_Blue) 계열의 짙은 남색 — 검정보다 아트와 이어져 보인다.
    private static readonly Color FadeColor = new Color(0.02f, 0.04f, 0.10f);
    private const float FadeOutSeconds = 0.16f;
    private const float FadeInSeconds = 0.24f;

    private static ScreenFader _instance;

    private CanvasGroup _cover;
    private bool _busy;

    public static void FadeToScene(string sceneName)
    {
        if (_instance == null) _instance = Create();
        if (_instance._busy) return; // 이미 넘어가는 중 — 연타다.

        _instance.StartCoroutine(_instance.FadeRoutine(sceneName));
    }

    private static ScreenFader Create()
    {
        var root = new GameObject("ScreenFader");
        DontDestroyOnLoad(root);

        // 씬 안의 어떤 UILayer(최대 Overlay 300)보다도 위에 온다.
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        root.AddComponent<GraphicRaycaster>();

        var coverGo = new GameObject("Img_Cover");
        coverGo.transform.SetParent(root.transform, false);

        var cover = coverGo.AddComponent<Image>();
        cover.color = FadeColor;
        cover.raycastTarget = true;

        var rect = cover.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var fader = root.AddComponent<ScreenFader>();
        // 알파는 색이 아니라 CanvasGroup 으로 만진다 — UITween.Fade 를 그대로 쓰고,
        // 색 갱신과 달리 이미지 지오메트리를 다시 만들지 않는다.
        fader._cover = coverGo.AddComponent<CanvasGroup>();
        coverGo.SetActive(false);
        return fader;
    }

    private IEnumerator FadeRoutine(string sceneName)
    {
        _busy = true;
        _cover.alpha = 0f;
        _cover.gameObject.SetActive(true);

        yield return UITween.Fade(_cover, 1f, FadeOutSeconds);

        SceneManager.LoadScene(sceneName);
        // 로드 직후 첫 프레임은 새 씬의 Awake/Start 가 몰려 길다 — 덮은 채로 넘긴다.
        yield return null;

        yield return UITween.Fade(_cover, 0f, FadeInSeconds);

        _cover.gameObject.SetActive(false);
        _busy = false;
    }
}

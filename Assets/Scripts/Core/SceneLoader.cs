using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    [Header("Loading Screen")]
    [SerializeField] private Canvas loadingCanvas;
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private TMP_Text loadingText;

    private Image loadingBackground;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureLoadingBackground();

        if (loadingCanvas != null)
            loadingCanvas.enabled = false;
    }

    private void EnsureLoadingBackground()
    {
        if (loadingCanvas == null) return;

        var bgT = loadingCanvas.transform.Find("LoadingBg");
        if (bgT != null)
        {
            loadingBackground = bgT.GetComponent<Image>();
            bgT.SetAsFirstSibling();
            return;
        }

        var bgGo = new GameObject("LoadingBg", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(loadingCanvas.transform, false);
        bgGo.transform.SetAsFirstSibling();

        var rt = bgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        loadingBackground = bgGo.GetComponent<Image>();
        loadingBackground.color = Color.black;
        loadingBackground.raycastTarget = true;
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void LoadSceneAsync(string sceneName, Action onComplete = null)
    {
        StartCoroutine(LoadSceneAsyncRoutine(sceneName, onComplete));
    }

    private System.Collections.IEnumerator LoadSceneAsyncRoutine(
        string sceneName, Action onComplete)
    {
        if (loadingCanvas != null)
            loadingCanvas.enabled = true;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError(
                $"[SceneLoader] async load failed: {sceneName}");
            if (loadingCanvas != null)
                loadingCanvas.enabled = false;
            yield break;
        }

        while (!op.isDone)
        {
            UpdateLoadingUI(op.progress);
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);

        if (loadingCanvas != null)
            loadingCanvas.enabled = false;
        onComplete?.Invoke();
    }

    private void UpdateLoadingUI(float pct)
    {
        if (loadingSlider != null) loadingSlider.value = pct;
        if (loadingText != null)
            loadingText.text = $"Loading... {Mathf.RoundToInt(pct * 100)}%";
    }
}

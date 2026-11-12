using UnityEngine;
using UnityEngine.UI;

/// <summary>Six reusable, non-interactive UI bursts. No world objects, colliders or raycast surfaces.</summary>
public sealed class GameFeedbackBurst : MonoBehaviour
{
    const int Capacity = 6, Stars = 6;
    sealed class Burst
    {
        public RectTransform root;
        public GameFeedbackStar[] stars;
        public float began = -10;
        public Color color;
    }
    readonly Burst[] pool = new Burst[Capacity];
    RectTransform canvasRect;
    int next;
    float nextAllowed;
    public int ActiveCount { get { int n = 0; foreach (var b in pool) if (b != null && b.root.gameObject.activeSelf) n++; return n; } }

    void Awake()
    {
        var canvasGo = new GameObject("Feedback Decorations", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 28000;
        LandscapeUI.ConfigureLandscapeScaler(canvasGo.GetComponent<CanvasScaler>());
        canvasRect = canvasGo.GetComponent<RectTransform>();
        for (int i = 0; i < pool.Length; i++)
        {
            var root = new GameObject("Success " + i, typeof(RectTransform)).GetComponent<RectTransform>(); root.SetParent(canvasRect, false);
            root.sizeDelta = Vector2.zero;
            var b = new Burst { root = root, stars = new GameFeedbackStar[Stars] }; pool[i] = b;
            for (int j = 0; j < Stars; j++)
            {
                var star = new GameObject("Spark " + j, typeof(RectTransform)).AddComponent<GameFeedbackStar>();
                star.transform.SetParent(root, false); star.raycastTarget = false; star.rectTransform.sizeDelta = new Vector2(j == 0 ? 18 : 9, j == 0 ? 18 : 9);
                b.stars[j] = star;
            }
            root.gameObject.SetActive(false);
        }
    }
    public void Play(Vector3? worldPoint, Color color)
    {
        if (GameFeedback.ReducedMotion || Time.unscaledTime < nextAllowed) return;
        Vector3 screen = new Vector3(Screen.width * .5f, Screen.height * .68f, 1);
        if (worldPoint.HasValue)
        {
            var camera = Camera.main; if (!camera) return;
            screen = camera.WorldToScreenPoint(worldPoint.Value + Vector3.up * 1.8f);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) return;
        }
        nextAllowed = Time.unscaledTime + .35f;
        screen.x = Mathf.Clamp(screen.x, 48, Mathf.Max(48, Screen.width - 48));
        screen.y = Mathf.Clamp(screen.y, 48, Mathf.Max(48, Screen.height - 48));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
        var burst = pool[next++ % Capacity]; burst.began = Time.unscaledTime; burst.color = color;
        burst.root.anchoredPosition = local; burst.root.gameObject.SetActive(true);
    }
    void Update()
    {
        if (GameFeedback.ReducedMotion) { Clear(); return; }
        foreach (var b in pool)
        {
            if (!b.root.gameObject.activeSelf) continue;
            float t = (Time.unscaledTime - b.began) / .62f;
            if (t >= 1) { b.root.gameObject.SetActive(false); continue; }
            for (int i = 0; i < b.stars.Length; i++)
            {
                float angle = (i * 137.5f + 25) * Mathf.Deg2Rad;
                var star = b.stars[i];
                star.rectTransform.anchoredPosition = i == 0 ? new Vector2(0, 14 * t) : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (10 + t * 34) + Vector2.up * 15 * t;
                star.rectTransform.localScale = Vector3.one * Mathf.Lerp(.65f, 1.15f, Mathf.Sin(t * Mathf.PI));
                star.color = new Color(b.color.r, b.color.g, b.color.b, Mathf.Min(1, t * 10) * (1 - t));
            }
        }
    }
    public void Clear() { foreach (var b in pool) if (b != null && b.root) b.root.gameObject.SetActive(false); }
}

public sealed class GameFeedbackStar : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var rect = rectTransform.rect; Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * .5f;
        vh.AddVert(center, color, Vector2.zero);
        for (int i = 0; i < 10; i++)
        {
            float angle = (90 + i * 36) * Mathf.Deg2Rad, r = radius * (i % 2 == 0 ? 1 : .4f);
            vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r, color, Vector2.zero);
        }
        for (int i = 0; i < 10; i++) vh.AddTriangle(0, i + 1, (i + 1) % 10 + 1);
    }
}

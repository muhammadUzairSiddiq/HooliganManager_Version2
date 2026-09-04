using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dynamically creates and manages off-screen indicators for player team members.
/// Indicators display the agent's portrait, an arrow pointing towards them, and their distance.
/// </summary>
public class OffscreenIndicatorManager : MonoBehaviour
{
    private class IndicatorUI
    {
        public GameObject rootGO;
        public RectTransform rectTransform;
        public Image portraitImage;
        public Image borderImage;
        public RectTransform pointerTransform;
        public TextMeshProUGUI distanceText;
        public CanvasGroup canvasGroup;
        public float currentAlpha;
    }

    [Header("Settings")]
    public float margin = 70f;              // Distance from screen edges to clamp indicator
    public float pointerOffset = 45f;       // Distance of the arrow pointer from the circle center
    public Vector2 indicatorSize = new Vector2(64f, 64f); // Size of the circular portrait badge
    public Color indicatorColor = new Color(0.20f, 0.60f, 1.00f, 1f); // Accent blue color for players
    public float fadeSpeed = 8f;            // Speed of fade in/out transitions

    private Camera _cam;
    private RectTransform _container;
    private Dictionary<AgentController, IndicatorUI> _indicators = new Dictionary<AgentController, IndicatorUI>();
    private Sprite _circleSprite;

    void Start()
    {
        _cam = Camera.main;

        // Create container on the Canvas
        GameObject containerGO = new GameObject("OffscreenIndicatorsContainer", typeof(RectTransform));
        containerGO.transform.SetParent(transform, false);
        _container = containerGO.GetComponent<RectTransform>();
        _container.anchorMin = Vector2.zero;
        _container.anchorMax = Vector2.one;
        _container.sizeDelta = Vector2.zero;
        _container.anchoredPosition = Vector2.zero;

        // Generate circular texture at runtime to avoid asset dependency
        _circleSprite = CreateCircleSprite(128);
    }

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];
        float center = size * 0.5f;
        float radiusSq = (size * 0.5f) * (size * 0.5f);
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dy = y - center + 0.5f;
                float dx = x - center + 0.5f;
                float distSq = dx * dx + dy * dy;
                
                if (distSq <= radiusSq)
                {
                    // Smooth anti-aliasing on the edges
                    float dist = Mathf.Sqrt(distSq);
                    float halfSize = size * 0.5f;
                    float alpha = Mathf.Clamp01(halfSize - dist + 0.5f);
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    colors[y * size + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void Update()
    {
        if (BattleManager.instance == null || _cam == null) return;

        var playerAgents = BattleManager.instance.PlayerAgents;

        // 1. Clean up stale indicators
        List<AgentController> toRemove = new List<AgentController>();
        foreach (var pair in _indicators)
        {
            if (pair.Key == null || !pair.Key.IsAlive || !playerAgents.Contains(pair.Key))
            {
                if (pair.Value.rootGO != null)
                {
                    Destroy(pair.Value.rootGO);
                }
                toRemove.Add(pair.Key);
            }
        }
        foreach (var key in toRemove)
        {
            _indicators.Remove(key);
        }

        // 2. Add new indicators
        foreach (var agent in playerAgents)
        {
            if (agent != null && agent.IsAlive && !_indicators.ContainsKey(agent))
            {
                CreateIndicator(agent);
            }
        }

        // 3. Update active indicators
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        foreach (var pair in _indicators)
        {
            AgentController agent = pair.Key;
            IndicatorUI ui = pair.Value;

            Vector3 worldPos = agent.transform.position;
            Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);

            bool isOffscreen = screenPos.z < 0 || 
                               screenPos.x < margin || 
                               screenPos.x > Screen.width - margin || 
                               screenPos.y < margin || 
                               screenPos.y > Screen.height - margin;

            float targetAlpha = isOffscreen ? 1f : 0f;
            ui.currentAlpha = Mathf.MoveTowards(ui.currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
            if (ui.canvasGroup != null)
            {
                ui.canvasGroup.alpha = ui.currentAlpha;
                ui.rootGO.SetActive(ui.currentAlpha > 0.01f);
            }

            if (ui.currentAlpha <= 0.01f) continue;

            // Compute direction from screen center to the agent's projected screen position
            Vector2 dir = (Vector2)screenPos - screenCenter;

            // If behind the camera, invert direction to point towards them
            if (screenPos.z < 0)
            {
                dir = -dir;
            }

            dir.Normalize();

            // Find intersection with the screen bounds
            float minX = margin;
            float maxX = Screen.width - margin;
            float minY = margin;
            float maxY = Screen.height - margin;

            float tX = dir.x > 0 ? (maxX - screenCenter.x) / dir.x : (minX - screenCenter.x) / dir.x;
            float tY = dir.y > 0 ? (maxY - screenCenter.y) / dir.y : (minY - screenCenter.y) / dir.y;
            float t = Mathf.Min(tX, tY);

            Vector2 clampedScreenPos = screenCenter + dir * t;

            // Update UI position (adjusting for Canvas coordinate system)
            ui.rectTransform.position = clampedScreenPos;

            // Rotate pointer arrow
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            ui.pointerTransform.localRotation = Quaternion.Euler(0, 0, angle - 90f);
            ui.pointerTransform.anchoredPosition = dir * pointerOffset;

            // Calculate horizontal ground distance
            Vector3 camProj = _cam.transform.position;
            camProj.y = worldPos.y;
            float distance = Vector3.Distance(worldPos, camProj);
            ui.distanceText.text = $"{distance:F0}m";
        }
    }

    private void CreateIndicator(AgentController agent)
    {
        // 1. Root Indicator object
        GameObject root = new GameObject("Indicator_" + agent.name, typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(_container, false);
        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = indicatorSize;

        // 2. Circular Dark Background
        GameObject bgGO = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(root.transform, false);
        Image bgImg = bgGO.GetComponent<Image>();
        bgImg.sprite = _circleSprite;
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 3. Colored Outer Border Ring
        GameObject borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderGO.transform.SetParent(root.transform, false);
        Image borderImg = borderGO.GetComponent<Image>();
        borderImg.sprite = _circleSprite;
        borderImg.color = indicatorColor;
        borderImg.type = Image.Type.Simple;
        RectTransform borderRect = borderGO.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = new Vector2(4f, 4f); // Slightly larger for border look

        // 4. Portrait Image with Mask to keep it circular
        GameObject maskGO = new GameObject("Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
        maskGO.transform.SetParent(root.transform, false);
        Image maskImg = maskGO.GetComponent<Image>();
        maskImg.sprite = _circleSprite;
        Mask mask = maskGO.GetComponent<Mask>();
        mask.showMaskGraphic = false;
        RectTransform maskRect = maskGO.GetComponent<RectTransform>();
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.sizeDelta = new Vector2(-4f, -4f); // Slightly smaller to show border

        GameObject portraitGO = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitGO.transform.SetParent(maskGO.transform, false);
        Image portraitImg = portraitGO.GetComponent<Image>();
        
        // Fetch original portrait sprite
        if (BattleManager.instance != null && BattleManager.instance.portraitRegistry != null && agent.modelPrefab != null)
        {
            portraitImg.sprite = BattleManager.instance.portraitRegistry.GetPortrait(agent.modelPrefab);
        }
        else
        {
            portraitImg.color = indicatorColor; // Fallback to flat color
        }

        RectTransform portraitRect = portraitGO.GetComponent<RectTransform>();
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        portraitRect.sizeDelta = Vector2.zero;

        // 5. Direction Pointer Arrow (Chevron)
        GameObject pointerGO = new GameObject("Pointer", typeof(RectTransform), typeof(TextMeshProUGUI));
        pointerGO.transform.SetParent(root.transform, false);
        TextMeshProUGUI pointerText = pointerGO.GetComponent<TextMeshProUGUI>();
        pointerText.text = "▲";
        pointerText.fontSize = 20f;
        pointerText.color = indicatorColor;
        pointerText.alignment = TextAlignmentOptions.Center;
        
        RectTransform pointerRect = pointerGO.GetComponent<RectTransform>();
        pointerRect.sizeDelta = new Vector2(24f, 24f);
        pointerRect.anchorMin = new Vector2(0.5f, 0.5f);
        pointerRect.anchorMax = new Vector2(0.5f, 0.5f);

        // 6. Distance Text (placed below or on the badge)
        GameObject distGO = new GameObject("DistanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
        distGO.transform.SetParent(root.transform, false);
        TextMeshProUGUI distText = distGO.GetComponent<TextMeshProUGUI>();
        distText.text = "0m";
        distText.fontSize = 13f;
        distText.fontStyle = FontStyles.Bold;
        distText.color = Color.white;
        distText.alignment = TextAlignmentOptions.Center;
        // Add a nice dark outline to text for readability on any background
        distText.outlineColor = Color.black;
        distText.outlineWidth = 0.2f;

        RectTransform distRect = distGO.GetComponent<RectTransform>();
        distRect.sizeDelta = new Vector2(100f, 20f);
        distRect.anchorMin = new Vector2(0.5f, 0f);
        distRect.anchorMax = new Vector2(0.5f, 0f);
        distRect.anchoredPosition = new Vector2(0f, -14f); // Position below the circle

        // Add to dictionary
        IndicatorUI ui = new IndicatorUI
        {
            rootGO = root,
            rectTransform = rect,
            portraitImage = portraitImg,
            borderImage = borderImg,
            pointerTransform = pointerRect,
            distanceText = distText,
            canvasGroup = canvasGroup,
            currentAlpha = 0f
        };

        _indicators.Add(agent, ui);
    }
}

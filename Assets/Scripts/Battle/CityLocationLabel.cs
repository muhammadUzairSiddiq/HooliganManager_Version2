using UnityEngine;
using TMPro;

public sealed class CityLocationLabel : MonoBehaviour
{
    Transform label;
    RectTransform pin, frame;
    TextMeshProUGUI pinLabel;
    public int LocationIndex;

    void Start()
    {
        label = transform.Find("ZoneLabel");
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (!cam) return;
        if (!pin)
        {
            frame = WorldButtonLayer.Frame();
            if (!frame) return;
            var button = LandscapeUI.Button("LocationPin" + LocationIndex, frame, name, 0, 0, 210, 48);
            button.onClick.AddListener(() => CityGameplay.Instance?.OpenLocation(LocationIndex));
            pin = button.transform as RectTransform;
            CityMissionHUD.Style(pin);
            pinLabel = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (pinLabel)
            {
                pinLabel.color = Color.white;
                pinLabel.fontSize = 22f;
                pinLabel.fontSizeMin = 16f;
                pinLabel.fontSizeMax = 24f;
                pinLabel.overflowMode = TextOverflowModes.Overflow;
            }
            if (label) label.gameObject.SetActive(false);
        }
        Vector3 projected = cam.WorldToScreenPoint(transform.position + Vector3.up * 4);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, projected, null, out var local);
        float x = local.x - frame.rect.xMin, y = frame.rect.yMax - local.y;
        var pinRect = new Rect(x - 105f, y - 24f, 210f, 48f);
        bool visible = projected.z > 0.1f && x > 250f && x < frame.rect.width - 190f && y > 120f && y < frame.rect.height - 70f
            && !WorldButtonLayer.ChoiceCovers(pinRect);
        pin.gameObject.SetActive(visible);
        if (pinLabel) pinLabel.color = Color.white;
        if (visible) LandscapeUI.Place(pin, x - 105, y - 24, 210, 48);
    }

    void OnDestroy() { if (pin) Destroy(pin.gameObject); }
}

/// <summary>World labels sit above the gameplay HUD so a panel underneath cannot swallow the click.
/// Mission and other decision sheets use a higher canvas so they cover those labels.</summary>
public static class WorldButtonLayer
{
    public const int ButtonOrder = 16000;
    public const int ChoiceOrder = 16800;
    public const int MissionOrder = 17500;
    static RectTransform frame;
    static RectTransform choiceFrame;
    static RectTransform missionFrame;
    static readonly System.Collections.Generic.List<Rect> choiceCovers = new System.Collections.Generic.List<Rect>();
    static int choiceFrameStamp = -1;

    public static RectTransform Frame() => frame ? frame : frame = Make("WorldButtonCanvas", ButtonOrder);

    public static RectTransform ChoiceFrame() => choiceFrame ? choiceFrame : choiceFrame = Make("ChoiceButtonCanvas", ChoiceOrder);

    public static RectTransform MissionFrame() => missionFrame ? missionFrame : missionFrame = Make("MissionPanelCanvas", MissionOrder);

    public static void CoverChoice(Rect area)
    {
        if (choiceFrameStamp != Time.frameCount)
        {
            choiceFrameStamp = Time.frameCount;
            choiceCovers.Clear();
        }
        choiceCovers.Add(area);
    }

    public static bool ChoiceCovers(Rect pin)
    {
        if (choiceFrameStamp < Time.frameCount - 1) return false;
        for (int i = 0; i < choiceCovers.Count; i++)
            if (choiceCovers[i].Overlaps(pin)) return true;
        return false;
    }

    static RectTransform Make(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        LandscapeUI.ConfigureLandscapeScaler(go.GetComponent<UnityEngine.UI.CanvasScaler>());
        var safe = LandscapeUI.Rect("Safe", go.transform, 0, 0, 1600, 900);
        LandscapeUI.Stretch(safe);
        var made = LandscapeUI.Rect("Frame", safe, 0, 0, 1600, 900);
        var viewport = safe.gameObject.AddComponent<LandscapeViewport>();
        viewport.frame = made;
        viewport.Fit();
        return made;
    }
}

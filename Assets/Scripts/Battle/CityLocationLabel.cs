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
        bool visible = (!ModernCityHud.Instance||ModernCityHud.InspectWorld)&&projected.z > 0.1f && x > 250f && x < frame.rect.width - 190f && y > 120f && y < frame.rect.height - 70f
            && !AgentSelectionManager.BlocksWorldTap() && !WorldButtonLayer.ChoiceCovers(pinRect) && !WorldButtonLayer.CoversCrew(pinRect);
        pin.gameObject.SetActive(visible);
        if (pinLabel) pinLabel.color = Color.white;
        if (visible) LandscapeUI.Place(pin, x - 105, y - 24, 210, 48);
    }

    void OnDestroy() { if (pin) Destroy(pin.gameObject); }
}

/// <summary>World annotations sit below the core HUD. Deliberate management and
/// decision sheets sit above both, so labels cannot cover essential controls.</summary>
public static class WorldButtonLayer
{
    public const int ButtonOrder = 10500;
    public const int ChoiceOrder = 10800;
    public const int MissionOrder = 17500;
    static RectTransform frame;
    static RectTransform choiceFrame;
    static RectTransform missionFrame;
    static readonly System.Collections.Generic.List<Rect> choiceCovers = new System.Collections.Generic.List<Rect>();
    static int choiceFrameStamp = -1;
    static int crewStamp=-1;
    static readonly System.Collections.Generic.List<Rect> crewRects=new System.Collections.Generic.List<Rect>();

    public static bool CoversCrew(Rect area)
    {
        if(crewStamp!=Time.frameCount)
        {
            crewStamp=Time.frameCount;crewRects.Clear();
            var cam=Camera.main;var f=Frame();var battle=BattleManager.instance;
            if(cam&&f&&battle)foreach(var member in battle.PlayerAgents)
            {
                if(!member||!member.IsAlive)continue;
                var p=cam.WorldToScreenPoint(member.transform.position+Vector3.up*1.6f);
                if(p.z<=0)continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(f,p,null,out var local);
                float x=local.x-f.rect.xMin,y=f.rect.yMax-local.y;
                crewRects.Add(new Rect(x-30,y-36,60,72));
            }
        }
        foreach(var member in crewRects)if(member.Overlaps(area))return true;
        return false;
    }

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

using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ACTION pins on civilians within 20m of the selected crew, matching the
/// recruitment / recovery location pins. Tapping one opens talk / rob / join / kill.
/// </summary>
public sealed class PedestrianActionHud : MonoBehaviour
{
    public const float Range = 20f;
    const int MaxPins = 8;

    RectTransform frame;
    readonly List<Pin> pins = new List<Pin>();
    float nextScan;
    readonly List<SocialNpc> nearby = new List<SocialNpc>();

    sealed class Pin
    {
        public RectTransform rt;
        public TextMeshProUGUI label;
        public SocialNpc npc;
    }

    public static PedestrianActionHud Ensure(RectTransform targetFrame)
    {
        var existing = FindFirstObjectByType<PedestrianActionHud>();
        if (existing)
        {
            if (!existing.frame) existing.Build(targetFrame);
            return existing;
        }
        var go = new GameObject("PedestrianActionHud");
        var hud = go.AddComponent<PedestrianActionHud>();
        hud.Build(targetFrame);
        return hud;
    }

    void Build(RectTransform targetFrame)
    {
        frame = targetFrame;
        if (!frame) return;
        transform.SetParent(frame, false);
        for (int i = 0; i < MaxPins; i++)
        {
            var button = LandscapeUI.Button("PedestrianAction" + i, frame, "ACT", 0, 0, 56, 24);
            var pin = new Pin
            {
                rt = button.transform as RectTransform,
                label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>()
            };
            button.onClick.AddListener(() => Open(pin.npc));
            if (pin.label)
            {
                pin.label.color = Color.white;
                pin.label.fontSize = 13f;
                pin.label.fontSizeMin = 11f;
                pin.label.fontSizeMax = 14f;
                pin.label.overflowMode = TextOverflowModes.Overflow;
            }
            CityMissionHUD.Style(pin.rt);
            pin.rt.gameObject.SetActive(false);
            pins.Add(pin);
        }
    }

    void LateUpdate()
    {
        if (!frame || !Camera.main) return;
        if (Time.timeScale == 0f || GamePopup.AnyOpen)
        {
            for (int i = 0; i < pins.Count; i++)
                if (pins[i].rt && pins[i].rt.gameObject.activeSelf) pins[i].rt.gameObject.SetActive(false);
            return;
        }

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.15f;
            Scan();
        }

        for (int i = 0; i < pins.Count; i++)
        {
            var pin = pins[i];
            if (!pin.npc)
            {
                if (pin.rt.gameObject.activeSelf) pin.rt.gameObject.SetActive(false);
                continue;
            }
            Vector3 projected = Camera.main.WorldToScreenPoint(pin.npc.transform.position + Vector3.up * 3.7f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, projected, null, out var local);
            float x = local.x - frame.rect.xMin, y = frame.rect.yMax - local.y;
            bool visible = !pin.npc.IsEngaged && projected.z > 0.1f && x > 250f && x < frame.rect.width - 190f && y > 80f && y < frame.rect.height - 70f;
            if (pin.rt.gameObject.activeSelf != visible) pin.rt.gameObject.SetActive(visible);
            if (visible) LandscapeUI.Place(pin.rt, x - 28f, y - 30f, 56f, 24f);
        }
    }

    void Scan()
    {
        nearby.Clear();
        var selected = AgentSelectionManager.instance?.SelectedAgents;
        if (selected == null || selected.Count == 0)
        {
            HideAll();
            return;
        }

        var npcs = FindObjectsByType<SocialNpc>(FindObjectsSortMode.None);
        foreach (var npc in npcs)
        {
            if (!npc || npc.JoinedCrew || npc.IsEngaged) continue;
            foreach (var agent in selected)
            {
                if (!agent || !agent.IsAlive) continue;
                Vector3 d = npc.transform.position - agent.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= Range * Range)
                {
                    nearby.Add(npc);
                    break;
                }
            }
            if (nearby.Count >= MaxPins) break;
        }

        for (int i = 0; i < pins.Count; i++)
        {
            pins[i].npc = i < nearby.Count ? nearby[i] : null;
            if (pins[i].npc == null && pins[i].rt.gameObject.activeSelf)
                pins[i].rt.gameObject.SetActive(false);
        }
    }

    void HideAll()
    {
        for (int i = 0; i < pins.Count; i++)
        {
            pins[i].npc = null;
            if (pins[i].rt && pins[i].rt.gameObject.activeSelf)
                pins[i].rt.gameObject.SetActive(false);
        }
    }

    void Open(SocialNpc npc)
    {
        if (!npc) return;
        npc.PauseForConversation(true, AgentSelectionManager.instance?.SelectedAgents.FirstOrDefault(a=>a&&a.IsAlive)?.transform.position ?? npc.transform.position);
        for (int i = 0; i < pins.Count; i++)
            if (pins[i].npc == npc && pins[i].rt) pins[i].rt.gameObject.SetActive(false);
        CityActionSystem.Instance?.OpenPedestrianActions(npc);
    }
}

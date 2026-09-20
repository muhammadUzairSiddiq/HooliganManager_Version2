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
            var hud = FindFirstObjectByType<LandscapeBattleHUD>();
            if (!hud) return;
            frame = hud.frame;
            var button = LandscapeUI.Button("LocationPin" + LocationIndex, frame, name, 0, 0, 210, 48);
            button.onClick.AddListener(() => CityGameplay.Instance?.OpenLocation(LocationIndex));
            pin = button.transform as RectTransform;
            pin.SetAsFirstSibling();
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
        bool visible = projected.z > 0.1f && x > 250f && x < frame.rect.width - 190f && y > 100f && y < frame.rect.height - 80f;
        pin.gameObject.SetActive(visible);
        if (pinLabel) pinLabel.color = Color.white;
        if (visible) LandscapeUI.Place(pin, x - 105, y - 24, 210, 48);
    }

    void OnDestroy() { if (pin) Destroy(pin.gameObject); }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Plays a professional pre-battle cinematic intro sequence every time the
/// Battle Scene loads.
///
/// Sequence order
/// ──────────────
///  1. Camera pans to the PLAYER spawn position (auto-resolved from BattleManager)
///  2. Mode banner (RIVAL FIGHT)
///  3. Cycling hype prompts            → territory-focused lines
///  4. Camera returns, HUD fades in    → BattleManager is released to start
///
/// Setup in Inspector
/// ──────────────────
///  • No camera target Transforms needed — the sequencer reads playerSpawnRoot
///    from BattleManager, which is set dynamically at scene load.
///  • Wire up the UI elements on the PreBattleCanvas.
///  • The PreBattleCanvas should be its own Canvas (Screen Space – Overlay)
///    so it renders above everything including the main BattleUIController canvas.
///  • BattleManager.RunBattle() yields on PreBattleSequencer.instance.PlaySequence().
/// </summary>
public class PreBattleSequencer : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────
    public static PreBattleSequencer instance;

    // ── Camera ────────────────────────────────────────────────────────────
    // No target Transforms exposed — the camera always pans to
    // BattleManager.playerSpawnRoot which is resolved dynamically at runtime
    // from GangPositionGameObject inside the loaded map.

    [Tooltip("Transform for the neutral CENTER position used during the VS splash and prompts.")]
    public CameraPanTouchOnly cameraPanTouchOnly;

    [Tooltip("How long (seconds) each camera pan step takes.")]
    public float cameraPanDuration = 2.0f;

    [Tooltip("How long (seconds) to hold on each position before panning away.")]
    public float cameraHoldDuration = 1.2f;

    [Header("Camera Height & Framing")]
    [Tooltip("World-space Y height the camera moves to during the pan.")]
    public float cameraPanHeight = 20f;

    [Tooltip("Extra world-space offset added on top of the auto-computed position. " +
             "Use this to fine-tune framing without changing code.")]
    public Vector3 cameraPanOffset = Vector3.zero;

    // ── UI ────────────────────────────────────────────────────────────────
    [Header("Pre-Battle Canvas (full-screen overlay)")]
    [Tooltip("Root canvas / panel that covers the screen during the intro. Disable it at the end.")]
    public GameObject preBattlePanel;

    [Header("Team Reveal Texts")]
    [Tooltip("Shows your firm name during the player-side pan.")]
    public TextMeshProUGUI playerFirmText;

    [Tooltip("Shows the enemy firm name during the enemy-side pan.")]
    public TextMeshProUGUI enemyFirmText;

    [Tooltip("Small sub-label under the firm name (e.g. 'HOOLIGANS' / 'MOB').")]
    public TextMeshProUGUI playerSubText;
    public TextMeshProUGUI enemySubText;

    [Header("Team Logos / Crests")]
    [Tooltip("Image component BG for the player crest — shows the player's team logo during the player-side pan and VS splash.")]
    public Image playerDetailBGImage;

    [Tooltip("Image component that shows the PLAYER team's crest during the player-side reveal AND the VS splash.")]
    public Image playerLogoImage;

    [Tooltip("Image component BG for the enemy crest — shows the enemy's team logo during the enemy-side pan and VS splash.")]
    public Image enemyDetailBGImage;

    [Tooltip("Image component that shows the ENEMY team's crest during the enemy-side reveal AND the VS splash.")]
    public Image enemyLogoImage;

    [Tooltip("Fallback sprite shown when no crest is found in BotRegistry (e.g. police badge / generic crest).")]
    public Sprite fallbackCrestSprite;

    [Tooltip("BotRegistry ScriptableObject. Leave null — auto-loaded from Resources/BotRegistry.")]
    public BotRegistry botRegistry;

    [Tooltip("PoliceRegistry ScriptableObject. Leave null — auto-loaded from Resources/PoliceRegistry.")]
    public PoliceDataRegistry policeRegistry;

    [Header("VS Splash")]
    public TextMeshProUGUI vsText;               // the big "VS" in the centre
    public Image           vsDividerLine;        // optional horizontal divider

    [Header("Prompt / Hype Text")]
    [Tooltip("Cycling hype lines and the final WARNING line are displayed here.")]
    public TextMeshProUGUI promptText;

    [Tooltip("Background tint panel behind promptText — tints red on warning.")]
    public Image           promptBackground;

    [Header("Mode / Round Banner")]
    public TextMeshProUGUI modeBannerText;       // "RIVAL FIGHT" / "POLICE RAID"

    // ── Timing tweaks ─────────────────────────────────────────────────────
    [Header("Timing")]
    [Tooltip("Seconds each hype prompt is visible before cycling to the next.")]
    public float promptHoldTime = 1.4f;

    [Tooltip("Duration of the fade-in / fade-out for most UI elements.")]
    public float uiFadeDuration = 0.4f;

    [Tooltip("Duration of the screen-shake when the warning appears.")]
    public float shakeDuration  = 0.5f;
    public float shakeMagnitude = 0.08f;

    // ── Colours ───────────────────────────────────────────────────────────
    [Header("Colors")]
    public Color playerColor = new Color(0.20f, 0.60f, 1.00f, 1f);  // blue
    public Color enemyColor  = new Color(1.00f, 0.25f, 0.25f, 1f);  // red
    public Color warningColor = new Color(0.90f, 0.10f, 0.10f, 1f);
    public Color neutralColor = Color.white;

    // ── Internal ──────────────────────────────────────────────────────────
    private Camera   _cam;
    private Vector3  _camOrigPos;
    private Quaternion _camOrigRot;
    // Position + rotation of the camera once it has arrived at the player gang.
    // The sequence ends here — no snap-back to the pre-intro position.
    private Vector3    _camGangPos;
    private Quaternion _camGangRot;
    private bool     _sequenceDone;

    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        _cam = Camera.main;
        if (_cam != null)
        {
            _camOrigPos = _cam.transform.position;
            _camOrigRot = _cam.transform.rotation;
        }

        // Auto-load BotRegistry if not assigned in Inspector
        if (botRegistry == null)
            botRegistry = Resources.Load<BotRegistry>("BotRegistry");
    }

    // ── Entry point called by BattleManager ──────────────────────────────

    /// <summary>
    /// BattleManager yields on this coroutine.
    /// Returns only after the full intro sequence has finished.
    /// Enemy gangs are now passive at their spots, so the sequence only
    /// reveals the player's own crew — no enemy pan, no VS splash,
    /// no "ENEMY ADVANCING" warning.
    /// </summary>
    public IEnumerator PlaySequence()
    {
        _sequenceDone = false;

        // Make sure the overlay is visible
        if (preBattlePanel) preBattlePanel.SetActive(true);

        // Hide everything to start
        SetAllAlpha(0f);

        // ── Resolve player-side camera target from BattleManager ──────────────
        // playerSpawnRoot is set at runtime by BattleManager.loadSpawnPoint()
        // which reads GangPositionGameObject from the dynamically loaded map.
        Transform playerTarget = BattleManager.instance?.playerSpawnRoot;

        // Collect team names from GameData / BattleManager context
        string myFirm     = GameData.instance?.PlayerData?.FirmName   ?? "YOUR FIRM";
        string enemyFirm  = GameData.instance?.PlayerData?.RivalClubName != null
                            ? GameData.instance.PlayerData.RivalClubName.Replace(" FC", "").ToUpper()
                            : "RIVAL FIRM";

        // If BattleManager has a specific enemy name passed through GameManager, prefer it
        // (We read the same field BattleManager reads so we stay in sync)
        string pendingName = GameManager.PendingEnemyFirmName;
        if (!string.IsNullOrEmpty(pendingName))
            enemyFirm = pendingName.ToUpper();

        bool isRaid = GameManager.PendingBattleMode == BattleManager.BattleMode.PoliceRaid;

        // ── Resolve crest + banner sprites from BotRegistry ─────────────────────
        Sprite playerCrest  = fallbackCrestSprite;
        Sprite playerBanner = null;

        if (botRegistry != null)
        {
            string myFirmKey = GameData.instance?.PlayerData?.FirmName ?? "";
            var playerEntry  = botRegistry.FindByFirmName(myFirmKey);
            if (playerEntry?.crestSprite  != null) playerCrest  = playerEntry.crestSprite;
            if (playerEntry?.bannerSprite != null) playerBanner = playerEntry.bannerSprite;
        }

        // ── Step 1: Player side reveal only ──────────────────────────────────
        // Enemy gangs are passive at their spots — no need to pan to them or
        // show a VS splash. The intro focuses entirely on the player's crew.
        // ── Tight ~3s cinematic: quick pan-in → firm reveal → hand off ────────
        // The old version played a long pan, a separate mode banner, and three
        // cycling hype lines (~10s of text). That was the "noise" before gameplay.
        // This is now a single clean beat.
        yield return StartCoroutine(PanCameraTo(playerTarget, Mathf.Min(cameraPanDuration, 1.1f)));

        // Save the camera position reached after arriving at the gang.
        // Rotation is always _camOrigRot (the pan never changes it) so the
        // camera keeps its original tilt throughout the sequence and at game start.
        if (_cam != null)
        {
            _camGangPos = _cam.transform.position;
            _camGangRot = _camOrigRot;   // rotation never changes during pans
        }

        // Reveal the firm name with the battle type folded in as the subtitle,
        // so there is ONE text beat instead of name + banner + hype lines.
        string modeSub = isRaid ? "POLICE RAID" : "RIVAL FIGHT";
        yield return StartCoroutine(RevealTeamText(
            playerFirmText, playerSubText, playerLogoImage, playerDetailBGImage,
            myFirm, modeSub, playerCrest, playerBanner,
            playerColor));
        yield return new WaitForSecondsRealtime(Mathf.Min(cameraHoldDuration, 0.9f));

        // Fade the reveal group out together (parallel) so it's quick.
        StartCoroutine(FadeElement(playerFirmText,      false, uiFadeDuration));
        StartCoroutine(FadeElement(playerSubText,       false, uiFadeDuration));
        StartCoroutine(FadeElement(playerDetailBGImage, false, uiFadeDuration));
        StartCoroutine(FadeElement(playerLogoImage,     false, uiFadeDuration));
        yield return new WaitForSecondsRealtime(uiFadeDuration);

        // ── Step 4: Hide overlay and signal done ──────────────────────────────
        // The camera deliberately stays at _camGangPos / _camGangRot (where the
        // player gang is). No snap-back pan — gameplay starts from this angle.
        yield return StartCoroutine(FadeElement(promptText,       false, uiFadeDuration));
        yield return StartCoroutine(FadeElement(promptBackground, false, uiFadeDuration));

        // Hide the full overlay — HUD will fade in via BattleUIController
        if (preBattlePanel) preBattlePanel.SetActive(false);

        // Restore camera exactly to the saved gang position so any floating-point
        // drift during the hold is corrected before we hand off to CameraPanTouchOnly.
        if (_cam != null)
        {
            _cam.transform.position = _camGangPos;
            _cam.transform.rotation = _camGangRot;
        }

        _sequenceDone = true;
        cameraPanTouchOnly.enabled = true;
    }

    // ── Sub-routines ──────────────────────────────────────────────────────

    /// <summary>
    /// Smoothly slides the camera position to the target.
    /// Rotation is always kept at _camOrigRot (the original scene tilt) —
    /// only the position moves so the camera never changes its angle.
    /// </summary>
    private IEnumerator PanCameraTo(Transform target, float duration)
    {
        if (_cam == null) yield break;

        Vector3 startPos = _cam.transform.position;

        // ── Compute framing-correct pan position ──────────────────────────────
        // The camera has a fixed tilt stored in _camOrigRot. To centre the target
        // point (gang / original pos) in the frame we back the camera up along
        // the horizontal component of its forward vector by enough distance that
        // the down-tilt aligns with the target at ground level.
        //
        //  camForward = _camOrigRot * Vector3.forward
        //  backDist   = cameraPanHeight / Abs(camForward.y)   (right-angle trig)
        //  endPos     = targetXZ - camForwardXZ.normalized * backDist  +  Y
        Vector3 rawTarget = target != null ? target.position : _camOrigPos;
        Vector3 camForward = _camOrigRot * Vector3.forward;

        Vector3 camForwardXZ = new Vector3(camForward.x, 0f, camForward.z);
        float backDist = camForwardXZ.magnitude > 0.001f && Mathf.Abs(camForward.y) > 0.001f
            ? cameraPanHeight / Mathf.Abs(camForward.y) * camForwardXZ.magnitude
            : 0f;

        Vector3 endPos = rawTarget
                         - camForwardXZ.normalized * backDist   // back up so tilt centres the gang
                         + cameraPanOffset;                      // inspector fine-tune
        endPos.y = cameraPanHeight;                             // lock height

        // Rotation is always the original camera tilt — never inherited from the target.
        _cam.transform.rotation = _camOrigRot;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.SmoothStep(0f, 1f, t / duration);
            _cam.transform.position = Vector3.Lerp(startPos, endPos, s);
            // Hold original rotation every frame so nothing can accidentally overwrite it.
            _cam.transform.rotation = _camOrigRot;
            yield return null;
        }

        _cam.transform.position = endPos;
        _cam.transform.rotation = _camOrigRot;
    }

    /// <summary>
    /// Fades in the team name, sub-label, logo crest, and background banner image simultaneously.
    /// - logoImage receives the crest sprite (small badge / club logo)
    /// - bgImage   receives the banner sprite (wide background image for the reveal panel)
    /// </summary>
    private IEnumerator RevealTeamText(
        TextMeshProUGUI nameText, TextMeshProUGUI subLabel, Image logoImage, Image bgImage,
        string name, string sub, Sprite crest, Sprite banner, Color col)
    {
        if (nameText)
        {
            nameText.text  = name;
            nameText.color = new Color(col.r, col.g, col.b, 0f);
        }
        if (subLabel)
        {
            subLabel.text  = sub;
            subLabel.color = new Color(col.r, col.g, col.b, 0f);
        }
        if (logoImage)
        {
            logoImage.sprite  = crest;
            logoImage.enabled = crest != null;
            logoImage.color   = new Color(1f, 1f, 1f, 0f);
        }
        if (bgImage)
        {
            // Banner goes into the BG image; fall back to a plain colour tint if no banner
            bgImage.sprite  = banner;           // null is fine — Image renders solid colour
            bgImage.enabled = true;
            bgImage.color   = new Color(0, 0, 0, 0f);
        }

        float t = 0f;
        while (t < uiFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / uiFadeDuration);
            if (nameText)  nameText.color  = new Color(col.r, col.g, col.b, a);
            if (subLabel)  subLabel.color  = new Color(col.r, col.g, col.b, a * 0.7f);
            if (logoImage) logoImage.color = new Color(1f, 1f, 1f, a);
            // BG fades in at half speed for a softer, cinematic wash-in
            if (bgImage)   bgImage.color   = new Color(0, 0, 0, a * 0.5f);
            yield return null;
        }
    }

    /// <summary>
    /// Shows the VS splash — both team crests flank the VS text with a
    /// punchy scale-in animation. Reuses playerLogoImage and enemyLogoImage
    /// (no extra Image components needed).
    /// </summary>
    private IEnumerator ShowVSSplash(bool isRaid, int rounds)
    {
        if (vsText)
        {
            vsText.text  = "VS";
            vsText.color = new Color(1f, 1f, 1f, 0f);
            vsText.transform.localScale = Vector3.one * 2f;
        }
        if (modeBannerText)
        {
            string modeName = isRaid ? "POLICE RAID" : "RIVAL FIGHT";
            modeBannerText.text  = isRaid ? $"{modeName}" :  $"{modeName}  ·  ROUND 1 OF {rounds}";
            modeBannerText.color = new Color(1f, 1f, 1f, 0f);
        }

        // Scale-in animation: VS text punches in, crests + banner fade in together
        float t = 0f;
        float dur = uiFadeDuration * 1.5f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(2f, 1f, Mathf.SmoothStep(0f, 1f, a));

            if (vsText)
            {
                vsText.color = new Color(1f, 1f, 1f, a);
                vsText.transform.localScale = Vector3.one * s;
            }
            if (modeBannerText)
                modeBannerText.color = new Color(1f, 1f, 1f, a);
            if (vsDividerLine)
                vsDividerLine.color = new Color(0.5764706f, 0.7695048f, 0f, a * 0.5f);

            yield return null;
        }
    }

    /// <summary>Shows a single hype prompt line, holds it, then fades it out.</summary>
    private IEnumerator ShowPrompt(string line, Color textCol, Color bgCol, float holdSeconds)
    {
        if (promptText)
        {
            promptText.text  = line;
            promptText.color = new Color(textCol.r, textCol.g, textCol.b, 0f);
        }
        if (promptBackground)
            promptBackground.color = new Color(bgCol.r, bgCol.g, bgCol.b, 0f);

        // Fade in
        float t = 0f;
        while (t < uiFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / uiFadeDuration);
            if (promptText)      promptText.color      = new Color(textCol.r, textCol.g, textCol.b, a);
            if (promptBackground) promptBackground.color = new Color(bgCol.r, bgCol.g, bgCol.b, a * 0.6f);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(holdSeconds);

        // Fade out
        t = 0f;
        while (t < uiFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / uiFadeDuration);
            if (promptText)      promptText.color      = new Color(textCol.r, textCol.g, textCol.b, a);
            if (promptBackground) promptBackground.color = new Color(bgCol.r, bgCol.g, bgCol.b, a * 0.6f);
            yield return null;
        }
    }

    /// <summary>
    /// The dramatic "ENEMY ADVANCING" / "POLICE INBOUND" warning with pulsing
    /// red background and optional screen shake.
    /// </summary>
    private IEnumerator ShowWarningPrompt(bool isRaid)
    {
        string warningLine = isRaid ? "⚠  POLICE INBOUND  ⚠" : "⚠  ENEMY ADVANCING  ⚠";

        if (promptText)
        {
            promptText.text  = warningLine;
            promptText.color = new Color(warningColor.r, warningColor.g, warningColor.b, 0f);
        }
        if (promptBackground)
            promptBackground.color = new Color(warningColor.r, warningColor.g, warningColor.b, 0f);

        // Fade in warning
        float t = 0f;
        while (t < uiFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / uiFadeDuration);
            if (promptText)       promptText.color       = new Color(1f, 1f, 1f, a);
            if (promptBackground) promptBackground.color = new Color(warningColor.r, warningColor.g, warningColor.b, a * 0.75f);
            yield return null;
        }

        // Pulse + shake for ~2 seconds
        yield return StartCoroutine(PulseWarning(2.0f));

        // Screen shake
        if (_cam != null)
            yield return StartCoroutine(ShakeCamera(shakeDuration, shakeMagnitude));
    }

    /// <summary>Pulses the warning text scale for dramatic effect.</summary>
    private IEnumerator PulseWarning(float totalTime)
    {
        float elapsed = 0f;
        float pulseSpeed = 3.5f;

        while (elapsed < totalTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = 1f + 0.08f * Mathf.Sin(elapsed * pulseSpeed * Mathf.PI * 2f);
            if (promptText) promptText.transform.localScale = Vector3.one * pulse;
            yield return null;
        }

        if (promptText) promptText.transform.localScale = Vector3.one;
    }

    /// <summary>Shakes the main camera by a random offset for <duration> seconds.</summary>
    private IEnumerator ShakeCamera(float duration, float magnitude)
    {
        if (_cam == null) yield break;

        Vector3 originalPos = _cam.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            _cam.transform.position = originalPos + new Vector3(x, y, 0f);
            yield return null;
        }

        _cam.transform.position = originalPos;
    }

    // ── Generic fade helpers ──────────────────────────────────────────────

    private IEnumerator FadeElement(Graphic g, bool fadeIn, float duration)
    {
        if (g == null) yield break;
        Color startColor = g.color;
        Color endColor   = new Color(startColor.r, startColor.g, startColor.b, fadeIn ? 1f : 0f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            g.color = Color.Lerp(startColor, endColor, t / duration);
            yield return null;
        }
        g.color = endColor;
    }

    private IEnumerator FadeElement(TextMeshProUGUI tmp, bool fadeIn, float duration)
    {
        if (tmp == null) yield break;
        Color c = tmp.color;
        float startA = c.a;
        float endA   = fadeIn ? 1f : 0f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(startA, endA, t / duration);
            tmp.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }
        tmp.color = new Color(c.r, c.g, c.b, endA);
    }

    // ── Utility: sets all managed UI elements to a given alpha instantly ──
    private void SetAllAlpha(float a)
    {
        SetTMPAlpha(playerFirmText,  a);
        SetTMPAlpha(playerSubText,   a);
        SetTMPAlpha(enemyFirmText,   a);
        SetTMPAlpha(enemySubText,    a);
        SetTMPAlpha(vsText,          a);
        SetTMPAlpha(modeBannerText,  a);
        SetTMPAlpha(promptText,      a);

        SetImageAlpha(vsDividerLine,     a);
        SetImageAlpha(promptBackground,  a);
        SetImageAlpha(playerLogoImage,   a);
        SetImageAlpha(enemyLogoImage,    a);
        SetImageAlpha(playerDetailBGImage, a);
        SetImageAlpha(enemyDetailBGImage,  a);
    }

    private static void SetImageAlpha(Image img, float a)
    {
        if (img == null) return;
        Color c = img.color;
        img.color = new Color(c.r, c.g, c.b, a);
    }

    private static void SetTMPAlpha(TextMeshProUGUI tmp, float a)
    {
        if (tmp == null) return;
        Color c = tmp.color;
        tmp.color = new Color(c.r, c.g, c.b, a);
    }
}

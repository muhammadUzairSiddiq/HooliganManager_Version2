using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

/// <summary>
/// Recruitment Center. One recruit per visit; leave + re-enter for the next.
/// Civilian count always equals remaining slots (visuals match the UI).
/// </summary>
public class RecruitArea : MonoBehaviour
{
    public int recruitCost = 700;

    private string _areaName = "RECRUITMENT CENTER";
    private float _radius;
    private float _detectRadius;
    private readonly Color _color = new Color(1f, 0.78f, 0.12f, 1f);
    private TextMeshPro _label;
    private WorldInteractBubble _bubble;
    private float _scanTimer;
    private bool _promptArmed = true;
    private bool _wasInside;
    private bool _mustLeaveBeforeReprompt;
    private bool _inConversation;
    private bool _camWasEnabled = true;
    private int _recruitedHere;
    private int _maxRecruits = 2;
    private int _centerIndex;
    private readonly List<RecruitCivilianWander> _civilians = new List<RecruitCivilianWander>();

    public Color ZoneColor => _color;
    public string AreaName => _areaName;
    public int RemainingSlots => Mathf.Max(0, _maxRecruits - _recruitedHere);
    public bool IsExhausted => RemainingSlots <= 0;

    public void Setup(Vector3 center, float radius, int cost, string areaName, int maxRecruits, int centerIndex = 0)
    {
        _radius = radius;
        _detectRadius = radius * 1.35f + 1.5f;
        recruitCost = cost;
        _areaName = string.IsNullOrEmpty(areaName) ? "RECRUITMENT CENTER" : areaName.ToUpperInvariant();
        _maxRecruits = Mathf.Clamp(maxRecruits, 1, 3);
        _centerIndex = Mathf.Clamp(centerIndex, 0, 2);
        _recruitedHere = LoadUsedSlots();

        transform.position = GroundedCenter(center);
        ZoneVolumeFactory.Create(transform, _color, radius, height: 2.4f);
        _label = ZoneLabelUtil.Create(transform, _areaName, 3.6f, 11f);
        _bubble = WorldInteractBubble.Create(transform);
        SpawnCiviliansExact(RemainingSlots);
        MiniMapIconFactory.Register(transform, MiniMapIconFactory.Kind.Recruit, _areaName);
        RefreshLabel();
    }

    private int LoadUsedSlots()
    {
        var used = GameData.instance?.PlayerData?.BattleRecruitSlotsUsed;
        if (used == null || _centerIndex < 0 || _centerIndex >= used.Length) return 0;
        return Mathf.Clamp(used[_centerIndex], 0, _maxRecruits);
    }

    private void PersistRecruitSlots()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;
        if (d.BattleRecruitSlotsUsed == null || d.BattleRecruitSlotsUsed.Length < 3)
            d.BattleRecruitSlotsUsed = new int[3];
        d.BattleRecruitSlotsUsed[_centerIndex] = _recruitedHere;
        GameData.instance.SaveData();
    }

    private Vector3 GroundedCenter(Vector3 center)
    {
        if (Physics.Raycast(center + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
            center.y = hit.point.y + 0.02f;
        return center;
    }

    private void RefreshLabel()
    {
        if (_label == null) return;
        if (IsExhausted)
            _label.text = "RECRUIT";
        else
            _label.text = $"RECRUIT  ·  {RemainingSlots}";
    }

    /// <summary>Spawn exactly <paramref name="count"/> walking civilians — matches UI slots.</summary>
    private void SpawnCiviliansExact(int count)
    {
        foreach (var c in _civilians)
            if (c != null) Destroy(c.gameObject);
        _civilians.Clear();

        var registry = BattleManager.instance != null ? BattleManager.instance.portraitRegistry : null;
        if (registry == null || registry.entries == null || registry.entries.Count == 0) return;

        var usable = new List<CharacterPortraitRegistry.Entry>();
        foreach (var e in registry.entries)
            if (e != null && e.modelPrefab != null) usable.Add(e);
        if (usable.Count == 0) return;

        for (int i = 0; i < count; i++)
        {
            var entry = usable[i % usable.Count];
            float ang = (float)i / Mathf.Max(1, count) * Mathf.PI * 2f;
            Vector3 local = new Vector3(Mathf.Cos(ang) * _radius * 0.45f, 0f, Mathf.Sin(ang) * _radius * 0.45f);
            Vector3 pos = transform.position + local;
            if (NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas))
                pos = hit.position;

            var model = Instantiate(entry.modelPrefab, pos, Quaternion.identity, transform);
            GameplayTuning.ScaleModel(model.transform);

            // Strip any battle AI that might have been on the prefab.
            foreach (var ac in model.GetComponentsInChildren<AgentController>())
                Destroy(ac);
            foreach (var ec in model.GetComponentsInChildren<EnemyController>())
                Destroy(ec);

            var anim = model.GetComponentInChildren<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.applyRootMotion = false;
            anim.runtimeAnimatorController = entry.animatorController != null
                ? entry.animatorController
                : BattleManager.instance != null ? BattleManager.instance.characterAnimator : null;

            var agent = model.GetComponent<NavMeshAgent>();
            if (agent == null) agent = model.AddComponent<NavMeshAgent>();
            agent.speed = 1.2f;
            agent.angularSpeed = 220f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.3f;
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.updateRotation = true;

            if (!agent.isOnNavMesh && NavMesh.SamplePosition(pos, out var snap, 5f, NavMesh.AllAreas))
            {
                agent.Warp(snap.position);
                pos = snap.position;
            }

            var wander = model.AddComponent<RecruitCivilianWander>();
            wander.Init(transform.position, _radius * 0.7f, anim, agent);
            _civilians.Add(wander);
        }
    }

    void Update()
    {
        if (_label != null && Camera.main != null)
            _label.transform.rotation = Camera.main.transform.rotation;

        if (_bubble != null)
        {
            if (IsExhausted || RecruitPackagePanel.AnyOpen) _bubble.Hide();
            else if (!_bubble.IsLive) _bubble.Show("RECRUIT", OpenCampaigns);
        }
    }

    private bool AnyPlayerInside()
    {
        float r2 = _detectRadius * _detectRadius;
        foreach (var a in BattleManager.instance.PlayerAgents)
        {
            if (a == null || !a.IsAlive) continue;
            Vector3 d = a.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) return true;
        }
        return false;
    }

    private void BeginConversation()
    {
        _inConversation = true;
        SetCiviliansPaused(true);

        RecruitDialogBox.Instance.Show(
            _areaName,
            $"{RemainingSlots} local{(RemainingSlots == 1 ? "" : "s")} here can join. Fancy a word — or run a full recruitment campaign?",
            new RecruitDialogBox.Choice("TALK TO THEM", new Color(0.85f, 0.65f, 0.1f), AskWhyJoin),
            new RecruitDialogBox.Choice("RECRUITMENT CAMPAIGNS", new Color(0.18f, 0.55f, 0.32f), OpenCampaigns),
            new RecruitDialogBox.Choice("WALK AWAY", new Color(0.25f, 0.32f, 0.4f), WalkAway)
        );
    }

    /// <summary>Same four-package board as headquarters; recruits spawn here.</summary>
    private void OpenCampaigns()
    {
        RecruitDialogBox.Instance.Hide();
        RecruitPackagePanel.Instance.Show(transform.position, _areaName, FinishRecruitVisit);
    }

    private void FocusCameraOnFans()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 target = transform.position;
        Vector3 fwd = cam.transform.forward;
        Vector3 fwdXZ = new Vector3(fwd.x, 0f, fwd.z);
        float height = Mathf.Clamp(cam.transform.position.y, 14f, 22f);
        float back = (fwdXZ.magnitude > 0.001f && Mathf.Abs(fwd.y) > 0.001f)
            ? height / Mathf.Abs(fwd.y) * fwdXZ.magnitude
            : 10f;
        Vector3 pos = target - fwdXZ.normalized * back;
        pos.y = height;
        cam.transform.position = pos;
    }

    private void AskWhyJoin()
    {
        RecruitDialogBox.Instance.Show(
            "RECRUIT",
            "\"Why should we join your firm?\"",
            new RecruitDialogBox.Choice("WE RUN THESE STREETS", new Color(0.85f, 0.65f, 0.1f), AskWhatsInIt),
            new RecruitDialogBox.Choice("GLORY & LOYALTY", new Color(0.2f, 0.45f, 0.55f), AskWhatsInIt)
        );
    }

    private void AskWhatsInIt()
    {
        RecruitDialogBox.Instance.Show(
            "RECRUIT",
            "\"Alright… what's in it for us?\"",
            new RecruitDialogBox.Choice($"RECRUIT (£{recruitCost:n0})", new Color(0.85f, 0.65f, 0.1f), TryRecruit),
            new RecruitDialogBox.Choice("FORFEIT", new Color(0.5f, 0.2f, 0.2f), WalkAway)
        );
    }

    private void TryRecruit()
    {
        if (IsExhausted)
        {
            RecruitDialogBox.Instance.Show(
                "RECRUIT",
                "\"We're all sorted here, mate.\"",
                new RecruitDialogBox.Choice("OK", new Color(0.25f, 0.32f, 0.4f), WalkAway)
            );
            return;
        }

        var pd = GameData.instance != null ? GameData.instance.PlayerData : null;
        if (pd == null || pd.Money < recruitCost)
        {
            RecruitDialogBox.Instance.Show(
                "RECRUIT",
                "\"Come back when you've got the cash.\"",
                new RecruitDialogBox.Choice("OK", new Color(0.25f, 0.32f, 0.4f), WalkAway)
            );
            return;
        }

        Vector3 spawnPos = transform.position;
        if (NavMesh.SamplePosition(spawnPos, out var hit, 5f, NavMesh.AllAreas))
            spawnPos = hit.position;

        if (BattleManager.instance == null || BattleManager.instance.SpawnRecruitedAgentAt(spawnPos, "New Recruit") == null)
        { RecruitDialogBox.Instance.Show("SQUAD FULL", "No payment taken. Free an active squad slot before recruiting.", new RecruitDialogBox.Choice("OK", Color.gray, WalkAway)); return; }
        pd.Money -= recruitCost;
        GameAudio.Play("recovery");
        _recruitedHere++;
        PersistRecruitSlots();
        RemoveOneCivilianVisual();
        RefreshLabel();

        RecruitDialogBox.Instance.Show(
            "RECRUIT",
            IsExhausted
                ? "\"That's all of us from this spot. Sorted!\""
                : $"\"Sorted. {RemainingSlots} still here — leave and come back for another.\"",
            new RecruitDialogBox.Choice("SORTED", new Color(0.85f, 0.65f, 0.1f), FinishRecruitVisit)
        );
    }

    private void RemoveOneCivilianVisual()
    {
        for (int i = 0; i < _civilians.Count; i++)
        {
            if (_civilians[i] == null) continue;
            Destroy(_civilians[i].gameObject);
            _civilians.RemoveAt(i);
            break;
        }
        // Keep list length in sync with remaining slots.
        while (_civilians.Count > RemainingSlots)
        {
            int last = _civilians.Count - 1;
            if (_civilians[last] != null) Destroy(_civilians[last].gameObject);
            _civilians.RemoveAt(last);
        }
    }

    private void SetCiviliansPaused(bool paused)
    {
        foreach (var c in _civilians)
            if (c != null) c.SetPaused(paused);
    }

    private void FinishRecruitVisit()
    {
        _mustLeaveBeforeReprompt = true;
        _promptArmed = false;
        EndConversation();
        BattleManager.instance?.PullPlayerAgentsFrom(transform.position, _detectRadius + 2.5f);
    }

    private void WalkAway()
    {
        _mustLeaveBeforeReprompt = true;
        _promptArmed = false;
        EndConversation();
        BattleManager.instance?.PullPlayerAgentsFrom(transform.position, _detectRadius + 2.5f);
    }

    private void EndConversation()
    {
        RecruitDialogBox.Instance.Hide();
        _inConversation = false;
        SetCiviliansPaused(false);
        BattleManager.instance?.SetAllAgentsCinematicIdle(false);
    }
}

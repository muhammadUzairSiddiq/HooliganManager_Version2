using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>Road response for a single Matchday cordon. Vehicles use the road mesh and officers use the foot mesh.</summary>
public sealed class MatchdayPoliceResponse : MonoBehaviour
{
    const int RoadMask = 1 << 3;
    static readonly Color Blue = new Color(.12f, .48f, 1f);
    static readonly Color Red = new Color(1f, .16f, .09f);
    static readonly Color Uniform = new Color(.15f, .4f, .85f);
    static AudioClip sirenClip;
    readonly List<EnemyController> officers = new List<EnemyController>(2);
    CityRoadIncidentSystem.RoadIncident cordon;
    Transform van;
    Renderer blue, red;
    AudioSource siren;
    Vector3 origin, parked;
    Vector3[] route;
    int corner;
    float nextDeployment, sirenUntil, deployAt;
    bool departing, audioPaused, queueCreated, announceArrival;

    public bool OnSite => van && corner >= route.Length && !departing;
    public Transform Vehicle => van;
    public IReadOnlyList<EnemyController> Officers => officers;
    public bool SirenPlayed { get; private set; }

    public static MatchdayPoliceResponse Create(CityRoadIncidentSystem.RoadIncident road, Transform parent)
    {
        var host = new GameObject("Police response · " + road.District);
        host.transform.SetParent(parent);
        host.transform.position = road.Location;
        var response = host.AddComponent<MatchdayPoliceResponse>();
        response.cordon = road;
        return response;
    }

    void Update()
    {
        if (Time.deltaTime <= 0 || GuidedCityTutorial.CombatSuppressed)
        {
            if (siren && siren.isPlaying) { siren.Pause(); audioPaused = true; }
            return;
        }
        if (!van && !departing && MatchdayStreetOperations.Active && CityActivityStreaming.Interested(cordon.Location))
            StartVehicle();
        if (van) UpdateVehicle();
        if (departing || !MatchdayStreetOperations.Active || Time.time < nextDeployment ||
            !CityActivityStreaming.Interested(cordon.OfficerWorkPoint)) return;
        nextDeployment = Time.time + 2f;
        if (van && !OnSite && Time.time < deployAt) return;
        if (!queueCreated) queueCreated = StadiumMatchdayActivity.Instance.EnsureCordonQueue(cordon);
        DeployOfficers();
    }

    void StartVehicle()
    {
        var prefab = PoliceManager.instance ? PoliceManager.instance.policeCarPrefab : null;
        if (!prefab) return;
        var forward = cordon.Direction;
        var parkRequest = cordon.Location - forward * 6f + Vector3.Cross(Vector3.up, forward) * 2.2f;
        parked = NavMesh.SamplePosition(parkRequest, out var parking, 3, RoadMask) ? parking.position : cordon.Location - forward * 6f;
        origin = parked;
        var path = new NavMeshPath();
        if (NavMesh.SamplePosition(parked - forward * 22f, out var start, 6, RoadMask) &&
            NavMesh.CalculatePath(start.position, parked, RoadMask, path) && path.status == NavMeshPathStatus.PathComplete &&
            PathDistance(path.corners) < 42f)
            origin = start.position;
        route = origin == parked ? System.Array.Empty<Vector3>() : path.corners;
        corner = Mathf.Min(1, route.Length);
        van = new GameObject("Marked police van").transform;
        van.SetParent(transform);
        van.position = origin;
        var bounds = PolicePatrolVehicle.CreatePatrolModel(prefab, van);
        van.rotation = Quaternion.LookRotation(forward);
        float roof = bounds.max.y - origin.y + .12f;
        blue = Beacon(new Vector3(-.48f, roof, 0), Blue);
        red = Beacon(new Vector3(.48f, roof, 0), Red);
        siren = van.gameObject.AddComponent<AudioSource>();
        siren.clip = GetSiren();
        siren.loop = true;
        siren.playOnAwake = false;
        siren.spatialBlend = 1;
        siren.rolloffMode = AudioRolloffMode.Linear;
        siren.minDistance = 27;
        siren.maxDistance = 175;
        siren.dopplerLevel = 0;
        deployAt = Time.time + 5;
        announceArrival = true;
    }

    static float PathDistance(Vector3[] corners)
    {
        float distance=0;
        for(int index=1;index<corners.Length;index++)distance+=Vector3.Distance(corners[index-1],corners[index]);
        return distance;
    }

    Renderer Beacon(Vector3 offset, Color color)
    {
        var lens = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lens.name = "Emergency roof light";
        Destroy(lens.GetComponent<Collider>());
        lens.transform.SetParent(van, false);
        lens.transform.localPosition = offset;
        lens.transform.localScale = new Vector3(.65f, .24f, .38f);
        var renderer = lens.GetComponent<Renderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color * 2.6f);
        properties.SetColor("_Color", color * 2.6f);
        renderer.SetPropertyBlock(properties);
        return renderer;
    }

    void UpdateVehicle()
    {
        if (corner < route.Length)
        {
            var target = route[corner];
            var direction = Vector3.ProjectOnPlane(target - van.position, Vector3.up);
            if (direction.sqrMagnitude > .02f)
                van.rotation = Quaternion.RotateTowards(van.rotation, Quaternion.LookRotation(direction), 260 * Time.deltaTime);
            float speed = 9f * CityRoadIncidentSystem.TrafficSpeedMultiplier(van.position, direction.normalized, cordon.Id);
            van.position = Vector3.MoveTowards(van.position, target, speed * Time.deltaTime);
            if (Vector3.Distance(van.position, target) < .15f) corner++;
        }
        else if (departing)
        {
            Destroy(gameObject);
            return;
        }

        bool flash = Mathf.FloorToInt(Time.time * 7) % 2 == 0;
        blue.enabled = flash;
        red.enabled = !flash;
        bool local = CityActivityStreaming.InView(van.position, .28f) || CityActivityStreaming.InView(cordon.Location, .18f);
        if (local && announceArrival)
        {
            sirenUntil = Mathf.Max(sirenUntil, Time.time + 7);
            announceArrival = false;
        }
        bool audible = local && Time.time < sirenUntil && GameFeedback.SfxGain > 0;
        siren.volume = .31f * GameFeedback.SfxGain;
        if (audible)
        {
            if (audioPaused) siren.UnPause();
            else if (!siren.isPlaying) siren.Play();
        }
        else if (siren.isPlaying || audioPaused) siren.Stop();
        SirenPlayed |= siren.isPlaying;
        audioPaused = false;
    }

    void DeployOfficers()
    {
        if(!BattleManager.instance)return;
        officers.RemoveAll(officer => !officer || !officer.IsAlive);
        for (int index = officers.Count; index < 2; index++)
        {
            var post = Post(index);
            var officer = BattleManager.instance.SpawnMatchdayFighter(
                post, "POLICE", Uniform, 120, 0, MatchdayUnitRole.Police, "CORDON DETAIL",true);
            if (!officer) break;
            var march = officer.GetComponent<MatchdayMarch>();
            if (march) march.enabled = false;
            officer.HoldFire(true);
            officers.Add(officer);
        }
        foreach (var officer in officers)
        {
            var street = StadiumMatchdayActivity.Instance.StreetOperations;
            if (street.IsPursuing(officer) || cordon.Saved.clearanceRequested) continue;
            var post = Post(officers.IndexOf(officer));
            if (Vector3.Distance(officer.transform.position, post) > 2) officer.WalkToRally(post);
        }
    }

    Vector3 Post(int index)
    {
        var besideRoad = (cordon.OfficerWorkPoint - cordon.Location).normalized;
        return cordon.OfficerWorkPoint + besideRoad * .7f + cordon.Direction * (index == 0 ? -2 : 2);
    }

    public void SignalResponse() => sirenUntil = Mathf.Max(sirenUntil, Time.time + 3f);

    public void Withdraw()
    {
        if (departing) return;
        departing = true;
        foreach (var officer in officers)
        {
            if (!officer || !officer.IsAlive) continue;
            StadiumMatchdayActivity.Instance?.StreetOperations?.ReleaseCordonOfficer(officer);
            BattleManager.instance?.DespawnMatchdayFighter(officer);
        }
        officers.Clear();
        var path = new NavMeshPath();
        if (!van || !NavMesh.CalculatePath(van.position, origin, RoadMask, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            Destroy(gameObject);
            return;
        }
        route = path.corners;
        corner = 1;
    }

    static AudioClip GetSiren()
    {
        if (sirenClip) return sirenClip;
        // Whole cycles and a short loop seam keep the synthesized two-tone siren click-free.
        const int rate = 22050;
        var samples = new float[rate * 2];
        float phase = 0;
        for (int index = 0; index < samples.Length; index++)
        {
            float seconds = index / (float)rate;
            float tone = Mathf.SmoothStep(670, 955, .5f + .5f * (float)System.Math.Tanh(7 * Mathf.Sin(seconds * Mathf.PI * 2)));
            phase += tone * (2 * Mathf.PI / rate);
            float seam = Mathf.Clamp01(Mathf.Min(index, samples.Length - index - 1) / 120f);
            samples[index] = seam * (.52f * Mathf.Sin(phase) + .12f * Mathf.Sin(2 * phase) + .065f * Mathf.Sin(3 * phase));
        }
        sirenClip = AudioClip.Create("Matchday two-tone siren", samples.Length, 1, rate, false);
        sirenClip.SetData(samples, 0);
        return sirenClip;
    }
}

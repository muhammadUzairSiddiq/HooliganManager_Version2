using UnityEngine;
using UnityEngine.AI;

/// <summary>Two staffed approach lanes with a real waiting queue and a measurable throughput goal.</summary>
public sealed class MatchdayGates : MonoBehaviour
{
    public const int AdmissionTarget = 5;
    public const int SideGateCost = 80;
    readonly Transform[] gates = new Transform[2];
    readonly Transform[] arms = new Transform[2];
    readonly Vector3[] exits = new Vector3[2];
    readonly TMPro.TextMeshPro[] signs = new TMPro.TextMeshPro[2];
    static readonly string[] Names = { "MAIN GATE", "SIDE GATE" };
    StadiumMatchdayActivity owner;
    public Vector3 QueuePoint { get; private set; }
    public Vector3 MainGate => exits[0];
    public Vector3 SideGate => exits[1];
    public bool Ready => gates[0] && gates[1];
    public void RemoveWorldGates()
    {
        foreach(var gate in gates)if(gate)Destroy(gate.gameObject);
    }

    public void Initialize(StadiumMatchdayActivity stadium)
    {
        owner = stadium;
        var strategy = MatchdayStrategySystem.Instance;
        if (!strategy) return;
        var prefabUnit = BattleManager.instance.enemyAgentPrefab;
        var unit = prefabUnit ? prefabUnit.GetComponent<NavMeshAgent>() : null;
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = unit ? unit.agentTypeID : 0,
            areaMask = unit ? unit.areaMask : NavMesh.AllAreas
        };
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(strategy.ResponsePoints[0], strategy.EscortPickup, filter, path) ||
            path.status != NavMeshPathStatus.PathComplete) return;
        var outerRoute=path.corners;
        if(outerRoute.Length<2 || PathLength(outerRoute)<42) return;
        // Build the lanes on the connected city side of the stadium approach.
        var main=PointAlong(outerRoute,13);
        QueuePoint=PointAlong(outerRoute,38);
        if(!NavMesh.CalculatePath(main,QueuePoint,filter,path)||path.status!=NavMeshPathStatus.PathComplete)return;
        var mainPath=path.corners;
        if(mainPath.Length<2)return;
        var along = Vector3.ProjectOnPlane(main - QueuePoint, Vector3.up).normalized;
        if (along.sqrMagnitude < .1f) return;
        var across = Vector3.Cross(Vector3.up, along);
        for (int sample = 0; sample < 16; sample++)
        {
            float distance = 19 + sample / 4 * 5;
            var wanted = main + across * (sample % 2 == 0 ? distance : -distance) + along * (sample % 4 < 2 ? -6 : 1);
            if (!NavMesh.SamplePosition(wanted, out var side, 7, filter) ||
                Mathf.Abs(side.position.y - main.y) > 2 ||
                CityBusinessSystem.HorizontalDistance(main, side.position) < 17 ||
                CityBusinessSystem.HorizontalDistance(QueuePoint, side.position) < 19 ||
                !NavMesh.CalculatePath(side.position, QueuePoint, filter, path) ||
                path.status != NavMeshPathStatus.PathComplete) continue;
            var sidePath = path.corners;
            if (sidePath.Length < 2 || PathLength(sidePath) > 58 ||
                CityBusinessSystem.HorizontalDistance(PointAlong(mainPath, 9), PointAlong(sidePath, 9)) < 12||
                !CityActivityStreaming.TryStreetPoint(PointAlong(sidePath,9),out _,3))
                continue;
            BuildGate(0, mainPath);
            BuildGate(1, sidePath);
            break;
        }
    }

    static float PathLength(Vector3[] corners)
    {
        float length = 0;
        for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
        return length;
    }

    static Vector3 PointAlong(Vector3[] corners, float remaining)
    {
        for (int i = 1; i < corners.Length; i++)
        {
            float length = Vector3.Distance(corners[i - 1], corners[i]);
            if (remaining <= length) return Vector3.MoveTowards(corners[i - 1], corners[i], remaining);
            remaining -= length;
        }
        return corners[corners.Length - 1];
    }

    void BuildGate(int index, Vector3[] approach)
    {
        var point = PointAlong(approach, 9);
        var forward = Vector3.ProjectOnPlane(PointAlong(approach, 8) - PointAlong(approach, 10), Vector3.up);
        var gate = new GameObject(Names[index]).transform;
        gate.SetParent(transform);
        gate.SetPositionAndRotation(point, forward.sqrMagnitude > .01f ? Quaternion.LookRotation(forward) : Quaternion.identity);
        gates[index] = gate;
        exits[index] = approach[0];
        for (int side = -1; side <= 1; side += 2)
        {
            CordonBarrier.Create(gate, Vector3.right * side * 4.7f);
            CordonBarrier.Create(gate, Vector3.right * side * 7.8f);
        }
        arms[index] = CordonBarrier.Create(gate, Vector3.zero);
        signs[index] = ZoneLabelUtil.Create(gate, Names[index], 3.8f, 5f);
        signs[index].fontSize = 6.5f;
        MiniMapIconFactory.Register(gate, MiniMapIconFactory.Kind.Turf, Names[index]);
    }

    public void Tick(MatchdayStreetState state)
    {
        if (!Ready || !owner.EnsureGateQueue(QueuePoint)) return;
        int open = state.gateChoice - 1;
        for (int i = 0; i < gates.Length; i++)
        {
            arms[i].gameObject.SetActive(i != open);
            signs[i].text = Names[i] + (i == open ? " · OPEN" : " · CLOSED");
        }
        if (open < 0 || open >= gates.Length || state.gateAdmitted) return;
        owner.RouteGateQueue(exits[open], Names[open]);
        if (owner.GateQueueAt(exits[open],state.automatic&&!CityActivityStreaming.Interested(exits[open],true)) >= AdmissionTarget)
            state.gateAdmitted = true;
    }
}

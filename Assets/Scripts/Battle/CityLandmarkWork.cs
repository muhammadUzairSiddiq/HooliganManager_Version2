using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Each city job is a short live action at a real building. A fill bar is not the task.
/// </summary>
public sealed class CityLandmarkWork : MonoBehaviour
{
    CityOperationNode node;

    public void Begin(CityOperationNode target, IList<AgentController> crew)
    {
        node = target;
        StopAllCoroutines();
        StartCoroutine(Run(crew));
    }

    IEnumerator Run(IList<AgentController> crew)
    {
        var living = new List<AgentController>();
        if (crew != null)
            foreach (var member in crew)
                if (member && member.IsAlive) living.Add(member);
        if (living.Count == 0 || !node) yield break;

        int flavor = node.Flavor;
        switch (node.Type)
        {
            case CityOperationType.PrepareStadium:
                yield return Circuit(living, flavor == 2 ? 4 : 3, true, flavor == 3 ? "YARD DRILL" : "GYM ROUND");
                break;
            case CityOperationType.SupporterRally:
                yield return Cheer(living, flavor == 4 ? 4 : 3, "CHANT");
                break;
            case CityOperationType.FirstAid:
                yield return Treat(living);
                break;
            case CityOperationType.CommunityEvent:
                yield return Cheer(living, flavor == 1 ? 4 : 3, "CROWD");
                break;
            case CityOperationType.CollectTickets:
                yield return TwoStop(living, transform.position, Side(transform.position, 150f, 5f), "TICKET WINDOW", "PLATFORM", false);
                break;
            case CityOperationType.PrepareTransport:
                yield return TwoStop(living, transform.position, Side(transform.position, 210f, 6f), "ENGINE BAY", "DEPARTURE", flavor == 3);
                break;
            case CityOperationType.GatherSupplies:
                yield return Circuit(living, flavor == 3 ? 4 : 3, false, "PICKUP");
                break;
            case CityOperationType.ObservePolice:
                yield return Watch(living, flavor == 2 ? 5 : 3, "WATCHING THE YARD");
                break;
            default:
                var school = CityLandmarks.School(transform.position + new Vector3(18f, 0f, 8f));
                yield return TwoStop(living, transform.position, school.Point, "LOOKOUT", "FAR WALL", false);
                break;
        }

        if (node && StillWorking(living)) node.FinishLive();
    }

    IEnumerator Circuit(List<AgentController> crew, int stations, bool punches, string label)
    {
        Vector3 center = transform.position;
        for (int station = 0; station < stations; station++)
        {
            if (!StillWorking(crew)) yield break;
            node.SetLiveStatus(node.Title + "\n" + label + " " + (station + 1) + "/" + stations);
            Vector3 pad = Pad(center, station * (360f / stations), 3.6f);
            foreach (var member in crew) member.JobMoveTo(pad + Spread(crew.IndexOf(member)));
            yield return WaitUntilClose(crew, pad, 8f);
            if (!StillWorking(crew)) yield break;
            if (punches)
            {
                foreach (var member in crew) member.PlayStreetPunch();
                yield return new WaitForSeconds(1.15f);
            }
            else yield return new WaitForSeconds(0.8f);
        }
    }

    IEnumerator Cheer(List<AgentController> crew, int beats, string label)
    {
        Vector3 center = transform.position;
        node.SetLiveStatus(node.Title + "\n" + label);
        for (int i = 0; i < crew.Count; i++)
            crew[i].JobMoveTo(Pad(center, i * (360f / Mathf.Max(1, crew.Count)), 2.4f));
        yield return WaitUntilClose(crew, center, 7f);
        for (int beat = 0; beat < beats; beat++)
        {
            if (!StillWorking(crew)) yield break;
            node.SetLiveStatus(node.Title + "\n" + label + " " + (beat + 1) + "/" + beats);
            foreach (var member in crew)
            {
                Face(member, center);
                member.PlayStreetPunch();
            }
            yield return new WaitForSeconds(1.25f);
        }
    }

    IEnumerator Treat(List<AgentController> crew)
    {
        node.SetLiveStatus(node.Title + "\nWARD");
        Vector3 door = transform.position;
        for (int i = 0; i < crew.Count; i++)
            crew[i].JobMoveTo(door + Spread(i));
        yield return WaitUntilClose(crew, door, 8f);
        if (!StillWorking(crew)) yield break;
        foreach (var member in crew)
        {
            if (!member || !member.IsAlive || member.Data == null) continue;
            if (member.Data.MaxStamina < 1f) member.Data.MaxStamina = 100f;
            member.Data.Stamina = Mathf.Min(member.Data.MaxStamina, member.Data.Stamina + 30f);
            member.PlayStreetPunch();
        }
        node.SetLiveStatus(node.Title + "\nPATCHED UP");
        yield return new WaitForSeconds(1.2f);
    }

    IEnumerator TwoStop(List<AgentController> crew, Vector3 first, Vector3 second, string firstLabel, string secondLabel, bool punches)
    {
        node.SetLiveStatus(node.Title + "\n" + firstLabel);
        for (int i = 0; i < crew.Count; i++) crew[i].JobMoveTo(first + Spread(i));
        yield return WaitUntilClose(crew, first, 8f);
        if (!StillWorking(crew)) yield break;
        if (punches) { foreach (var member in crew) member.PlayStreetPunch(); yield return new WaitForSeconds(1f); }
        else yield return new WaitForSeconds(0.7f);
        node.SetLiveStatus(node.Title + "\n" + secondLabel);
        for (int i = 0; i < crew.Count; i++) crew[i].JobMoveTo(second + Spread(i));
        yield return WaitUntilClose(crew, second, 8f);
        if (!StillWorking(crew)) yield break;
        foreach (var member in crew) { Face(member, second); member.PlayStreetPunch(); }
        yield return new WaitForSeconds(0.9f);
    }

    IEnumerator Watch(List<AgentController> crew, int turns, string label)
    {
        Vector3 center = transform.position;
        node.SetLiveStatus(node.Title + "\n" + label);
        for (int i = 0; i < crew.Count; i++) crew[i].JobMoveTo(center + Spread(i));
        yield return WaitUntilClose(crew, center, 7f);
        for (int turn = 0; turn < turns; turn++)
        {
            if (!StillWorking(crew)) yield break;
            node.SetLiveStatus(node.Title + "\n" + label + " " + (turn + 1) + "/" + turns);
            Vector3 look = center + Quaternion.Euler(0f, turn * 70f, 0f) * Vector3.forward * 8f;
            foreach (var member in crew) Face(member, look);
            yield return new WaitForSeconds(1.15f);
        }
    }

    bool StillWorking(List<AgentController> crew)
    {
        if (!node || !node.LiveWork) return false;
        for (int i = crew.Count - 1; i >= 0; i--)
        {
            var member = crew[i];
            if (!member || !member.IsAlive || !member.IsOnLiveJob)
                crew.RemoveAt(i);
        }
        if (crew.Count > 0) return true;
        if (node) node.CancelLive("CREW PULLED OFF THE JOB");
        return false;
    }

    static IEnumerator WaitUntilClose(List<AgentController> crew, Vector3 point, float seconds)
    {
        float left = seconds;
        while (left > 0f)
        {
            left -= Time.deltaTime;
            bool there = true;
            foreach (var member in crew)
            {
                if (!member || !member.IsAlive) continue;
                Vector3 delta = member.transform.position - point;
                delta.y = 0f;
                if (delta.sqrMagnitude > 20f) { there = false; break; }
            }
            if (there) yield break;
            yield return null;
        }
    }

    static Vector3 Pad(Vector3 center, float yaw, float distance)
    {
        Vector3 guess = center + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance;
        return NavMesh.SamplePosition(guess, out var hit, 6f, NavMesh.AllAreas) ? hit.position : center;
    }

    static Vector3 Side(Vector3 center, float yaw, float distance)
    {
        return Pad(center, yaw, distance);
    }

    static Vector3 Spread(int index) => new Vector3((index % 3) * 1.5f - 1.5f, 0f, (index / 3) * 1.5f);

    static void Face(AgentController member, Vector3 point)
    {
        if (!member) return;
        Vector3 dir = point - member.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.05f)
            member.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}

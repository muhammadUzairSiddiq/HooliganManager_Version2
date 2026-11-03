using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MatchdayGroupSave
{
    public string name,color,task;
    public int role,taskIndex,opponent=-1;
    public Vector3 home,destination;
    public bool inConflict;
    public float nextTaskSeconds,conflictSeconds;
    public List<MatchdayMemberSave> members=new List<MatchdayMemberSave>();
}
[Serializable]
public sealed class MatchdayMemberSave
{
    public float hp;
    public Vector3 position;
    public bool wasSpawned;
}

using UnityEngine;
public sealed class MatchdayFlagWave : MonoBehaviour
{
    Quaternion rest;
    void Start(){rest=transform.localRotation;}
    void Update(){transform.localRotation=rest*Quaternion.Euler(0,Mathf.Sin(Time.time*3.5f+transform.position.x)*14f,Mathf.Sin(Time.time*2.2f)*4f);}
}

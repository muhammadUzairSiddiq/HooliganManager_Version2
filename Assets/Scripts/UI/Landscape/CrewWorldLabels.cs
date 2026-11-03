using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>Small non-interactive crew identities, with selected members first.</summary>
public sealed class CrewWorldLabels : MonoBehaviour
{
    readonly Dictionary<AgentController,TextMeshProUGUI> labels=new Dictionary<AgentController,TextMeshProUGUI>();
    readonly List<Rect> occupied=new List<Rect>();
    RectTransform frame;
    float nextRefresh;
    void LateUpdate()
    {
        if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.1f;
        var battle=BattleManager.instance;var cam=Camera.main;
        if(!battle||!cam)return;if(!frame)frame=WorldButtonLayer.Frame();
        foreach(var label in labels.Values)if(label)label.gameObject.SetActive(false);
        occupied.Clear();int shown=0;
        foreach(var member in battle.PlayerAgents.Where(a=>a&&a.IsAlive).OrderByDescending(a=>a.IsSelected))
        {
            var p=cam.WorldToScreenPoint(member.transform.position+Vector3.up*5.5f);
            if(p.z<=0)continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,p,null,out var local);
            float x=local.x-frame.rect.xMin,y=frame.rect.yMax-local.y;
            if(x<80||x>frame.rect.width-80||y<205||y>frame.rect.height-155)continue;
            var area=new Rect(x-90,y-44,180,42);
            if(occupied.Exists(r=>r.Overlaps(area)))continue;
            if(!labels.TryGetValue(member,out var label)||!label)
            {
                label=LandscapeUI.Text("CrewIdentity",frame,"",0,0,180,42,17,LandscapeUI.Green,true);
                label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
                label.outlineColor=Color.black;label.outlineWidth=.22f;
                labels[member]=label;
            }
            string duty=CityOperationsSystem.AssignmentFor(member);
            if(string.IsNullOrEmpty(duty))duty=member.CurrentOrder!=AgentController.StandingOrder.None?member.CurrentOrder.ToString().ToUpperInvariant():member.CurrentState==AgentController.State.MovingToPoint?"MOVING":"READY";
            if(duty.Length>16)duty="ASSIGNED";
            label.text=member.Data.AgentName.ToUpperInvariant()+" · "+(member.IsSelected?"SELECTED":duty)+"\n"+Mathf.CeilToInt(member.CurrentHp)+" HP";
            label.color=LandscapeUI.White;
            LandscapeUI.Place(label.rectTransform,area.x,area.y,area.width,area.height);
            label.gameObject.SetActive(true);occupied.Add(area);if(++shown>=6)break;
        }
    }
    void OnDestroy(){foreach(var label in labels.Values)if(label)Destroy(label.gameObject);}
}

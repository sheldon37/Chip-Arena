using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private sealed class HealthLag {public float Value,Last,Time=-100,Hold;}
        private readonly Dictionary<int,HealthLag> healthLags=new Dictionary<int,HealthLag>();private int hudGeneration=-1;
        private void CombatWorldHud(float scale,float width,float height)
        {
            if(menu||arena?.Local==null||cameraView==null||IntroActive||ReplayActive)return;
            if(hudGeneration!=arena.Generation){healthLags.Clear();hudGeneration=arena.Generation;}
            var physics=arena.Root.scene.GetPhysicsScene();var old=GUI.color;
            bool ScreenPoint(Vector3 p,float range,out Vector3 point){point=cameraView.WorldToScreenPoint(p)/scale;if(point.z<=0||Vector3.Distance(cameraView.transform.position,p)>range)return false;Vector3 delta=p-cameraView.transform.position;return !physics.Raycast(cameraView.transform.position,delta.normalized,out _,delta.magnitude-.08f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);}
            void RectColor(Rect rect,Color color){GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
            void Health(int id,Vector3 pos,float hp,float max,float worldWidth,bool ally,string name,bool lag)
            {
                if(hp<=0||!ScreenPoint(pos,55,out var at))return;float ratio=Mathf.Clamp01(hp/Math.Max(1,max)),pixels=Mathf.Clamp(worldWidth*Screen.height/(2*Mathf.Tan(cameraView.fieldOfView*Mathf.Deg2Rad*.5f)*Math.Max(1,Vector3.Distance(cameraView.transform.position,pos)))/scale,16,85),x=at.x-pixels*.5f,y=height-at.y;
                RectColor(new Rect(x-2,y-2,pixels+4,8),new Color(.03f,.04f,.07f,.8f));
                if(lag){if(!healthLags.TryGetValue(id,out var value)){value=new HealthLag();healthLags[id]=value;}float dt=arena.Time-value.Time;value.Time=arena.Time;if(dt<0||dt>.35f||ratio>=value.Value){value.Value=ratio;value.Hold=0;}else{value.Hold=ratio<value.Last?0:value.Hold+Math.Min(dt,.05f);if(value.Hold>.22f)value.Value=Math.Max(ratio,value.Value-Math.Min(dt,.05f)*.9f);}value.Last=ratio;RectColor(new Rect(x,y,pixels*value.Value,4),new Color(1,.85f,.63f));}
                RectColor(new Rect(x,y,pixels*ratio,4),ratio<.25f?new Color(1,.45f,.5f):ally?new Color(.35f,.87f,.51f):new Color(1,.35f,.28f));if(!string.IsNullOrEmpty(name)&&Vector3.Distance(cameraView.transform.position,pos)<32){var style=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=12};GUI.Label(new Rect(at.x-110,y-27,220,23),name,style);}
            }
            foreach(var p in arena.Pilots){if(!p.Seat.connected||prefs.firstPerson&&p==arena.ViewPilot||p.Seat.AuthorityBoss&&arena.Pve?.Hidden==true)continue;bool ally=p==arena.Local||match.SameSide(p.Seat,arena.Local.Seat);Health(p.Seat.id,p.Body.transform.position+Vector3.up*1.3f*(p.Seat.spec=="siren"?p.Pose.RigScale:1),p.Hp,(float)p.Stats["hp"],.72f,ally,match.Teams?(ally?"ALLY · ":"ENEMY · ")+p.Seat.name:p.Seat.name,true);}
            foreach(var m in arena.Minions)if(m.Hp<m.MaxHp)Health(m.Id,m.Body.transform.position+Vector3.up*(m.Kind=="slender"?1.52f:m.Kind=="toy"?.85f:1.2f),m.Hp,m.MaxHp,m.Kind=="toy"?.42f:.6f,m.Owner==arena.Local||match.SameSide(m.Owner.Seat,arena.Local.Seat),null,false);
            foreach(var e in arena.Events){float age=arena.Time-e.Time;if(age<0||age>.8f||e.Source!=arena.LocalId||(e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot)||e.Amount<=0)continue;if(ScreenPoint(NativeArena.Vec(e.Position)+Vector3.up*(.9f+age*.7f),55,out var point)){GUI.color=new Color(1,e.Kind==EventKind.Headshot?.45f:.9f,.5f,1-age/.8f);GUI.Label(new Rect(point.x-32,height-point.y,80,30),Math.Ceiling(e.Amount).ToString(),small);}}
            GUI.color=old;
            GUI.Label(new Rect(width/2-260,height-235,520,65),string.Join("\n",presentation.DeathMessages(arena.Time)),small);
            var local=arena.Local;if(local.Controlling!=null)GUI.Label(new Rect(width/2-180,140,360,40),"CONTROLLING "+local.Controlling.Seat.name,label);else if(arena.Time<local.FrozenUntil)GUI.Label(new Rect(width/2-180,140,360,40),"ADMIN PAUSE · "+(local.FrozenUntil-arena.Time).ToString("0.0")+"s",label);
            if(arena.Pve!=null)
            {
                var row=arena.Pve.Rows.TryGetValue(local.Seat.id,out var r)?r:null;
                foreach(var m in arena.Pve.Content.machines)if(ScreenPoint(new Vector3(m.x,2.62f,m.z),24,out var point)){int price=m.kind=="chip"?NativePveRules.ChipPrice(row?.Bought??0):m.kind=="recruit"?300:75;string value=m.kind=="med"&&row?.HealReady>arena.Time?Math.Ceiling(row.HealReady-arena.Time)+"s":"$"+price;var style=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};GUI.Label(new Rect(point.x-80,height-point.y,160,50),m.kind.ToUpperInvariant()+"\n"+value,style);if(Vector3.Distance(local.Body.position,NativeArena.Vec(m.front.Position(.9f)))<2.2f)GUI.Label(new Rect(width/2-170,height-165,340,32),"F · "+(m.kind=="chip"?"Buy Chip":m.kind=="recruit"?"Hire recruit":"Heal")+"  "+value,label);}
                foreach(var k in arena.Pve.Pickups)if(k.Owner==local&&arena.Time>=k.Born&&ScreenPoint(k.Position+Vector3.up*.65f,30,out var point))GUI.Label(new Rect(point.x-40,height-point.y,90,25),"YOURS",small);
            }
        }
    }
}

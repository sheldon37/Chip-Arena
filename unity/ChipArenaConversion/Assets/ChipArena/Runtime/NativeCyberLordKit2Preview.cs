using System;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Dev Sandbox's "Preview Cyber Kit v2 ranged" (developer sandbox only; Claude's presentation, docs/CYBER-LORD.md
    // "Kit v2"): until Astra's v2 record replicates, the local Cyber Lord, once in ranged mode (key 5), loops Kit v2's ranged
    // set on its own screen: six Plasma Byte throws (two Overflows), Daisy Chain over the nearest pilots on the aim (or an arc of
    // points when none), Singularity where the aim meets the world, Packet Storm on the aimed floor point and Orbital Uplink
    // steered by the live aim. Each cast is written as the proposed host would write the record (Core CyberKit2RangedPreview
    // through CyberLordFeed.Kit2RangedSource), so the poses (over the pilot's real movement: walk, run and strafe while it
    // plays), the effects, the HUD cards and the sounds all show it. Nothing reaches the authority, the actor, the network
    // frame, damage or movement.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The Dev Sandbox's Kit v2 ranged preview (see above). Off everywhere else.</summary>
        public bool CyberKit2RangedPreviewOn;
        private readonly CyberKit2RangedPreview cyberKit2R=new CyberKit2RangedPreview();
        private float cyberKit2RStart=-100;private int cyberKit2RBeat;
        /// <summary>The loop's beats (s from its start) and what each casts: 0 a throw, 1 Daisy Chain, 2 Singularity, 3 Packet
        /// Storm, 4 Orbital Uplink.</summary>
        private static readonly float[] CyberKit2RAt={.4f,.7f,1f,1.3f,1.6f,1.9f,3f,4.4f,8.6f,11.2f};
        private static readonly int[] CyberKit2Kind={0,0,0,0,0,0,1,2,3,4};
        private const float CyberKit2RPeriod=17;
        private readonly int[] chainIds=new int[CyberKit2Ranged.MaxChain];private readonly NVector[] chainPoints=new NVector[CyberKit2Ranged.MaxChain];
        private void CyberKit2RStep(Snapshot s,float time)
        {
            Actor me=null;if(s?.Actors!=null)foreach(var a in s.Actors)if(a!=null&&a.Id==LocalId){me=a;break;}
            if(!CyberKit2RangedPreviewOn||me==null||!CyberLordFeed.Handles(me))
            {
                if(cyberKit2R.Any)cyberKit2R.Clear();if(CyberLordFeed.Kit2RangedSource==cyberKit2R)CyberLordFeed.Kit2RangedSource=null;cyberKit2RStart=-100;return;
            }
            CyberLordFeed.Kit2RangedSource=cyberKit2R;cyberKit2R.For(me.Id);
            // The loop plays in ranged mode only (press 5): it waits, and starts over, in melee.
            bool ranged=me.Alive&&cyberViews.TryGetValue(me.Id,out var v)&&v.ModeWeight(time)>.99f;
            if(!ranged){cyberKit2RStart=-100;cyberKit2R.Advance(time);return;}
            if(cyberKit2RStart<-90||time<cyberKit2RStart-.25f||time-cyberKit2RStart>=CyberKit2RPeriod){cyberKit2RStart=time;cyberKit2RBeat=0;}
            float t=time-cyberKit2RStart;
            while(cyberKit2RBeat<CyberKit2RAt.Length&&t>=CyberKit2RAt[cyberKit2RBeat]){CyberKit2Cast(s,me,CyberKit2Kind[cyberKit2RBeat],time);cyberKit2RBeat++;}
            // The column follows the live aim (the host's tick, written each frame here).
            var feet=visualPositions.TryGetValue(me.Id,out var vp)?vp:Vec(me.Position);cyberKit2R.Steer(me.Id,time,N2(CyberKit2Floor(me,feet,CyberKit2Spec.OrbitalRange)),N2(feet));
            cyberKit2R.Advance(time);
        }
        private static NVector N2(Vector3 v)=>new NVector(v.x,v.y,v.z);
        private void CyberKit2Cast(Snapshot s,Actor me,int kind,float time)
        {
            var feet=visualPositions.TryGetValue(me.Id,out var vp)?vp:Vec(me.Position);float yaw=me.Yaw*Mathf.Deg2Rad,pitch=Mathf.Clamp(me.Pitch,-75,75)*Mathf.Deg2Rad;
            var aim=new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch));var flat=new Vector3(Mathf.Sin(yaw),0,Mathf.Cos(yaw));var right=new Vector3(flat.z,0,-flat.x);
            Vector3 Hand(bool l){if(rigs.TryGetValue(me.Id,out var rig)&&rig?.Bones!=null)return rig.Bones[l?CapturedRig.LHand:CapturedRig.RHand].position+aim*.1f;return feet+Vector3.up*1.3f+flat*.42f+right*(l?-.12f:.12f);}
            switch(kind)
            {
                case 0:
                {
                    // The throw leaves the next hand (both for an Overflow), from the body's centre line like the host's.
                    var from=feet+Vector3.up*(CyberLordFeed.BodyCentre+.3f);float end=CyberRangedEnd(s,me,from,aim,CyberKit2Spec.PlasmaRange,out bool pilot,out _);
                    cyberKit2R.Plasma(me.Id,time,N2(from),N2(aim),end,pilot);break;
                }
                case 1:
                {
                    // The first pilot near the aim (within its range and a lenient cone), then hops to the nearest not yet struck.
                    int n=0;var from=Hand(false);var at=from;
                    if(s?.Actors!=null)
                    {
                        Actor best=null;float bestDot=Mathf.Cos(14*Mathf.Deg2Rad);
                        foreach(var a in s.Actors){if(a==null||a.Id==me.Id||!a.Alive)continue;var c=CyberKit2Chest(a);var d=c-from;float dl=d.magnitude;if(dl<.5f||dl>CyberKit2Spec.ChainRange)continue;float dot=Vector3.Dot(d/dl,aim);if(dot>bestDot){bestDot=dot;best=a;}}
                        while(best!=null&&n<CyberKit2Ranged.MaxChain)
                        {
                            chainIds[n]=best.Id;at=CyberKit2Chest(best);chainPoints[n]=N2(at);n++;Actor next=null;float near=CyberKit2Spec.HopRange;
                            foreach(var a in s.Actors){if(a==null||a.Id==me.Id||!a.Alive||Struck(a.Id,n))continue;float dl=(CyberKit2Chest(a)-at).magnitude;if(dl<near){near=dl;next=a;}}
                            best=next;
                        }
                    }
                    // None near the aim: an arc of points ahead, so the chain still shows.
                    if(n==0)for(int i=0;i<3;i++){var p=feet+flat*(7+4*i)+right*((i%2==0?1:-1)*2.5f)+Vector3.up*1.1f;chainIds[n]=0;chainPoints[n]=N2(p);n++;}
                    cyberKit2R.Chain(me.Id,time,N2(from),chainIds,chainPoints,n);break;
                }
                case 2:
                {
                    // The orb stops where the aim meets the world or a pilot (a floor lifts the hole off it).
                    var from=(Hand(true)+Hand(false))*.5f;float end=CyberRangedEnd(s,me,from,aim,CyberKit2Spec.OrbRange,out bool pilot,out var hit);var stop=pilot?hit:from+aim*end;
                    RaycastHit below=default;bool floor=!pilot&&CyberPhysics().Raycast(stop+Vector3.up*.1f,Vector3.down,out below,1.2f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);
                    cyberKit2R.Singularity(me.Id,time,N2(from),N2(floor?below.point:stop),floor);break;
                }
                case 3:cyberKit2R.Storm(me.Id,time,N2(CyberKit2Floor(me,feet,CyberKit2Spec.StormRange)),(int)(time*977)&0x7fff);break;
                case 4:cyberKit2R.Orbital(me.Id,time,N2(CyberKit2Floor(me,feet,CyberKit2Spec.OrbitalRange)));break;
            }
        }
        private bool Struck(int id,int n){for(int i=0;i<n;i++)if(chainIds[i]==id)return true;return false;}
        private Vector3 CyberKit2Chest(Actor a)=>(visualPositions.TryGetValue(a.Id,out var p)?p:Vec(a.Position))+Vector3.up*1.05f;
        // The aimed floor point within `range`: where the aim meets the world (then down to its floor), else straight ahead.
        private Vector3 CyberKit2Floor(Actor me,Vector3 feet,float range)
        {
            float yaw=me.Yaw*Mathf.Deg2Rad,pitch=Mathf.Clamp(me.Pitch,-75,75)*Mathf.Deg2Rad;var aim=new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch));
            var eye=feet+Vector3.up*1.6f;var physics=CyberPhysics();var point=feet+new Vector3(aim.x,0,aim.z).normalized*Mathf.Min(14,range);
            if(physics.Raycast(eye,aim,out var hit,range,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))point=hit.point;
            point.y=physics.Raycast(point+Vector3.up*.5f,Vector3.down,out var floor,12,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)?floor.point.y:feet.y;return point;
        }
    }
}

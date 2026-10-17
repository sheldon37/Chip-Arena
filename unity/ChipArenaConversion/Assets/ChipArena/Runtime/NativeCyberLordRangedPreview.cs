using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Cyber Lord's ranged mode in the arena before its rules land (Claude's presentation; docs/CYBER-LORD.md "Ranged mode",
    // Core/CyberLordRanged.cs). Two parts:
    //  - the scene's hardlight shots for the effects (ICyberShots): replicated bullets of Spec cyberlord with a ranged Detail
    //    (the contract's Bit Bolts; the default shot drawer skips them) and the preview's;
    //  - the Dev Sandbox's "Preview Cyber Ranged" (developer sandbox only): the local Cyber Lord loops the whole set on its own
    //    screen (the key-5 switch, a Bit Bolt burst, Ping Lance, Lag Field, Proxy Swap, Root Access and the switch back), each
    //    cast aimed where the pilot looks and ended on the world or a pilot, written as the proposed host would write the
    //    record (CyberRangedPreview through CyberLordFeed.RangedSource), so the pose, the look, the effects, the HUD and the
    //    sounds all show it. Nothing reaches the authority, the actor, the network frame, damage or movement: the swap's
    //    blink is drawn, the pilot stays where it is.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The Dev Sandbox's "Preview Cyber Ranged" (see above). Off everywhere else; real replicated casts win once
        /// the rules write them (the feed reads the actor's own mode fields when no preview record exists).</summary>
        public bool CyberRangedPreviewOn;
        private readonly CyberRangedPreview cyberRanged=new CyberRangedPreview();
        private NativeArena cyberArena;private float cyberRangedStart=-100;private int cyberRangedBeat;
        /// <summary>The preview loop's beats (s from its start) and what each casts: 0 the switch, 1 a bolt, 2-5 the 1-4 set.</summary>
        private static readonly float[] CyberRangedAt={.2f,.8f,1.06f,1.32f,1.58f,1.84f,2.1f,3f,4.6f,6.6f,8.4f,11f};
        private static readonly int[] CyberRangedKind={0,1,1,1,1,1,1,2,3,4,5,0};
        private const float CyberRangedPeriod=13;
        private void CyberRangedStep(Snapshot s,float time)
        {
            Actor me=null;if(s?.Actors!=null)foreach(var a in s.Actors)if(a!=null&&a.Id==LocalId){me=a;break;}
            if(!CyberRangedPreviewOn||me?.Cyber?.Ranged!=null)
            {
                if(cyberRanged.Any)cyberRanged.Clear();if(CyberLordFeed.RangedSource==cyberRanged)CyberLordFeed.RangedSource=null;cyberRangedStart=-100;return;
            }
            CyberLordFeed.RangedSource=cyberRanged;
            if(me!=null&&CyberLordFeed.Handles(me)&&me.Alive)
            {
                // A new loop (or a rewound clock) starts in melee mode.
                if(cyberRangedStart<-90||time<cyberRangedStart-.25f||time-cyberRangedStart>=CyberRangedPeriod)
                {cyberRangedStart=time;cyberRangedBeat=0;var r=cyberRanged.For(me.Id);if(r.Ranged){r.Ranged=false;r.ModeAt=r.ModeReady=-100;}}
                float t=time-cyberRangedStart;
                while(cyberRangedBeat<CyberRangedAt.Length&&t>=CyberRangedAt[cyberRangedBeat]){CyberRangedCast(s,me,CyberRangedKind[cyberRangedBeat],time);cyberRangedBeat++;}
            }
            cyberRanged.Advance(time);
        }
        // One preview cast along the pilot's aim, ended on the world (the arena's world mask) or the nearest pilot on the line.
        private void CyberRangedCast(Snapshot s,Actor me,int kind,float time)
        {
            var feet=visualPositions.TryGetValue(me.Id,out var vp)?vp:Vec(me.Position);float yaw=me.Yaw*Mathf.Deg2Rad,pitch=Mathf.Clamp(me.Pitch,-75,75)*Mathf.Deg2Rad;
            var aim=new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch));var flat=new Vector3(Mathf.Sin(yaw),0,Mathf.Cos(yaw));var right=new Vector3(flat.z,0,-flat.x);
            var r=cyberRanged.For(me.Id);bool left=kind==1&&r.BoltHand==0;
            Vector3 Hand(bool l){if(rigs.TryGetValue(me.Id,out var rig)&&rig?.Bones!=null)return rig.Bones[l?CapturedRig.LHand:CapturedRig.RHand].position+aim*.09f;return feet+Vector3.up*1.3f+flat*.42f+right*(l?-.12f:.12f);}
            switch(kind)
            {
                case 0:cyberRanged.Switch(me.Id,time);break;
                case 1:{var from=Hand(left);float end=CyberRangedEnd(s,me,from,aim,CyberRangedSpec.BoltRange,out bool pilot,out _);cyberRanged.Bolt(me.Id,time,N(from),N(aim),end,pilot);break;}
                case 2:{var from=Hand(false);float end=CyberRangedEnd(s,me,from,aim,CyberRangedSpec.LanceRange,out bool pilot,out var at);cyberRanged.Lance(me.Id,time,N(from),N(from+aim*end),N(at),pilot);break;}
                case 3:
                {
                    // The cube lobbed to the floor a dozen metres ahead (the floor found under the point).
                    var point=feet+flat*Mathf.Min(12,CyberRangedSpec.FieldRange);var physics=CyberPhysics();
                    point.y=physics.Raycast(point+Vector3.up*3,Vector3.down,out var hit,8,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)?hit.point.y:feet.y;
                    cyberRanged.Field(me.Id,time,N(Hand(true)),N(point));break;
                }
                case 4:
                {
                    // Straight back (no move input in the loop), stopped half a metre short of a wall.
                    var physics=CyberPhysics();float d=CyberRangedSpec.SwapDistance;
                    if(physics.Raycast(feet+Vector3.up*.9f,-flat,out var hit,d,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))d=Mathf.Max(0,hit.distance-.5f);
                    cyberRanged.Swap(me.Id,time,N(feet),N(feet-flat*d));break;
                }
                case 5:{var from=(Hand(false)+Hand(true))*.5f;float end=CyberRangedEnd(s,me,from,aim,CyberRangedSpec.RootRange,out bool pilot,out var at);cyberRanged.Root(me.Id,time,N(from),N(from+aim*end),N(at),pilot);break;}
            }
        }
        private PhysicsScene CyberPhysics()=>cyberArena?.Root!=null?cyberArena.Root.scene.GetPhysicsScene():Physics.defaultPhysicsScene;
        // The distance along the aim to the first wall or pilot (within `range`); `pilot` and its chest point when a pilot ends it.
        private float CyberRangedEnd(Snapshot s,Actor me,Vector3 from,Vector3 dir,float range,out bool pilot,out Vector3 at)
        {
            float end=range;pilot=false;at=from+dir*range;
            if(CyberPhysics().Raycast(from,dir,out var hit,range,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))end=hit.distance;
            if(s?.Actors!=null)foreach(var a in s.Actors)
            {
                if(a==null||a.Id==me.Id||!a.Alive)continue;var c=(visualPositions.TryGetValue(a.Id,out var p)?p:Vec(a.Position))+Vector3.up*1.05f;
                float along=Vector3.Dot(c-from,dir);if(along<=0||along>=end)continue;if((from+dir*along-c).sqrMagnitude>.45f*.45f)continue;end=along;pilot=true;at=c;
            }
            if(!pilot)at=from+dir*end;return end;
        }

        // The scene's hardlight shots (ICyberShots on CyberScene): the replicated bolts, then the preview's.
        private sealed class CyberShotList
        {
            private readonly List<CyberShot> bolts=new List<CyberShot>(32);private CyberRangedPreview preview;private CyberKit2RangedPreview kit2;
            public void Bind(NativeArena arena,CyberRangedPreview pv,CyberKit2RangedPreview k2=null)
            {
                bolts.Clear();preview=pv!=null&&pv.Any?pv:null;kit2=k2!=null&&k2.Any?k2:null;if(arena?.Bullets==null)return;
                foreach(var b in arena.Bullets)
                {
                    // (Kind: a v1 Bit Bolt, or Kit v2's Plasma Byte / Overflow by its Detail.)
                    int slot=CyberLordFeed.ShotSlot(b.Spec,b.Detail);if(slot<0)continue;
                    bolts.Add(new CyberShot{Id=b.Id,Owner=b.Owner,Slot=slot,Kind=CyberKit2RangedFeed.ShotKind(b.Detail),Position=N(b.Position),Velocity=N(b.Velocity),Born=b.Born});
                }
            }
            private int Previewed=>preview?.CyberShots??0;
            public int Shots=>bolts.Count+Previewed+(kit2?.CyberShots??0);
            public CyberShot ShotAt(int i)=>i<bolts.Count?bolts[i]:i<bolts.Count+Previewed?preview.CyberShotAt(i-bolts.Count):kit2.CyberShotAt(i-bolts.Count-Previewed);
            public int Impacts=>(preview?.CyberImpacts??0)+(kit2?.CyberImpacts??0);
            public CyberImpact ImpactAt(int i){int n=preview?.CyberImpacts??0;return i<n?preview.CyberImpactAt(i):kit2.CyberImpactAt(i-n);}
        }
    }
}

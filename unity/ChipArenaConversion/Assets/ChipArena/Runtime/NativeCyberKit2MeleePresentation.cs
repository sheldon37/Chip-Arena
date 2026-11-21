using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;

namespace ChipArena.UnityConversion
{
    // Kit v2's melee and shared slots in the arena (Claude's presentation, docs/CYBER-LORD.md "Kit v2"; Astra builds the rules
    // and the record). Live only where a pilot's v2 record has Version 2 (Core/CyberKit2Melee.cs, through CyberLordFeed); until
    // her host writes it, the Dev Sandbox's "Preview Cyber Kit v2" writes one for the local pilot. Here:
    //  - the Fork Bomb's clones (CyberGhost.Fork): stand-in actors posed on rigs of their own from their records (the caster's
    //    swing with a lag), drawn as see-through ghosts of the pilot model with the look's pieces;
    //  - the preview loop (developer sandbox only): every v2 melee and shared ability in turn on the local pilot, written as the
    //    proposed host would write the record, the drawn body moved along the zip, the dash and the meteor's arc (the authority's
    //    pilot never moves: presentation only) and the dummy hauled in by the yank;
    //  - the screen: the Overclock's RGB-split scan bars along the screen's edges and the Glitch Cut's freeze-frame flash;
    //  - the audio: the v2 state cues and event cues (Core/CyberKit2MeleeSounds.cs).
    // Nothing here reaches the authority, the actor, the network frame, damage or movement.
    public sealed partial class ArenaPresentation
    {
        // ---- the Fork Bomb's clones ----
        private readonly CapturedAnimation.Player[] cyberForkRigs=new CapturedAnimation.Player[CyberKit2Spec.Clones];
        private readonly Actor[] cyberForkActors={new Actor(),new Actor(),new Actor()};
        /// <summary>Poses fork clone `g` on its own rig and queues it as a ghost (with the look's pieces); false when it cannot.</summary>
        private bool CyberForkGhost(in CyberGhost g,Snapshot s,string pilot,GhostPose gp,float time)
        {
            if(g.Index<0||g.Index>=cyberForkRigs.Length)return false;Actor caster=null;foreach(var a in s.Actors)if(a!=null&&a.Id==g.Actor){caster=a;break;}
            var clone=cyberForkActors[g.Index];NativeCyberLordFx.ForkActor(clone,caster,g);var rig=cyberForkRigs[g.Index]??=captured.Create(transform,-830-g.Index);
            rig.ActorPose(clone,new Vector3(g.Position.X,g.Position.Y,g.Position.Z),time);gp.Root=rig.Root.transform.localToWorldMatrix;for(int b=0;b<22&&b<rig.Bones.Length;b++)gp.Bones[b]=rig.Bones[b].localToWorldMatrix;
            // The model's live ghost where the caster wears the model (NativeVisualBodyPresentation.cs), else the voxel one.
            var tint=new Color(g.Tint.X,g.Tint.Y,g.Tint.Z,0);
            if(!(VisualBodyPosed(rigs.TryGetValue(g.Actor,out var own)?own:null)&&VisualLiveGhostDraw(6L<<40|(long)g.Index<<32|(uint)g.Actor,rig,g.Actor,rig.Pose,Vector3.zero,"NativeWeaponGhost",tint,g.Opacity)))
                sourceModels.Queue(Ghost(BodyModel(BodySkin(g.Actor),BodyTier(rig.Root.transform.position,false)))??pilot,gp.Root,gp.Bone,null,0,null,tint,null,g.Opacity);
            var flare=new NativeCyberLordLook.Flare{Left=.6f,Right=.6f};int n=NativeCyberLordLook.PiecesIn(CyberSkin(null,g.Actor),g.Actor*3+g.Index+17,time,-100,flare,false,g.Opacity,.3f,true,cyberPieces,default);
            for(int k=0;k<n;k++)QueueCyberPiece(cyberPieces[k],rig.Bones[cyberPieces[k].Bone].localToWorldMatrix,1,0,null);return true;
        }
        private void CyberKit2Clear(){for(int i=0;i<cyberForkRigs.Length;i++){cyberForkRigs[i]?.Dispose();cyberForkRigs[i]=null;}cyberKit2.Clear();cyberKit2Shift.Clear();cyberKit2LastShift.Clear();CyberKit2Melee.Clones.Clear();cyberKit2Start=-100;if(CyberLordFeed.Kit2MeleeSource==cyberKit2)CyberLordFeed.Kit2MeleeSource=null;}

        // ---- the Dev Sandbox preview ----
        /// <summary>The Dev Sandbox's "Preview Cyber Kit v2" (developer sandbox only; see above). Off everywhere else.</summary>
        public bool CyberKit2MeleePreviewOn;
        private readonly CyberKit2MeleeSource cyberKit2=new CyberKit2MeleeSource();
        private readonly Dictionary<int,Vector3> cyberKit2Shift=new Dictionary<int,Vector3>(),cyberKit2LastShift=new Dictionary<int,Vector3>();
        private float cyberKit2Start=-100;private int cyberKit2Beat;private Vector3 cyberKit2Feet;private float cyberKit2Yaw;private int cyberKit2Target;private Vector3 cyberKit2TargetFeet;
        /// <summary>The loop's beats (s from its start) and what each does (CyberKit2Beat), and its period.</summary>
        private static readonly float[] CyberKit2At={.3f,2.4f,4.4f,6.2f,6.8f,8.6f,9.3f,11.3f,12.6f,14.6f};
        private const int BeatCombo=0,BeatZip=1,BeatYank=2,BeatOverclock=3,BeatOverclockCombo=4,BeatFork=5,BeatForkCombo=6,BeatForkBurst=7,BeatCut=8,BeatMeteor=9;
        private const float CyberKit2Period=17;
        private readonly float[] cyberKit2ComboAt=new float[CyberKit2Spec.ComboSteps];private int cyberKit2ComboSteps;private float cyberKit2ComboRate=1;
        private void CyberKit2Step(Snapshot s,float time)
        {
            Actor me=null;if(s?.Actors!=null)foreach(var a in s.Actors)if(a!=null&&a.Id==LocalId){me=a;break;}
            if(!CyberKit2MeleePreviewOn||me==null)
            {
                if(cyberKit2.Any||cyberKit2Shift.Count>0){cyberKit2.Clear();cyberKit2Shift.Clear();}if(CyberLordFeed.Kit2MeleeSource==cyberKit2)CyberLordFeed.Kit2MeleeSource=null;cyberKit2Start=-100;return;
            }
            CyberLordFeed.Kit2MeleeSource=cyberKit2;cyberKit2Shift.Clear();
            if(!CyberLordFeed.Handles(me)||!me.Alive){cyberKit2.Remove(me.Id);cyberKit2Start=-100;return;}
            var r=cyberKit2.For(me.Id);
            // A new loop (or a rewound clock) starts from where the pilot stands now.
            if(cyberKit2Start<-90||time<cyberKit2Start-.25f||time-cyberKit2Start>=CyberKit2Period)
            {
                cyberKit2Start=time;cyberKit2Beat=0;cyberKit2ComboSteps=0;cyberKit2.Remove(me.Id);r=cyberKit2.For(me.Id);cyberKit2Feet=Vec(me.Position);cyberKit2Yaw=me.Yaw;
                cyberKit2Target=0;float best=24;var f=Kit2Flat(cyberKit2Yaw);
                foreach(var a in s.Actors){if(a==null||a.Id==me.Id||!a.Alive)continue;var d=Vec(a.Position)-cyberKit2Feet;float along=Vector3.Dot(d,f),dist=d.magnitude;if(along>1&&dist<best){best=dist;cyberKit2Target=a.Id;cyberKit2TargetFeet=Vec(a.Position);}}
            }
            float t=time-cyberKit2Start;
            while(cyberKit2Beat<CyberKit2At.Length&&t>=CyberKit2At[cyberKit2Beat]){CyberKit2Beat(s,r,cyberKit2Beat,cyberKit2Start+CyberKit2At[cyberKit2Beat]);cyberKit2Beat++;}
            // The string's steps on their beats (each chained at the last's end), the clones copying them.
            for(int i=cyberKit2ComboSteps-1;i>=0;i--)if(time>=cyberKit2ComboAt[i]){if(r.At[CyberKit2Spec.MeleeLmb]!=cyberKit2ComboAt[i]){CyberKit2MeleeFixture.Combo(r,i,cyberKit2ComboAt[i],cyberKit2ComboRate);
                if(r.ForkAt>-90&&time<r.ForkUntil)for(int k=0;k<CyberKit2Spec.Clones;k++)CyberKit2MeleeFixture.ForkStrike(r,k,r.Release[CyberKit2Spec.MeleeLmb]+NativeCyberLordFx.ForkLag+NativeCyberLordFx.ForkLagStep*k,cyberKit2Target,Kit2N(cyberKit2TargetFeet));}break;}
            if(r.ForkAt>-90&&time<r.ForkUntil)CyberKit2MeleeFixture.ForkFollow(r,Kit2N(cyberKit2Feet),cyberKit2Yaw);
            CyberKit2Bodies(r,me.Id,time);
        }
        private static Vector3 Kit2Flat(float yaw){float y=yaw*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(y),0,Mathf.Cos(y));}
        private static NVector Kit2N(Vector3 v)=>new NVector(v.x,v.y,v.z);private static NVector Kit2N(V3 v)=>new NVector(v.x,v.y,v.z);
        private static Vector3 Kit2V(NVector v)=>new Vector3(v.X,v.Y,v.Z);
        private void CyberKit2Combo(float at,int steps,float rate){cyberKit2ComboSteps=steps;cyberKit2ComboRate=rate;float t0=at;for(int i=0;i<steps;i++){cyberKit2ComboAt[i]=t0;t0+=CyberKit2Spec.ComboSeconds[i]/rate;}}
        private void CyberKit2Beat(Snapshot s,CyberKit2MeleeRecord r,int beat,float at)
        {
            var f=Kit2Flat(cyberKit2Yaw);var centre=cyberKit2Feet+Vector3.up*CyberLordFeed.BodyCentre;var physics=CyberPhysics();
            switch(beat)
            {
                case BeatCombo:CyberKit2Combo(at,CyberKit2Spec.ComboSteps,1);break;
                case BeatZip:
                {
                    // Onto the first surface along the facing within the cable's range, else a point 10 m out (a zip into the air).
                    var emit=centre+Vector3.up*CyberKit2Melee.EmitterHeight;var dir=(f+Vector3.up*.08f).normalized;float range=10;
                    if(physics.Raycast(emit,dir,out var hit,CyberKit2Spec.UplinkRange,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))range=hit.distance;
                    CyberKit2MeleeFixture.Uplink(r,at,Kit2N(centre),Kit2N(emit+dir*range),CyberKit2Melee.Zip);break;
                }
                case BeatYank:
                    if(cyberKit2Target!=0)CyberKit2MeleeFixture.Uplink(r,at,Kit2N(centre),Kit2N(cyberKit2TargetFeet+Vector3.up*CyberLordFeed.BodyCentre),CyberKit2Melee.Yank,cyberKit2Target);
                    else CyberKit2MeleeFixture.Uplink(r,at,Kit2N(centre),Kit2N(centre+f*CyberKit2Spec.UplinkRange),CyberKit2Melee.Miss);break;
                case BeatOverclock:CyberKit2MeleeFixture.Overclock(r,at);break;
                case BeatOverclockCombo:CyberKit2Combo(at,3,1+CyberKit2Spec.OverclockRate);break;
                case BeatFork:CyberKit2MeleeFixture.Fork(r,at,Kit2N(cyberKit2Feet),cyberKit2Yaw);break;
                case BeatForkCombo:CyberKit2Combo(at,CyberKit2Spec.ComboSteps,1);break;
                case BeatForkBurst:
                {
                    // The recast: each clone dashes onto the target (round it), or fans out ahead with none.
                    var pts=new NVector[CyberKit2Spec.Clones];for(int k=0;k<pts.Length;k++)pts[k]=cyberKit2Target!=0?Kit2N(cyberKit2TargetFeet)+Kit2N(Kit2Flat(cyberKit2Yaw+180+120*(k-1)))*.6f:Kit2N(cyberKit2Feet)+Kit2N(Kit2Flat(cyberKit2Yaw-40+40*k))*4;
                    if(r.ForkAt>-90&&at<r.ForkUntil)CyberKit2MeleeFixture.ForkBurst(r,at,pts);break;
                }
                case BeatCut:
                {
                    float d=CyberKit2Spec.CutDistance;if(physics.Raycast(centre,f,out var hit,d,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))d=Mathf.Max(0,hit.distance-.5f);
                    var to=centre+f*d;int marked=0;var ids=new int[CyberKit2MeleeRecord.MaxMarks];
                    foreach(var a in s.Actors){if(a==null||a.Id==LocalId||!a.Alive||marked>=ids.Length)continue;var p=Vec(a.Position)+Vector3.up*CyberLordFeed.BodyCentre;var seg=to-centre;float u=Mathf.Clamp01(Vector3.Dot(p-centre,seg)/Mathf.Max(1e-4f,seg.sqrMagnitude));if((centre+seg*u-p).magnitude<=CyberKit2Spec.CutWidth+.4f)ids[marked++]=a.Id;}
                    Array.Resize(ref ids,marked);CyberKit2MeleeFixture.GlitchCut(r,at,Kit2N(centre),Kit2N(to),ids);break;
                }
                case BeatMeteor:
                {
                    var target=cyberKit2Feet+f*10;if(physics.Raycast(target+Vector3.up*6,Vector3.down,out var hit,12,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))target=hit.point;
                    CyberKit2MeleeFixture.Meteor(r,at,Kit2N(centre),Kit2N(target));break;
                }
            }
        }
        // Where the preview draws the bodies the proposed host would move (the local pilot's zip, dash and arc; the yanked pilot),
        // eased back to where the authority holds them after each.
        private void CyberKit2Bodies(CyberKit2MeleeRecord r,int me,float time)
        {
            var centre=Kit2N(cyberKit2Feet+Vector3.up*CyberLordFeed.BodyCentre);NVector? body=null;float back=.25f;
            int Cut=CyberKit2Spec.Cut,M=CyberKit2Spec.Meteor;
            if(r.UplinkOutcome==CyberKit2Melee.Zip&&r.UplinkLatchAt>-90&&time>=r.UplinkLatchAt&&time<r.UplinkArriveAt+.5f+back)
            {var stop=CyberKit2Melee.ZipBody(Kit2N(r.UplinkFrom),Kit2N(r.UplinkAnchor),r.UplinkLatchAt,r.UplinkArriveAt,Mathf.Min(time,r.UplinkArriveAt));body=NVector.Lerp(stop,centre,Mathf.Clamp01((time-r.UplinkArriveAt-.5f)/back));}
            if(r.At[Cut]>-90&&time>=r.Release[Cut]&&time<r.DetonateAt+.3f+back)
            {var stop=CyberKit2Melee.CutBody(Kit2N(r.CutFrom),Kit2N(r.CutTo),r.Release[Cut],r.Until[Cut],Mathf.Min(time,r.Until[Cut]));body=NVector.Lerp(stop,centre,Mathf.Clamp01((time-r.DetonateAt-.3f)/back));}
            if(r.At[M]>-90&&time>=r.At[M]&&time<r.LandAt+.45f+back)
            {var stop=CyberKit2Melee.MeteorBody(Kit2N(r.MeteorFrom),Kit2N(r.MeteorTarget),r.At[M],Mathf.Min(time,r.LandAt));body=NVector.Lerp(stop,centre,Mathf.Clamp01((time-r.LandAt-.45f)/back));}
            if(body.HasValue)cyberKit2Shift[me]=Kit2V(body.Value-centre);
            if(r.UplinkOutcome==CyberKit2Melee.Yank&&r.UplinkTarget!=0&&r.UplinkLatchAt>-90&&time>=r.UplinkLatchAt&&time<r.UplinkArriveAt+.8f+back)
            {
                var start=Kit2N(cyberKit2TargetFeet+Vector3.up*CyberLordFeed.BodyCentre);var pulled=CyberKit2Melee.YankBody(start,Kit2N(r.UplinkFrom),Kit2N(Kit2Flat(cyberKit2Yaw)),r.UplinkLatchAt,time);
                cyberKit2Shift[r.UplinkTarget]=Kit2V(NVector.Lerp(pulled,start,Mathf.Clamp01((time-r.UplinkArriveAt-.8f)/back))-start);
            }
        }
        /// <summary>Render's hooks round the smoothing: the last frame's preview shift comes off the drawn position before it is
        /// smoothed (so it never accumulates or lags), and this frame's goes on after.</summary>
        private Vector3 CyberKit2Unshift(int id,ref Vector3 drawn){if(cyberKit2LastShift.TryGetValue(id,out var last))drawn-=last;return drawn;}
        private Vector3 CyberKit2Place(Actor a,Vector3 smoothed)
        {
            if(!cyberKit2Shift.TryGetValue(a.Id,out var shift)){cyberKit2LastShift.Remove(a.Id);return smoothed;}cyberKit2LastShift[a.Id]=shift;return smoothed+shift;
        }
    }

    public sealed partial class ConversionGame
    {
        /// <summary>The local pilot's v2 screen: the Overclock's edge glitch (RGB-split scan bars creeping in from the edges) and
        /// the Glitch Cut's freeze-frame flash (the caster's and every marked pilot's). Reduced effects: a steady soft edge, no
        /// strobe.</summary>
        private void CyberKit2Screen()
        {
            if(arena==null||presentation==null||Event.current.type!=EventType.Repaint)return;var t=arena.Time;bool reduced=prefs.reducedEffects;float W=U.W,H=U.H;
            var mine=presentation.CyberLordViewOf(arena.LocalId);
            float edge=CyberKit2MeleeHud.EdgeGlitch(mine,t,reduced);
            if(edge>0)
            {
                int step=reduced?0:(int)(t*24);
                for(int i=0;i<10;i++)
                {
                    uint h=(uint)((i*2654435761u)^(uint)(step*97+13));float y=(h%1000)/1000f*H,len=(40+(h>>10)%160)*edge,th=2+(h>>20)%5;
                    bool left=(h&1)==0;var cyan=new Rect(left?0:W-len,y,len,th);var mag=new Rect(left?0:W-len*.8f,y+th+1,len*.8f,Mathf.Max(1,th*.6f));
                    U.Fill(cyan,C.Cyan(.35f*edge));U.Fill(mag,C.Magenta(.3f*edge));
                }
                U.Fill(new Rect(0,0,6*edge,H),C.Cyan(.18f*edge));U.Fill(new Rect(W-6*edge,0,6*edge,H),C.Magenta(.16f*edge));
            }
            float flash=0;foreach(var p in arena.Pilots){var v=p?.Pose!=null?presentation.CyberLordViewOf(p.Pose.Id):null;if(v!=null)flash=Mathf.Max(flash,NativeCyberLordFx.FreezeFlash(v,arena.LocalId,t,reduced));}
            if(flash>0){U.Fill(new Rect(-4,-4,W+8,H+8),new Color(1,1,1,.55f*flash));if(!reduced)for(int i=0;i<4;i++)U.Fill(new Rect(0,H*(.18f+.2f*i),W,3),C.Magenta(.5f*flash));}
        }
    }

    // ---- audio: the v2 cues (existing bank keys, shipped fallbacks) ----
    public sealed partial class NativeAudio
    {
        private readonly Dictionary<int,string[]> cyberKit2Keys=new Dictionary<int,string[]>();
        /// <summary>A Cyber Lord pilot's v2 state cues, each once while fresh (after CyberLordCues has read the view).</summary>
        private void CyberKit2Cues(int id,Vector3 pos,bool mine,float t)
        {
            if(!cyberView.Kit2.Live)return;
            if(!cyberKit2Keys.TryGetValue(id,out var keys)){keys=new string[CyberKit2MeleeSounds.Slots];for(int i=0;i<keys.Length;i++)keys[i]=id+":cyber2-"+i;cyberKit2Keys.Add(id,keys);}
            for(int slot=0;slot<CyberKit2MeleeSounds.Slots;slot++)
            {
                int n=CyberKit2MeleeSounds.State(cyberView,slot,cyberSounds,out float at);if(n==0||!(t>=at&&t-at<.15f)||!Once(keys[slot],at))continue;
                for(int i=0;i<n;i++)Cue(cyberSounds[i].Key,cyberSounds[i].Clip,pos,cyberSounds[i].Gain,mine:mine);
            }
        }
        /// <summary>A fresh v2 event's cues (its Detail).</summary>
        private void CyberKit2Event(in ArenaEvent e,Vector3 pos,bool targetMe,bool mine)
        {
            var hit=CyberKit2MeleeFeed.Hit(e.Detail);if(hit==Kit2Hit.None)return;int n=CyberKit2MeleeSounds.Event(e.Kind,hit,targetMe,cyberSounds);
            for(int i=0;i<n;i++)Cue(cyberSounds[i].Key,cyberSounds[i].Clip,pos,cyberSounds[i].Gain,mine:mine);
        }
    }
}

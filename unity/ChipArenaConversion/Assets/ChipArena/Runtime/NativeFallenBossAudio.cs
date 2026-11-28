using System.Collections.Generic;
using UnityEngine;
using NV=System.Numerics.Vector3;
namespace ChipArena.UnityConversion
{
    // NOCTIEL's sounds (Core NativeFallenBossCues; docs/EXPEDITION-BOSS-FINAL.md section 17): the cutscene on its clock, the fight's
    // timed moments on the arena clock, and what only a change in the replicated state shows. Presentation only: nothing here is
    // read back by a rule. A key with no clips (the batch not imported yet) is silent.
    public sealed partial class ArenaPresentation
    {
        private readonly FallenBossCue[] fbCueBuffer=new FallenBossCue[NativeFallenBossCues.QueueSize];
        private float fbCueIntro=float.NaN,fbCueFight=float.NaN,fbCuePreview=float.NaN;private bool fbCueFlat;private int fbCueFrame;
        private float fbPhaseValue=-100,fbPhaseSeen=-100,fbDraftWas=-100,fbBossHpWas=-1,fbHurtAt=-100,fbPlateAt=-100;private bool fbGlideWas,fbTurnWas;
        private struct FbSeatSeen{public int Lives,Reviver;public float RespawnAt,HushAt,X,Z;}
        private struct FbAddSeen{public string Kind;public float LastAttack,CueAt;public NV At;public bool Alive;public int Frame;}
        private readonly Dictionary<int,FbSeatSeen> fbSeats=new Dictionary<int,FbSeatSeen>();
        private readonly Dictionary<int,FbAddSeen> fbAddsSeen=new Dictionary<int,FbAddSeen>();private readonly List<int> fbAddsGone=new List<int>();
        private void FallenCueReset()
        {
            fbCueIntro=fbCueFight=fbCuePreview=float.NaN;fbCueFlat=false;fbPhaseValue=fbPhaseSeen=fbDraftWas=-100;fbBossHpWas=-1;fbGlideWas=fbTurnWas=false;
            fbSeats.Clear();fbAddsSeen.Clear();
        }
        private void FallenCueCollect(int count){for(int i=0;i<count;i++){var c=fbCueBuffer[i];NativeFallenBossCues.Queue(c.Key,c.At,c.At3,c.Gain,c.Flat);}}
        /// <summary>A footfall the posed body put on the water (FallenFeet).</summary>
        private void FallenStepCue(Vector3 at,float now)=>NativeFallenBossCues.Queue(NativeFallenBossCues.Step,now,ToNV(at),.8f,fbCueFlat);

        /// <summary>This frame's cues, after the pose (FallenBossScenery). previewP: the DEV preview's clock.</summary>
        private void FallenCues(NativeArena a,FallenBossFrame f,float now,bool live,float previewP)
        {
            fbCueFrame++;
            if(!live)
            {
                // The DEV preview, far below the level: everything flat; its cutscene on its own clock (a loop starts it over).
                fbCueFlat=true;fbCueIntro=fbCueFight=float.NaN;
                if(f.Kind==NativeFallenBossPreview.KindCutscene&&!float.IsNaN(fbCuePreview)&&previewP>=fbCuePreview)FallenCueCollect(NativeFallenBossCues.Cutscene(fbCuePreview,previewP,fbCueBuffer));
                fbCuePreview=previewP;return;
            }
            fbCuePreview=float.NaN;var z=a.ZoneBoss;var r=z.fallen;
            if(a.Pve.Holding&&a.Pve.Cinematic=="intro")
            {
                float p=a.Pve.HoldDuration-a.Pve.HoldLeft;
                if(!float.IsNaN(fbCueIntro)&&p>=fbCueIntro)FallenCueCollect(NativeFallenBossCues.Cutscene(fbCueIntro,p,fbCueBuffer));
                fbCueIntro=p;fbCueFlat=true;fbCueFight=float.NaN;return;
            }
            // The hold can end a frame short of the cutscene's last moments (the opening Slash on the drop): they play now.
            if(!float.IsNaN(fbCueIntro))FallenCueCollect(NativeFallenBossCues.Cutscene(fbCueIntro,NativeFallenPilots.CutsceneEnd+.05f,fbCueBuffer));
            fbCueIntro=float.NaN;fbCueFlat=false;
            float t=a.Time;bool first=float.IsNaN(fbCueFight)||t<fbCueFight;
            var m=a.Minions.Find(x=>x.Id==z.actor);var boss=m!=null?ToNV(m.Body.position):f.Boss;var me=a.Local;
            if(r.phaseChangeAt>=0&&r.phaseChangeAt!=fbPhaseValue){fbPhaseValue=r.phaseChangeAt;fbPhaseSeen=t;if(!first)NativeFallenBossCues.Queue(NativeFallenBossCues.Kneel,t,boss);}
            var v=new FallenCueFight{Boss=boss,Listener=me!=null?ToNV(me.Body.position):boss,HasListener=me!=null,Action=z.action,ActionAt=z.actionAt,HitAt=z.hitAt,ActionYaw=z.actionYaw,
                From=new NV(z.laneX0,0,z.laneZ0),To=new NV(z.laneX1,0,z.laneZ1),SecondWing=NativeFallenRules.Chip(r,"second-wing"),Seed=r.patternSeed,Pools=r.pools,
                PhaseChangeAt=r.phaseChangeAt,PhaseSeenAt=fbPhaseSeen,DraftAt=r.draftAt,DeathAt=z.deathAt,Started=r.started,HushAt=-100};
            // The local pilot's stillness clock, while the Hush can open under it.
            if(me!=null&&me.Hp>0&&z.deathAt<0&&r.draftAt<0&&r.phaseChangeAt<0)
                foreach(var s in r.hush)if(s.id==a.LocalId){var q=me.Body.position;q.y=0;v.HushAt=s.hushAt;v.HushSeconds=NativeFallenRules.HushSeconds(r,z.phase,q.magnitude);v.HushPlace=new NV(s.x,0,s.z);break;}
            if(!first)FallenCueCollect(NativeFallenBossCues.Fight(v,fbCueFight,t,fbCueBuffer));
            fbCueFight=t;FallenCueChanges(a,z,r,m,boss,t,first);
        }

        // What only a change shows: a hit on it, its glide and turns, the draft's end, the adds, each pilot's lives and the Hush.
        private void FallenCueChanges(NativeArena a,NativeZoneBossFrame z,NativeFallenRecord r,NativeMinion m,NV boss,float t,bool first)
        {
            // A phase change clears the adds and the draft resets every pilot's stillness: no cries or openings for those.
            bool resumed=fbDraftWas>=0&&r.draftAt<0,calm=r.phaseChangeAt>=0||r.draftAt>=0||resumed;bool on=!first;
            float hp=m!=null?m.Hp:-1;if(on&&fbBossHpWas>=0&&hp>=0&&hp<fbBossHpWas-.5f&&t-fbHurtAt>=1.2f){NativeFallenBossCues.Queue(NativeFallenBossCues.Hurt,t,boss,.8f);fbHurtAt=t;}fbBossHpWas=hp;
            if(on&&fbLive.Gliding&&!fbGlideWas)NativeFallenBossCues.Queue(NativeFallenBossCues.Glide,t,boss);fbGlideWas=fbLive.Gliding;
            if(on&&fbLive.Turning&&!fbTurnWas&&t-fbPlateAt>1f){NativeFallenBossCues.Queue(NativeFallenBossCues.Plate,t,boss,.7f);fbPlateAt=t;}fbTurnWas=fbLive.Turning;
            if(on&&resumed)NativeFallenBossCues.Queue(NativeFallenBossCues.SlashPart,t,boss,.8f);fbDraftWas=r.draftAt;
            // The adds: a Penitent rising, a Throne arriving; each attack (a Cantor's song, a Throne's shot, a Penitent's grip); each death.
            foreach(var add in a.Minions)
            {
                if(add.Kind==null||!add.Kind.StartsWith("fallen-",System.StringComparison.Ordinal))continue;
                var at=ToNV(add.Body.position);bool alive=add.Hp>0;
                if(!fbAddsSeen.TryGetValue(add.Id,out var s))
                {
                    s=new FbAddSeen{Kind=add.Kind,LastAttack=add.LastAttack,CueAt=-100,Alive=alive};
                    if(on&&!calm){if(add.Kind=="fallen-penitent")NativeFallenBossCues.Queue(NativeFallenBossCues.Penitent,t,at);else if(add.Kind=="fallen-throne")NativeFallenBossCues.Queue(NativeFallenBossCues.Throne,t,at,.8f);}
                }
                else
                {
                    if(on&&add.LastAttack>s.LastAttack+.01f)
                    {
                        if(add.Kind=="fallen-throne")NativeFallenBossCues.Queue(NativeFallenBossCues.ThroneFire,t,at);
                        else if(t-s.CueAt>=(add.Kind=="fallen-cantor"?2f:1.5f)){NativeFallenBossCues.Queue(add.Kind=="fallen-cantor"?NativeFallenBossCues.Cantor:NativeFallenBossCues.PenitentLatch,t,at,.85f);s.CueAt=t;}
                    }
                    if(on&&s.Alive&&!alive&&!calm)FallenAddDeath(s.Kind,at,t);
                }
                s.LastAttack=add.LastAttack;s.At=at;s.Alive=alive;s.Frame=fbCueFrame;fbAddsSeen[add.Id]=s;
            }
            fbAddsGone.Clear();
            foreach(var kv in fbAddsSeen)if(kv.Value.Frame!=fbCueFrame){fbAddsGone.Add(kv.Key);if(on&&kv.Value.Alive&&!calm)FallenAddDeath(kv.Value.Kind,kv.Value.At,t);}
            foreach(int id in fbAddsGone)fbAddsSeen.Remove(id);
            // The pilots: the local pilot's feather lost, anyone falling back in, a revive begun and given (heard by the two in it),
            // the water opening under someone who stood still.
            foreach(var s in r.hush)
            {
                NativePilot p=null;foreach(var q in a.Pilots)if(q.Seat.id==s.id){p=q;break;}
                var at=p!=null?ToNV(p.Body.position):new NV(s.x,0,s.z);bool mine=s.id==a.LocalId;
                if(on&&fbSeats.TryGetValue(s.id,out var was))
                {
                    bool involved=mine||s.reviver==a.LocalId||was.Reviver==a.LocalId;
                    if(s.lives<was.Lives&&mine)NativeFallenBossCues.Queue(NativeFallenBossCues.LifeLost,t,at);
                    if(was.RespawnAt>=0&&s.respawnAt<0&&s.lives>0)NativeFallenBossCues.Queue(NativeFallenBossCues.Return,t,at);
                    if(was.Reviver<0&&s.reviver>=0&&involved)NativeFallenBossCues.Queue(NativeFallenBossCues.Revive,t,at);
                    bool revived=was.Lives==0&&s.lives>0;if(revived&&involved)NativeFallenBossCues.Queue(NativeFallenBossCues.ReviveDone,t,at);
                    bool stood=Mathf.Abs(s.x-was.X)<.01f&&Mathf.Abs(s.z-was.Z)<.01f;
                    if(s.hushAt>was.HushAt+.01f&&stood&&!calm&&!revived&&s.lives==was.Lives&&was.Reviver<0&&s.reviver<0)NativeFallenBossCues.Queue(NativeFallenBossCues.HushOpen,t,new NV(s.x,0,s.z));
                }
                fbSeats[s.id]=new FbSeatSeen{Lives=s.lives,Reviver=s.reviver,RespawnAt=s.respawnAt,HushAt=s.hushAt,X=s.x,Z=s.z};
            }
        }
        private static void FallenAddDeath(string kind,NV at,float t)
        {
            if(kind=="fallen-cantor")NativeFallenBossCues.Queue(NativeFallenBossCues.CantorDeath,t,at);
            else if(kind=="fallen-throne")NativeFallenBossCues.Queue(NativeFallenBossCues.ThroneBreak,t,at);
        }
    }
    public sealed partial class NativeAudio
    {
        // ---------- NOCTIEL's cues (Core NativeFallenBossCues: the boss.noctiel.* keys) ----------
        private readonly FallenBossCue[] fallenBossCues=new FallenBossCue[NativeFallenBossCues.QueueSize];
        private void FallenBossCues(NativeArena arena,bool menu)
        {
            int n=NativeFallenBossCues.Drain(fallenBossCues);if(menu||arena==null)return;
            for(int i=0;i<n;i++){var c=fallenBossCues[i];Vector3? place=c.Flat?(Vector3?)null:new Vector3(c.At3.X,c.At3.Y,c.At3.Z);Cue(NativeFallenBossCues.Keys[c.Key],null,place,c.Gain);}
        }
    }
}

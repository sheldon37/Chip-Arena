using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The melee sprint attacks' effects (Core/NativeSprintAttackFx). Each pilot's swings are watched by a
    /// NativeSprintAttackWatch (the rule the rig's sprint attack uses). It draws, through the shared effects renderer:
    /// - a dust burst kicked up round the feet as a swing leaves the sprint;
    /// - weapon-accent speed streaks trailing the pilot through the hit window;
    /// - the greatsword's landing ring (dust rolling out and a ring of accent light spreading to its reach round the feet) as
    ///   its leaping cleave lands and the ring goes live.
    /// Reduced effects and Low graphics thin it. Presentation only: host, guests, killcam and spectators each draw it from what
    /// they present; the swing is the authority's.
    /// </summary>
    public sealed class NativeSprintAttackFxPresentation
    {
        private struct Burst{public float At,Floor,Yaw;public Vector3 Feet;public int Seed,Pilot;public bool Impact;public Color Accent;}
        private readonly NativeVfxDraw vfx;
        private readonly Dictionary<int,NativeSprintAttackWatch> watches=new Dictionary<int,NativeSprintAttackWatch>();
        private readonly List<Burst> bursts=new List<Burst>(8);
        private NativeArena last;private int generation=-1,seeds;private float lastTime=-1;
        public int Live=>bursts.Count;
        public NativeSprintAttackFxPresentation(NativeVfxDraw vfx){this.vfx=vfx;}
        public void Clear(){bursts.Clear();watches.Clear();lastTime=-1;}
        /// <summary>now: the snapshot clock (ArenaPresentation.FieldClock); quality: the effects quality (1, .3 Reduced).</summary>
        public void Render(NativeArena arena,float now,float quality,bool low)
        {
            if(arena==null||vfx==null)return;
            if(arena!=last||generation!=arena.Generation||now<lastTime-.25f){Clear();last=arena;generation=arena.Generation;}
            lastTime=Mathf.Max(lastTime,now);
            int streaks=NativeSprintAttackFx.Count(NativeSprintAttackFx.Streaks,NativeSprintAttackFx.ReducedStreaks,quality,low);
            foreach(var p in arena.Pilots)
            {
                if(p?.Body==null||p.Pose==null)continue;var a=p.Pose;
                if(!watches.TryGetValue(p.Seat.id,out var w))watches[p.Seat.id]=w=new NativeSprintAttackWatch();
                Vector3 feet=p.Body.transform.position-Vector3.up*.865f;
                bool blocked=a.Hp<=0||a.Rolling(now)||a.StunUntil>now||a.JuggleUntil>now||NativeSpecialPose.Active(a,now)||NativeSpecialPose.Down(a,now);
                int entries=w.Entries,impacts=w.Impacts;
                w.Observe(a.AttackAt>-90?a.AttackAt:-100,a.SprintAttack,a.SwingAt,a.SwingUntil,a.Grounded,p.Seat.spec,now,blocked);
                var tint=NativeGlowRules.ArmorTint(a.AccentColor);var accent=new Color(tint.X,tint.Y,tint.Z,0);
                if(w.Entries>entries&&a.Hp>0)Add(new Burst{At=now,Feet=feet,Floor=feet.y,Yaw=a.Yaw,Seed=++seeds,Pilot=p.Seat.id,Accent=accent});
                if(w.Impacts>impacts&&a.Hp>0)Add(new Burst{At=now,Feet=feet,Floor=feet.y,Yaw=a.Yaw,Seed=++seeds,Pilot=p.Seat.id,Impact=true,Accent=accent});
                // The speed streaks ride the pilot through its hit window.
                float u=w.Window(now);
                if(!float.IsNaN(u))
                    for(int k=0;k<streaks;k++)
                        if(NativeSprintAttackFx.Streak(k,streaks,(int)(w.SwingAt*60)+p.Seat.id*7,u,new NVector(feet.x,feet.y,feet.z),a.Yaw,out var from,out var along,out float alpha))
                            vfx.Streak(new Vector3(from.X,from.Y,from.Z),new Vector3(along.X,along.Y,along.Z),accent*(1.6f*alpha),.03f,along.Length(),.25f,.7f,u,p.Seat.id*31+k);
            }
            for(int i=bursts.Count-1;i>=0;i--){float life=bursts[i].Impact?NativeSprintAttackFx.PuffLife:NativeSprintAttackFx.BurstLife;if(now-bursts[i].At>=life||now<bursts[i].At-.25f)bursts.RemoveAt(i);}
            int kicks=NativeSprintAttackFx.Count(NativeSprintAttackFx.Burst,NativeSprintAttackFx.ReducedBurst,quality,low),puffs=NativeSprintAttackFx.Count(NativeSprintAttackFx.Puffs,NativeSprintAttackFx.ReducedPuffs,quality,low),motes=NativeSprintAttackFx.Count(NativeSprintAttackFx.Motes,NativeSprintAttackFx.ReducedMotes,quality,low);
            var dustTone=new Color(.55f,.52f,.48f,0);
            foreach(var b in bursts)
            {
                float age=now-b.At;var feet=new NVector(b.Feet.x,b.Feet.y,b.Feet.z);
                if(!b.Impact)
                {
                    for(int k=0;k<kicks;k++)
                        if(NativeSprintAttackFx.Kick(k,kicks,b.Seed,age,feet,b.Yaw,b.Floor,out var at,out float size,out float opacity))
                            vfx.SoftSmoke(new Vector3(at.X,at.Y,at.Z),dustTone,opacity,size,age/NativeSprintAttackFx.BurstLife,b.Seed*17+k,b.Floor);
                    continue;
                }
                for(int k=0;k<motes;k++)
                    if(NativeSprintAttackFx.Mote(k,motes,age,feet,b.Floor,out var at,out float size,out float fade))
                        vfx.Glow(new Vector3(at.X,at.Y,at.Z),b.Accent*(2.2f*fade),size,NativeVfxDraw.CoreGlow,age/NativeSprintAttackFx.ShockLife,b.Seed*7+k);
                for(int k=0;k<puffs;k++)
                    if(NativeSprintAttackFx.Puff(k,puffs,b.Seed,age,feet,b.Floor,out var at,out float size,out float opacity))
                        vfx.SoftSmoke(new Vector3(at.X,at.Y,at.Z),dustTone,opacity,size,age/NativeSprintAttackFx.PuffLife,b.Seed*13+k,b.Floor);
            }
        }
        private void Add(Burst b){if(bursts.Count>=8)bursts.RemoveAt(0);bursts.Add(b);}
    }
    public sealed partial class ArenaPresentation
    {
        // The sprint attacks' effects (Runtime/NativeSprintAttackFxPresentation), queued into the shared effects renderer.
        private NativeSprintAttackFxPresentation sprintAttackFx;
        private void SprintAttackFx(NativeArena arena,float now)
        {
            if(vfx==null||arena==null)return;sprintAttackFx??=new NativeSprintAttackFxPresentation(vfx);
            sprintAttackFx.Render(arena,now,WeaponEffectsQuality,LowGraphics);
        }
    }
    public sealed partial class NativeAudio
    {
        // ---------- the melee sprint attacks (Core/NativeSprintAttackVisuals) ----------
        // Each pilot's swings are watched by the rule the rig uses: a swing leaving a sprint scuffs the floor (the existing roll
        // dash, short and bright), and the greatsword's landing ring adds the existing ground impact over the jump's own
        // landing thump (NativeEffectsAudio plays that for the hop). No new bank key.
        private readonly Dictionary<int,NativeSprintAttackWatch> sprintWatches=new Dictionary<int,NativeSprintAttackWatch>();
        private int sprintGeneration=-1;
        public const float SprintScuffGain=.45f,SprintImpactGain=.8f;
        private void SprintAttackCues(NativeArena arena)
        {
            if(arena==null)return;if(arena.Generation!=sprintGeneration){sprintGeneration=arena.Generation;sprintWatches.Clear();}
            float t=arena.Replica?arena.ReplicaRenderTime:arena.Time;var me=arena.Local;
            foreach(var p in arena.Pilots)
            {
                if(p?.Body==null||p.Pose==null)continue;var a=p.Pose;
                if(!sprintWatches.TryGetValue(p.Seat.id,out var watch))sprintWatches[p.Seat.id]=watch=new NativeSprintAttackWatch();
                bool blocked=a.Hp<=0||a.Rolling(t)||a.StunUntil>t||a.JuggleUntil>t||NativeSpecialPose.Active(a,t)||NativeSpecialPose.Down(a,t);
                int entries=watch.Entries,impacts=watch.Impacts;
                watch.Observe(a.AttackAt>-90?a.AttackAt:-100,a.SprintAttack,a.SwingAt,a.SwingUntil,a.Grounded,p.Seat.spec,t,blocked);
                if(a.Hp<=0)continue;bool mine=p==me;
                if(watch.Entries>entries)BankPlay("move.roll",p.Body.position,SprintScuffGain,0,null,mine,null,1.18f);
                if(watch.Impacts>impacts)BankPlay("impact.ground",p.Body.position,SprintImpactGain,0,null,mine,null,.85f);
            }
        }
    }
}

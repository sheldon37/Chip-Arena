using System;
using UnityEngine;
using NVector3=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The magic bolts' pack look (Core/NativeMagicPackFx; owner 2026-10-28, the licensed packs as in-place upgrades): a school's
    // primary bolt draws its pack head and pack trail sprites (ShotFx, NativeWeaponFxPresentation.cs) and a pack burst where it
    // hits a surface (its Impact event) or a pilot (its direct Hit event), all as sprites of the shared effects renderer
    // (NativeVfxDraw.Pack). Only while the magic sheet is loaded (NativeVfxDraw.PackSprites); without it the bolts keep their
    // energy core, model and soft trail sprites. Reads the replicated shots, events and actors on the presentation clock, as
    // the other weapon effects do; hit timing, hitboxes, damage, cooldowns, movement and the network frame are untouched.
    public sealed partial class ArenaPresentation
    {
        private struct MagicHit{public Vector3 At,Normal;public float Born,Scale;public long Seed;public Color Tint;public NativeMagicSchool School;}
        private readonly NativePackSprite[] packSprites=new NativePackSprite[NativeMagicPackFx.ImpactMax];
        private readonly MagicHit[] magicHits=new MagicHit[32];private int magicHitNext,magicHitEvent;private NativeArena magicHitArena;private int magicHitGeneration=-1;
        private bool PackLooks=>vfx!=null&&vfx.PackSprites;
        private static FxColor Fx(Color c)=>new FxColor(c.r,c.g,c.b,0);

        /// <summary>A school bolt's head at `at` (in place of the energy core), on the presentation clock.</summary>
        private void PackHead(NativeMagicSchool school,Vector3 at,Color halo,float radius,long seed,float now)
        {
            int n=NativeMagicPackFx.Head(school,new NVector3(at.x,at.y,at.z),radius,Fx(halo),seed,now,packSprites);
            for(int i=0;i<n;i++)vfx.Pack(packSprites[i]);
        }
        /// <summary>One pack trail sprite at `age` of its life: NativeTrailFx's size curve, drift and opacity; the school's tile,
        /// colour ramp and spin.</summary>
        private void PackTrailSprite(in TrailSprite p,float age)
        {
            if(!(age>=0&&age<p.Life))return;float u=age/p.Life;var c=NativeMagicPackFx.TrailColor(p.School,Fx(p.Color),u,p.Tile);
            vfx.Pack(p.At+p.Drift*age,new Color(c.R,c.G,c.B,0),p.Size*NativeTrailFx.SpriteSize(u),p.Roll+p.Spin*age,p.Tile,NativeMagicPackFx.HotOf(p.Tile));
        }
        /// <summary>
        /// The pack bursts: every fresh surface Impact and direct (or reflected) pilot Hit whose source holds a school's weapon
        /// (its spec among the replicated actors, as NativeMagicFx reads it) is kept in a ring of 32 and drawn for its
        /// ImpactLife, seeded by the replicated event id; on a pilot the burst opens back along the bolt's path.
        /// </summary>
        private void MagicPackHits(NativeArena arena,float now,float q,bool low)
        {
            if(!PackLooks||arena==null)return;
            if(!ReferenceEquals(magicHitArena,arena)||magicHitGeneration!=arena.Generation){magicHitArena=arena;magicHitGeneration=arena.Generation;magicHitEvent=0;for(int i=0;i<magicHits.Length;i++)magicHits[i].Born=-100;}
            int newest=0;foreach(var e in arena.Events)newest=Math.Max(newest,e.Id);if(newest<magicHitEvent)magicHitEvent=0;
            var actors=weaponSnapshot?.Actors;
            foreach(var e in arena.Events)
            {
                if(e.Id<=magicHitEvent||now-e.Time>=NativeMagicPackFx.ImpactLife)continue;
                bool surface=e.Kind==EventKind.Impact&&!string.IsNullOrEmpty(e.SurfaceId),pilot=(e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot)&&e.Target>0&&(e.Detail=="direct"||e.Detail=="reflected");
                if(!surface&&!pilot)continue;
                var school=NativeMagicPackFx.School(NativeMagicFx.Spec(actors,e.Source));if(school==NativeMagicSchool.None)continue;
                var normal=NativeArena.Vec(e.Normal);normal=surface?(normal.sqrMagnitude>.25f?normal.normalized:Vector3.zero):(normal.sqrMagnitude>.25f?-normal.normalized:Vector3.zero);
                var weapon=WeaponLoadout?.Content?.Weapon(NativeMagicFx.Spec(actors,e.Source));int color=e.Color!=0?e.Color:weapon!=null?(int)weapon.color:0xffffff;
                magicHits[magicHitNext]=new MagicHit{At=NativeArena.Vec(e.Position),Normal=normal,Born=e.Time,Seed=e.Id*1000003L+11,Tint=NativeWeaponFxShapes.Lin(color),School=school,Scale=surface?1.3f:1.1f};
                magicHitNext=(magicHitNext+1)%magicHits.Length;
            }
            magicHitEvent=Math.Max(magicHitEvent,newest);
            for(int i=0;i<magicHits.Length;i++)
            {
                ref var m=ref magicHits[i];double age=now-m.Born;if(!(age>=0&&age<NativeMagicPackFx.ImpactLife))continue;
                int n=NativeMagicPackFx.Impact(m.School,new NVector3(m.At.x,m.At.y,m.At.z),new NVector3(m.Normal.x,m.Normal.y,m.Normal.z),m.Seed,age,m.Scale,Fx(m.Tint),q,low,packSprites);
                for(int k=0;k<n;k++)vfx.Pack(packSprites[k]);
            }
        }
        private void ClearMagicPack(){magicHitArena=null;magicHitGeneration=-1;magicHitEvent=0;for(int i=0;i<magicHits.Length;i++)magicHits[i].Born=-100;}
    }
}

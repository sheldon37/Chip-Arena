using System;
using System.Collections.Generic;
using UnityEngine;
using NVector3=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Civil Defence caches in the world (Claude, 2026-11-19; R9, W6; Core NativeCacheOpenFx, NativeCacheClock,
    /// NativeCacheLockerModels): the CD-647 steel locker where A2's solid chest block stands (bones posed: the lid on its hinge,
    /// the latches, the lamp faces, the LED strip, EPIC's seams, the tray's glow; the livery sweep at restock), and every cache's
    /// opening by rarity in the shared effects renderer (the glows, glints, embers, puffs, the light from the gap and the tier's
    /// column as streaks: no ring, nothing on the floor, no shake), the buyer's three cartridges (only on the buyer's client), the
    /// opening's light in a muzzle-light slot (single pulses and smooth envelopes: 3 Hz or slower). Today's hologram cache plays
    /// the same opening as a flare (its lid and flare on the world pieces). Chip-machine purchases open by the chip's rarity at
    /// the machine's slot (NativeCacheTiers.OfRarity of the cartridge the machine dispensed; an Expedition machine's chip is
    /// chosen later with C, so its opening reads STANDARD). Presentation only: ConversionGame hands the caches over each frame
    /// from the replicated view (CacheShows); nothing here reaches a rule, a collider or the network.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        /// <summary>The caches to draw this frame (ConversionGame.CacheWorldFrame: the live clock's, or the DEV preview's); null:
        /// none. Reduced effects; where the local buyer's chest is (world), for the cartridges' dock.</summary>
        public List<NativeCacheShow> CacheShows;public bool CacheReduced;public Vector3 CacheDock;public bool CacheDockKnown;
        private readonly List<NativeCacheShow> cacheAll=new List<NativeCacheShow>(16);
        private readonly List<NativeCacheFrame> cacheFrames=new List<NativeCacheFrame>(16);private int cacheFrameCount,cacheBuilt=-1;
        private readonly Dictionary<(int,float),NativeCacheTier> machineTiers=new Dictionary<(int,float),NativeCacheTier>();
        private Func<int,Color> cacheTint;private int cacheTintIndex;private float cacheSweepShare=1;
        private static readonly Vector3 LockerHinge=CacheV(NativeCacheLockerModels.Hinge),LockerLatch=CacheV(NativeCacheLockerModels.LatchPivot);
        private static Vector3 CacheV(NVector3 v)=>new Vector3(v.X,v.Y,v.Z);

        // ---- the frame's caches: the handed-over shows, then the chip machines' openings ----
        private void CacheBuild(NativeArena arena)
        {
            if(cacheBuilt==Time.frameCount)return;cacheBuilt=Time.frameCount;cacheAll.Clear();
            if(CacheShows!=null)cacheAll.AddRange(CacheShows);
            MachineOpenings(arena);
            while(cacheFrames.Count<cacheAll.Count)cacheFrames.Add(new NativeCacheFrame());cacheFrameCount=cacheAll.Count;
            for(int i=0;i<cacheAll.Count;i++)CacheFrameOf(cacheAll[i],cacheFrames[i]);
        }
        // A chip machine's purchase (its use record, which every peer holds) opens by the dispensed cartridge's rarity: the
        // machine's own flap is its lid, so only the light, the glints and the column play, from its slot.
        private void MachineOpenings(NativeArena arena)
        {
            if(arena?.Pve==null||!NativeMachineRules.Present(arena.MapId,arena.Expedition!=null)||CacheReduced)return;var view=arena.Pve;float t=arena.Time;
            if(machineTiers.Count>64)machineTiers.Clear();
            foreach(var use in view.Machines)
            {
                if(use.result!="chip")continue;float age=t-use.at;if(age<0||age>=NativeCacheOpenFx.End)continue;
                PveMachine m=null;foreach(var x in view.Content.machines)if(x.m==use.machine){m=x;break;}if(m==null)continue;
                var key=(use.machine,use.at);
                if(!machineTiers.TryGetValue(key,out var tier))
                {
                    tier=NativeCacheTier.Standard;bool found=false;
                    foreach(var k in view.Pickups)if(k.Machine==use.machine&&k.Owner!=null&&k.Owner.Seat.id==use.owner&&Mathf.Abs(k.Born-(use.at+.6f))<.05f){tier=NativeCacheTiers.OfRarity(k.Tier);found=true;break;}
                    if(found||arena.Expedition!=null||age>.2f)machineTiers[key]=tier;
                }
                float yaw=m.yaw*Mathf.Rad2Deg;var root=Matrix4x4.TRS(NativeArena.Vec(NativeMachineRules.Root(m)),Quaternion.Euler(0,yaw,0),Vector3.one);
                var slot=root.MultiplyPoint3x4(CacheV(NativeMachineModels.Slot))+Quaternion.Euler(0,yaw,0)*Vector3.forward*.12f;
                uint seed=(uint)(use.machine*7919)^(uint)(use.at*1000);
                cacheAll.Add(new NativeCacheShow{Id=null,X=slot.x,Y=slot.y-NativeCacheOpenFx.MouthY,Z=slot.z,Yaw=yaw,Tier=tier,Next=tier,State=NativeCacheState.Opening,Since=age,Sweep=float.NaN,Machine=true,Seed=seed});
            }
        }
        private void CacheFrameOf(in NativeCacheShow s,NativeCacheFrame f)
        {
            switch(s.State)
            {
                case NativeCacheState.ForSale:
                    if(s.Locker)NativeCacheOpenFx.ForSale(f,s.Tier,s.Sale,CacheReduced,s.Short);else f.Clear();break;
                case NativeCacheState.Opening:
                {
                    if(!s.Locker&&s.Since>=NativeCacheOpenFx.End){f.Clear();break;}
                    var o=NativeCacheOpenFx.Opening(s.Tier,s.Seed,CacheReduced);float dx=0,dy=0,dz=0;bool near=false;
                    if(s.Buyer&&CacheDockKnown)
                    {
                        float wx=CacheDock.x-s.X,wz=CacheDock.z-s.Z;NativeCacheOpenFx.ToLocal(s.Yaw,wx,wz,out dx,out dz);dy=CacheDock.y-s.Y;
                        near=wx*wx+wz*wz<NativeCacheOpenFx.BuyerNear*NativeCacheOpenFx.BuyerNear;
                    }
                    o.Frame(f,s.Since,s.Buyer&&CacheDockKnown,near,dx,dy,dz,!s.Locker);break;
                }
                default:if(s.Locker)NativeCacheOpenFx.Shut(f,s.Tier);else f.Clear();break;
            }
        }
        private static Vector3 World(in NativeCacheShow s,float x,float y,float z){NativeCacheOpenFx.ToWorld(s.Yaw,x,z,out float wx,out float wz);return new Vector3(s.X+wx,s.Y+y,s.Z+wz);}
        private static Vector3 Axis(in NativeCacheShow s,float x,float y,float z){NativeCacheOpenFx.ToWorld(s.Yaw,x,z,out float wx,out float wz);return new Vector3(wx,y,wz);}
        private static Color Lit(int hex,float gain)=>NativeVfxDraw.Linear(hex)*gain;

        // ---- the source models' pass (NativeSourcePresentation, beside the machines): the lockers and the cartridges ----
        private void CacheLockers(NativeArena arena)
        {
            CacheBuild(arena);if(sourceModels==null)return;cacheTint??=CacheTint;
            for(int i=0;i<cacheFrameCount;i++)
            {
                var s=cacheAll[i];var f=cacheFrames[i];var root=Matrix4x4.TRS(new Vector3(s.X,s.Y,s.Z),Quaternion.Euler(0,s.Yaw+180,0),Vector3.one);
                if(s.Locker)
                {
                    float sweep=float.IsNaN(s.Sweep)?1:NativeCacheOpenFx.Smooth(0,NativeCacheClock.SweepSeconds,s.Sweep);
                    if(sweep<1)Locker(root,s.SweepFrom,f,1-sweep,i);
                    Locker(root,s.Tier,f,sweep,i);
                }
                // the buyer's three cartridges: the tier's own chip cartridge, rising, fanned, docking (only this client sees them)
                if(f.CartridgeCount>0)
                {
                    string model=sourceModels.MachineModel(NativeMachineModels.PickupModel(NativeCacheTiers.Cartridge(s.Tier)),"pve-pickup");
                    for(int k=0;k<f.CartridgeCount;k++)
                    {
                        var c=f.Cartridges[k];if(c.Alpha<.02f)continue;
                        var m=Matrix4x4.TRS(World(s,c.X,c.Y,c.Z),Quaternion.Euler(0,s.Yaw,0)*Quaternion.Euler(-1.25f*Mathf.Rad2Deg,0,0)*Quaternion.Euler(0,c.Roll,0),Vector3.one*(.62f*c.Scale));
                        var lit=Lit(NativeCacheTiers.Hex(s.Tier),1.2f);
                        sourceModels.Queue(model,m,null,null,0,null,null,null,c.Alpha,null,b=>b==1||b==NativeMachineModels.CoreLights||b==NativeMachineModels.Orbit?lit:Color.white);
                    }
                }
            }
        }
        // One locker of one livery at `share` (the sweep's cross-fade): the body, the lid on its hinge, the two latches.
        private void Locker(Matrix4x4 root,NativeCacheTier tier,NativeCacheFrame f,float share,int index)
        {
            if(share<.01f)return;cacheTintIndex=index;cacheSweepShare=share;var lidTier=tier;
            sourceModels.Queue(NativeCacheLockerModels.BodyId(tier),root,null,null,0,null,null,null,share,null,cacheTint);
            var lid=root*Matrix4x4.Translate(LockerHinge)*Matrix4x4.Rotate(Quaternion.Euler(f.Lid,0,0))*Matrix4x4.Translate(-LockerHinge);
            sourceModels.Queue(NativeCacheLockerModels.LidId(lidTier),lid,null,null,0,null,null,null,share,null,cacheTint);
            for(int side=-1;side<=1;side+=2)
            {
                var latch=root*Matrix4x4.Translate(new Vector3(side*.33f,0,0))*Matrix4x4.Translate(LockerLatch)*Matrix4x4.Rotate(Quaternion.Euler(f.Latch,0,0))*Matrix4x4.Translate(-LockerLatch);
                sourceModels.Queue(NativeCacheLockerModels.LatchId(tier),latch,null,null,0,null,null,null,share);
            }
        }
        // The tinted bones of the locker being queued: 1 the strip and EPIC's seams, 2 and 3 the lamp faces, -1 the tray's glow.
        private Color CacheTint(int bone)
        {
            var f=cacheFrames[cacheTintIndex];var s=cacheAll[cacheTintIndex];int hex=NativeCacheTiers.Hex(s.Tier);
            switch(bone)
            {
                case NativeCacheLockerModels.Lights:
                    if(f.StripHex==NativeCacheTiers.Red)return Lit(NativeCacheTiers.Red,f.StripGain);
                    return Lit(hex,s.Tier==NativeCacheTier.Epic?Mathf.Max(f.StripGain,.9f*f.Seams):Mathf.Max(f.StripGain,.35f));
                // (a lamp the frame does not hold: the outgoing livery's extra lamps while it sweeps away, dark in its colour)
                case NativeCacheLockerModels.LampsA:return f.Lamps>0?Lit(f.LampHex[0],Mathf.Max(.15f,f.LampGain[0])):Lit(hex,.15f);
                case NativeCacheLockerModels.LampsB:return f.Lamps>1?Lit(f.LampHex[1],Mathf.Max(.15f,f.LampGain[1])):Lit(hex,.15f);
                case -1:return Lit(hex,.25f+f.Inner);
                default:return Color.white;
            }
        }

        // ---- the effects pass (RenderGlow, before the muzzle lights resolve): the openings' light, glows, streaks and puffs ----
        private void CacheFx(NativeArena arena,Camera camera)
        {
            CacheBuild(arena);CachePackFx(camera);if(vfx==null||cacheFrameCount==0)return;
            for(int i=0;i<cacheFrameCount;i++)
            {
                var s=cacheAll[i];var f=cacheFrames[i];
                for(int k=0;k<f.GlowCount;k++){var g=f.Glows[k];vfx.Glow(World(s,g.X,g.Y,g.Z),Lit(g.Hex,g.Gain),g.Size,g.Bloom?NativeVfxDraw.CoreGlow:NativeVfxDraw.Mote,0,i*17+k);}
                for(int k=0;k<f.StreakCount;k++)
                {
                    var g=f.Streaks[k];var axis=Axis(s,g.Ax,g.Ay,g.Az);var head=World(s,g.X,g.Y,g.Z);float taper=g.Taper;
                    // a long one (the columns, the crown's blades) laid onto its head's camera plane, so it stands on its line
                    if(camera!=null&&!camera.orthographic&&axis.sqrMagnitude>NativeCacheOpenFx.ExactStreak*NativeCacheOpenFx.ExactStreak)
                    {
                        var eye=camera.transform.position;var fw=camera.transform.forward;float ax=axis.x,ay=axis.y,az=axis.z;
                        if(NativeCacheOpenFx.OntoHeadPlane(eye.x,eye.y,eye.z,fw.x,fw.y,fw.z,Mathf.Max(.2f,camera.nearClipPlane*2),head.x,head.y,head.z,ref ax,ref ay,ref az,ref taper))axis=new Vector3(ax,ay,az);
                    }
                    vfx.Streak(head,axis,Lit(g.Hex,g.Gain),g.Width,.01f,1,taper,0,i*31+k);
                }
                for(int k=0;k<f.PuffCount;k++)
                {
                    var p=f.Puffs[k];var at=World(s,p.X,p.Y,p.Z);var albedo=NativeVfxDraw.Linear(p.Hex);
                    if(p.Vapour)vfx.Vapor(at,albedo,p.Opacity,p.Size,p.U,p.Seed,s.Y,p.Glow);else vfx.Smoke(at,albedo,p.Opacity,p.Size,p.U,p.Seed,s.Y);
                }
                // the buyer's cartridges' faint glow behind each
                for(int k=0;k<f.CartridgeCount;k++){var c=f.Cartridges[k];if(c.Alpha>.02f)vfx.Glow(World(s,c.X,c.Y,c.Z),Lit(NativeCacheTiers.Hex(s.Tier),.45f*c.Alpha),.3f*c.Scale,NativeVfxDraw.Mote);}
                // the opening's light (one slot per cache: the spill from the mouth, or the flash's single pulse), and LEGENDARY's
                // pillar light 7 m up: smooth envelopes and single pulses only; the lamps' red after the shut lights nothing
                float spill=f.Spill,flash=f.Flash+f.SeamLight;
                if(spill>.01f||flash>.01f)
                {
                    bool fl=flash>spill;var look=new MuzzleLightLook{Color=fl?f.FlashHex:f.SpillHex,Peak=1,Range=fl?f.FlashRange:f.SpillRange};
                    var at=World(s,0,.8f,-.2f);muzzleLights.Offer(-1000-i,new NVector3(at.x,at.y,at.z),look,Mathf.Max(spill,flash),false);
                }
                if(f.Pillar>.01f){var at=World(s,0,7,NativeCacheOpenFx.ColumnZ);muzzleLights.Offer(-2000-i,new NVector3(at.x,at.y,at.z),new MuzzleLightLook{Color=NativeCacheTiers.CoreHex(s.Tier),Peak=1,Range=16},f.Pillar,false);}
                // the livery sweep: a band of the new tier's light crossing the front
                if(s.Locker&&!float.IsNaN(s.Sweep)&&!CacheReduced)
                {
                    float k=NativeCacheOpenFx.Smooth(0,NativeCacheClock.SweepSeconds,s.Sweep),x=-.5f+k,g=4*(float)Math.Sin(Math.PI*k);
                    for(int j=0;j<4;j++)vfx.Glow(World(s,x,.12f+j*.14f,-.3f),Lit(NativeCacheTiers.Hex(s.Tier),g),.14f,NativeVfxDraw.Mote);
                }
            }
        }
    }
}

using System;

namespace ChipArena.UnityConversion
{
    /// <summary>One thing the Top Dog has out in the world (docs/TOP-DOG.md "The record"): an anvil coming down, a banana peel, a pie in flight,
    /// the spring glove, the mustard stream and its puddles, the spring under the feet, the dash's dust. `Kind` is one of NativeTopDog's kinds.
    /// `Pos`/`Vel` are sampled at `SampleAt` (flights), `To` a second point (the dash's end), `Dir` a facing, `Size` a reach or radius, `At` when it
    /// began, `LandAt` its moment (the anvil's impact, the peel's landing, the glove's full reach, the dash's go), `Until` when it is gone, `Hit` who
    /// it caught (0: nobody).</summary>
    [Serializable] public sealed class NativeDogProp
    {
        public int Id,Hit;public string Kind;public V3 Pos,Vel,To,Dir;public float At,SampleAt,LandAt,Until,Size;
    }
    /// <summary>One moment of contact for the effects and the comic words (the last 1.5 s): `Kind` (bonk, slam, thud, swat, anvil, slip, splat,
    /// pow, pie, gulp, pop, snap), where, which way, and who.</summary>
    [Serializable] public sealed class NativeDogHit
    {
        public int Id,Victim;public string Kind;public float At;public V3 Pos,Dir;
    }
    /// <summary>A victim's toon state, on the Top Dog that caused it: pancaked flat, slipped flat on their back, a pie in the face, mustard on them
    /// (slowed), seeing stars, or stuffed and swelling to the pop.</summary>
    [Serializable] public sealed class NativeDogMark
    {
        public int Id;public string Kind;public float At,Until;
    }
    /// <summary>All You Can Eat, the ultimate (docs/TOP-DOG.md): the lasso of links is twirled from At and thrown at Throw; it flies (`Lasso`,
    /// `LassoVel` sampled at `LassoAt`) until it catches `Victim` at Catch (-100: not yet, or missed: `Miss`). Caught, the victim is reeled to
    /// `Hold` (facing `Dir` back at the Top Dog) and fed, one hot dog every NativeTopDog.FeedPeriod from FeedAt, `Fed` swallowed so far; at Release
    /// they are let go stuffed, and at Pop they burst. `Snap` is when the links broke early (-100: they held). Until is when it is over.</summary>
    [Serializable] public sealed class NativeDogFeast
    {
        public float At=-100,Throw,LassoAt,Catch=-100,FeedAt,Release,Pop,Until,Snap=-100;public int Victim,Fed;public bool Miss;
        public V3 Lasso,LassoVel,Hold,Dir,From;
    }
    /// <summary>
    /// The Top Dog's replicated kit (docs/TOP-DOG.md "The record"): everything the pose, the effects, the sound and the HUD read, and the only
    /// thing they read. The authority (Runtime NativeTopDogAuthority.cs) writes it; <see cref="NativeTopDogPreview"/> writes it the same way for
    /// the Dev Sandbox. Copied in snapshots, late join and the killcam like the Bombadier's (`Actor.Dog`), checked with <see cref="Valid"/>.
    /// </summary>
    [Serializable] public sealed class NativeTopDogKit
    {
        public NativeDogProp[] Props;public NativeDogHit[] Hits;public NativeDogMark[] Marks;public NativeDogFeast Feast;
        /// <summary>The meter (0..1), the banana peels in hand, the mallet's combo step (0, 1, 2) and when it lapses.</summary>
        public float Meter,ComboUntil;public int Peels,Combo;
        /// <summary>The chips' share of what is seen (Core NativeDogChips): Bigger Mallet's copies, the peels it can hold, Hot Sauce in the stream,
        /// Second Helpings' wide lasso, Double Boing's second bounce in hand.</summary>
        public int Mallet,PeelCap;public bool Sauce,Wide,Bounce;
        /// <summary>Absolute ready times in NativeTopDog.Slot order: Q, E, 1, 2, 3, 4, 5.</summary>
        public float[] Ready;
        /// <summary>What the pilot is doing, for the pose: the ability's id and its times (CastRelease: the moment it lands or leaves the hand).</summary>
        public string CastKind;public float CastAt=-100,CastRelease,CastUntil;public int CastStep;
        public bool HasFeast;
        public void CapturePresence(){HasFeast=Feast!=null;}
        public void RestorePresence(){if(!HasFeast)Feast=null;if(string.IsNullOrEmpty(CastKind))CastKind=null;}
        public NativeTopDogKit ViewCopy()
        {
            var c=(NativeTopDogKit)MemberwiseClone();
            if(Props!=null){c.Props=new NativeDogProp[Props.Length];for(int i=0;i<Props.Length;i++){var p=Props[i];c.Props[i]=p==null?null:new NativeDogProp{Id=p.Id,Hit=p.Hit,Kind=p.Kind,Pos=p.Pos,Vel=p.Vel,To=p.To,Dir=p.Dir,At=p.At,SampleAt=p.SampleAt,LandAt=p.LandAt,Until=p.Until,Size=p.Size};}}
            if(Hits!=null){c.Hits=new NativeDogHit[Hits.Length];for(int i=0;i<Hits.Length;i++){var h=Hits[i];c.Hits[i]=h==null?null:new NativeDogHit{Id=h.Id,Victim=h.Victim,Kind=h.Kind,At=h.At,Pos=h.Pos,Dir=h.Dir};}}
            if(Marks!=null){c.Marks=new NativeDogMark[Marks.Length];for(int i=0;i<Marks.Length;i++){var m=Marks[i];c.Marks[i]=m==null?null:new NativeDogMark{Id=m.Id,Kind=m.Kind,At=m.At,Until=m.Until};}}
            if(Feast!=null){var f=Feast;c.Feast=new NativeDogFeast{At=f.At,Throw=f.Throw,LassoAt=f.LassoAt,Catch=f.Catch,FeedAt=f.FeedAt,Release=f.Release,Pop=f.Pop,Until=f.Until,Snap=f.Snap,Victim=f.Victim,Fed=f.Fed,Miss=f.Miss,Lasso=f.Lasso,LassoVel=f.LassoVel,Hold=f.Hold,Dir=f.Dir,From=f.From};}
            c.Ready=Ready==null?null:(float[])Ready.Clone();
            return c;
        }
        public bool Valid()
        {
            bool F(float v)=>MathUtil.Finite(v)&&Math.Abs(v)<10000000;bool V(V3 v)=>F(v.x)&&F(v.y)&&F(v.z)&&v.Length<100000;bool K(string s)=>s==null||s.Length<=24;
            if(!F(Meter)||Meter<0||Meter>1||Peels<0||Peels>NativeTopDog.MaxPeelCharges||PeelCap<0||PeelCap>NativeTopDog.MaxPeelCharges||Mallet<0||Mallet>NativeDogChips.MalletMax||Combo<0||Combo>2||!F(ComboUntil)||!F(CastAt)||!F(CastRelease)||!F(CastUntil)||CastStep<0||CastStep>8||!K(CastKind))return false;
            if(Props!=null){if(Props.Length>NativeTopDog.MaxProps)return false;foreach(var p in Props)if(p==null||!NativeTopDog.IsKind(p.Kind)||!V(p.Pos)||!V(p.Vel)||!V(p.To)||!V(p.Dir)||!F(p.At)||!F(p.SampleAt)||!F(p.LandAt)||!F(p.Until)||!F(p.Size)||p.Size<0||p.Size>64)return false;}
            if(Hits!=null){if(Hits.Length>NativeTopDog.MaxHits)return false;foreach(var h in Hits)if(h==null||!K(h.Kind)||!F(h.At)||!V(h.Pos)||!V(h.Dir))return false;}
            if(Marks!=null){if(Marks.Length>NativeTopDog.MaxMarks)return false;foreach(var m in Marks)if(m==null||!NativeTopDog.IsMark(m.Kind)||!F(m.At)||!F(m.Until))return false;}
            if(Feast!=null){var f=Feast;if(!F(f.At)||!F(f.Throw)||!F(f.LassoAt)||!F(f.Catch)||!F(f.FeedAt)||!F(f.Release)||!F(f.Pop)||!F(f.Until)||!F(f.Snap)||f.Fed<0||f.Fed>NativeTopDog.Feeds||!V(f.Lasso)||!V(f.LassoVel)||!V(f.Hold)||!V(f.Dir)||!V(f.From))return false;}
            if(Ready!=null){if(Ready.Length>NativeTopDog.Slots)return false;foreach(var r in Ready)if(!F(r))return false;}
            return true;
        }
    }
    /// <summary>Where the presentation reads a pilot's kit: the snapshot's, or the sandbox preview's.</summary>
    public interface ITopDogSource{NativeTopDogKit Of(int actor);}
}

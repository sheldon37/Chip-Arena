using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The horde's gore (owner 2026-10-19: "I want them to be more "standard" zombies, that produce gore when shot / gibbed, not
    /// just black flames. High-quality design and fx please"; docs/ZOMBIE-GORE.md). Presentation only, engine free: every rule is a
    /// pure function of what host, guests and the killcam replay all hold (an add's replicated hit events: its time, point,
    /// direction, detail, amount and attacker; its death record: id, seed, time, position, yaw, lost limbs, damage kind), its seed
    /// and the presentation clock, so every peer draws the same wound, spurt, chunk and pool. Nothing here is read by a rule.
    /// <list type="bullet">
    /// <item>Hits (<see cref="Cause"/>, <see cref="Place"/>): the struck joint is the drawn body's nearest to the hit point; the
    /// wound sits on that part's surface facing the shot and rides the joint (live, dying, then the corpse). A hit sprays blood
    /// out of the wound and along the shot (the blood sim, <see cref="Spray"/>), puffs a little red mist and throws flesh bits
    /// (<see cref="Bit"/>); a head hit sprays harder. Wounds stay on the add, a few per add (<see cref="WoundsShown"/>), wet then
    /// drying, and drip (<see cref="Drips"/>).</item>
    /// <item>Deaths (<see cref="Decide"/>): the body falls on its kind's death take and stays as a corpse over a spreading pool of
    /// blood (<see cref="PoolRadius"/>), then sinks and fades (<see cref="CorpseLife"/>, <see cref="Sink"/>). A blast close in, a
    /// crushing hazard, a heavy one-shot or a big gun's killing head shot gibs it instead (<see cref="Fate"/>): limbs thrown as
    /// the model's own parts, the trunk or the head burst into voxel chunks of flesh and bone (<see cref="Chunk"/>).</item>
    /// <item>Tiers (<see cref="Tier"/>, the zombie effects' tiers): Reduced effects and the low graphics tier keep fewer corpses for
    /// less time, fewer wounds, bits, chunks and mist, and gib less often (<see cref="GibCap"/>). Everything is in fixed arrays.</item>
    /// </list>
    /// </summary>
    public static class NativeHordeGore
    {
        public const int Full=NativeZombieFx.Full,Reduced=NativeZombieFx.Reduced,Low=NativeZombieFx.Low,Tiers=3;
        public static int Tier(bool lowGraphics,float quality)=>NativeZombieFx.Tier(lowGraphics,quality);
        private static int T(int tier)=>tier<Full?Full:tier>Low?Low:tier;

        // ---- budgets per tier (Full, Reduced effects, the low graphics tier) ----
        /// <summary>Corpses (and gib remains) kept; past it the oldest sinks away early.</summary>
        public static readonly int[] CorpseCap={24,14,8};
        /// <summary>How long a corpse lies before it sinks (s).</summary>
        public static readonly float[] CorpseLife={22,15,9};
        /// <summary>Wounds drawn per add, and the adds (nearest first) whose wounds draw.</summary>
        public static readonly int[] WoundsShown={8,5,3},WoundAdds={24,14,8};
        /// <summary>Gibs (full or head) within <see cref="GibWindow"/> s; past it a would-be gib falls as a corpse.</summary>
        public static readonly int[] GibCap={6,4,2};
        /// <summary>Flesh bits a hit throws, chunks a gib throws, mist puffs a hit puffs, drips a wound sheds.</summary>
        public static readonly int[] Bits={9,6,3},Chunks={30,18,9},Mist={3,2,1},WoundDrips={5,3,2};
        public const int WoundMax=8,AddMax=96,BurstMax=48,RemainsMax=32,BlockMax=2048,MistMax=128,SprayMax=96,ChunkMax=30;
        public const float GibWindow=4,SinkSeconds=2.4f,SinkDepth=.42f,WoundRange=24,BurstLife=2.4f,DripSeconds=4.5f,DryAt=9;

        // ================================================================================================================
        // Causes and fates
        // ================================================================================================================
        /// <summary>What made a hit (from the replicated event: detail, damage kind and the attacker's weapon).</summary>
        public enum Cause:byte{None,Bullet,Heavy,Melee,Arrow,Blast,Crush}
        /// <summary>How a body ends.</summary>
        public enum Fate:byte{Corpse,HeadGib,Gib,Crush,Burst}
        /// <summary>The hazards that crush or cut a body apart (the train, the Factory's saws, presses and crane load).</summary>
        public static bool Crushing(string kind)=>kind!=null&&(kind=="maglev"||kind=="train"||kind=="press"||kind=="crane"||kind.IndexOf("saw",StringComparison.Ordinal)>=0||kind.IndexOf("press",StringComparison.Ordinal)>=0);
        /// <summary>
        /// A replicated hit's cause: ticks (field, burn, poison, chain) none (they never bleed, as the wound ink never did); a
        /// crushing hazard; a blast; a melee blow (the presentation's own reading, NativeHordeKindPresentation); the bow's arrow;
        /// the rifle's heavy round; any other projectile a bullet.
        /// </summary>
        public static Cause Classify(string detail,string spec,bool melee,string damageKind=null)
        {
            if(detail=="field"||detail=="burn"||detail=="poison"||detail=="chain")return Cause.None;
            if(Crushing(damageKind)||Crushing(detail))return Cause.Crush;
            if(detail=="blast")return Cause.Blast;
            if(melee)return Cause.Melee;
            if(spec=="bow")return Cause.Arrow;
            if(spec=="rifle"||detail=="plasma-overflow-hit")return Cause.Heavy;
            return Cause.Bullet;
        }
        /// <summary>
        /// How a death ends before the gib cap (<see cref="NativeHordeGoreState.Fate"/> applies it): a crushing hazard crushes; a
        /// blast whose killing share was heavy (a third of the add's health, or within 1.4 m of its centre) gibs; a killing head hit
        /// by the rifle, a heavy melee blow or one that took half the add's health bursts the head; a one-shot that took four fifths
        /// of the add's health from a heavy cause gibs; the bloater always bursts (its own death); everything else is a corpse.
        /// `amount` is the killing hit's replicated damage (clamped to the health left), `maxHp` the add's.
        /// </summary>
        public static Fate Decide(int kind,Cause cause,bool head,float amount,float maxHp,float blastDistance,bool heavyMelee)
        {
            if(kind==NativeHordeKinds.Bloater)return Fate.Burst;
            float share=maxHp>0?amount/maxHp:0;
            if(cause==Cause.Crush)return Fate.Crush;
            if(cause==Cause.Blast)return share>=.33f||blastDistance<=1.4f?Fate.Gib:Fate.Corpse;
            if(head&&(cause==Cause.Heavy||cause==Cause.Melee&&heavyMelee||share>=.5f&&cause!=Cause.Arrow))return Fate.HeadGib;
            if(share>=.8f&&(cause==Cause.Heavy||cause==Cause.Melee&&heavyMelee))return Fate.Gib;
            return Fate.Corpse;
        }
        public static bool Gibs(Fate f)=>f==Fate.HeadGib||f==Fate.Gib||f==Fate.Crush;

        // ================================================================================================================
        // Placing a wound: the drawn body's nearest part to the hit, on its surface, facing the shot
        // ================================================================================================================
        /// <summary>Each joint's part as a segment to its child (-1: to its own tip, <see cref="TipOf"/>) and its radius (rig m,
        /// the Standard pilot's; <see cref="Girth"/> widens it per kind). The body root (0) has none.</summary>
        public static readonly int[] SegmentTo={-2,2,3,4,5,-1, 7,8,9,-1, 11,12,13,-1, 15,16,17,-1, 19,20,21,-1};
        public static readonly float[] SegmentRadius={0,.15f,.15f,.16f,.06f,.15f, .07f,.065f,.055f,.05f, .07f,.065f,.055f,.05f, .085f,.07f,.06f,.05f, .085f,.07f,.06f,.05f};
        public static Vector3 TipOf(int joint)=>joint==CapturedRig.Head?new Vector3(0,.3f,0):joint==CapturedRig.LHand||joint==CapturedRig.RHand?new Vector3(0,-.14f,0):joint==CapturedRig.LToe||joint==CapturedRig.RToe?new Vector3(0,0,.1f):Vector3.Zero;
        /// <summary>A kind's girth over the Standard pilot's (its limbs, then its trunk): the stripped runners thin, the big kinds wide.</summary>
        public static float Girth(int kind,bool trunk)
        {
            switch(NativeHordeKinds.Base(kind))
            {
                case NativeHordeKinds.Runner:case NativeHordeKinds.Crawler:return trunk?.86f:.82f;
                case NativeHordeKinds.Brute:return trunk?1.2f:1.45f;
                case NativeHordeKinds.Bloater:return trunk?1.55f:1.08f;
                case NativeHordeKinds.Hulk:return trunk?1.35f:1.6f;
                case NativeHordeKinds.Spitter:return .93f;
                default:return 1;
            }
        }
        private static bool Trunk(int joint)=>joint>=CapturedRig.Pelvis&&joint<=CapturedRig.Chest;
        /// <summary>A cut's raw cap radius on joint `joint`'s part (rig m, before the drawn scale): its part's radius, the kind's
        /// girth held to .85..1.3 (the big kinds' necks are not as wide as their arms).</summary>
        public static float CutRadius(int kind,int joint)=>SegmentRadius[joint>=0&&joint<22?joint:3]*.9f*MeleeMath.Clamp(kind>=0?Girth(kind,Trunk(joint)):1,.85f,1.3f);
        /// <summary>A thrown part's own cut (at its joint): a head is cut through the neck, a limb at its root.</summary>
        public static float PartCut(int kind,int joint)=>CutRadius(kind,joint==CapturedRig.Head?CapturedRig.Neck:joint);
        /// <summary>The way out of a thrown part's cut, in its joint's frame: down out of a head (its part grows up from the joint),
        /// up out of a limb (its part hangs down to its child).</summary>
        public static Vector3 CutOut(int joint)=>joint==CapturedRig.Head?-Vector3.UnitY:Vector3.UnitY;
        /// <summary>A joint's world origin from its world matrix (System.Numerics: translation in the fourth row).</summary>
        public static Vector3 Origin(in Matrix4x4 m)=>new Vector3(m.M41,m.M42,m.M43);
        /// <summary>The world segment of joint j's part (its origin to its child's or its own tip).</summary>
        public static void Segment(Matrix4x4[] bones,int j,out Vector3 a,out Vector3 b)
        {
            a=Origin(bones[j]);int to=SegmentTo[j];b=to>=0?Origin(bones[to]):Vector3.Transform(TipOf(j),bones[j]);
        }
        /// <summary>How much the drawn rig is scaled (the Hulk's 1.8): the length of a joint matrix's x axis.</summary>
        public static float ScaleOf(in Matrix4x4 m){float s=new Vector3(m.M11,m.M12,m.M13).Length();return s>1e-4f&&s<1e4f?s:1;}
        /// <summary>
        /// Where a hit lands on the drawn body: `point` the replicated hit point (on the authority's capsule, or a blast's centre),
        /// `dir` the shot's travel (the push). For a blast the point is moved onto the body toward the blast. Picks the joint whose
        /// part (a capsule round its segment, the kind's girth) is nearest, and returns the surface point there facing the shot,
        /// its outward normal (out of the wound) and whether it is the head. `bones`: the 22 joint world matrices (System.Numerics).
        /// </summary>
        public static int Place(Matrix4x4[] bones,int kind,Vector3 point,Vector3 dir,bool blast,out Vector3 surface,out Vector3 normal,out bool head,bool aimHead=false)
        {
            var root=Origin(bones[CapturedRig.Chest]);float scale=ScaleOf(bones[CapturedRig.Body]);
            dir=Unit(dir,Vector3.UnitZ);
            // A blast's point is its centre: the body's side facing it, at the chest's height or the floor-hugging parts for a low one.
            if(blast){var toward=Unit(new Vector3(point.X-root.X,0,point.Z-root.Z),-dir);float y=Math.Max(Origin(bones[CapturedRig.Pelvis]).Y-.3f*scale,Math.Min(root.Y+.25f*scale,point.Y+.6f*scale));point=new Vector3(root.X,y,root.Z)+toward*.5f*scale;}
            int best=CapturedRig.Chest;float bestD=float.MaxValue;Vector3 bestC=root;
            for(int j=1;j<22;j++)
            {
                Segment(bones,j,out var a,out var b);var c=Closest(a,b,point);float r=SegmentRadius[j]*Girth(kind,Trunk(j))*scale,d=Vector3.Distance(c,point)-r;
                if(d<bestD&&(!aimHead||j==CapturedRig.Head)){bestD=d;best=j;bestC=c;}
            }
            float rad=SegmentRadius[best]*Girth(kind,Trunk(best))*scale;var off=point-bestC;
            // On the part's side facing the shot: the hit point's side, else back along the shot.
            var n=off.LengthSquared()>1e-6f?Vector3.Normalize(off):-dir;if(Vector3.Dot(n,dir)>.35f)n=Unit(n-dir*Vector3.Dot(n,dir)*1.4f,-dir);
            surface=bestC+n*rad;normal=n;head=best==CapturedRig.Head||best==CapturedRig.Neck&&point.Y>Origin(bones[CapturedRig.Head]).Y;return best;
        }
        public static Vector3 Closest(Vector3 a,Vector3 b,Vector3 p){var ab=b-a;float l=ab.LengthSquared();if(l<1e-10f)return a;float t=Math.Max(0,Math.Min(1,Vector3.Dot(p-a,ab)/l));return a+ab*t;}
        public static Vector3 Unit(Vector3 v,Vector3 fallback){float l=v.Length();return l>1e-5f&&l<1e6f?v/l:fallback;}

        // ================================================================================================================
        // Hashes, palette
        // ================================================================================================================
        public static float H(uint seed,int i,int k)=>(float)NativeGoreRules.Hash01(seed,i,k);
        /// <summary>A hit's seed: the add's id digits and the event's time (what every peer holds of a replicated hit).</summary>
        public static uint HitSeed(int id,float time)=>NativeGoreRules.FnvDigits(id)^unchecked((uint)BitConverter.SingleToInt32Bits(time)*2654435761u);
        /// <summary>A death's seed: the add's id digits and the death record's seed (an event id).</summary>
        public static uint DeathSeed(int id,int seed)=>NativeGoreRules.FnvDigits(id)^unchecked((uint)seed*2246822519u^0x9e3779b9u);
        /// <summary>The palette (sRGB): dried and fresh blood, wet flesh, deep wound, bone, suit and skin scraps.</summary>
        public const int BloodWet=0x6e0710,BloodDark=0x3a0308,Flesh=0x8c2a2c,FleshDark=0x5a1418,Hole=0x170204,Bone=0xd4c9a8,Fat=0xc9a27c,Suit=0x343d48,Skin=0x8f9a85,Scorch=0x2a1712;
        public static Vector3 Lin(int hex){NativeBlastSim.Linear(hex,out double r,out double g,out double b);return new Vector3((float)r,(float)g,(float)b);}
        /// <summary>A wet surface's gloss and specular share as it dries (fresh 1 .. dry 0 by <see cref="DryAt"/>).</summary>
        public static float Wet(float age)=>!(age>=0)?1:1-MeleeMath.Smooth(age/DryAt)*.85f;

        // ================================================================================================================
        // Sprays, mist, bits, drips
        // ================================================================================================================
        /// <summary>The blood sim's wound kind and severity for a hit (NativeBloodRules.Count): a bullet's entry puff and exit cone,
        /// a heavy round's or a head hit's cavity, a blade's cut, a blast's or a crusher's spatter.</summary>
        public static string SprayKind(Cause c,bool head)=>c==Cause.Melee?"cut":c==Cause.Blast||c==Cause.Crush?"crush":c==Cause.Heavy||head?"cavity":"puncture";
        public static float Severity(Cause c,bool head,float amount,float maxHp){float s=maxHp>0?amount/maxHp*1.6f:.5f;if(head)s+=.35f;if(c==Cause.Heavy||c==Cause.Blast)s+=.25f;return MeleeMath.Clamp(s,.2f,1);}
        /// <summary>Extra drops on a head hit and a kill.</summary>
        public static int Extra(Cause c,bool head,bool kill,int tier){int n=(head?10:0)+(kill?8:0)+(c==Cause.Heavy?4:0);return tier==Low?n/3:tier==Reduced?n*2/3:n;}

        /// <summary>
        /// Mist puff k of a hit `age` s old at the wound (`n` out of it, `dir` the shot): a fine red cloud blown back out of the
        /// entry and, for a head or heavy hit, a larger one out of the exit; growing and thinning over .45 s. False when gone.
        /// </summary>
        public static bool MistPuff(uint seed,int k,float age,Vector3 at,Vector3 n,Vector3 dir,float power,out Vector3 p,out float size,out float opacity,out float u)
        {
            float life=.32f+.18f*H(seed,k,11)+.12f*power;u=age/life;p=at;size=opacity=0;if(age<0||u>=1)return false;
            bool exit=k%2==1;var push=exit?dir:Unit(n*.7f-dir*.5f,n);float speed=(.9f+.8f*H(seed,k,12))*(.6f+.6f*power);float travel=speed*life*(1-(float)Math.Exp(-u*3))/3;
            var side=Unit(Vector3.Cross(push,Vector3.UnitY),Vector3.UnitX);p=at+push*travel+side*(H(seed,k,13)-.5f)*.08f+Vector3.UnitY*(.04f*u);
            size=(.09f+.07f*H(seed,k,14))*(.45f+.55f*power)*(1+1.3f*(float)Math.Sqrt(u));opacity=(.4f+.22f*power)*(1-MeleeMath.Smooth(u))*(u<.12f?u/.12f:1);return true;
        }
        /// <summary>
        /// Flesh bit i of a hit or a gib: thrown from `at` along `push` (the hit's back-spray and exit, a gib's blast) with gravity,
        /// bouncing once off the floor (`floor` its height) and sliding to rest, spinning until then; small bits shrink away by
        /// <see cref="BurstLife"/> (a gib's chunks stay: `stay`). Writes its centre, turn, edge (m) and colour; false once gone.
        /// </summary>
        public static bool Bit(uint seed,int i,float age,Vector3 at,Vector3 push,float power,float floor,bool stay,out Vector3 p,out Quaternion turn,out float size,out int hex)
        {
            p=at;turn=Quaternion.Identity;size=0;hex=Flesh;if(!(age>=0))return false;
            float h1=H(seed,i,21),h2=H(seed,i,22),h3=H(seed,i,23),h4=H(seed,i,24),h5=H(seed,i,25);
            // A hit's bits are mostly wet flesh (a sliver of bone, fat or skin now and then); a gib's chunks add bone, fat and the suit.
            hex=stay?(h5<.42f?Flesh:h5<.68f?FleshDark:h5<.8f?Bone:h5<.88f?Fat:h5<.94f?Skin:Suit):h5<.52f?Flesh:h5<.86f?FleshDark:h5<.91f?Bone:h5<.95f?Fat:h5<.98f?Skin:Suit;
            size=(stay?.03f+.05f*h4*h4:.011f+.017f*h4)*(.8f+.4f*power);if(hex==Bone)size*=.8f;
            if(!stay){float fade=1-MeleeMath.Smooth((age-1.3f)/(BurstLife-1.3f));size*=fade;if(size<.002f)return false;}
            // The launch: a cone round the push, wider for a gib.
            var dir=Unit(push,Vector3.UnitZ);var side=Unit(Vector3.Cross(dir,Vector3.UnitY),Vector3.UnitX);var up=Vector3.Cross(side,dir);
            float spread=stay?1.1f:.65f,a=(h1-.5f)*spread,b=(h2-.5f)*spread;var v=Unit(dir+side*a*2+up*b*2,dir)*(stay?2.2f+4.5f*h3:1.4f+2.6f*h3)*(.6f+.6f*power);v.Y+=stay?1.6f+2.2f*h2:.6f+1.1f*h2;
            Ballistic(at,v,age,floor+size*.5f,out p,out float rest);
            float spin=(6+10*h4)*(h1<.5f?-1:1),ang=spin*Math.Min(age,rest);var axis=Unit(new Vector3(h2-.5f,.4f,h3-.5f),Vector3.UnitY);
            turn=Quaternion.CreateFromAxisAngle(axis,ang)*Quaternion.CreateFromAxisAngle(Vector3.UnitY,h5*6.283f);return true;
        }
        /// <summary>
        /// A thrown piece from `at` at velocity `v`: the arc to the floor (height `floor`), one bounce keeping .28 of its fall and
        /// .45 of its slide, a short slide, then rest. Pure in `age`; `rest` the time it comes to rest.
        /// </summary>
        public static void Ballistic(Vector3 at,Vector3 v,float age,float floor,out Vector3 p,out float rest)
        {
            const float g=9.8f;float drop=Math.Max(0,at.Y-floor);float t1=(v.Y+(float)Math.Sqrt(v.Y*v.Y+2*g*drop))/g;var flat=new Vector3(v.X,0,v.Z);
            float b=Math.Max(0,(v.Y-g*t1))*-.28f,t2=2*b/g;rest=t1+t2+.25f;
            if(age<=t1){p=at+v*age+new Vector3(0,-.5f*g*age*age,0);if(p.Y<floor)p.Y=floor;return;}
            var land=at+flat*t1;land.Y=floor;float a2=age-t1;
            if(a2<=t2){p=land+flat*.45f*a2;p.Y=floor+b*a2-.5f*g*a2*a2;if(p.Y<floor)p.Y=floor;return;}
            float a3=Math.Min(a2-t2,.25f);p=land+flat*.45f*t2+flat*.2f*(a3-a3*a3*2);p.Y=floor;
        }
        /// <summary>How many drips a wound `age` s old has shed (a decaying trickle over <see cref="DripSeconds"/>, `most` in all).</summary>
        public static int Drips(float age,int most)=>!(age>0)||most<=0?0:Math.Min(most,(int)Math.Floor(most*(1-(float)Math.Exp(-age/1.3f))/(1-(float)Math.Exp(-DripSeconds/1.3f))+.0001f));

        // ================================================================================================================
        // Gib chunks, the stump and the pool
        // ================================================================================================================
        /// <summary>
        /// Chunk i of a gib (`fate`) burst from `at` (the trunk's or the head's centre) `age` s ago, `push` the killing blow's
        /// travel, `scale` the body's: a crush flattens them low and wide, a head gib throws small skull and flesh chunks up and
        /// back, a gib throws the trunk's flesh, ribs and scraps of suit out round the push. They come to rest and stay.
        /// </summary>
        public static bool Chunk(uint seed,int i,Fate fate,float age,Vector3 at,Vector3 push,float scale,float floor,out Vector3 p,out Quaternion turn,out Vector3 size,out int hex)
        {
            p=at;turn=Quaternion.Identity;size=Vector3.Zero;hex=Flesh;if(!(age>=0))return false;
            float h1=H(seed,i,31),h2=H(seed,i,32),h3=H(seed,i,33),h4=H(seed,i,34),h5=H(seed,i,35),h6=H(seed,i,36);bool head=fate==Fate.HeadGib,crush=fate==Fate.Crush;
            hex=head?(h5<.36f?Bone:h5<.75f?Flesh:h5<.88f?FleshDark:Skin):h5<.38f?Flesh:h5<.58f?FleshDark:h5<.7f?Bone:h5<.8f?Fat:h5<.9f?Suit:Skin;
            float e=(head?.024f+.03f*h4:.032f+.06f*h4*h4)*scale;size=new Vector3(e*(.8f+.5f*h1),e*(.6f+.5f*h2),e*(.8f+.5f*h3));if(hex==Bone&&!head)size.Y*=.45f;
            var dir=Unit(new Vector3(push.X,0,push.Z),Vector3.UnitZ);var side=new Vector3(dir.Z,0,-dir.X);float ang=(h1-.5f)*(crush?3.6f:head?2.4f:4.2f);
            var flat=Unit(dir*(float)Math.Cos(ang)+side*(float)Math.Sin(ang),dir);float speed=crush?1.2f+2.4f*h2:head?1+2.6f*h2:1.8f+4.4f*h2;
            var v=flat*speed*(.6f+.4f*scale);v.Y=crush?.4f+1.2f*h3:head?1.6f+2.6f*h3:1.2f+3.6f*h3;
            var start=at+new Vector3((h4-.5f)*.25f,(h6-.5f)*.3f,(h5-.5f)*.25f)*scale;
            Ballistic(start,v,age,floor+size.Y*.5f,out p,out float rest);
            float spin=(4+11*h6)*(h2<.5f?-1:1),turnAng=spin*Math.Min(age,rest);var axis=Unit(new Vector3(h3-.5f,.3f,h1-.5f),Vector3.UnitY);
            turn=Quaternion.CreateFromAxisAngle(axis,turnAng)*Quaternion.CreateFromAxisAngle(Vector3.UnitY,h4*6.283f);
            // At rest a chunk lies flat (its turn eased level by its last bounce).
            if(age>rest-.2f){float k=MeleeMath.Smooth((age-rest+.2f)/.35f);turn=Quaternion.Slerp(turn,Quaternion.CreateFromAxisAngle(Vector3.UnitY,h4*6.283f+turnAng*.3f),k);}
            return true;
        }
        /// <summary>The chunks a fate throws on a tier (a head gib a third of them; none for a plain corpse).</summary>
        public static int ChunkCount(Fate f,int tier)=>f==Fate.HeadGib?Math.Max(3,Chunks[T(tier)]/3):f==Fate.Corpse?0:f==Fate.Burst?Chunks[T(tier)]*2/3:Chunks[T(tier)];
        /// <summary>
        /// The pool under a corpse (or a gib's remains) `age` s after the body hit the floor: it spreads fast, then creeps (a viscous
        /// spread), to `most` m. Its outline has <see cref="PoolLobes"/> lobes by seed (<see cref="PoolLobe"/>).
        /// </summary>
        public static float PoolRadius(float age,float most)=>!(age>0)?0:most*(1-(float)Math.Exp(-age/1.7f))*(.35f+.65f*MeleeMath.Smooth(age/.6f));
        public const int PoolLobes=18;
        public static float PoolLobe(uint seed,int k,float age){float a=H(seed,k,41),b=H(seed,(k+1)%PoolLobes,41);return .78f+.34f*a+.06f*(float)Math.Sin(age*.7f+b*6)*(1-MeleeMath.Smooth(age/6));}
        /// <summary>The pool's largest radius for a kind, a fate and the wounds the body carried.</summary>
        public static float PoolMost(int kind,Fate f,int wounds,float scale){float r=.42f+.05f*Math.Min(4,wounds);if(f==Fate.Gib||f==Fate.Crush||f==Fate.Burst)r+=.32f;else if(f==Fate.HeadGib)r+=.12f;return r*Girth(kind,true)*(scale>1?(float)Math.Sqrt(scale):1);}

        /// <summary>
        /// A vent climb's dust (a standard zombie's entrance; the Siren's black-flame pool and flames are gone): puff i `e` s into
        /// the climb at the vent's floor round the add, grit and dust kicked up as it claws out over the rim. Writes its offset from
        /// the add's floor point, size, opacity and age share; false while it rests.
        /// </summary>
        public const int VentPuffs=10;
        public static bool VentDust(uint seed,int i,float e,out Vector3 offset,out float size,out float opacity,out float u)
        {
            float born=.3f+1.4f*H(seed,i,71),life=.7f+.5f*H(seed,i,72);u=(e-born)/life;offset=Vector3.Zero;size=opacity=0;if(!(u>=0)||u>=1)return false;
            float a=H(seed,i,73)*6.283f,r=.25f+.35f*H(seed,i,74)+.3f*(float)Math.Sqrt(u);
            offset=new Vector3((float)Math.Cos(a)*r,.05f+.35f*u*(.5f+H(seed,i,75)),(float)Math.Sin(a)*r);
            size=(.18f+.16f*H(seed,i,76))*(.6f+.8f*(float)Math.Sqrt(u));opacity=.45f*(1-MeleeMath.Smooth(u))*Math.Min(1,u/.15f);return true;
        }
        /// <summary>The neck's fountain after a head gib: drop slot k leaves at `k`·<see cref="FountainStep"/> s, pulsing weaker,
        /// for <see cref="FountainSeconds"/> s (how many are due at `age`).</summary>
        public const float FountainStep=.07f,FountainSeconds=1.5f;
        public static int FountainDue(float age)=>!(age>=0)?0:Math.Min((int)(FountainSeconds/FountainStep),1+(int)Math.Floor(age/FountainStep));
        public static float FountainPower(int k){float t=k*FountainStep;return (.55f+.45f*(float)Math.Abs(Math.Sin(t*7.5f)))*(1-t/FountainSeconds*.8f);}
        /// <summary>A flying part's trail: drop slot k leaves its stump at k·<see cref="TrailStep"/> s for <see cref="TrailSeconds"/> s.</summary>
        public const float TrailStep=.055f,TrailSeconds=1.1f;
        public static int TrailDue(float age,int tier)=>!(age>=0)?0:Math.Min((int)(TrailSeconds/TrailStep)/(tier==Low?3:tier==Reduced?2:1),1+(int)Math.Floor(age/(TrailStep*(tier==Low?3:tier==Reduced?2:1))));

        // ================================================================================================================
        // Corpses
        // ================================================================================================================
        /// <summary>A corpse's sink (m) and opacity at `age` s after its death, sinking from `fadeAt` (its life's end, or earlier
        /// when the cap pushed it out).</summary>
        public static void Sink(float age,float fadeAt,out float sink,out float alpha)
        {
            float u=MeleeMath.Clamp((age-fadeAt)/SinkSeconds,0,1);sink=SinkDepth*u*u;alpha=1-MeleeMath.Smooth((u-.25f)/.75f);
        }
        public static bool Gone(float age,float fadeAt)=>age>=fadeAt+SinkSeconds;
    }

    /// <summary>One replicated hit on an add as the presentation reads it (Runtime/NativeHordeGorePresentation.cs fills it).</summary>
    public struct GoreHit
    {
        public int Id,Kind,Index;public float Time,Amount,MaxHp;public NativeHordeGore.Cause Cause;public bool Kill;
        /// <summary>The authority called it a head shot (EventKind.Headshot; adds do not send it yet: the head is then the drawn
        /// part nearest the hit).</summary>
        public bool Head;
        /// <summary>The hit point, the shot's travel (world) and, for a melee blow, the blade's sweep across the body.</summary>
        public Vector3 Point,Dir,Sweep;
    }
    /// <summary>One lit voxel the gore draws (world; linear colours): a wound's block, a flesh bit, a chunk, a stump.</summary>
    public struct GoreBlock{public Vector3 At,Size,Albedo,Emit;public Quaternion Turn;public float Spec,Gloss;}
    /// <summary>One red mist puff (world): its size, opacity and age share.</summary>
    public struct GoreMist{public Vector3 At;public float Size,Opacity,U;public uint Seed;}
    /// <summary>One blood sim spray the gore asks for (NativeBloodSim.Spray's arguments; `Kind` one of the sim's own kinds).</summary>
    public struct GoreSpray{public string Kind;public float Severity,Time;public uint Seed;public long Key;public Vector3 Origin,Normal,Along;public int Extra,Target;public bool Kill;}
    /// <summary>One pool to draw (world): centre on the floor, radius, seed, age (for its lobes and drying) and opacity.</summary>
    public struct GorePool{public Vector3 At;public float Radius,Age,Alpha;public uint Seed;}

    /// <summary>
    /// The horde gore's state on one presentation: each add's wounds, the hits' mist and bits in flight, the remains (corpses,
    /// gib chunks and pools) under their caps, and the sprays asked of the blood sim. Fixed arrays; nothing allocates after
    /// construction. The runtime feeds it the replicated hits and deaths and the drawn joints, then draws what
    /// <see cref="Collect"/> and <see cref="DrawWounds"/> write.
    /// </summary>
    public sealed class NativeHordeGoreState
    {
        public int Tier=NativeHordeGore.Full;public bool Enabled=true;
        // ---- wounds ----
        public struct Wound{public int Bone,Bled;public Vector3 P,N,T,Down;public float Size,At;public NativeHordeGore.Cause Cause;public bool Head,Stump;public uint Seed;}
        public sealed class AddGore{public int Id=-1;public float Seen;public readonly Wound[] W=new Wound[NativeHordeGore.WoundMax];public int Count,Next;}
        public readonly AddGore[] Adds=new AddGore[NativeHordeGore.AddMax];
        // ---- the hits' mist and bits ----
        public struct Burst{public bool Live;public Vector3 At,N,Dir;public float Born,Floor,Power;public uint Seed;public int Bits,Mist;}
        public readonly Burst[] Bursts=new Burst[NativeHordeGore.BurstMax];private int burstNext;
        // ---- remains: a corpse (its pose, model and pieces hidden), a gib's chunks, the pool ----
        public sealed class Remains
        {
            public bool Live,Body,Posed;public int Id,Kind,Look,Model,Lost,Hidden,Wounds,WoundNext,Extra=-1,Prop=-1,Frame,Bled;public float At,FadeAt,Floor,PoolAt,PoolMost,Jaw,Scale=1,ChunkDelay;public uint Seed;
            public NativeHordeGore.Fate Fate;public Vector3 PoolCentre,ChunkAt,Push;public Vector4 Variation=Vector4.One;
            public Matrix4x4 Root=Matrix4x4.Identity,PropMatrix=Matrix4x4.Identity;public readonly Matrix4x4[] Bones=new Matrix4x4[22];public readonly Wound[] W=new Wound[NativeHordeGore.WoundMax];
        }
        public readonly Remains[] Corpses=new Remains[NativeHordeGore.RemainsMax];
        // ---- the gibs inside the window (the cap) ----
        private readonly float[] gibAt=new float[16];private int gibNext;
        // ---- out ----
        public readonly GoreBlock[] Blocks=new GoreBlock[NativeHordeGore.BlockMax];public int BlockCount;
        public readonly GoreMist[] Mists=new GoreMist[NativeHordeGore.MistMax];public int MistCount;
        public readonly GorePool[] Pools=new GorePool[NativeHordeGore.RemainsMax];public int PoolCount;
        public readonly GoreSpray[] Sprays=new GoreSpray[NativeHordeGore.SprayMax];public int SprayCount;
        private readonly Matrix4x4[] scratch=new Matrix4x4[22];

        public NativeHordeGoreState(){for(int i=0;i<Adds.Length;i++)Adds[i]=new AddGore();for(int i=0;i<Corpses.Length;i++)Corpses[i]=new Remains();for(int i=0;i<gibAt.Length;i++)gibAt[i]=float.NegativeInfinity;}
        /// <summary>A new arena or a rewind: everything goes.</summary>
        public void Clear()
        {
            foreach(var a in Adds){a.Id=-1;a.Count=a.Next=0;}for(int i=0;i<Bursts.Length;i++)Bursts[i].Live=false;foreach(var c in Corpses)c.Live=false;
            for(int i=0;i<gibAt.Length;i++)gibAt[i]=float.NegativeInfinity;BlockCount=MistCount=PoolCount=SprayCount=0;burstNext=gibNext=0;
        }
        public int Wounds(int id){var a=Find(id,false);return a?.Count??0;}
        public AddGore Find(int id,bool make,float now=0)
        {
            for(int i=0;i<Adds.Length;i++)if(Adds[i].Id==id)return Adds[i];if(!make)return null;
            int o=0;for(int i=0;i<Adds.Length;i++){if(Adds[i].Id<0){o=i;break;}if(Adds[i].Seen<Adds[o].Seen)o=i;}
            var s=Adds[o];s.Id=id;s.Count=s.Next=0;s.Seen=now;return s;
        }
        /// <summary>Forgets an add's wounds (it left without dying, or its remains took them).</summary>
        public void Forget(int id){var a=Find(id,false);if(a!=null){a.Id=-1;a.Count=a.Next=0;}}

        // ================================================================================================================
        // A hit
        // ================================================================================================================
        /// <summary>
        /// One replicated hit on an add drawn with `bones` (its 22 joint world matrices, System.Numerics) standing on `floor`:
        /// places the wound and keeps it on the struck joint, puffs the mist and throws the bits, and asks the blood sim for the
        /// spray. Returns the struck joint (-1 for a tick that never bleeds). A hit seen twice (the same add and time) is one.
        /// </summary>
        public int Hit(in GoreHit h,Matrix4x4[] bones,float floor,out bool head)
        {
            head=false;if(!Enabled||h.Cause==NativeHordeGore.Cause.None||bones==null)return -1;var g=Find(h.Id,true,h.Time);g.Seen=Math.Max(g.Seen,h.Time);
            int bone=NativeHordeGore.Place(bones,h.Kind,h.Point,h.Dir,h.Cause==NativeHordeGore.Cause.Blast||h.Cause==NativeHordeGore.Cause.Crush,out var surface,out var n,out head,h.Head);
            uint seed=NativeHordeGore.HitSeed(h.Id,h.Time)^unchecked((uint)h.Index*0x9e3779b1u);var dir=NativeHordeGore.Unit(h.Dir,-n);
            AddWound(g,bones[bone],bone,surface,n,h.Cause==NativeHordeGore.Cause.Melee&&h.Sweep.LengthSquared()>1e-4f?h.Sweep:dir,h.Time,h.Cause,head,false,seed,NativeHordeGore.ScaleOf(bones[CapturedRig.Body]));
            int t=Tier;float power=MeleeMath.Clamp((h.MaxHp>0?h.Amount/h.MaxHp:.3f)*1.5f+(head?.4f:0)+(h.Cause==NativeHordeGore.Cause.Heavy||h.Cause==NativeHordeGore.Cause.Blast?.3f:0),.15f,1);
            int bits=NativeHordeGore.Bits[t]*(head?3:2)/3,mist=Math.Min(NativeHordeGore.Mist[t],head||power>.6f?3:h.Cause==NativeHordeGore.Cause.Melee?1:2);
            ref var b=ref Bursts[burstNext++%Bursts.Length];b=new NativeHordeGoreState.Burst{Live=true,At=surface,N=n,Dir=dir,Born=h.Time,Floor=floor,Power=power,Seed=seed,Bits=bits,Mist=mist};
            long key=((long)(h.Id&0xffffff)<<36)^((long)(BitConverter.SingleToInt32Bits(h.Time)&0xfffffff)<<4)^((long)(h.Index&15))^(1L<<62);
            Spray(NativeHordeGore.SprayKind(h.Cause,head),NativeHordeGore.Severity(h.Cause,head,h.Amount,h.MaxHp),seed,key,surface+n*.01f,n,dir,h.Time,NativeHordeGore.Extra(h.Cause,head,h.Kill,t),h.Id,h.Kill);
            return bone;
        }
        /// <summary>
        /// A gib's (or a head burst's, the bloater's) burst at `at` at `time` (`push` the blow's travel, `power` 0..1): a cloud of
        /// red mist, a shower of flesh bits and a heavy spatter of blood round the push that paints the floor and the walls near it.
        /// </summary>
        public void Blast(int id,Vector3 at,Vector3 push,float time,float floor,uint seed,float power)
        {
            if(!Enabled)return;int t=Tier<0?0:Tier>2?2:Tier;push=NativeHordeGore.Unit(push,Vector3.UnitZ);var up=NativeHordeGore.Unit(push*.45f+Vector3.UnitY,Vector3.UnitY);
            ref var b=ref Bursts[burstNext++%Bursts.Length];b=new Burst{Live=true,At=at,N=up,Dir=push,Born=time,Floor=floor,Power=MeleeMath.Clamp(power,.2f,1),Seed=seed^0xb1a5u,Bits=NativeHordeGore.Bits[t]*2,Mist=NativeHordeGore.Mist[t]+2};
            long key=((long)(id&0xffffff)<<36)^((long)(BitConverter.SingleToInt32Bits(time)&0xfffffff)<<4)^(1L<<58);
            Spray("crush",1,seed^0x5eedu,key,at,up,push,time,(int)((t==NativeHordeGore.Low?8:t==NativeHordeGore.Reduced?16:28)*power),id,true);
            Spray("sever",1,seed^0x5eeeu,key^0x1234567L,at,push,push,time,(int)((t==NativeHordeGore.Low?4:t==NativeHordeGore.Reduced?8:14)*power),id,true);
        }
        /// <summary>A stump where the authority severed a limb (or the blade cut it): a raw flesh cap round the bone on the parent
        /// joint, kept with the add's wounds, and a gush along the cut.</summary>
        public void Stump(int id,Matrix4x4[] bones,int joint,float time,Vector3 along,int kind=-1)
        {
            if(!Enabled||bones==null||joint<=0||joint>=22)return;var g=Find(id,true,time);int parent=CapturedRig.Bones[joint].Parent;if(parent<0)return;
            for(int i=0;i<g.Count;i++)if(g.W[i].Stump&&g.W[i].Bone==parent&&Math.Abs(g.W[i].At-time)<.05f)return;
            var at=NativeHordeGore.Origin(bones[joint]);var axis=NativeHordeGore.Unit(at-NativeHordeGore.Origin(bones[parent]),Vector3.UnitY);uint seed=NativeHordeGore.HitSeed(id,time)^0x5157u;
            AddWound(g,bones[parent],parent,at,axis,along,time,NativeHordeGore.Cause.Melee,joint==CapturedRig.Head,true,seed,NativeHordeGore.ScaleOf(bones[CapturedRig.Body]),NativeHordeGore.CutRadius(kind,parent));
            long key=((long)(id&0xffffff)<<36)^((long)(BitConverter.SingleToInt32Bits(time)&0xfffffff)<<4)^((long)joint<<30)^(1L<<61);
            Spray("sever",.8f,seed,key,at,axis,NativeHordeGore.Unit(along,axis),time,joint==CapturedRig.Head?10:4,id,false);
        }
        private void AddWound(AddGore g,in Matrix4x4 bone,int joint,Vector3 surface,Vector3 n,Vector3 along,float time,NativeHordeGore.Cause cause,bool head,bool stump,uint seed,float scale,float cut=0)
        {
            if(!Matrix4x4.Invert(bone,out var inv))return;
            var p=Vector3.Transform(surface,inv);var ln=NativeHordeGore.Unit(Vector3.TransformNormal(n,inv),Vector3.UnitZ);
            var down=Vector3.TransformNormal(-Vector3.UnitY,inv);down=NativeHordeGore.Unit(down-ln*Vector3.Dot(down,ln),Vector3.Cross(ln,Vector3.UnitX));
            var t=Vector3.TransformNormal(along,inv);t=NativeHordeGore.Unit(t-ln*Vector3.Dot(t,ln),Vector3.Cross(ln,down));
            float size=(cause==NativeHordeGore.Cause.Melee?.03f:cause==NativeHordeGore.Cause.Blast||cause==NativeHordeGore.Cause.Crush?.05f:cause==NativeHordeGore.Cause.Heavy?.036f:.026f)*(head&&!stump?1.25f:1)*(.85f+.3f*NativeHordeGore.H(seed,0,51));
            if(stump)size=cut>0?cut:NativeHordeGore.CutRadius(-1,joint);
            var w=new Wound{Bone=joint,P=p,N=ln,T=t,Down=down,Size=size,At=time,Cause=cause,Head=head,Stump=stump,Seed=seed};
            if(g.Count<g.W.Length){g.W[g.Count++]=w;g.Next=g.Count%g.W.Length;}else{g.W[g.Next]=w;g.Next=(g.Next+1)%g.W.Length;}
        }
        private void Spray(string kind,float severity,uint seed,long key,Vector3 origin,Vector3 normal,Vector3 along,float time,int extra,int target,bool kill)
        {
            if(SprayCount>=Sprays.Length)return;Sprays[SprayCount++]=new GoreSpray{Kind=kind,Severity=severity,Seed=seed,Key=key,Origin=origin,Normal=normal,Along=along,Time=time,Extra=extra,Target=target,Kill=kill};
        }
        /// <summary>The runtime has handed the frame's sprays to the blood sim.</summary>
        public void Sprayed()=>SprayCount=0;

        // ================================================================================================================
        // Deaths
        // ================================================================================================================
        /// <summary>
        /// A death's fate under the gib cap: <see cref="NativeHordeGore.Decide"/>, then a gib past <see cref="NativeHordeGore.GibCap"/>
        /// within the window (counted by the deaths' own times, in the order they are learnt) falls as a corpse. The bloater's own
        /// burst is never capped (its death is its attack's rule). Gore off: a corpse.
        /// </summary>
        public NativeHordeGore.Fate Fate(int kind,float at,NativeHordeGore.Cause cause,bool head,float amount,float maxHp,float blastDistance,bool heavyMelee)
        {
            var f=NativeHordeGore.Decide(kind,cause,head,amount,maxHp,blastDistance,heavyMelee);if(!Enabled)return f==NativeHordeGore.Fate.Burst?f:NativeHordeGore.Fate.Corpse;
            if(!NativeHordeGore.Gibs(f))return f;int recent=0;for(int i=0;i<gibAt.Length;i++)if(at-gibAt[i]>=0&&at-gibAt[i]<NativeHordeGore.GibWindow)recent++;
            if(recent>=NativeHordeGore.GibCap[Tier<0?0:Tier>2?2:Tier])return NativeHordeGore.Fate.Corpse;gibAt[gibNext++%gibAt.Length]=at;return f;
        }
        /// <summary>The remains of add `id` (null before it settles).</summary>
        public Remains RemainsOf(int id){foreach(var c in Corpses)if(c.Live&&c.Id==id)return c;return null;}
        /// <summary>
        /// An add has died at `at` (its death record's time): its remains join the pile now (a corpse's pose is filled when the
        /// fall settles, <see cref="Pose"/>; a gib's are its chunks and pool). Past the tier's cap the oldest remains (by death
        /// time, then id) start sinking at `at`; with no slot free the one furthest gone makes room. Returns the remains to fill
        /// (model, pool, chunks), or the existing ones for the same add and death.
        /// </summary>
        public Remains Die(int id,int kind,float at,uint seed,NativeHordeGore.Fate fate)
        {
            foreach(var c in Corpses)if(c.Live&&c.Id==id&&Math.Abs(c.At-at)<.05f)return c;
            int t=Tier<0?0:Tier>2?2:Tier,steady=0;foreach(var c in Corpses)if(c.Live&&!Fading(c,at))steady++;
            while(steady>=NativeHordeGore.CorpseCap[t])
            {
                Remains old=null;foreach(var c in Corpses)if(c.Live&&!Fading(c,at)&&(old==null||c.At<old.At||c.At==old.At&&c.Id<old.Id))old=c;
                if(old==null)break;old.FadeAt=Math.Max(old.At,at)-old.At;steady--;
            }
            Remains slot=null;foreach(var c in Corpses)if(!c.Live){slot=c;break;}
            if(slot==null){float most=-1;foreach(var c in Corpses){float u=at-c.At-c.FadeAt;if(u>most){most=u;slot=c;}}}
            slot.Live=true;slot.Id=id;slot.Kind=kind;slot.At=at;slot.Seed=seed;slot.Fate=fate;slot.FadeAt=NativeHordeGore.CorpseLife[t];slot.Body=fate!=NativeHordeGore.Fate.Gib&&fate!=NativeHordeGore.Fate.Crush&&fate!=NativeHordeGore.Fate.Burst;
            slot.Lost=slot.Hidden=0;slot.Wounds=slot.WoundNext=slot.Frame=slot.Bled=0;slot.Posed=false;slot.Extra=slot.Prop=-1;slot.Jaw=0;slot.Scale=1;slot.ChunkDelay=0;slot.PoolAt=at+NativeHordeGait.DownAt;slot.PoolMost=0;slot.Variation=Vector4.One;slot.Push=Vector3.UnitZ;
            return slot;
        }
        /// <summary>The corpse's pose is filled (its fall has settled): the add's wounds move onto it, and it draws from now on.</summary>
        public void Pose(Remains r,int frame)
        {
            if(r==null)return;r.Posed=true;r.Frame=frame;var g=Find(r.Id,false);
            if(g!=null){for(int i=0;i<g.Count;i++)r.W[i]=g.W[i];r.Wounds=g.Count;r.WoundNext=g.Next;g.Id=-1;g.Count=g.Next=0;}
        }
        /// <summary>
        /// A wound list's drips due at `now` (a decaying trickle per wound over <see cref="NativeHordeGore.DripSeconds"/>, the
        /// tier's count): each a "drip" spray out of the wound and down, keyed by the wound and its slot so it stains once.
        /// </summary>
        public void Drip(Wound[] w,int count,Matrix4x4[] bones,float now,int target,float sink=0)
        {
            int most=NativeHordeGore.WoundDrips[Tier<0?0:Tier>2?2:Tier];
            for(int i=0;i<count&&i<w.Length;i++)
            {
                ref var x=ref w[i];float age=now-x.At;if(age<0||age>NativeHordeGore.DripSeconds+.2f||x.Bone<0||x.Bone>=22)continue;int due=NativeHordeGore.Drips(age,x.Stump?most*2:most);
                for(;x.Bled<due&&SprayCount<Sprays.Length;x.Bled++)
                {
                    var m=bones[x.Bone];var p=Vector3.Transform(x.P,m);p.Y-=sink;var n=NativeHordeGore.Unit(Vector3.TransformNormal(x.N,m),Vector3.UnitY);var down=NativeHordeGore.Unit(Vector3.TransformNormal(x.Down,m),-Vector3.UnitY);
                    long key=((long)(target&0xffffff)<<36)^((long)(BitConverter.SingleToInt32Bits(x.At)&0xfffffff)<<4)^((long)(x.Bled+1)<<30)^((long)x.Bone<<24)^(1L<<60);
                    Spray("drip",1,x.Seed^unchecked((uint)(x.Bled+1)*0x9e3779b1u),key,p+n*.012f+down*x.Size*.6f,n,down,x.At+age,0,target,false);
                }
            }
        }
        public void Drip(int id,Matrix4x4[] bones,float now){var g=Find(id,false);if(g!=null)Drip(g.W,g.Count,bones,now,id);}
        /// <summary>Whether remains are sinking at `now` (their fade start is an age: <see cref="Remains.FadeAt"/> s after the death).</summary>
        public static bool Fading(Remains c,float now)=>now-c.At>=c.FadeAt;
        /// <summary>Drops an add's remains at once (the DEV preview's loop restarting).</summary>
        public void Drop(int id){foreach(var c in Corpses)if(c.Live&&c.Id==id)c.Live=false;}
        /// <summary>Drops the remains that are gone at `now` (or that a rewind put in the future).</summary>
        public void Expire(float now){foreach(var c in Corpses)if(c.Live&&(NativeHordeGore.Gone(now-c.At,c.FadeAt)||c.At>now+.5f))c.Live=false;}
        public int LiveRemains{get{int n=0;foreach(var c in Corpses)if(c.Live)n++;return n;}}
        public int SteadyRemains(float now){int n=0;foreach(var c in Corpses)if(c.Live&&!Fading(c,now))n++;return n;}

        // ================================================================================================================
        // Drawing
        // ================================================================================================================
        /// <summary>Starts a frame's draw lists.</summary>
        public void Begin(){BlockCount=MistCount=PoolCount=0;}
        private void Block(Vector3 at,Quaternion turn,Vector3 size,int hex,float wet,float shade=1,float emit=0)
        {
            if(BlockCount>=Blocks.Length||!(size.X>1e-4f))return;var c=NativeHordeGore.Lin(hex)*shade;
            Blocks[BlockCount++]=new GoreBlock{At=at,Turn=turn,Size=size,Albedo=c,Emit=c*emit,Spec=.15f+.75f*wet,Gloss=.3f+.65f*wet};
        }
        /// <summary>
        /// An add's wounds on its joints `bones` at `now` (the newest `WoundsShown` of the tier): each a recessed dark core, torn
        /// flesh round it, a run of blood down the body (a gash along the blade; a blast's scorched patch; a stump's flesh cap round
        /// the bone), glossy while wet, darker as it dries. `sink` lowers a sinking corpse's; `hidden` skips joints drawn elsewhere.
        /// </summary>
        public void DrawWounds(int id,Matrix4x4[] bones,float now,float sink=0,int hidden=0)
        {
            var g=Find(id,false);if(g==null||bones==null)return;g.Seen=now;DrawWounds(g.W,g.Count,g.Next,bones,now,sink,hidden);
        }
        public void DrawWounds(Wound[] w,int count,int next,Matrix4x4[] bones,float now,float sink=0,int hidden=0)
        {
            int shown=Math.Min(count,NativeHordeGore.WoundsShown[Tier<0?0:Tier>2?2:Tier]);
            for(int k=0;k<shown;k++)
            {
                int i=((count<w.Length?count:next+w.Length)-1-k)%w.Length;ref var x=ref w[i];if(x.Bone<0||x.Bone>=22||(hidden&(1<<x.Bone))!=0)continue;
                var m=bones[x.Bone];float s=NativeHordeGore.ScaleOf(m);var p=Vector3.Transform(x.P,m);p.Y-=sink;
                var n=NativeHordeGore.Unit(Vector3.TransformNormal(x.N,m),Vector3.UnitY);var down=NativeHordeGore.Unit(Vector3.TransformNormal(x.Down,m),-Vector3.UnitY);var t=NativeHordeGore.Unit(Vector3.TransformNormal(x.T,m),Vector3.Cross(n,down));
                var b=NativeHordeGore.Unit(Vector3.Cross(n,t),down);t=Vector3.Cross(b,n);var turn=Basis(t,b,n);float age=now-x.At,wet=NativeHordeGore.Wet(age),dry=NativeBloodRules.Darken(age),z=x.Size*s;
                if(age<0)continue;
                if(x.Stump){StumpBlocks(p,turn,t,b,n,z,wet,dry,x.Seed);continue;}
                bool gash=x.Cause==NativeHordeGore.Cause.Melee,burn=x.Cause==NativeHordeGore.Cause.Blast||x.Cause==NativeHordeGore.Cause.Crush;
                // The core: a hole sunk into the surface (a long gash for a blade), its dark depth.
                if(gash){Block(p-n*z*.12f,turn,new Vector3(z*3.4f,z*.55f,z*.5f),NativeHordeGore.Hole,wet*.6f);
                    for(int e=-1;e<=1;e+=2)Block(p+b*e*z*.42f+n*z*.04f,turn,new Vector3(z*3f*(.8f+.2f*NativeHordeGore.H(x.Seed,e+2,62)),z*.34f,z*.32f),NativeHordeGore.Flesh,wet,dry);}
                else if(burn){Block(p-n*z*.05f,turn,new Vector3(z*1.6f,z*1.3f,z*.3f),NativeHordeGore.Scorch,.1f);Block(p+n*z*.02f,turn,new Vector3(z*.9f,z*.8f,z*.35f),NativeHordeGore.Flesh,wet,dry);
                    for(int r=0;r<2;r++){float a=NativeHordeGore.H(x.Seed,r,63)*6.283f;Block(p+(t*(float)Math.Cos(a)+b*(float)Math.Sin(a))*z*.75f+n*z*.06f,turn,new Vector3(z*.5f,z*.4f,z*.3f),NativeHordeGore.FleshDark,wet,dry);}}
                else
                {
                    Block(p-n*z*.15f,turn,new Vector3(z*.8f,z*.8f,z*.5f),NativeHordeGore.Hole,wet*.5f);
                    int torn=x.Head||x.Cause==NativeHordeGore.Cause.Heavy?4:3;
                    for(int r=0;r<torn;r++){float a=r*6.283f/torn+NativeHordeGore.H(x.Seed,r,64)*1.2f,d=z*(.55f+.25f*NativeHordeGore.H(x.Seed,r,65));Block(p+(t*(float)Math.Cos(a)+b*(float)Math.Sin(a))*d+n*z*.05f,turn,new Vector3(z*.55f,z*.45f,z*.36f),r%2==0?NativeHordeGore.Flesh:NativeHordeGore.FleshDark,wet,dry);}
                    if(x.Head&&z>.02f)Block(p+t*z*.3f+n*z*.12f,turn,new Vector3(z*.35f,z*.3f,z*.25f),NativeHordeGore.Bone,.3f);
                }
                // The run: blood down the body from the wound, longer the fresher the wound bled, a drop swelling at its end.
                float run=z*(2.2f+3.4f*NativeHordeGore.H(x.Seed,1,66))*MeleeMath.Smooth(age/1.6f+.15f);
                if(run>z*.4f){var r0=p-down*z*.4f+n*z*.02f;Block(r0-down*run*.5f,turn,new Vector3(z*.38f,run,z*.18f),NativeHordeGore.BloodWet,wet,dry);
                    Block(r0-down*run+n*z*.03f,turn,new Vector3(z*.5f,z*.5f,z*.3f),NativeHordeGore.BloodWet,wet,dry);}
            }
        }
        /// <summary>
        /// A raw stub over a cut at the joint `p` (`n` out of it, `z` the part's radius): a short column of wet flesh standing out
        /// of the cut (the models' parts overlap their joints by up to about .85 of their radius, so the stub covers the part's
        /// own end and stands a little proud where it does not), a glaze of blood on its face, the bone through its middle and
        /// ragged dark flesh round its rim.
        /// </summary>
        private void StumpBlocks(Vector3 p,Quaternion turn,Vector3 t,Vector3 b,Vector3 n,float z,float wet,float dry,uint seed)
        {
            Block(p+n*z*.35f,turn,new Vector3(z*2,z*2,z),NativeHordeGore.Flesh,wet,dry);
            Block(p+n*z*.86f,turn,new Vector3(z*1.6f,z*1.6f,z*.06f),NativeHordeGore.BloodWet,wet,dry);
            Block(p+n*z*.95f,turn,new Vector3(z*.62f,z*.62f,z*.42f),NativeHordeGore.Bone,wet*.4f);
            for(int r=0;r<3;r++){float a=r*2.1f+NativeHordeGore.H(seed,r,61)*.8f;var o=(t*(float)Math.Cos(a)+b*(float)Math.Sin(a))*z*.95f;Block(p+o+n*z*.72f,turn,new Vector3(z*.6f,z*.45f,z*.42f),NativeHordeGore.FleshDark,wet,dry);}
        }
        /// <summary>A thrown part's cut end at `at` (world), `axis` out of the cut, `radius` m, `age` s since the cut: its raw cap.</summary>
        public void Cap(Vector3 at,Vector3 axis,float radius,float age,uint seed,float sink=0)
        {
            if(!Enabled||!(radius>.002f)||!(age>=0))return;var n=NativeHordeGore.Unit(axis,Vector3.UnitY);var t=Vector3.Cross(n,Vector3.UnitY);t=t.LengthSquared()<1e-4f?Vector3.UnitX:Vector3.Normalize(t);var b=Vector3.Cross(n,t);
            StumpBlocks(at,Basis(t,b,n),t,b,n,radius,NativeHordeGore.Wet(age),NativeBloodRules.Darken(age),seed);
        }
        /// <summary>The rotation whose axes are t (x), b (y) and n (z).</summary>
        public static Quaternion Basis(Vector3 t,Vector3 b,Vector3 n)=>Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(new Matrix4x4(t.X,t.Y,t.Z,0,b.X,b.Y,b.Z,0,n.X,n.Y,n.Z,0,0,0,0,1)));
        /// <summary>
        /// The frame's world gore at `now`: the hits' mist and bits, the remains' gib chunks and pools and their wounds. `eye`:
        /// the camera (bits and mist past <see cref="NativeHordeGore.WoundRange"/> m are left out; chunks to twice that).
        /// </summary>
        public void Collect(float now,Vector3 eye)
        {
            int t=Tier<0?0:Tier>2?2:Tier;
            for(int i=0;i<Bursts.Length;i++)
            {
                ref var b=ref Bursts[i];if(!b.Live)continue;float age=now-b.Born;if(age>NativeHordeGore.BurstLife||age<-.5f){b.Live=false;continue;}if(age<0)continue;
                if(Vector3.DistanceSquared(b.At,eye)>NativeHordeGore.WoundRange*NativeHordeGore.WoundRange)continue;
                for(int k=0;k<b.Mist&&MistCount<Mists.Length;k++)if(NativeHordeGore.MistPuff(b.Seed,k,age,b.At,b.N,b.Dir,b.Power,out var mp,out float ms,out float mo,out float mu))Mists[MistCount++]=new GoreMist{At=mp,Size=ms,Opacity=mo,U=mu,Seed=b.Seed^(uint)k};
                var push=NativeHordeGore.Unit(b.N*.8f-b.Dir*.6f,b.N);
                for(int k=0;k<b.Bits;k++)
                {
                    // Two in three bits blow back out of the entry, the rest on along the shot (the exit).
                    var dir=k%3==2?b.Dir:push;
                    if(NativeHordeGore.Bit(b.Seed,k,age,b.At,dir,b.Power,b.Floor,false,out var p,out var q,out float s,out int hex))Block(p,q,new Vector3(s,s*.8f,s),hex,NativeHordeGore.Wet(age));
                }
            }
            foreach(var c in Corpses)
            {
                if(!c.Live)continue;float age=now-c.At;if(age<0)continue;NativeHordeGore.Sink(age,c.FadeAt,out float sink,out float alpha);
                if(c.PoolMost>0&&PoolCount<Pools.Length){float pr=NativeHordeGore.PoolRadius(now-c.PoolAt,c.PoolMost);if(pr>.01f)Pools[PoolCount++]=new GorePool{At=c.PoolCentre,Radius=pr,Age=now-c.PoolAt,Alpha=alpha,Seed=c.Seed};}
                if(Vector3.DistanceSquared(c.ChunkAt,eye)>4*NativeHordeGore.WoundRange*NativeHordeGore.WoundRange)continue;
                int chunks=NativeHordeGore.ChunkCount(c.Fate,t);float ca=age-c.ChunkDelay;
                for(int k=0;k<chunks;k++)if(NativeHordeGore.Chunk(c.Seed,k,c.Fate,ca,c.ChunkAt,c.Push,c.Scale,c.Floor,out var p,out var q,out var size,out int hex))
                {p.Y-=sink;size*=alpha<1?.4f+.6f*alpha:1;Block(p,q,size,hex,NativeHordeGore.Wet(ca),NativeBloodRules.Darken(ca));}
                if(c.Body&&c.Posed&&c.Wounds>0&&Vector3.DistanceSquared(NativeHordeGore.Origin(c.Bones[CapturedRig.Chest]),eye)<=NativeHordeGore.WoundRange*NativeHordeGore.WoundRange){DrawWounds(c.W,c.Wounds,c.WoundNext,c.Bones,now,sink,c.Hidden);Drip(c.W,c.Wounds,c.Bones,now,c.Id,sink);}
            }
        }
    }
}

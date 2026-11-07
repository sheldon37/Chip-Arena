using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One live charge (docs/BOMBADIER.md).</summary>
    [Serializable] public sealed class NativeBomb
    {
        public int Id,Stuck;public string Kind;public V3 Pos,Vel,Normal;public float At,SampleAt,ArmAt,BlowAt=-100;
        // SampleAt is the arena clock for Pos/Vel (At remains the spawn time); zero is valid for legacy frames.
        public bool Armed(float time)=>time>=ArmAt;
    }
    /// <summary>One blast of the last moments. `Link` is the chain link it made (0: none); `FromPos` the blast that handed on to it.</summary>
    [Serializable] public sealed class NativeBlast
    {
        public int Id,Link;public string Kind;public float At,Radius;public V3 Pos,FromPos;public bool Handed;
    }
    /// <summary>The Firecracker Line while it burns.</summary>
    [Serializable] public sealed class NativeBombLine{public float At,Speed;public V3 From,To;public float End=>At+Length/Math.Max(.1f,Speed)+1.2f;public float Length=>(To-From).Length;}
    /// <summary>One enemy's circle in the Grand Finale.</summary>
    [Serializable] public sealed class NativeBombCircle{public int Id;public V3 Pos;}
    /// <summary>The Grand Finale: cut-in to At+.6, plunger down at Lock, columns at Strike, over at Until.</summary>
    [Serializable] public sealed class NativeBombFinale{public float At=-100,Lock,Strike,Until;public V3 Box,Dir;public NativeBombCircle[] Circles;}
    /// <summary>A victim the chain has finished with: this Bombadier cannot launch them from At to Until.</summary>
    [Serializable] public sealed class NativeBombScorch{public int Id;public float At,Until;}
    /// <summary>
    /// The Bombadier's replicated kit (docs/BOMBADIER.md): the contract between the rules (Astra's) and the presentation. Everything the pose, the
    /// effects, the sound and the HUD read. <see cref="NativeBombPreview"/> writes it the way the host would, for the Dev Sandbox and as the worked example.
    /// </summary>
    [Serializable] public sealed class NativeBombKit
    {
        public NativeBomb[] Bombs;public NativeBlast[] Blasts;public NativeBombLine Line;public NativeBombFinale Finale;public NativeBombScorch[] Scorched;
        public int Chain,MineCharges;public float ChainUntil,Meter;
        /// <summary>Absolute ready times in NativeBombs.Slot order: Q, E, 1, 2, 3, 4.</summary>
        public float[] Ready;
        /// <summary>What the pilot is doing, for the pose: the ability's id and its times.</summary>
        public string CastKind;public float CastAt=-100,CastRelease,CastUntil;
        public bool HasLine,HasFinale;
        public void CapturePresence(){HasLine=Line!=null;HasFinale=Finale!=null;}
        public void RestorePresence(){if(!HasLine)Line=null;if(!HasFinale)Finale=null;if(string.IsNullOrEmpty(CastKind))CastKind=null;}
        public NativeBombKit ViewCopy()
        {
            var c=(NativeBombKit)MemberwiseClone();
            if(Bombs!=null){c.Bombs=new NativeBomb[Bombs.Length];for(int i=0;i<Bombs.Length;i++){var b=Bombs[i];c.Bombs[i]=b==null?null:new NativeBomb{Id=b.Id,Stuck=b.Stuck,Kind=b.Kind,Pos=b.Pos,Vel=b.Vel,Normal=b.Normal,At=b.At,SampleAt=b.SampleAt,ArmAt=b.ArmAt,BlowAt=b.BlowAt};}}
            if(Blasts!=null){c.Blasts=new NativeBlast[Blasts.Length];for(int i=0;i<Blasts.Length;i++){var b=Blasts[i];c.Blasts[i]=b==null?null:new NativeBlast{Id=b.Id,Link=b.Link,Kind=b.Kind,At=b.At,Radius=b.Radius,Pos=b.Pos,FromPos=b.FromPos,Handed=b.Handed};}}
            if(Scorched!=null){c.Scorched=new NativeBombScorch[Scorched.Length];for(int i=0;i<Scorched.Length;i++){var b=Scorched[i];c.Scorched[i]=b==null?null:new NativeBombScorch{Id=b.Id,At=b.At,Until=b.Until};}}
            c.Ready=Ready==null?null:(float[])Ready.Clone();
            if(Line!=null)c.Line=new NativeBombLine{At=Line.At,Speed=Line.Speed,From=Line.From,To=Line.To};
            if(Finale!=null){c.Finale=new NativeBombFinale{At=Finale.At,Lock=Finale.Lock,Strike=Finale.Strike,Until=Finale.Until,Box=Finale.Box,Dir=Finale.Dir};if(Finale.Circles!=null){c.Finale.Circles=new NativeBombCircle[Finale.Circles.Length];for(int i=0;i<Finale.Circles.Length;i++){var b=Finale.Circles[i];c.Finale.Circles[i]=b==null?null:new NativeBombCircle{Id=b.Id,Pos=b.Pos};}}}
            return c;
        }
        public bool Valid()
        {
            bool F(float v)=>MathUtil.Finite(v)&&Math.Abs(v)<10000000;bool V(V3 v)=>F(v.x)&&F(v.y)&&F(v.z)&&v.Length<100000;
            if(MineCharges<0||MineCharges>NativeBombChipRules.MaxMineCharges||!F(Meter)||Meter<0||Meter>1||Chain<0||Chain>64||!F(ChainUntil)||!F(CastAt)||!F(CastRelease)||!F(CastUntil))return false;
            if(Bombs!=null){if(Bombs.Length>NativeBombs.MaxBombs)return false;foreach(var b in Bombs)if(b==null||!NativeBombs.IsKind(b.Kind)||!V(b.Pos)||!V(b.Vel)||!V(b.Normal)||!F(b.At)||!F(b.SampleAt)||!F(b.ArmAt)||!F(b.BlowAt))return false;}
            if(Blasts!=null){if(Blasts.Length>NativeBombs.MaxBlasts)return false;foreach(var b in Blasts)if(b==null||!V(b.Pos)||!V(b.FromPos)||!F(b.At)||!F(b.Radius)||b.Radius<0||b.Radius>64||b.Link<0||b.Link>64||b.Kind!=null&&b.Kind.Length>24)return false;}
            if(Line!=null&&(!F(Line.At)||!F(Line.Speed)||!V(Line.From)||!V(Line.To)))return false;
            if(Finale!=null){if(!F(Finale.At)||!F(Finale.Lock)||!F(Finale.Strike)||!F(Finale.Until)||!V(Finale.Box)||!V(Finale.Dir))return false;if(Finale.Circles!=null){if(Finale.Circles.Length>NativeBombs.MaxCircles)return false;foreach(var c in Finale.Circles)if(c==null||!V(c.Pos))return false;}}
            if(Scorched!=null){if(Scorched.Length>NativeBombs.MaxCircles)return false;foreach(var c in Scorched)if(c==null||!F(c.At)||!F(c.Until))return false;}
            if(Ready!=null&&Ready.Length>NativeBombs.Slots)return false;if(Ready!=null)foreach(var r in Ready)if(!F(r))return false;
            return CastKind==null||CastKind.Length<=24;
        }
    }
    /// <summary>Where the presentation reads a pilot's kit: the snapshot's once the rules replicate it, or the preview's.</summary>
    public interface IBombSource{NativeBombKit Of(int actor);}

    /// <summary>The Bombadier's names and numbers (docs/BOMBADIER.md).</summary>
    public static class NativeBombs
    {
        public const string Spec="bombadier",Entrance="dev-bombadier";
        public const string Pop="pop",Mine="mine",Sticky="sticky",Lob="lob",Bomblet="bomblet",Jump="jump",Spark="spark",Column="column";
        public const string PopBomb="pop-bomb",BlastJump="blast-jump",DeadMansSwitch="dead-mans-switch",BounceMine="bounce-mine",StickyFuse="sticky-fuse",ClusterLob="cluster-lob",FirecrackerLine="firecracker-line",GrandFinale="grand-finale";
        public const int MaxBombs=32,MaxBlasts=48,MaxCircles=16,Slots=6;
        public const float ScorchSeconds=2.5f,CircleRadius=4,PlungeAt=.6f,MineArm=.5f,MineReach=1.6f,RingReach=5,LineReach=16;
        /// <summary>The ability cards, in their HUD order, with the key each shows.</summary>
        public static readonly string[] Slot={BlastJump,DeadMansSwitch,BounceMine,StickyFuse,ClusterLob,FirecrackerLine};
        public static readonly string[] SlotKeys={"Q","E","1","2","3","4"};
        public static string Name(string id)
        {
            switch(id){case PopBomb:return "Pop Bomb";case BlastJump:return "Blast Jump";case DeadMansSwitch:return "Dead Man's Switch";case BounceMine:return "Bounce Mine";case StickyFuse:return "Sticky Fuse";case ClusterLob:return "Cluster Lob";case FirecrackerLine:return "Firecracker Line";case GrandFinale:return "Grand Finale";default:return "";}
        }
        public static string Icon(string id)
        {
            switch(id){case BlastJump:return "rocket";case DeadMansSwitch:return "zap";case BounceMine:return "circle-dot";case StickyFuse:return "activity";case ClusterLob:return "bomb";case FirecrackerLine:return "flame";case GrandFinale:return "skull";default:return "bomb";}
        }
        public static bool IsKind(string k)=>k==Pop||k==Mine||k==Sticky||k==Lob||k==Bomblet;
        /// <summary>Who holds the records (null: nothing to draw). Set from the rendered snapshot; the sandbox preview may override it.</summary>
        public static IBombSource Source;
        public static NativeBombKit Of(Actor a)=>a==null||Source==null?null:Source.Of(a.Id);
        // ---- the TNT in the fists (owner 2026-11-04: "holding TNTs"): presentation only, read from what the kit already says ----
        private static float Smooth(float x){x=x<0?0:x>1?1:x;return x*x*(3-2*x);}
        /// <summary>How often a Pop Bomb can go (1.5 a second), which is what makes the hands take turns under held fire.</summary>
        public const float PopPeriod=2/3f;
        /// <summary>Whether the left fist throws this Pop Bomb: they alternate.</summary>
        public static bool ThrowsLeft(float castAt)=>(long)Math.Floor(castAt/PopPeriod)%2!=0;
        /// <summary>How much of the TNT a fist shows, 0..1. The stick that goes is gone from the hand until the next is drawn from the belt; a mine or the switch takes the left fist's place, the big bundle both, the plunger both, and a reload sends the right to the satchel.</summary>
        public static float HeldShare(NativeBombKit k,float time,float reloadAt,float reloadUntil,bool left)
        {
            float share=1;
            if(!left&&time>=reloadAt&&time<reloadUntil){float u=(time-reloadAt)/Math.Max(.05f,reloadUntil-reloadAt);share=Math.Max(1-Smooth((u-.22f)/.1f),Smooth((u-.5f)/.12f));}
            if(k==null)return share;
            if(k.Finale!=null&&k.Finale.At>-50&&time>=k.Finale.At)share=Math.Min(share,Smooth((time-k.Finale.Lock-.34f)/.25f));
            if(k.CastKind==null||time<k.CastAt)return share;
            float since=time-k.CastRelease,again=since<0?1:Smooth((since-.14f)/.16f);
            switch(k.CastKind)
            {
                case PopBomb:if(ThrowsLeft(k.CastAt)==left)share=Math.Min(share,again);break;
                case StickyFuse:if(!left)share=Math.Min(share,again);break;
                case BounceMine:if(left)share=Math.Min(share,since<0?0:again);break;
                case DeadMansSwitch:if(left)share=Math.Min(share,Smooth((time-k.CastUntil+.22f)/.18f));break;
                case ClusterLob:share=Math.Min(share,since<0?0:Smooth((since-.2f)/.2f));break;
            }
            return share;
        }
        /// <summary>Where the Grand Finale's crate goes, from the pilot's feet and facing (unit, flat): under both hands' place as it stoops and as it slams, a little to its right, because the pose stands turned.</summary>
        public static Vector3 BoxSpot(Vector3 feet,Vector3 dir)=>feet+dir*.63f+new Vector3(dir.Z,0,-dir.X)*.1f;
        /// <summary>A thrown stick's attitude at `t`, its axis local y with the fuse up it: end over end through the air, lying on its side once it has stopped.</summary>
        public static Quaternion ChargeTurn(NativeBomb b,float t)
        {
            if(b.Vel.Length<.5f)return Quaternion.CreateFromAxisAngle(Vector3.UnitY,b.Id*1.7f)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,b.Kind==Lob?0:1.45f);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(.93f,.12f,.35f)),(t-b.At)*(b.Kind==Lob?4:b.Kind==Pop?11:14)+b.Id*.9f);
        }
    }
    /// <summary>One piece of a stick of TNT, a stick of length 1 along y about the origin (linear tints, as the pieces take them).</summary>
    public struct NativeTntPiece{public WeaponShape Shape;public bool Glow;public Vector3 At,Size,Tint;}
    /// <summary>The Bombadier's TNT as loose pieces, for what the rig's fists do not hold: what is thrown, the belt, the charges in the intro. The held bundle is the sculpted model (NativeWeaponModels.Bomb.cs), in the same colours.</summary>
    public static class NativeTnt
    {
        public const float Radius=.115f,FuseTip=.7f;
        /// <summary>Round stock, the shape the sticks are drawn from. Beside the four WeaponShapes rather than among them: those files are the Prototype Series' own and stay byte-identical (ChipModuleChecks). NativeSourceModels.QueueAny draws either.</summary>
        public const WeaponShape Round=(WeaponShape)4;
        /// <summary>Its mesh: a capped cylinder of radius 1 and height 1 along y about the origin, the same winding as the source primitives.</summary>
        public static void RoundPrimitive(List<Vector3> positions,List<Vector3> normals,List<int> indices)
        {
            positions.Clear();normals.Clear();indices.Clear();const int Sides=10;
            for(int y=0;y<=1;y++)for(int x=0;x<=Sides;x++)
            {
                double theta=(double)x/Sides*Math.PI*2;float s=(float)Math.Sin(theta),c=(float)Math.Cos(theta);
                positions.Add(new Vector3(s,.5f-y,c));normals.Add(new Vector3(s,0,c));
            }
            for(int x=0;x<Sides;x++){int a=x,b=Sides+1+x,c=Sides+1+x+1,d=x+1;indices.Add(a);indices.Add(b);indices.Add(d);indices.Add(b);indices.Add(c);indices.Add(d);}
            for(int cap=0;cap<2;cap++)
            {
                float h=cap==0?.5f:-.5f,up=cap==0?1:-1;int middle=positions.Count;positions.Add(new Vector3(0,h,0));normals.Add(new Vector3(0,up,0));int rim=positions.Count;
                for(int x=0;x<=Sides;x++){double theta=(double)x/Sides*Math.PI*2;positions.Add(new Vector3((float)Math.Sin(theta),h,(float)Math.Cos(theta)));normals.Add(new Vector3(0,up,0));}
                for(int x=0;x<Sides;x++){if(cap==0){indices.Add(rim+x);indices.Add(rim+x+1);indices.Add(middle);}else{indices.Add(rim+x+1);indices.Add(rim+x);indices.Add(middle);}}
            }
        }
        private static readonly Vector3 Paper=new Vector3(.5f,.032f,.022f),Crimp=new Vector3(.17f,.014f,.01f),Tape=new Vector3(.018f,.018f,.02f),Label=new Vector3(.72f,.62f,.36f),Rope=new Vector3(.09f,.075f,.06f),Ember=new Vector3(4,1.5f,.35f);
        private static NativeTntPiece P(WeaponShape s,float y,float across,float along,Vector3 tint,bool glow=false)=>new NativeTntPiece{Shape=s,Glow=glow,At=new Vector3(0,y,0),Size=new Vector3(across,along,across),Tint=tint};
        /// <summary>The whole stick: paper, crimped ends, its label and two bands of tape, the fuse and its ember.</summary>
        public static readonly NativeTntPiece[] Stick={P(Round,0,Radius,1,Paper),P(Round,.5f,Radius*.8f,.03f,Crimp),P(Round,-.5f,Radius*.8f,.03f,Crimp),P(Round,.04f,Radius*1.02f,.3f,Label),P(Round,.28f,Radius*1.05f,.09f,Tape),P(Round,-.25f,Radius*1.05f,.09f,Tape),P(Round,.6f,.016f,.2f,Rope),P(WeaponShape.Sphere,FuseTip,.026f,.026f,Ember,true)};
        /// <summary>The same with less on it, for what is small on the screen (the belt, the bomblets).</summary>
        public static readonly NativeTntPiece[] Small={P(Round,0,Radius,1,Paper),P(Round,.1f,Radius*1.05f,.16f,Tape),P(Round,.6f,.022f,.2f,Rope)};
        /// <summary>The length of a stick of this kind (m). A Cluster Lob's is a whole bundle of them.</summary>
        public static float Length(string kind)=>kind==NativeBombs.Lob?.6f:kind==NativeBombs.Pop?.42f:kind==NativeBombs.Sticky?.26f:kind==NativeBombs.Bomblet?.2f:.3f;
    }

    /// <summary>
    /// The Dev Sandbox's "Preview Bombadier": the whole kit on a loop, written as the host would write the record, so the pose, the effects, the sound
    /// and the HUD all show it. It reaches nothing else: not the authority, the network frame, damage or movement, so a victim's flight is not shown here.
    /// One combo, on a fixed script: two mines, a sticky fuse, the switch, a cluster, a line, then the Finale.
    /// </summary>
    public sealed class NativeBombPreview:IBombSource
    {
        public const float Loop=17;
        private readonly Dictionary<int,NativeBombKit> kits=new Dictionary<int,NativeBombKit>();private float started=-100,last;
        public NativeBombKit Of(int actor)=>kits.TryGetValue(actor,out var k)?k:null;
        public bool Any=>kits.Count>0;
        public void Clear(){kits.Clear();started=-100;}
        public void Step(Actor a,Vector3 feet,float yaw,float time)
        {
            if(a==null||a.SourceSpec!=NativeBombs.Spec){kits.Clear();started=-100;return;}
            if(time<last-.25f||started<-50)started=(float)Math.Ceiling(time+.3f);last=time;if(time-started>=Loop)started=time;
            if(!kits.TryGetValue(a.Id,out var k)){k=new NativeBombKit();kits[a.Id]=k;}
            Write(k,feet,yaw,started,time,a.Id);
        }
        private static V3 V(Vector3 v)=>new V3(v.X,v.Y,v.Z);
        private static Vector3 Lob(Vector3 from,Vector3 to,float u,float arc)=>Vector3.Lerp(from,to,u)+Vector3.UnitY*(arc*4*u*(1-u));
        /// <summary>The record at `time` of a loop that began at `t0` for a pilot at `feet` facing `yaw`. Pure: the same clock writes the same record.</summary>
        public static void Write(NativeBombKit k,Vector3 feet,float yaw,float t0,float time,int scorch=0)
        {
            var f=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));var r=new Vector3(f.Z,0,-f.X);var up=Vector3.UnitY;var hand=feet+up*1.3f+f*.5f;float t=time-t0;
            var bombs=new List<NativeBomb>();var blasts=new List<NativeBlast>();k.CastKind=null;k.CastAt=-100;k.Line=null;k.Finale=null;k.Chain=0;k.ChainUntil=-100;k.MineCharges=2;
            void Cast(string id,float at,float release,float until){if(t>=at&&t<until){k.CastKind=id;k.CastAt=t0+at;k.CastRelease=t0+release;k.CastUntil=t0+until;}}
            void Blast(int id,string kind,float at,Vector3 pos,float radius,int link,Vector3? from){if(t>=at&&t<at+1.5f)blasts.Add(new NativeBlast{Id=id,Kind=kind,At=t0+at,Pos=V(pos),Radius=radius,Link=link,Handed=from!=null,FromPos=V(from??pos)});}
            void Thrown(int id,string kind,float at,float land,float blow,Vector3 from,Vector3 to,float arc,Vector3 normal)
            {
                if(t<at||t>=blow)return;float u=Math.Min(1,(t-at)/Math.Max(.05f,land-at));var p=Lob(from,to,u,arc);var v=t<land?(Lob(from,to,Math.Min(1,u+.01f),arc)-p)/Math.Max(.0005f,.01f*(land-at)):Vector3.Zero;
                bombs.Add(new NativeBomb{Id=id,Kind=kind,At=t0+at,SampleAt=time,ArmAt=t0+land+(kind==NativeBombs.Mine?NativeBombs.MineArm:0),BlowAt=t0+blow,Pos=V(p),Vel=V(v),Normal=V(normal)});
            }
            // Pop bombs off the hip.
            var pop0=feet+f*7+r*-2.2f;var pop1=feet+f*8.5f+r*1.4f;
            Cast(NativeBombs.PopBomb,.0f,.16f,.56f);Thrown(1,NativeBombs.Pop,.16f,.76f,1.06f,hand,pop0,1.3f,up);Blast(101,NativeBombs.Pop,1.06f,pop0+up*.2f,2.4f,0,null);
            Cast(NativeBombs.PopBomb,.67f,.83f,1.23f);Thrown(2,NativeBombs.Pop,.83f,1.43f,1.73f,hand,pop1,1.3f,up);Blast(102,NativeBombs.Pop,1.73f,pop1+up*.2f,2.4f,1,pop0+up*.2f);
            // Two mines on the floor.
            var mine0=feet+f*9+r*3;var mine1=feet+f*11-r*2.5f;
            Cast(NativeBombs.BounceMine,2.2f,2.42f,2.9f);Thrown(3,NativeBombs.Mine,2.42f,2.92f,7,hand,mine0,1.1f,up);
            Cast(NativeBombs.BounceMine,3,3.22f,3.7f);Thrown(4,NativeBombs.Mine,3.22f,3.72f,7.12f,hand,mine1,1.1f,up);
            // A sticky fuse on a wall out front, burning.
            var stick=feet+f*10+up*1.4f;
            Cast(NativeBombs.StickyFuse,4.4f,4.55f,5);Thrown(5,NativeBombs.Sticky,4.55f,4.75f,5.95f,hand,stick,.05f,-f);
            Blast(103,NativeBombs.Sticky,5.95f,stick,3,1,mine0+up*.2f);
            // The switch: nearest first, one chasing the next.
            Cast(NativeBombs.DeadMansSwitch,6.7f,6.82f,7.2f);Blast(104,NativeBombs.Mine,7,mine0+up*.2f,2.6f,2,stick);Blast(105,NativeBombs.Mine,7.12f,mine1+up*.2f,2.6f,3,mine0+up*.2f);
            if(t>=5.95f&&t<7.6f){k.Chain=t>=7.12f?3:t>=7?2:1;k.ChainUntil=t0+8.3f;}
            // The third link ends the chain: its victim (the preview has only the pilot to show it on) smoulders through the window.
            k.Scorched=scorch>0&&t>=7.12f&&t<7.12f+NativeBombs.ScorchSeconds?new[]{new NativeBombScorch{Id=scorch,At=t0+7.12f,Until=t0+7.12f+NativeBombs.ScorchSeconds}}:null;
            // The cluster.
            var over=feet+f*9.5f;Cast(NativeBombs.ClusterLob,8.2f,8.48f,9);Thrown(6,NativeBombs.Lob,8.48f,9.2f,9.2f,hand,over+up*4.2f,1,up);
            if(t>=9.2f)for(int i=0;i<6;i++){float ang=i*1.0471976f+.3f;var at=over+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*(NativeBombs.RingReach*.72f);float land=9.48f+i*.07f;Thrown(7+i,NativeBombs.Bomblet,9.2f,land,land,over+up*4.2f,at,.2f,up);Blast(106+i,NativeBombs.Bomblet,land,at+up*.1f,1.9f,0,null);}
            if(t>=9.2f&&t<10.7f)blasts.Add(new NativeBlast{Id=112,Kind=NativeBombs.Lob,At=t0+9.2f,Pos=V(over+up*4.2f),Radius=1.2f,FromPos=V(over+up*4.2f)});
            // The line.
            float la=10.9f;if(t>=la&&t<la+2.6f){k.Line=new NativeBombLine{At=t0+la,Speed=14,From=V(feet+f*1.5f),To=V(feet+f*1.5f+f*NativeBombs.LineReach)};}
            Cast(NativeBombs.FirecrackerLine,la-.14f,la,la+.4f);for(int i=0;i<8;i++){float w=(i+.5f)/8;Blast(120+i,NativeBombs.Spark,la+w*NativeBombs.LineReach/14+.16f,feet+f*(1.5f+w*NativeBombs.LineReach)+up*.08f,1.4f,0,null);}
            // The Finale.
            float fa=13;
            if(t>=fa&&t<fa+3.8f)
            {
                float lk=fa+2.1f,st=fa+2.6f;var box=NativeBombs.BoxSpot(feet,f);Vector3 C(int i){float ang=i*2.0943951f+.5f;return feet+f*10+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*5.2f;}
                var circles=new NativeBombCircle[3];for(int i=0;i<3;i++){float roam=Math.Min(t,lk)-fa;circles[i]=new NativeBombCircle{Id=1000+i,Pos=V(C(i)+r*(float)Math.Sin(roam*1.6f+i)*1.1f)};}
                k.Finale=new NativeBombFinale{At=t0+fa,Lock=t0+lk,Strike=t0+st,Until=t0+fa+3.6f,Box=V(box),Dir=V(f),Circles=circles};Cast(NativeBombs.GrandFinale,fa,fa+.6f,fa+3.6f);
                if(t>=st)for(int i=0;i<3;i++)blasts.Add(new NativeBlast{Id=140+i,Kind=NativeBombs.Column,At=t0+st,Pos=circles[i].Pos,Radius=NativeBombs.CircleRadius,FromPos=circles[i].Pos});
            }
            k.Bombs=bombs.ToArray();k.Blasts=blasts.ToArray();k.Meter=t<fa?Math.Min(1,t/fa):Math.Max(0,1-(t-fa)/.6f);
            k.Ready=new float[NativeBombs.Slots];k.Ready[2]=t>=2.42f&&t<9.42f?t0+9.42f:0;k.Ready[3]=t>=4.55f&&t<13.55f?t0+13.55f:0;k.Ready[4]=t>=8.48f&&t<16?t0+16:0;k.Ready[5]=t>=la?t0+la+12:0;k.Ready[1]=t>=6.82f&&t<8.82f?t0+8.82f:0;
        }
    }
}

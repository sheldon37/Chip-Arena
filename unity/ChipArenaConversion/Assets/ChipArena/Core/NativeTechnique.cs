using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A weapon's technique, an optional Q/E replacement (owner 2026-11-03, docs/WEAPON-TECHNIQUES.md). The contract between the rules
    /// (Astra's) and the presentation: everything the pose, the effects, the sound and the HUD read. Authority-owned in play; <see cref="NativeTechniquePreview"/> supplies optional Dev Sandbox previews. Times are on the presentation clock.
    /// </summary>
    [Serializable] public sealed class NativeTechnique
    {
        /// <summary>The technique's id and its phase (docs/WEAPON-TECHNIQUES.md).</summary>
        public string Kind,Phase;
        /// <summary>The press; when the move leaves the pilot; the effect's end; the pilot's own lock's end.</summary>
        public float At=-100,Release,Until,RecoverUntil;
        /// <summary>The triggered moment and the recast (-100: not yet); a time of the kind's own (armed, stuck, burst), or a share.</summary>
        public float HitAt=-100,RecastAt=-100,Arm;
        public V3 From,Point,To,Dir;
        /// <summary>A kind's own trail, oldest first (Wildfire Run: a point every <see cref="NativeTechniques.PathStep"/> s from Release). At most 32.</summary>
        public V3[] Path;
        public int Target;
        public float End=>Math.Max(Until,RecoverUntil);
        public bool Hit(float time)=>HitAt>-50&&time>=HitAt;
        public bool Recast(float time)=>RecastAt>-50&&time>=RecastAt;
        public NativeTechnique ViewCopy()=>(NativeTechnique)MemberwiseClone();
        private static readonly string[] Phases=new[]{"windup","active","guard","counter","recovery","throw","cloud","plant","wall","recall","flight","stuck","leap","return","flick","air","drop","armed","fire","reel","draw","burst","crouch","cast","pool","set","bolt","swap","hop","line","snap","mount","ride","brace","shot","marked","blast","ping","dive","ignite","run","stream","rod","zap","sink","walk","rise"};
        public bool Valid()
        {
            bool F(float v)=>MathUtil.Finite(v)&&Math.Abs(v)<10000000;bool V(V3 v)=>F(v.x)&&F(v.y)&&F(v.z)&&v.Length<100000;bool S(string v)=>v==null||v.Length<=32;
            if(Path!=null){if(Path.Length>32)return false;foreach(var p in Path)if(!V(p))return false;}
            return (Kind==null||NativeTechniques.Find(Kind)!=null)&&(Phase==null||Array.IndexOf(Phases,Phase)>=0)&&Target>=0&&Target<1000000&&S(Kind)&&S(Phase)&&F(At)&&F(Release)&&F(Until)&&F(RecoverUntil)&&F(HitAt)&&F(RecastAt)&&F(Arm)&&V(From)&&V(Point)&&V(To)&&V(Dir)&&(Kind==null||Release>=At&&Until>=Release&&Until-At<=60&&RecoverUntil-At<=60);
        }
    }
    /// <summary>Where the presentation reads an actor's technique: the snapshot's, once the rules replicate it, or the preview's.</summary>
    public interface ITechniqueSource{NativeTechnique Of(int actor);}

    /// <summary>Every weapon's techniques (docs/WEAPON-TECHNIQUES.md). A new one is a row here, its text, its content row, its effect and its pose:
    /// nothing else counts them, and the Locker lays out however many a weapon has.</summary>
    public static class NativeTechniques
    {
        public sealed class Def{public string Id,Spec,Name,Icon,Tag;public float Cooldown,Cost;public int Accent;}
        public const string Riposte="riposte",SmokeVeil="smoke-veil",Bastion="bastion",AnchorThrow="anchor-throw",RicochetCoin="ricochet-coin",SentryDrop="sentry-drop",GrappleLine="grapple-line",GaleArrow="gale-arrow",IgnitionLeap="ignition-leap",HealingSpring="healing-spring",StaticRecall="static-recall",ShadeSwap="shade-swap";
        public const string PartingLine="parting-line",Tripwire="tripwire",ShieldSurf="shield-surf",IronVow="iron-vow",BountyRound="bounty-round",RecoilBurst="recoil-burst",SonarDart="sonar-dart",SpiritHawk="spirit-hawk",WildfireRun="wildfire-run",Slipstream="slipstream",LightningRod="lightning-rod",GraveWalk="grave-walk";
        private static Def D(string id,string spec,string name,string icon,float cooldown,float cost,int accent,string tag)=>new Def{Id=id,Spec=spec,Name=name,Icon=icon,Cooldown=cooldown,Cost=cost,Accent=accent,Tag=tag};
        public static readonly Def[] All=
        {
            D(Riposte,"sword","Riposte","shield-check",12,20,0x8fe8ff,"COUNTER"),D(SmokeVeil,"daggers","Smoke Veil","cloud",16,25,0xb48cff,"STEALTH"),D(Bastion,"shield","Bastion","shield",14,25,0x4fa8ff,"COVER"),D(AnchorThrow,"greatsword","Anchor Throw","anchor",14,30,0xffa23a,"MOBILITY"),
            D(RicochetCoin,"pistol","Ricochet Coin","coins",9,0,0xffcf5a,"TRICK SHOT"),D(SentryDrop,"machinegun","Sentry Drop","crosshair",20,0,0xff5a4a,"TURRET"),D(GrappleLine,"rifle","Grapple Line","spline",12,0,0x4fe6ff,"MOBILITY"),D(GaleArrow,"bow","Gale Arrow","wind",10,0,0xbff5e4,"CONTROL"),
            D(IgnitionLeap,"fire","Ignition Leap","flame",11,22,0xff7a22,"MOBILITY"),D(HealingSpring,"water","Healing Spring","droplets",18,30,0x4ff0c8,"SUPPORT"),D(StaticRecall,"lightning","Static Recall","zap",13,20,0x9ec4ff,"ESCAPE"),D(ShadeSwap,"darkness","Shade Swap","repeat",15,24,0xa050ff,"CONTROL"),
        };
        /// <summary>The second set (owner 2026-11-04), in `All`'s weapon order. Apart from `All` until the rules and their content rows catch up: the
        /// Dev Sandbox previews them, and the Locker offers one as soon as its content row is live.</summary>
        public static readonly Def[] More=
        {
            D(PartingLine,"sword","Parting Line","swords",11,18,0xd6f2ff,"TRAP"),D(Tripwire,"daggers","Tripwire","activity",13,20,0xe0ff6a,"TRAP"),D(ShieldSurf,"shield","Shield Surf","move",12,20,0x6ec1ff,"MOBILITY"),D(IronVow,"greatsword","Iron Vow","shield-alert",14,25,0xffb648,"DEFENCE"),
            D(BountyRound,"pistol","Bounty Round","target",14,0,0xffd86a,"RECON"),D(RecoilBurst,"machinegun","Recoil Burst","zap",10,0,0xff8a3c,"MOBILITY"),D(SonarDart,"rifle","Sonar Dart","radio",16,0,0x5cffb0,"RECON"),D(SpiritHawk,"bow","Spirit Hawk","feather",12,0,0xa6fff0,"HUNTER"),
            D(WildfireRun,"fire","Wildfire Run","footprints",13,25,0xff9a2e,"MOBILITY"),D(Slipstream,"water","Slipstream","wind",15,22,0x54d8ff,"TEAM"),D(LightningRod,"lightning","Lightning Rod","radio",16,24,0xc9b6ff,"DEFENCE"),D(GraveWalk,"darkness","Grave Walk","ghost",17,26,0x8a5cff,"ESCAPE"),
        };
        /// <summary>A weapon's techniques, in the order the Locker lists them (empty: a DEV class). Allocation free once built.</summary>
        public static Def[] Of(string spec)
        {
            if(spec==null)return Array.Empty<Def>();if(bySpec.TryGetValue(spec,out var list))return list;var l=new List<Def>();foreach(var d in All)if(d.Spec==spec)l.Add(d);foreach(var d in More)if(d.Spec==spec)l.Add(d);list=l.ToArray();bySpec[spec]=list;return list;
        }
        private static readonly Dictionary<string,Def[]> bySpec=new Dictionary<string,Def[]>();
        /// <summary>Wildfire Run's trail: a Path point this often from Release.</summary>
        public const float PathStep=.125f;
        /// <summary>The Locker card's sentence.</summary>
        public static string Text(string id)
        {
            switch(id)
            {
                case Riposte:return "A short guard that counters a melee hit and returns shots. Miss it and you are open.";
                case SmokeVeil:return "A smoke pellet at your feet. Anyone inside is hidden from outside the cloud.";
                case Bastion:return "Plant your shield as a wall that stops shots from both sides. No blocking while it stands.";
                case AnchorThrow:return "Hurl your blade, then press again to leap to it. You only punch until it returns.";
                case RicochetCoin:return "Flip a coin and shoot it: the bullet turns to the nearest enemy for bonus damage.";
                case SentryDrop:return "Drop a tripod gun that fires at the nearest enemy for six seconds. It can be shot down.";
                case GrappleLine:return "Fire a line at a surface and reel yourself to it. No shooting on the way.";
                case GaleArrow:return "An arrow that bursts into wind, shoving everything away. Shoot your feet to launch.";
                case IgnitionLeap:return "A burst under your feet throws you high and burns the ground you left.";
                case HealingSpring:return "A healing pool on the floor you aim at. Allies heal inside it, enemies slow down.";
                case StaticRecall:return "Leave an anchor, then press again to bolt back to it, shocking anyone in the line.";
                case ShadeSwap:return "A slow orb. If it touches an enemy, the two of you trade places.";
                case PartingLine:return "Hop back and leave a cut hanging in the air. It bursts a moment later on whoever chased you.";
                case Tripwire:return "Throw a dagger to string a wire behind you. The first enemy through it is snared.";
                case ShieldSurf:return "Ride your shield across the floor, steering as you go. No blocking until you hop off.";
                case IronVow:return "Plant your blade and brace: less damage, no stagger, no moving. It ends in a shove.";
                case BountyRound:return "A marked shot. Whoever it hits shows through walls to your team, and the kill refunds it.";
                case RecoilBurst:return "Dump the magazine to throw yourself away from your aim. You land empty.";
                case SonarDart:return "A dart that pings three times, showing enemies near it through walls. It can be shot.";
                case SpiritHawk:return "Loose a hawk of light that hunts the first enemy near its line, and slows what it strikes.";
                case WildfireRun:return "Run faster for three seconds, burning a trail behind you that chasers have to cross.";
                case Slipstream:return "A current along the floor: anyone riding it moves faster, enemies too. Against it is slow.";
                case LightningRod:return "Plant a rod that pulls enemy shots near it down into itself, until it breaks.";
                case GraveWalk:return "Sink into your shadow: untouchable and moving, but unable to act, with the pool in plain sight.";
                default:return "";
            }
        }
        /// <summary>A technique's HUD card in the slot it replaced. The recast comes first: it is the one state that asks for a press.
        /// `left` is the slot's deadline less the clock, `cooldown` as this pilot's chips scale it.
        /// `refused` is why a press would be refused for where it is aimed (NativeArena.TechniqueRefusal); `Held` marks that state, off
        /// cooldown but not pressable here, so the card does not flare every time the aim comes back.</summary>
        public struct CardState{public string State;public bool Ready,Active,Recast,Low,Held;public float Dial;}
        public static CardState Card(NativeTechnique k,float now,float left,float cooldown,bool low,string lowText,string refused=null)
        {
            var c=new CardState{Dial=left>0?1-Math.Min(1,left/Math.Max(.01f,cooldown)):1};string no=RefusalState(refused);
            if(NativeTechniqueRules.Recast(k,now)){if(no!=null){c.State=no;c.Low=c.Held=true;}else{c.State="RECAST";c.Recast=true;}}
            else if(k?.Kind!=null&&now>=k.At&&now<k.End){c.State="ACTIVE";c.Active=true;}
            else if(left>0)c.State=left.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"s";
            else if(low){c.State=lowText;c.Low=true;}
            else if(no!=null){c.State=no;c.Low=c.Held=true;}
            else{c.State="READY";c.Ready=true;}
            return c;
        }
        /// <summary>Why a press would be refused for where it is aimed: what NativeArena.TechniqueRefusal answers, its card state and the line
        /// under the crosshair when a press is refused.</summary>
        public const string NoSurface="no-surface",Blocked="blocked",NoFloor="no-floor";
        public static string RefusalState(string why)=>why==NoSurface?"NO SURFACE":why==Blocked?"BLOCKED":why==NoFloor?"NO FLOOR":null;
        public static string RefusalLine(string why)=>why==NoSurface?"NO SURFACE IN RANGE":why==Blocked?"THE WAY BACK IS BLOCKED":why==NoFloor?"NO FLOOR THERE":null;
        /// <summary>The weapon's technique (null: a DEV class, or none).</summary>
        public static Def For(string spec){foreach(var d in All)if(d.Spec==spec)return d;return null;}
        public static Def Find(string id){foreach(var d in All)if(d.Id==id)return d;foreach(var d in More)if(d.Id==id)return d;return null;}
        /// <summary>The technique in this weapon's slot (null: the slot keeps its own ability).</summary>
        public static Def Equipped(string map,string spec,string slot){return NativeTechniqueLoadout.Slot(map,spec)==slot?For(spec):null;}
        /// <summary>Who holds the records (null: no active snapshot). Set from the rendered snapshot; the sandbox preview may override it.</summary>
        public static ITechniqueSource Source;
        public static NativeTechnique Of(Actor a)=>a==null||Source==null?null:Source.Of(a.Id);
        /// <summary>The weapon out of the pilot's hand this frame: the planted shield, the thrown greatsword until it is caught.</summary>
        public static bool WeaponAway(NativeTechnique k,float time)=>k?.Kind!=null&&time>=k.Release&&time<k.Until&&(k.Kind==Bastion||k.Kind==AnchorThrow||k.Kind==ShieldSurf||k.Kind==IronVow);
        /// <summary>The coin in the air at `time`.</summary>
        public static Vector3 CoinAt(NativeTechnique k,float time)
        {
            float t=Math.Max(0,time-k.Release);return new Vector3(k.From.x+k.Dir.x*CoinDrift*t,k.From.y+CoinLift*t-.5f*CoinFall*t*t,k.From.z+k.Dir.z*CoinDrift*t);
        }
        public const float CoinDrift=2.2f,CoinLift=6.1f,CoinFall=9.8f,CoinAir=1.4f;
        /// <summary>The thrown greatsword's place at `time`, and how far through its flight or its return (0..1).</summary>
        public static Vector3 BladeAt(NativeTechnique k,float time,Vector3 catcher,out float flight)
        {
            var from=new Vector3(k.From.x,k.From.y,k.From.z);var to=new Vector3(k.Point.x,k.Point.y,k.Point.z);
            if(time<k.Arm){flight=Math.Max(0,Math.Min(1,(time-k.Release)/Math.Max(.05f,k.Arm-k.Release)));return Vector3.Lerp(from,to,flight);}
            if(k.Recast(time)){flight=1;return to;}
            float back=(time-(k.Until-BladeReturn))/BladeReturn;if(back<=0){flight=1;return to;}
            flight=Math.Min(1,back);return Vector3.Lerp(to,catcher,flight*flight);
        }
        public const float BladeReturn=.32f;
    }

    /// <summary>
    /// The Dev Sandbox's "Preview Techniques": the local pilot's weapon's technique on a loop, each cast written as the proposed host would
    /// write the record (docs/WEAPON-TECHNIQUES.md), so the pose, the effects, the sound and the HUD all show it. It reaches nothing else: not
    /// the authority, the actor, the network frame, damage or movement, so a move that travels draws only its effects here.
    /// </summary>
    public sealed class NativeTechniquePreview:ITechniqueSource
    {
        private readonly Dictionary<int,NativeTechnique> records=new Dictionary<int,NativeTechnique>();private float next=-100,clock;private bool odd;private int turn=-1;
        public NativeTechnique Of(int actor)=>records.TryGetValue(actor,out var r)?r:null;
        public void Clear(){records.Clear();next=-100;}
        public bool Any=>records.Count>0;
        /// <summary>Seconds a kind's loop takes, its effect with a breath after.</summary>
        public static float Period(string id)
        {
            switch(id)
            {
                case NativeTechniques.Riposte:return 2.6f;case NativeTechniques.SmokeVeil:return 6.4f;case NativeTechniques.Bastion:return 6.2f;case NativeTechniques.AnchorThrow:return 4.4f;
                case NativeTechniques.RicochetCoin:return 3.4f;case NativeTechniques.SentryDrop:return 9.2f;case NativeTechniques.GrappleLine:return 3.2f;case NativeTechniques.GaleArrow:return 3.2f;
                case NativeTechniques.IgnitionLeap:return 3.6f;case NativeTechniques.HealingSpring:return 7.2f;case NativeTechniques.StaticRecall:return 4.2f;
                case NativeTechniques.PartingLine:return 3.2f;case NativeTechniques.Tripwire:return 5.6f;case NativeTechniques.ShieldSurf:return 4.2f;case NativeTechniques.IronVow:return 4.4f;
                case NativeTechniques.BountyRound:return 5.2f;case NativeTechniques.RecoilBurst:return 2.8f;case NativeTechniques.SonarDart:return 6.2f;case NativeTechniques.SpiritHawk:return 3.6f;
                case NativeTechniques.WildfireRun:return 8f;case NativeTechniques.Slipstream:return 7f;case NativeTechniques.LightningRod:return 7.6f;case NativeTechniques.GraveWalk:return 3.8f;
                default:return 3.4f;
            }
        }
        /// <summary>Advances the loop for the pilot at `feet` facing `yaw` (radians), through the weapon's techniques in turn, or only `only`.</summary>
        public void Step(Actor a,Vector3 feet,float yaw,float time,string only=null)
        {
            var list=NativeTechniques.Of(a?.SourceSpec);if(list.Length==0){records.Clear();next=-100;return;}
            if(time<clock-.25f)next=-100;clock=time;
            if(next<-50)next=(float)Math.Ceiling(time+.3f);
            if(!records.TryGetValue(a.Id,out var k)){k=new NativeTechnique();records[a.Id]=k;}
            if(time>=next){odd=!odd;string id=only;if(id==null){turn=(turn+1)%list.Length;id=list[turn].Id;}Write(k,id,feet,yaw,time,odd);next=time+Period(id);}
            k.Phase=Phase(k,time);
        }
        /// <summary>Fills the record as the proposed host would for a cast at `t0`. `first`: the sword's parry lands, the other loop it does not.</summary>
        public static void Write(NativeTechnique k,string id,Vector3 feet,float yaw,float t0,bool first)
        {
            var f=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));var r=new Vector3(f.Z,0,-f.X);var up=Vector3.UnitY;Vector3 hand=feet+up*1.25f+f*.35f+r*.1f;
            k.Kind=id;k.At=t0;k.HitAt=-100;k.RecastAt=-100;k.Arm=0;k.Target=0;k.Dir=V(f);k.From=V(hand);k.Point=V(feet);k.To=V(feet);k.Release=t0+.1f;k.Until=t0+1;k.RecoverUntil=0;k.Path=null;
            switch(id)
            {
                case NativeTechniques.Riposte:k.Release=t0+.06f;k.Until=t0+.5f;k.Point=V(feet+up*1.25f+f*.55f);k.Dir=V(-f);if(first){k.HitAt=t0+.26f;k.RecoverUntil=k.HitAt+.45f;}else k.RecoverUntil=k.Until+.45f;break;
                case NativeTechniques.SmokeVeil:k.Release=t0+.24f;k.Until=k.Release+4;k.Point=V(feet+f*.6f);k.RecoverUntil=k.Release+.18f;break;
                case NativeTechniques.Bastion:k.Release=t0+.42f;k.Until=k.Release+4;k.Point=V(feet+f*1.3f);k.Arm=1;k.HitAt=k.Release+1.5f;k.To=V(feet+f*1.3f+up*1.05f+r*.55f);k.RecoverUntil=k.Release+.25f;break;
                case NativeTechniques.AnchorThrow:k.Release=t0+.32f;{var to=feet+f*4.2f+r*1.6f;k.Point=V(to);k.Dir=V(Vector3.Normalize(to-feet));}k.Arm=k.Release+4.4f/26f;if(first){k.RecastAt=k.Arm+1.1f;k.Until=k.RecastAt+.45f;}else k.Until=k.Arm+1.4f;k.RecoverUntil=k.Release+.3f;break;
                case NativeTechniques.RicochetCoin:k.Release=t0+.16f;k.HitAt=k.Release+.56f;k.Until=k.Release+NativeTechniques.CoinAir;k.Point=V(NativeTechniques.CoinAt(k,k.HitAt));k.To=V(feet+f*9+r*5+up*1.15f);k.RecoverUntil=k.Release+.2f;break;
                case NativeTechniques.SentryDrop:k.Release=t0+.34f;k.Point=V(feet+f*1.5f+r*1);k.Arm=k.Release+.6f;k.Until=k.Arm+6;k.To=V(feet+f*12+r*2+up*1.1f);k.RecoverUntil=k.Release+.25f;break;
                case NativeTechniques.GrappleLine:k.Release=t0+.14f;k.Point=V(feet+f*14+up*5);k.Arm=k.Release+.24f;k.Until=k.Arm+.95f;k.RecoverUntil=k.Until;break;
                case NativeTechniques.GaleArrow:k.Release=t0+.34f;k.From=V(feet+up*1.35f+f*.6f);k.Point=V(feet+f*6.5f);k.Arm=k.Release+.26f;k.Until=k.Arm+.9f;k.RecoverUntil=k.Release+.3f;break;
                case NativeTechniques.IgnitionLeap:k.Release=t0+.16f;k.Until=k.Release+1.35f;k.RecoverUntil=k.Release+.5f;break;
                case NativeTechniques.HealingSpring:k.Release=t0+.38f;k.Point=V(feet+f*3.5f);k.Until=k.Release+5;k.RecoverUntil=k.Release+.3f;break;
                case NativeTechniques.StaticRecall:k.Release=t0+.12f;k.Point=V(feet+f*5.5f+r*2.4f);k.RecastAt=t0+2.5f;k.From=V(feet);k.Until=k.RecastAt+.3f;k.RecoverUntil=k.Release+.2f;break;
                case NativeTechniques.ShadeSwap:k.Release=t0+.22f;k.Point=V(feet+f*11);k.HitAt=k.Release+.5f;k.Until=k.HitAt+.28f;k.From=V(feet+up*1.2f+f*.5f);k.RecoverUntil=k.Until;break;
                case NativeTechniques.PartingLine:k.Release=t0+.12f;k.From=V(feet);k.Point=V(feet+f*2.2f);k.Arm=k.Release+.7f;k.Until=k.Arm+.3f;k.RecoverUntil=k.Release+.28f;break;
                case NativeTechniques.Tripwire:k.Release=t0+.2f;k.From=V(feet+f*1.2f-r*3+up*.9f);k.Point=V(feet+f*5.5f+r*3.4f+up*.9f);k.Arm=k.Release+.32f;k.Until=k.Arm+3.8f;if(first){k.HitAt=k.Arm+2.2f;k.To=V(feet+f*3.3f+up*.9f);k.Until=k.HitAt+.85f;}k.RecoverUntil=k.Release+.18f;break;
                case NativeTechniques.ShieldSurf:k.Release=t0+.18f;k.Until=k.Release+2.6f;k.RecoverUntil=k.Until;if(first){k.RecastAt=k.Release+2f;k.Until=k.RecoverUntil=k.RecastAt+.25f;}break;
                case NativeTechniques.IronVow:k.Release=t0+.3f;k.Point=V(feet+f*.62f);k.Until=k.Release+2.4f;k.RecoverUntil=k.Until+.2f;k.HitAt=k.Release+1.1f;k.To=V(feet+up*1.25f+f*.9f+r*.3f);break;
                case NativeTechniques.BountyRound:k.Release=t0+.14f;k.Point=V(feet+f*11+up*1.2f);k.To=k.Point;k.Target=1000000-1;k.HitAt=k.Release+.1f;k.Until=k.HitAt+3.6f;k.RecoverUntil=k.Release+.2f;break;
                case NativeTechniques.RecoilBurst:k.Release=t0+.12f;k.Dir=V(Vector3.Normalize(f+up*(-.4f)));k.Until=k.Release+.45f;k.RecoverUntil=k.Until+.1f;break;
                case NativeTechniques.SonarDart:k.Release=t0+.18f;k.Point=V(feet+f*7+up*2.2f);k.Dir=V(-f);k.Arm=k.Release+.5f;k.Until=k.Arm+3.4f;k.RecoverUntil=k.Release+.2f;if(first){k.HitAt=k.Until-.3f;k.Until=k.HitAt;}break;
                case NativeTechniques.SpiritHawk:k.Release=t0+.3f;k.From=V(feet+up*1.45f+f*.5f);k.Point=V(feet+up*1.9f+f*10);k.Arm=k.Release+.5f;k.RecoverUntil=k.Release+.25f;if(first){k.To=V(feet+f*8+r*3.2f+up*1.1f);k.HitAt=k.Arm+.28f;k.Until=k.HitAt+.3f;}else k.Until=k.Arm+.9f;break;
                case NativeTechniques.WildfireRun:k.Release=t0+.15f;k.Arm=k.Release+3;k.Until=k.Arm+3;k.RecoverUntil=k.Release+.1f;{var path=new V3[24];for(int i=0;i<path.Length;i++){float u=i/(path.Length-1f);path[i]=V(feet+f*(.6f+u*8)+r*(float)Math.Sin(u*5)*1.8f);}k.Path=path;}break;
                case NativeTechniques.Slipstream:k.Release=t0+.28f;k.From=V(feet+f*.8f);k.To=V(feet+f*14.8f);k.Until=k.Release+5;k.RecoverUntil=k.Release+.25f;break;
                case NativeTechniques.LightningRod:k.Release=t0+.22f;k.Point=V(feet+f*5.5f+r*1.4f);k.Arm=k.Release+.3f;k.Until=k.Arm+5;k.RecoverUntil=k.Release+.2f;k.HitAt=k.Arm+1.7f;k.To=V(feet+f*3.3f+r*3+up*1.3f);break;
                case NativeTechniques.GraveWalk:k.Release=t0+.34f;k.From=V(feet);k.Until=k.Release+1.5f;k.RecoverUntil=k.Until+.34f;break;
            }
        }
        private static V3 V(Vector3 v)=>new V3(v.X,v.Y,v.Z);
        private static string Phase(NativeTechnique k,float t)
        {
            if(t<k.Release)return "windup";return t<k.Until?"active":"recovery";
        }
    }
}

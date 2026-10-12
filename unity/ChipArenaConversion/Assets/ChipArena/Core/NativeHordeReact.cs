using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// How a horde add takes a hit (owner 2026-10-12: "Give them hitting from melee too"): the pilots' felt hits
    /// (Core/NativeStagger's rules) on the Siren's minions, scaled by each kind's weight (NativeHordeKinds.Mass: a greatsword
    /// throws a runner about and only rocks a brute). Presentation only: it reads the replicated Hit / Headshot events on the
    /// add (their time, damage, attacker and the authority's push, ArenaEvent.Normal) and moves only the rig's bones, never the
    /// body, so hitboxes, timing and movement are untouched.
    /// <list type="bullet">
    /// <item>Directional: the hips are knocked along the push by NativeStaggerRules' critically damped spring (a lone hit peaks
    /// at the weight's peak about 1/omega after it, a hit soon after the last adds less), and the trunk recoils away from it
    /// on an impact envelope (bends back from a blow in front, twists from one at the side, folds over from behind); the head
    /// snaps a beat later, harder on a headshot.</item>
    /// <item>Stagger-back steps: the feet stay planted in the world (Core/NativeHordeMotion), so the shoved hips leave them
    /// behind and they step to catch the body; a step or heavy blow asks for its own steps (<see cref="Staggering"/>).</item>
    /// <item>The authority's own stagger (a hit of a quarter of the add's health: StaggerAt, which also cancels a wind-up) reels
    /// the add for the content's stagger seconds: arms thrown out, trunk arched back.</item>
    /// <item>Hit-stop: a melee blow holds the add's gait and reaction clock for a few frames (<see cref="Hold"/>; the strike of
    /// its own attack never waits), with the struck flash (NativeHitFlash) on its body.</item>
    /// </list>
    /// Fixed arrays: no allocation per frame.
    /// </summary>
    public sealed class NativeHordeReact
    {
        public struct Hit{public float At,Amount,Scale;public StaggerWeight Weight;public bool Melee,Head,Stagger;public float StaggerFor;public Vector2 Dir;}
        public const int Slots=8;
        private readonly Hit[] hits=new Hit[Slots];private int count,head;private int kind;private float give=1,time=float.NaN;
        /// <summary>Hit-stop per weight (s): a dagger nick barely catches, a greatsword blow hangs.</summary>
        public static float StopFor(StaggerWeight w)=>w==StaggerWeight.Heavy?.085f:w==StaggerWeight.Step?.06f:w==StaggerWeight.Flinch?.04f:0;
        /// <summary>The trunk's recoil per weight (rad) before the kind's give.</summary>
        public static float RecoilFor(StaggerWeight w)=>w==StaggerWeight.Heavy?.5f:w==StaggerWeight.Step?.32f:w==StaggerWeight.Flinch?.17f:0;
        public int Count=>count;
        public void Bind(int kind){this.kind=Math.Max(0,Math.Min(NativeHordeKinds.Count-1,kind));give=NativeHordeKinds.Give(this.kind);}
        public void Clear(){count=0;head=0;time=float.NaN;}
        /// <summary>
        /// A hit landed at `at`: `dir` the push (Unity world, horizontal unit; zero when unknown), its weight (NativeStaggerRules.
        /// Classify), damage, whether a melee blow and a headshot, and whether the authority staggers the add for it (StaggerAt)
        /// and for how long. Hits arrive in time order; a same-tick hit from one volley joins the last.
        /// </summary>
        public void Add(float at,Vector2 dir,StaggerWeight weight,float amount,bool melee,bool headshot,bool stagger,float staggerFor)
        {
            if(weight==StaggerWeight.None&&!stagger)return;if(float.IsNaN(at))return;
            if(count>0){ref var n=ref hits[(head+count-1)%Slots];if(Math.Abs(n.At-at)<.004f&&n.Melee==melee){n.Amount+=amount;if(weight>n.Weight)n.Weight=weight;n.Scale=NativeStaggerRules.Scale(n.Weight,n.Amount);n.Head|=headshot;n.Stagger|=stagger;n.StaggerFor=Math.Max(n.StaggerFor,staggerFor);return;}}
            var h=new Hit{At=at,Amount=amount,Weight=weight==StaggerWeight.None?StaggerWeight.Flinch:weight,Melee=melee,Head=headshot,Stagger=stagger,StaggerFor=staggerFor,Dir=dir.LengthSquared()>1e-6f?Vector2.Normalize(dir):Vector2.Zero};h.Scale=NativeStaggerRules.Scale(h.Weight,amount);
            if(count<Slots){hits[(head+count)%Slots]=h;count++;}else{hits[head]=h;head=(head+1)%Slots;}
        }
        /// <summary>Moves to `t`: hits newer than it (a rewind) and those long past are forgotten.</summary>
        public void Step(float t)
        {
            if(!float.IsNaN(time)&&t<time-.25f)Clear();time=t;
            while(count>0&&t-hits[head].At>NativeStaggerRules.Window+1.5f){head=(head+1)%Slots;count--;}
            while(count>0&&hits[(head+count-1)%Slots].At>t+.5f)count--;
        }
        private ref Hit At(int i)=>ref hits[(head+i)%Slots];
        /// <summary>1 while a melee blow's hit-stop holds the add (its gait and reactions wait), else 0.</summary>
        public float Hold(float t){for(int i=count-1;i>=0;i--){ref var h=ref At(i);if(!h.Melee)continue;float a=t-h.At;if(a>=0&&a<StopFor(h.Weight)*(.7f+.3f*give))return 1;if(a>=0)break;}return 0;}
        /// <summary>The clock the reaction reads: hit-stop frames do not advance it.</summary>
        private float Clock(float t){float held=0;for(int i=0;i<count;i++){ref var h=ref At(i);if(!h.Melee)continue;float stop=StopFor(h.Weight)*(.7f+.3f*give),a=t-h.At;if(a>0)held+=Math.Min(a,stop);}return t-held*.85f;}
        /// <summary>A step or heavy blow (or the authority's stagger) is still being caught: the feet step sooner.</summary>
        public bool Staggering(float t){for(int i=count-1;i>=0;i--){ref var h=ref At(i);float a=t-h.At;if(a<0)continue;if(a>.7f)break;if(h.Weight>=StaggerWeight.Step||h.Stagger)return true;}return false;}
        /// <summary>How far into the authority's stagger the add is (0..1 while it reels, NaN when not staggered).</summary>
        public float Reel(float t){for(int i=count-1;i>=0;i--){ref var h=ref At(i);if(!h.Stagger)continue;float a=t-h.At;if(a<0)continue;float d=Math.Max(.2f,h.StaggerFor);return a<d+.25f?a/d:float.NaN;}return float.NaN;}
        /// <summary>The newest hit's time, or -inf.</summary>
        public float Newest=>count>0?At(count-1).At:float.NegativeInfinity;

        /// <summary>The hips' shove now (Unity world, horizontal m): the stagger spring per hit, refractory and soft-limited, by the kind's give.</summary>
        public Vector2 Shove(float t)
        {
            t=Clock(t);Vector2 x=Vector2.Zero;float last=float.NegativeInfinity;
            for(int i=0;i<count;i++)
            {
                ref var h=ref At(i);float tau=t-h.At;if(tau<0)break;if(h.Dir==Vector2.Zero){last=h.At;continue;}
                float omega=NativeStaggerRules.Omega(h.Weight),peak=NativeStaggerRules.Peak(h.Weight)*h.Scale*give*(h.Melee?1.25f:1)*(h.Stagger?1.4f:1);
                float share=float.IsNegativeInfinity(last)?1:NativeStaggerRules.Refractory(h.At-last);last=h.At;
                NativeStaggerRules.Spring(0,NativeStaggerRules.Impulse(peak,omega)*share,omega,tau,out float s,out _);x+=h.Dir*s;
            }
            float l=x.Length(),soft=NativeStaggerRules.MaxHip*1.3f*give,hard=NativeStaggerRules.HardHip*1.6f;if(l>soft){float k=soft+(l-soft)/(1+(l-soft)/Math.Max(1e-3f,hard-soft));if(k>hard)k=hard;x*=k/l;}
            return x;
        }
        // An impact envelope: up fast to 1 at .07 s, back over ~.35 s.
        private static float Envelope(float tau,float rise=.07f){if(tau<0)return 0;float u=tau/rise;return u*(float)Math.Exp(1-u);}

        /// <summary>
        /// Layers the reaction onto the trunk of pose `p` at `t` (the gait's facing `fwd` / `right`, Unity world): the hips'
        /// shove in the rig's frame, the trunk's recoil away from each blow, the head's snap and the authority's stagger reel.
        /// </summary>
        public void Trunk(CapturedRig.Pose p,float t,Vector2 fwd,Vector2 right)
        {
            if(count==0)return;float c=Clock(t);var shove=Shove(t);
            // Into the rig's source frame (+x its left, +z forward).
            p.Position[CapturedRig.Body]+=new Vector3(-Vector2.Dot(shove,right),0,Vector2.Dot(shove,fwd));
            float pitch=0,roll=0,yaw=0,snap=0,headRoll=0;
            for(int i=0;i<count;i++)
            {
                ref var h=ref At(i);float tau=c-h.At;if(tau<0||tau>1.2f)continue;
                float e=Envelope(tau)*RecoilFor(h.Weight)*h.Scale*give*(h.Melee?1.2f:.85f),late=Envelope(tau-.03f,.06f)*RecoilFor(h.Weight)*h.Scale*give*(h.Head?2.2f:1);
                // Push along the rig: forward z, toward its left x. A blow from in front pushes it back: the trunk bends back.
                float lz=h.Dir==Vector2.Zero?-1:Vector2.Dot(h.Dir,fwd),lx=h.Dir==Vector2.Zero?0:-Vector2.Dot(h.Dir,right);
                pitch+=lz*e;roll+=-lx*e*.8f;yaw+=lx*e*.6f;snap+=lz*late;headRoll+=-lx*late*.7f;
            }
            float reel=Reel(t),r=float.IsNaN(reel)?0:Sm(reel/.2f)*(1-Sm((reel-.7f)/.5f));
            pitch-=.35f*r*give;snap-=.25f*r*give;
            Bend(p,CapturedRig.Spine,pitch*.45f,yaw*.4f,roll*.45f);Bend(p,CapturedRig.Chest,pitch*.55f,yaw*.6f,roll*.55f);
            Bend(p,CapturedRig.Neck,snap*.4f,0,headRoll*.4f);Bend(p,CapturedRig.Head,snap*.6f,0,headRoll*.6f);
            // The hips drop a little under a heavy blow.
            float drop=0;for(int i=0;i<count;i++){ref var h=ref At(i);float tau=c-h.At;if(tau>=0&&tau<.6f)drop+=Envelope(tau,.09f)*(h.Weight==StaggerWeight.Heavy?.05f:h.Weight==StaggerWeight.Step?.025f:.01f)*give;}
            p.Position[CapturedRig.Body].Y-=Math.Min(.08f,drop+.04f*r);
        }
        /// <summary>The arms' part: thrown out by a heavy blow or the authority's stagger, flinching up from a lighter one.</summary>
        public void Arms(CapturedRig.Pose p,float t)
        {
            if(count==0)return;float c=Clock(t),fling=0,flinch=0;
            for(int i=0;i<count;i++){ref var h=ref At(i);float tau=c-h.At;if(tau<0||tau>1)continue;float e=Envelope(tau,.08f)*give;if(h.Weight==StaggerWeight.Heavy)fling+=e;else flinch+=e*(h.Weight==StaggerWeight.Step?.7f:.4f);}
            float reel=Reel(t);if(!float.IsNaN(reel))fling+=Sm(reel/.15f)*(1-Sm((reel-.65f)/.5f))*.8f*give;
            fling=Math.Min(1,fling);flinch=Math.Min(1,flinch);if(fling+flinch<=0)return;
            for(int i=0;i<2;i++)
            {
                bool left=i==0;int arm=left?CapturedRig.LArm:CapturedRig.RArm;float s=left?1:-1;
                p.Rotation[arm]=Quaternion.Slerp(p.Rotation[arm],CapturedRig.Euler(-.9f*flinch+.35f*fling,0,s*(.25f*flinch+1.1f*fling)),Math.Min(1,fling+flinch*.6f));
                p.Rotation[arm+1]=Quaternion.Slerp(p.Rotation[arm+1],CapturedRig.Euler(-.9f*flinch-.3f*fling),Math.Min(1,fling+flinch*.6f));
            }
        }
        private static void Bend(CapturedRig.Pose p,int bone,float x,float y,float z){if(x==0&&y==0&&z==0)return;p.Rotation[bone]=CapturedRig.Euler(x,y,z)*p.Rotation[bone];}
        private static float Sm(float x)=>MeleeMath.Smooth(x);
    }
}

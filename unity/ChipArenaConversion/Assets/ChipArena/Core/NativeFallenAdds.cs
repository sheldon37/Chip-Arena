using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// NOCTIEL's adds as living things (the owner, 2026-11-08: "Begin animating his minions as well"; docs/EXPEDITION-BOSS-FINAL.md section 9). In
    /// place of the gear bank's rigid "cantor", "penitent", "throne-ring" and "throne-eye" (which slid, turned and, live, hung a metre over the water),
    /// each add is posed from its facts every frame and built (Core NativeFallenKit) in the bank's shapes and finishes:
    ///  - The Cantor, a hollow robe hanging in the air: it rises out of the water and unfurls; the robe sways and its hem ripples, trailing as it
    ///    moves; the torn strips swing; the empty sleeves lift as it sings, the hood tipping back and the mouth opening wider as the beam
    ///    strengthens, a pulse through it on each note that hurts; it flinches when struck; killed, the robe crumples and falls into the water.
    ///  - The Penitent, a pale husk: it claws its way up out of the water by its hands; it crawls on all fours, each hand planted on the water
    ///    where it came down until the body has passed it (a ring where it lands), the back heaving with each reach and the knees dragging; close
    ///    to its quarry it rears up on its knees and claws at it, striking on each blow that lands; killed, it falls forward and sinks.
    ///  - The Throne, black blades round one white eye: it comes down from above folded like a bud and opens; the ring turns, its blades breathing
    ///    in a wave round it; the eye looks at whoever it hunts and blinks; before each shot the blades close and pitch toward its quarry and the
    ///    ring spins up, the eye narrowing; on the shot they throw open, the ring kicks back and the eye flares; brought down, its blades fly
    ///    apart and fall to the water and the eye drops dark.
    /// The facts are what the authority sends (place, facing quarry, birth, last blow, last hit; Runtime NativeFallenBinding) or the preview's script
    /// (Core NativeFallenBossFx.AddsOf). Everything here is presentation: no hit time, hitbox, damage, cooldown or movement is read back. Stage
    /// frame (the water at y 0). Pure.
    /// </summary>
    public static class NativeFallenAdds
    {
        public const byte Cantor=0,Penitent=1,Throne=2;
        /// <summary>What's known of an add this frame. Born: when it came; Until: when it would leave (live, its lifetime's end); LastAttack: its last
        /// blow (NaN or before Born: none); LastHit: when it was last struck; Strength: a Cantor's song (0 to 1); Period: the time between its blows
        /// (0: the authority's Every); Target: its quarry's feet.</summary>
        public struct Seen{public int Id;public byte Kind;public Vector3 At;public float Born,Until,LastAttack,LastHit,Strength,Period;public Vector3 Target;public bool HasTarget;}
        public static byte KindOf(string kind)=>kind=="cantor"?Cantor:kind=="penitent"?Penitent:Throne;
        /// <summary>A Cantor's song: it strengthens over ten seconds of holding a pilot (the authority's damage, 8 rising to 20).</summary>
        public static float CantorStrength(float age)=>Math.Max(0,Math.Min(1,age/10f));
        /// <summary>A Throne's shots, a Cantor's and a Penitent's blows: every 1.2 s while it has a quarry (the authority's NextAttack).</summary>
        public const float Every=1.2f;
    }

    public sealed class NativeFallenAddMotion
    {
        public const float CantorRise=1.3f,PenitentRise=1.25f,ThroneArrive=1.5f;
        public static float DeathTime(byte kind)=>kind==NativeFallenAdds.Cantor?1.4f:kind==NativeFallenAdds.Penitent?1.5f:1.8f;
        /// <summary>A Penitent's reach ahead of its shoulders, the distance its planted hand may fall behind before it reaches again, and how close
        /// it rears up to claw.</summary>
        public const float Reach=.9f,Stride=1.3f,Latch=2.4f;
        public sealed class Add
        {
            public int Id;public byte Kind;public NativeFallenAdds.Seen Last;public Vector3 At,Vel;public float Yaw,Born,GoneAt=-1,LastSeen;public bool Killed;
            /// <summary>The Throne's facing (its eye's way), its ring's turn, its kick back; the Cantor's and Penitent's rear-up.</summary>
            public Vector3 Facing=Vector3.UnitZ;public float Spin,Recoil,Rear,Gait;public float ShotSeen=float.NaN;
            /// <summary>A Penitent's hands: where each is (planted on the water, or swinging from From to To since SwingAt).</summary>
            public readonly Vector3[] Hand=new Vector3[2],From=new Vector3[2],To=new Vector3[2];public readonly float[] SwingAt={-10,-10};public readonly bool[] Up=new bool[2];public bool HandsSet;
            /// <summary>After the pose: where its song or shot leaves it, and how strongly.</summary>
            public Vector3 Mouth;public float Sing,Fire;
            public float SplashDone;
        }
        public readonly List<Add> Adds=new List<Add>();
        /// <summary>This frame's rings on the water (where, how strong) and beams (from, to, width, alpha).</summary>
        public readonly List<(Vector3 at,float strength)> Rings=new List<(Vector3,float)>();
        public readonly List<(Vector3 from,Vector3 to,float width,float alpha)> Beams=new List<(Vector3,Vector3,float,float)>();
        /// <summary>Crowns of water this frame (where, its age, a seed).</summary>
        public readonly List<(Vector3 at,float age,int seed)> Splashes=new List<(Vector3,float,int)>();
        private float last=float.NaN;private readonly HashSet<int> seenNow=new HashSet<int>();

        public void Clear(){Adds.Clear();Rings.Clear();Beams.Clear();Splashes.Clear();last=float.NaN;}
        public void Step(IList<NativeFallenAdds.Seen> seen,int count,float now,float dt)
        {
            if(!float.IsNaN(last)&&(now<last-.05f||now>last+5))Adds.Clear();last=now;dt=Math.Max(0,Math.Min(.1f,dt));
            Rings.Clear();Beams.Clear();Splashes.Clear();seenNow.Clear();
            for(int i=0;i<count;i++)
            {
                var s=seen[i];seenNow.Add(s.Id);var a=Find(s.Id);
                if(a==null||a.Kind!=s.Kind){if(a!=null)Adds.Remove(a);a=new Add{Id=s.Id,Kind=s.Kind,At=s.At,Born=Math.Min(s.Born,now),Yaw=YawTo(s.At,s.Target,s.HasTarget,0)};a.Facing=s.HasTarget&&(s.Target-s.At).LengthSquared()>.01f?Vector3.Normalize(s.Target-s.At):Vector3.UnitZ;Adds.Add(a);}
                if((s.At-a.At).LengthSquared()>36)a.HandsSet=false;
                var v=dt>0?(s.At-a.At)/dt:Vector3.Zero;a.Vel=Vector3.Lerp(a.Vel,v,1-(float)Math.Exp(-dt*10));a.At=s.At;a.Last=s;a.LastSeen=now;a.GoneAt=-1;
            }
            foreach(var a in Adds)if(a.GoneAt<0&&!seenNow.Contains(a.Id)){a.GoneAt=now;a.Killed=a.Last.Until<=0||now<a.Last.Until-.25f;a.Vel*=.3f;}
            Adds.RemoveAll(a=>a.GoneAt>=0&&now-a.GoneAt>DeathTime(a.Kind));
            foreach(var a in Adds)
            {
                bool dying=a.GoneAt>=0;var s=a.Last;
                if(!dying){float want=YawTo(a.At,s.Target,s.HasTarget,a.Yaw);a.Yaw=TurnTo(a.Yaw,want,dt*(a.Kind==NativeFallenAdds.Penitent?7f:2.4f));}
                if(a.Kind==NativeFallenAdds.Penitent)PenitentStep(a,now,dt,dying);
                else if(a.Kind==NativeFallenAdds.Throne)ThroneStep(a,now,dt,dying);
                else CantorStep(a,now,dt,dying);
            }
        }
        private Add Find(int id){foreach(var a in Adds)if(a.Id==id)return a;return null;}
        private static float YawTo(Vector3 from,Vector3 to,bool has,float keep){var d=to-from;d.Y=0;return has&&d.LengthSquared()>.0004f?(float)Math.Atan2(d.X,d.Z):keep;}
        private static float TurnTo(float yaw,float want,float max){float d=want-yaw;while(d>Math.PI)d-=2*(float)Math.PI;while(d<-Math.PI)d+=2*(float)Math.PI;return yaw+Math.Max(-max,Math.Min(max,d));}
        private static Vector3 Fwd(float yaw)=>new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));
        private static Vector3 Right(float yaw)=>new Vector3((float)Math.Cos(yaw),0,-(float)Math.Sin(yaw));
        private static float C(float x)=>Math.Max(0,Math.Min(1,x));
        private static float Smooth(float x){x=C(x);return x*x*(3-2*x);}
        private static float Flat(Vector3 v)=>(float)Math.Sqrt(v.X*v.X+v.Z*v.Z);
        /// <summary>The add's last blow, if it has had one since it came.</summary>
        private static float Blow(Add a)=>!float.IsNaN(a.Last.LastAttack)&&a.Last.LastAttack>=a.Born-.05f?a.Last.LastAttack:float.NaN;
        private static float Pulse(Add a,float now,float rate){float b=Blow(a);return float.IsNaN(b)||now<b?0:(float)Math.Exp(-(now-b)*rate);}
        private static float Flinch(Add a,float now){float h=now-a.Last.LastHit;return h<0||h>.7f?0:(float)Math.Exp(-h*7)*Math.Min(1,h*30);}

        // ---- the Cantor ----
        private void CantorStep(Add a,float now,float dt,bool dying)
        {
            var s=a.Last;float age=now-a.Born;a.Sing=dying?a.Sing*(float)Math.Exp(-dt*6):s.HasTarget?Smooth((age-CantorRise*.8f)/.6f)*(.3f+.7f*C(s.Strength)):a.Sing*(float)Math.Exp(-dt*3);
            if(age<1.2f)Splashes.Add((Floor(a.At),age,a.Id*7));
        }
        public static void CantorPose(Add a,float now,int tier,NativeFallenKit k)
        {
            float age=now-a.Born,rise=Smooth(age/CantorRise),d=a.GoneAt>=0?C((now-a.GoneAt)/DeathTime(a.Kind)):0,id=a.Id*1.618f;
            float sing=a.Sing,pulse=Pulse(a,now,5),hit=Flinch(a,now);
            var f=Fwd(a.Yaw);var r=Right(a.Yaw);var up=Vector3.UnitY;
            float hover=.55f+.14f*(float)Math.Sin(now*1.7f+id)-(1-rise)*2.7f-2.8f*d*d;var origin=Floor(a.At)+up*hover-f*(.28f*hit);
            // Its motion in its own frame: the robe trails it.
            float vf=Vector3.Dot(a.Vel,f),vr=Vector3.Dot(a.Vel,r);
            int n=tier==0?14:9;float t=now;
            // The bell: narrow at the shoulders, wide at the torn hem; each ring swaying a little later than the one above it, the hem rippling.
            var hs=new[]{2.32f,2.2f,2.0f,1.6f,1.1f,.55f,.2f};var rx=new[]{.05f,.24f,.3f,.34f,.44f,.58f,.66f};var rz=new[]{.05f,.17f,.2f,.26f,.36f,.5f,.58f};
            var rings=new List<Vector3[]>(hs.Length);var cs=new List<Vector3>(hs.Length);var hem=new Vector3[n];
            float open=.32f+.68f*Smooth(age/(CantorRise*1.3f));float crumple=1-.65f*d;
            for(int i=0;i<hs.Length;i++)
            {
                float q=i/(float)(hs.Length-1),q2=q*q;float wob=1+hit*1.4f+pulse*.6f;
                var c=origin+up*(hs[i]*(1-.35f*d*q))+r*(.16f*q2*(float)Math.Sin(t*1.3f-2.2f*q+id)*wob-vr*q*.1f)+f*(.12f*q2*(float)Math.Sin(t*1.1f-1.8f*q+1.3f+id)*wob-vf*q*.12f);
                float wide=i>=2?open:1;var ring=new Vector3[n];
                for(int j=0;j<n;j++)
                {
                    double th=2*Math.PI*j/n;float ca=(float)Math.Cos(th),sa=(float)Math.Sin(th);float fl=1+q2*(.07f*(float)Math.Sin(3*th+2.1f*t+id)+.04f*(float)Math.Sin(5*th-1.7f*t)+.05f*pulse*(float)Math.Sin(2*th+t*9));
                    ring[j]=c+r*(ca*rx[i]*wide*fl*crumple)+f*(sa*rz[i]*wide*fl*crumple-.02f);
                }
                rings.Add(ring);cs.Add(c);if(i==hs.Length-1)hem=ring;
            }
            k.Loft(rings,cs,NativeFallenKit.Feather,false,false,false,false);
            // The inside of the bell, dark (there is no one in it).
            var inner=new List<Vector3[]>();for(int i=3;i<rings.Count;i++){var ring=new Vector3[n];for(int j=0;j<n;j++)ring[j]=cs[i]+(rings[i][j]-cs[i])*.97f;inner.Add(ring);}
            InsideOut(k,inner,cs.GetRange(3,cs.Count-3),NativeFallenKit.Black);
            // The torn strips below the hem, swinging, trailing its motion.
            int strips=tier==0?7:4;for(int s=0;s<strips;s++)
            {
                int j=(int)((s+.5f)/strips*n)%n;var p0=hem[j];var outw=Vector3.Normalize(new Vector3(p0.X-cs[cs.Count-1].X,0,p0.Z-cs[cs.Count-1].Z)+new Vector3(0,.0001f,0));
                float sw=.22f*(float)Math.Sin(t*1.9f+s*2.3f+id)+.06f*hit*(float)Math.Sin(t*25);float len=.38f+.3f*Hash(a.Id*13+s);
                var trail=-(f*vf+r*vr)*.05f;var p1=p0+outw*.05f-up*(.2f)+(r*sw*.25f+trail)*.5f;var p2=p1-up*len+(r*sw+f*(.15f*(float)Math.Sin(t*1.4f+s))*.6f+trail)*len;
                k.Tube(new[]{p0,p1,p2},new[]{.05f*crumple,.04f,.018f},tier==0?5:4,NativeFallenKit.FeatherDark,false,false,true);
            }
            // The hood, tipped back as it sings (more on each note), the mouth open wider.
            var neck=origin+up*(2.2f*(1-.35f*d));float pitch=-(.18f*sing+.12f*pulse)+.55f*d*d+.06f*(float)Math.Sin(t*1.2f+id);float tilt=.07f*(float)Math.Sin(t*.9f+id*2);
            var hq=Quaternion.CreateFromAxisAngle(r,pitch)*Quaternion.CreateFromAxisAngle(f,tilt);Vector3 H(Vector3 local)=>neck+Vector3.Transform(local,hq);
            // The hood is open over its face: a cowl round the back, the opening wider as it sings (more on each note), black inside to the back
            // of it (no one there), a pale rim round the opening and the white of the note deep in it.
            var hr=new[]{(.2f,.2f),(.24f,.24f),(.25f,.25f),(.22f,.23f),(.15f,.16f),(.06f,.07f)};float face=.62f+.28f*sing+.22f*pulse;var opens=new[]{0f,.75f*face,face,.85f*face,.35f*face,0f};
            int m=n+1;var hood=new List<Vector3[]>();var hollow=new List<Vector3[]>();var hc=new List<Vector3>();
            for(int i=0;i<hr.Length;i++)
            {
                var c=up*(.02f+i*.092f)+f*(.02f+.012f*i);var ring=new Vector3[m];var inside=new Vector3[m];float o=opens[i];
                for(int j=0;j<m;j++){double th=Math.PI*.5+o+(2*Math.PI-2*o)*j/(m-1);var off=r*((float)Math.Cos(th)*hr[i].Item1)+f*((float)Math.Sin(th)*hr[i].Item2);ring[j]=H(c+off);inside[j]=H(c+off*.9f);}
                hood.Add(ring);hollow.Add(inside);hc.Add(H(c));
            }
            OpenLoft(k,hood,hc,NativeFallenKit.Feather,false,true);OpenLoft(k,hollow,hc,NativeFallenKit.Black,true,false);
            // The rim round the opening: up its left edge, over, down its right.
            var rim=new List<Vector3>();for(int i=1;i<hr.Length-1;i++)rim.Add(hood[i][0]);for(int i=hr.Length-2;i>=1;i--)rim.Add(hood[i][m-1]);
            k.Tube(rim,.018f*(1+.3f*pulse),5,NativeFallenKit.PlateTrim,false,true,true);
            float note=.03f*(1+1.6f*sing+2.2f*pulse)*(1-d);if(note>.002f)k.Ellipsoid(H(f*.02f+up*.24f),Vector3.Transform(r,hq)*note,Vector3.Transform(up,hq)*(note*1.6f),Vector3.Transform(f,hq)*(note*.6f),8,6,NativeFallenKit.Light,true);
            a.Mouth=H(f*.16f+up*.24f);
            // The empty sleeves, held out, lifting as it sings, flicking on each note.
            for(int side=-1;side<=1;side+=2)
            {
                var sh=origin+up*(2f*(1-.35f*d))+r*(side*.25f);float lift=.7f*sing+.22f*pulse+.1f*(float)Math.Sin(t*.8f+side+id)-.5f*d;float sweep=.25f*sing*side*-1+.08f*(float)Math.Sin(t*.6f+id+side);
                var q=Quaternion.CreateFromAxisAngle(f,side*lift)*Quaternion.CreateFromAxisAngle(up,sweep);Vector3 S(Vector3 l)=>sh+Vector3.Transform(l,q);
                var e=S(r*(side*.37f)+up*-.28f+f*.1f);var w=S(r*(side*.53f)+up*-.66f+f*.22f+f*(-vf*.04f));
                k.Tube(new[]{sh,e,w},new[]{.1f,.095f,.09f*(1-.3f*d)},tier==0?8:5,NativeFallenKit.Feather,false,false,false);
                k.Ellipsoid(w+Vector3.Normalize(w-e)*.015f,r*.085f,Vector3.Normalize(w-e)*.05f,Vector3.Cross(r,Vector3.Normalize(w-e))*.085f,8,5,NativeFallenKit.Black,false,false);
            }
        }
        // Rings that do not close round (each its own points, the first and last not joined), facing away from their centres or toward them.
        private static void OpenLoft(NativeFallenKit k,List<Vector3[]> rings,List<Vector3> centres,uint color,bool inward,bool line)
        {
            int n=rings[0].Length,start=k.Count;foreach(var r in rings)foreach(var p in r)k.V(p,color,line);
            for(int i=0;i+1<rings.Count;i++)for(int j=0;j+1<n;j++){int a=start+i*n+j,b=a+1,c=a+n+1,d=a+n;var mid=(k.P[a]+k.P[c])*.5f;var o=mid-(centres[i]+centres[i+1])*.5f;k.Quad(a,b,c,d,inward?-o:o,false);}
        }
        // A loft facing its middle (the inside of a bell).
        private static void InsideOut(NativeFallenKit k,List<Vector3[]> rings,List<Vector3> centres,uint color)
        {
            int n=rings[0].Length,start=k.Count;foreach(var r in rings)foreach(var p in r)k.V(p,color,false);
            for(int i=0;i+1<rings.Count;i++)for(int j=0;j<n;j++){int jj=(j+1)%n,a=start+i*n+j,b=start+i*n+jj,c=start+(i+1)*n+jj,d=start+(i+1)*n+j;var mid=(k.P[a]+k.P[c])*.5f;var cm=(centres[i]+centres[i+1])*.5f;var o=cm-mid;o.Y=0;k.Quad(a,b,c,d,o,false);}
        }

        // ---- the Penitent ----
        private void PenitentStep(Add a,float now,float dt,bool dying)
        {
            var s=a.Last;float age=now-a.Born;var f=Fwd(a.Yaw);var r=Right(a.Yaw);var body=Floor(a.At);float speed=Flat(a.Vel);
            float dist=s.HasTarget?Flat(s.Target-a.At):99;bool latch=!dying&&s.HasTarget&&dist<Latch&&speed<3.5f&&age>PenitentRise;a.Rear+=((latch?1:0)-a.Rear)*(1-(float)Math.Exp(-dt*(latch?9:5)));
            a.Gait+=speed*dt;
            if(age<PenitentRise+.3f)Splashes.Add((body+f*.5f,age,a.Id*11));
            for(int h=0;h<2;h++)
            {
                int side=h==0?-1:1;var want=body+f*(Reach+Math.Min(.5f,speed*.06f))+r*(side*.3f);want.Y=0;
                if(!a.HandsSet){a.Hand[h]=a.To[h]=want;a.Up[h]=false;if(h==1)a.HandsSet=true;continue;}
                if(a.Up[h])
                {
                    float dur=Math.Max(.09f,Math.Min(.26f,.3f-speed*.03f));float u=(now-a.SwingAt[h])/dur;
                    // The hand reaches for a point the body will pass, so it lands ahead.
                    a.To[h]=want+f*(Math.Min(Stride*.45f,speed*dur*1.4f));a.To[h].Y=0;
                    if(u>=1){a.Up[h]=false;a.Hand[h]=a.To[h];if(!dying&&a.Rear<.5f)Rings.Add((a.Hand[h],.32f+Math.Min(.3f,speed*.05f)));}
                    else{float e=Smooth(u);a.Hand[h]=Vector3.Lerp(a.From[h],a.To[h],e)+Vector3.UnitY*(float)(.22f*Math.Sin(Math.PI*u)*(.5f+.5f*Math.Min(1,speed/4)));}
                }
                else if(!dying&&a.Rear<.5f&&age>PenitentRise*.5f)
                {
                    bool other=a.Up[1-h];float behind=Vector3.Distance(a.Hand[h],want);
                    if(!other&&(behind>Stride*.5f||(behind>.18f&&speed<.3f&&now-a.SwingAt[h]>.8f))&&now-a.SwingAt[1-h]>.05f){a.Up[h]=true;a.SwingAt[h]=now;a.From[h]=a.Hand[h];}
                }
                if(Vector3.Distance(a.Hand[h],want)>3.5f){a.Hand[h]=want;a.Up[h]=false;}
            }
        }
        public static void PenitentPose(Add a,float now,int tier,NativeFallenKit k)
        {
            float age=now-a.Born,rise=Smooth(age/PenitentRise),d=a.GoneAt>=0?C((now-a.GoneAt)/DeathTime(a.Kind)):0,id=a.Id*1.618f;
            var f=Fwd(a.Yaw);var r=Right(a.Yaw);var up=Vector3.UnitY;var o=Floor(a.At);float speed=Flat(a.Vel),crawl=Smooth(speed/3f),rear=a.Rear,hit=Flinch(a,now),blow=Pulse(a,now,6);
            // The gait: a heave with each reach, the back swaying to the planted side.
            float ph=a.Gait/Stride*(float)Math.PI*2;float heave=.07f*crawl*(float)Math.Abs(Math.Sin(ph))+.02f*(float)Math.Sin(now*1.3f+id)*(1-crawl);
            float sway=.12f*crawl*(float)Math.Sin(ph);float rock=.05f*(float)Math.Sin(now*1.1f+id)*(1-crawl)*(1-rear);
            Vector3 L(float x,float y,float z)=>o+r*x+up*y+f*z;
            // Three poses: kneeling bowed (the bank's), crawling low and long, reared up on its knees.
            Vector3 Mix(Vector3 kneel,Vector3 low,Vector3 high)=>Vector3.Lerp(Vector3.Lerp(kneel,low,crawl),high,rear);
            var pelvis=Mix(L(0,.3f,0),L(0,.36f,-.1f),L(0,.44f,-.02f))+up*heave*.5f;
            var back=Mix(L(0,.62f,.1f),L(sway*.3f,.6f,.18f),L(0,.85f,.02f))+up*heave;
            var chest=Mix(L(0,.9f+rock,.27f),L(sway,.72f,.45f),L(0,1.14f,.1f))+up*heave;
            var neck=Mix(L(0,1.04f+rock,.45f),L(sway*.6f,.8f,.68f),L(0,1.3f,.18f))+up*heave;
            var head=Mix(L(0,1f+rock*1.4f,.74f),L(sway*.4f,.82f,.92f),L(0,1.42f,.3f))+up*heave;
            // Rising: up out of the water, back first, pitched up; flinching, thrown back; dying, down on its face and under.
            float sink=(1-rise)*1.15f+d*d*1.4f;var push=-f*(.2f*hit)+up*(-sink);
            float fall=d*.55f;Vector3 Bend(Vector3 p){var rel=p-o;rel=Vector3.Transform(rel,Quaternion.CreateFromAxisAngle(r,fall-(1-rise)*.6f));return o+rel+push;}
            pelvis=Bend(pelvis);back=Bend(back);chest=Bend(chest);neck=Bend(neck);
            // The head looks at its quarry, shaking when it claws.
            var look=a.Last.HasTarget?a.Last.Target+up*1.1f:head+f;float thrash=rear*(.06f*(float)Math.Sin(now*23+id)+.08f*blow);head=Bend(head)+r*thrash;
            // The spine: rings along pelvis, back, chest, neck.
            var spine=NativeFallenKit.Curve(new[]{pelvis,back,chest,neck},tier==0?3:2);var sr=new[]{(.13f,.1f),(.17f,.12f),(.21f,.14f),(.19f,.13f),(.09f,.08f)};int n=tier==0?12:7;
            var rings=new List<Vector3[]>();var cs=new List<Vector3>();
            for(int i=0;i<spine.Count;i++)
            {
                var tg=Vector3.Normalize(i+1<spine.Count?spine[i+1]-spine[i]:spine[i]-spine[i-1]);var x=Vector3.Normalize(r-tg*Vector3.Dot(r,tg));var y=Vector3.Cross(tg,x);if(Vector3.Dot(y,up)<0)y=-y;
                float u=i/(float)(spine.Count-1)*(sr.Length-1);int j=Math.Min(sr.Length-2,(int)u);float w=u-j;float ax=sr[j].Item1+(sr[j+1].Item1-sr[j].Item1)*w,ay=sr[j].Item2+(sr[j+1].Item2-sr[j].Item2)*w;
                rings.Add(NativeFallenKit.Ring(spine[i]+y*.01f,x,y,ax,ay,n));cs.Add(spine[i]);
            }
            k.Loft(rings,cs,NativeFallenKit.Bone,false,true,true,true);
            // The ribs standing off the back.
            for(int i=0;i<(tier==0?5:2);i++)
            {
                float u=.35f+i*.11f;int si=Math.Min(spine.Count-2,(int)(u*(spine.Count-1)));var p=spine[si];var tg=Vector3.Normalize(spine[si+1]-spine[si]);var x=Vector3.Normalize(r-tg*Vector3.Dot(r,tg));var y=Vector3.Cross(tg,x);if(Vector3.Dot(y,up)<0)y=-y;
                for(int side=-1;side<=1;side+=2)k.Tube(new[]{p+y*.1f+x*(side*.03f),p+y*.12f+x*(side*.15f)-tg*.03f,p+y*.04f+x*(side*.2f)+tg*.06f},.012f,4,NativeFallenKit.PlateDark,false,true,false);
            }
            // The bowed head, no face.
            var hf=Vector3.Normalize(Vector3.Lerp(f*.6f-up*.8f,Vector3.Normalize(look-head),.35f+.65f*rear));var hx=Vector3.Normalize(Vector3.Cross(up,hf));var hy=Vector3.Cross(hf,hx);
            k.Ellipsoid(head,hx*.105f,hy*.125f,hf*.135f,tier==0?12:7,tier==0?9:5,NativeFallenKit.Bone);
            k.Ellipsoid(head+hf*.1f-hy*.02f,hx*.05f,hy*.06f,hf*.03f,8,5,NativeFallenKit.Black,false,false);
            // The arms: a long upper arm and forearm to the hand, the elbows out like a crawling thing's.
            for(int h=0;h<2;h++)
            {
                int side=h==0?-1:1;var sh=chest+r*(side*.2f)+up*.02f;Vector3 hand;
                if(rear>.01f)
                {
                    // Clawing at its quarry: the hands thrown at it on each blow, pulled back between, trembling.
                    var to=a.Last.HasTarget?a.Last.Target+up*1f:sh+f;var way=Vector3.Normalize(to-sh);float strike=h==0?blow:Pulse(a,now-.09f,6);float wind=.72f+.26f*strike-.12f*(float)Math.Abs(Math.Sin(now*3.1f+h*1.7f));
                    var claw=sh+way*(.85f*wind)+r*(side*(.12f-.1f*strike))+up*(.04f*(float)Math.Sin(now*31+h*2));hand=Vector3.Lerp(a.Hand[h]+push,claw,rear);
                }
                else hand=a.Hand[h]+(rise<1?up*-(1-Smooth(age/(PenitentRise*.45f)))*.8f:Vector3.Zero)+push*(d>0?1:0);
                if(d>0)hand=Vector3.Lerp(hand,o+f*1.05f+r*(side*.35f)+up*(-d*1.2f),Smooth(d*1.5f));
                var elbow=Elbow(sh,hand,.47f,.5f,Vector3.Normalize(r*side*(1.1f-.5f*rear)+up*(.7f-1f*rear)-f*(.2f+.2f*rear)));
                k.Tube(new[]{sh,elbow,hand},new[]{.05f,.04f,.035f},tier==0?7:5,NativeFallenKit.Bone,false,true,true);
                // Fingers spread on the water, curled when clawing.
                var fw=Vector3.Normalize(new Vector3(hand.X-elbow.X,0,hand.Z-elbow.Z)+f*.5f);var fr=Vector3.Cross(up,fw);
                for(int g=-1;g<=1;g++){var tipAt=hand+fw*(.11f-.04f*rear)+fr*(g*.05f)-up*(.02f-.08f*rear)+up*(.06f*rear*Math.Abs(g));k.Tube(new[]{hand,hand+fw*.05f+fr*(g*.03f),tipAt},.012f,4,NativeFallenKit.Bone,false,true,true);}
            }
            // The legs folded under it, the knees dragging on the water.
            for(int side=-1;side<=1;side+=2)
            {
                float shuffle=.08f*crawl*(float)Math.Sin(ph+(side>0?Math.PI:0));var hip=pelvis+r*(side*.12f);var knee=o+r*(side*.2f)+f*(.3f-.25f*rear+shuffle)+up*(.1f)+push;var foot=o+r*(side*.16f)+f*(-.16f+shuffle*.5f)+up*.05f+push;
                k.Tube(new[]{hip,knee},new[]{.085f,.07f},tier==0?8:5,NativeFallenKit.Bone,false,true,true);k.Tube(new[]{knee,foot},new[]{.06f,.04f},tier==0?7:5,NativeFallenKit.Bone,false,true,true);
            }
            a.Mouth=head;
        }
        /// <summary>Two-bone reach: the elbow of an arm from `a` to `c` with these lengths, bent toward `pole`.</summary>
        public static Vector3 Elbow(Vector3 a,Vector3 c,float l1,float l2,Vector3 pole)
        {
            var d=c-a;float len=d.Length();if(len<1e-4f)return a+pole*l1;var dir=d/len;len=Math.Min(len,(l1+l2)*.999f);
            float x=(l1*l1-l2*l2+len*len)/(2*len);float h=(float)Math.Sqrt(Math.Max(0,l1*l1-x*x));var p=pole-dir*Vector3.Dot(pole,dir);p=p.LengthSquared()>1e-8f?Vector3.Normalize(p):Vector3.UnitY;return a+dir*x+p*h;
        }

        // ---- the Throne ----
        private void ThroneStep(Add a,float now,float dt,bool dying)
        {
            var s=a.Last;var c=a.At;
            if(!dying&&s.HasTarget){var want=Vector3.Normalize(s.Target-c);a.Facing=Slerp(a.Facing,want,1-(float)Math.Exp(-dt*3));}
            float charge=Charge(a,now);a.Spin+=dt*(.45f+4.6f*charge)*(dying?.3f:1);
            float b=Blow(a);
            if(!float.IsNaN(b)&&b!=a.ShotSeen&&now-b<.3f){a.ShotSeen=b;if(s.HasTarget)Rings.Add((Floor(s.Target),1.1f));}
            a.Fire=float.IsNaN(b)||now<b?0:(float)Math.Exp(-(now-b)*7);a.Recoil+=(a.Fire*.7f-a.Recoil)*(1-(float)Math.Exp(-dt*(a.Fire>a.Recoil?40:6)));
            if(dying)
            {
                float tau=now-a.GoneAt;if(a.SplashDone==0&&tau>.85f){a.SplashDone=1;Rings.Add((Floor(c),1.2f));for(int i=0;i<5;i++){var w=Dir(a.Id*31+i*5);Rings.Add((Floor(c)+w*(3+i*.8f),.6f));}}
            }
        }
        /// <summary>A Throne's charge before its next shot (0 to 1).</summary>
        public static float ChargeOf(Add a,float now)=>Charge(a,now);
        // The charge before each shot (the authority's 1.2 s between blows).
        private static float Charge(Add a,float now){float b=Blow(a);if(float.IsNaN(b)||!a.Last.HasTarget||a.GoneAt>=0)return 0;float next=b+(a.Last.Period>0?a.Last.Period:NativeFallenAdds.Every);return Smooth((now-(next-.5f))/.5f)*(now<next+.15f?1:0);}
        public static void ThronePose(Add a,float now,int tier,NativeFallenKit k)
        {
            float age=now-a.Born,arrive=Smooth(age/ThroneArrive),d=a.GoneAt>=0?C((now-a.GoneAt)/DeathTime(a.Kind)):0,tau=a.GoneAt>=0?now-a.GoneAt:0,id=a.Id*1.618f;
            float charge=Charge(a,now),fire=a.Fire,hit=Flinch(a,now);
            var fw=a.Facing;var c=a.At+Vector3.UnitY*(.25f*(float)Math.Sin(now*.7f+id)+(1-arrive)*14)-fw*a.Recoil;
            // Shudder when struck.
            var shake=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.12f*hit*(float)Math.Sin(now*41))*Quaternion.CreateFromAxisAngle(Vector3.UnitX,.1f*hit*(float)Math.Cos(now*37));
            fw=Vector3.Normalize(Vector3.Transform(fw,shake));var ux=Vector3.Normalize(Vector3.Cross(Math.Abs(fw.Y)<.95f?Vector3.UnitY:Vector3.UnitX,fw));var uy=Vector3.Cross(fw,ux);
            float spin=a.Spin;int count=11;
            for(int i=0;i<count;i++)
            {
                float ang=spin+i*2*(float)Math.PI/count;var outw=ux*(float)Math.Cos(ang)+uy*(float)Math.Sin(ang);var tan=Vector3.Cross(fw,outw);
                // Breathing in a wave round the ring; closing and pitching to its quarry before a shot; thrown open on it; folded back as it comes.
                float breathe=.14f*(float)Math.Sin(now*1.4f-i*2*Math.PI/count*2+id);float tilt=breathe+.6f*charge-.35f*fire-(1-arrive)*1.25f;float pitch=1.35f*charge+.25f*(float)Math.Sin(now*.9f+i);
                var way=Vector3.Normalize(outw+tan*.35f);way=Vector3.Normalize(way*(float)Math.Cos(tilt)+fw*(float)Math.Sin(tilt));
                var across=Vector3.Normalize(fw*(float)Math.Cos(pitch)+tan*(float)Math.Sin(pitch));var thick=Vector3.Normalize(Vector3.Cross(way,across));
                var b=c+outw*.62f;float len=1.5f;
                if(d>0){var fly=Dir(a.Id*17+i*3);var vel=Vector3.Normalize(outw*1.4f+fly*.6f)*5.5f+Vector3.UnitY*2.2f;b=b+vel*tau-Vector3.UnitY*(4.9f*tau*tau);var q=Quaternion.CreateFromAxisAngle(Vector3.Normalize(fly+Vector3.UnitY*.3f),tau*(6+i%3*2));way=Vector3.Transform(way,q);across=Vector3.Transform(across,q);thick=Vector3.Transform(thick,q);}
                k.Blade(b,way,across,thick,len,new[]{.03f,.1f,.13f,.11f,.07f,.03f,0},NativeFallenKit.SteelDark,false,true,.22f);
            }
            var cc=d>0?c-Vector3.UnitY*(4.9f*Math.Max(0,tau-.1f)*Math.Max(0,tau-.1f)):c;
            if(d<.5f){k.Torus(cc,fw,.66f,.05f,tier==0?28:16,6,NativeFallenKit.SteelDark);k.Torus(cc,fw,.78f*(1+.06f*fire),.014f,tier==0?28:16,4,NativeFallenKit.PlateTrim);}
            // The eye: it looks at its quarry (with small glances), narrows before a shot, flares on it, blinks; dead, it goes dark.
            float glance=.07f*(float)Math.Sin(now*.83f+id)*(float)Math.Sin(now*2.9f);var gaze=Vector3.Normalize(fw+ux*glance+uy*(.05f*(float)Math.Sin(now*1.31f+id*2)));
            var gx=Vector3.Normalize(Vector3.Cross(Math.Abs(gaze.Y)<.95f?Vector3.UnitY:Vector3.UnitX,gaze));var gy=Vector3.Cross(gaze,gx);
            int u=tier==0?20:10,v=tier==0?14:8;k.Ellipsoid(cc,gx*.5f,gy*.5f,gaze*.5f,u,v,NativeFallenKit.Plate);
            float lit=(1-d);if(lit>.05f)k.Torus(cc+gaze*.36f,gaze,.3f*(1-.25f*charge+.12f*fire),.018f*(1+1.2f*fire),tier==0?22:12,4,NativeFallenKit.Light,true);
            float pupil=1-.45f*charge+.35f*fire;k.Ellipsoid(cc+gaze*.42f,gx*(.2f*pupil),gy*(.2f*pupil),gaze*.1f,tier==0?14:8,8,NativeFallenKit.Black,false,false);
            if(lit>.05f)k.Ellipsoid(cc+gaze*.5f,gx*(.05f*(1+fire)),gy*(.05f*(1+fire)),gaze*.025f,8,5,NativeFallenKit.Light,true);
            float blinkAt=(now+id*3.1f)%5.3f;float shut=blinkAt<.22f?(float)Math.Sin(blinkAt/.22f*Math.PI):0;shut=Math.Max(shut,d);shut=Math.Max(shut,.25f*charge);
            Lid(k,cc,gaze,gx,gy,.535f,shut,1,tier);Lid(k,cc,gaze,gx,gy,.535f,shut,-1,tier);
            a.Mouth=cc+gaze*.55f;
        }
        // An eyelid: a shell over half the eye, swung over its front as it shuts.
        private static void Lid(NativeFallenKit k,Vector3 c,Vector3 gaze,Vector3 gx,Vector3 gy,float radius,float shut,int side,int tier)
        {
            // Its edge's latitude from the eye's middle: near the top open, at the middle shut (where it meets the other).
            float edge=(float)(Math.PI*.5*(1-shut))*.9f;int u=tier==0?16:8,v=tier==0?6:4;var rings=new List<Vector3[]>();
            for(int j=0;j<=v;j++)
            {
                float lat=(float)Math.PI*.5f-j/(float)v*((float)Math.PI*.5f-edge);var ring=new Vector3[u];
                for(int i=0;i<u;i++){double th=Math.PI*i/(u-1);var around=gx*(float)Math.Cos(th)+gaze*(float)Math.Sin(th);ring[i]=c+(gy*side*(float)Math.Sin(lat)+around*(float)Math.Cos(lat))*radius;}
                rings.Add(ring);
            }
            // An open shell: faces outward by hand (a loft would close it round).
            int start=k.Count;foreach(var r in rings)foreach(var p in r)k.V(p,NativeFallenKit.SteelDark);
            for(int j=0;j<v;j++)for(int i=0;i+1<u;i++){int a=start+j*u+i,b=a+1,cc=a+u+1,d=a+u;var mid=(k.P[a]+k.P[cc])*.5f;k.Quad(a,b,cc,d,mid-c,false);}
        }

        private static Vector3 Floor(Vector3 p)=>new Vector3(p.X,0,p.Z);
        private static float Hash(int n){uint s=(uint)n*2654435761u;s^=s>>15;s*=2246822519u;s^=s>>13;return (s&0xFFFFFF)/16777216f;}
        private static Vector3 Dir(int n){float a=Hash(n)*6.2831853f;return new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));}
        private static Vector3 Slerp(Vector3 a,Vector3 b,float t){float dot=Math.Max(-1,Math.Min(1,Vector3.Dot(a,b)));if(dot>.9995f)return Vector3.Normalize(Vector3.Lerp(a,b,t));float th=(float)Math.Acos(dot)*t;var rel=Vector3.Normalize(b-a*dot);return a*(float)Math.Cos(th)+rel*(float)Math.Sin(th);}
        /// <summary>After each add's pose: the Cantors' songs and the Thrones' shots, from where they leave them.</summary>
        public void BeamsOf(float now)
        {
            Beams.Clear();
            foreach(var a in Adds)
            {
                if(a.GoneAt>=0||!a.Last.HasTarget)continue;var to=a.Last.Target+Vector3.UnitY*1.1f;
                if(a.Kind==NativeFallenAdds.Cantor&&a.Sing>.02f){float p=Pulse(a,now,5);Beams.Add((a.Mouth,to,.025f+.07f*a.Sing+.05f*p,Math.Min(1,.35f+.65f*a.Sing+.4f*p)));}
                else if(a.Kind==NativeFallenAdds.Throne&&a.Fire>.05f)Beams.Add((a.Mouth,to,.05f+.22f*a.Fire,a.Fire));
            }
        }
        /// <summary>The add's body this frame into `k` (stage frame).</summary>
        public static void Pose(Add a,float now,int tier,NativeFallenKit k)
        {
            if(a.Kind==NativeFallenAdds.Penitent)PenitentPose(a,now,tier,k);else if(a.Kind==NativeFallenAdds.Throne)ThronePose(a,now,tier,k);else CantorPose(a,now,tier,k);
        }
    }
}

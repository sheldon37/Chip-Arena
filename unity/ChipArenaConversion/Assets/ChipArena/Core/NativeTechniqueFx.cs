using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The melee kit's drawer plus what fire and water need (Runtime: NativeTechniquePresentation; Tests record it).</summary>
    public interface ITechniqueFxSink:IKitFxSink
    {
        void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor);
        void Water(Vector3 at,Vector3 lit,int shade,float opacity,float size,Vector3 segment,float u,float seed,float floor);
        void Drop(Vector3 head,Vector3 segment,Vector3 color,float coverage,float width,float u,float seed);
        /// <summary>A flame tongue from tail to head on the weapons drawer.</summary>
        void Tongue(bool view,Vector3 tail,Vector3 head,Vector3 color,float width);
    }

    /// <summary>
    /// The techniques' effects (docs/WEAPON-TECHNIQUES.md), each a pure function of the replicated record and the presentation clock, so
    /// host, guests, spectators and the killcam draw the same frame. Effects quality (Reduced, .3) thins the particles and the low graphics
    /// tier halves them. The solid things (the planted shield, the thrown blade, the sentry) are models, drawn by the runtime. Nothing here
    /// reaches the authority, the network frame, hit timing, hitboxes, damage or movement.
    /// </summary>
    public sealed partial class NativeTechniqueFx
    {
        private const int Mote=0,Core=1,RingGlow=2,Snow=3,Soft=0,Hot=1,RingSprite=2,Sparkle=3;
        /// <summary>How long a record's leavings outlast its end (s).</summary>
        public const float Tail=.9f;
        private static readonly Vector3 Up=Vector3.UnitY,Steel=new Vector3(.5f,.82f,1),Violet=new Vector3(.52f,.22f,1),Mint=new Vector3(.4f,1,.8f),Guard=new Vector3(.18f,.52f,1),GuardCore=new Vector3(.75f,.92f,1),Amber=new Vector3(1,.5f,.1f),Gold=new Vector3(1,.7f,.2f),Red=new Vector3(1,.07f,.05f),
            Cyan=new Vector3(.2f,.9f,1),Wind=new Vector3(.72f,.95f,.88f),Flame=new Vector3(1,.4f,.07f),FlameCore=new Vector3(1,.8f,.35f),Aqua=new Vector3(.2f,.95f,.78f),Shock=new Vector3(.5f,.72f,1),Ink=new Vector3(.03f,.015f,.06f);

        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static float OutCubic(float x){x=1-Clamp01(x);return 1-x*x*x;}
        private static Vector3 N(V3 v)=>new Vector3(v.x,v.y,v.z);
        private static Vector3 Flat(Vector3 v,Vector3 fallback){v.Y=0;return v.LengthSquared()>1e-6f?Vector3.Normalize(v):fallback;}
        private static Vector3 RightOf(Vector3 f)=>new Vector3(f.Z,0,-f.X);
        private static int Count(int full,float q,bool low){float n=full*Math.Max(.35f,Math.Min(1,q));if(low)n*=.5f;return Math.Max(1,(int)Math.Round(n));}
        // A stable random 0..1 per seed and index.
        private static float R(long seed,int i){uint h=unchecked((uint)(seed*2654435761L)+(uint)i*0x9E3779B1u);h^=h>>15;h=unchecked(h*0x2c1b3c6du);h^=h>>12;h=unchecked(h*0x297a2d39u);h^=h>>15;return (h&0xffffff)/16777216f;}
        private static Vector3 RandomDir(long seed,int i){float z=R(seed,i)*2-1,a=R(seed,i+977)*6.2831853f,s=(float)Math.Sqrt(Math.Max(0,1-z*z));return new Vector3(s*(float)Math.Cos(a),z,s*(float)Math.Sin(a));}

        /// <summary>Draws every live technique of the frame at `time`.</summary>
        public void Draw(ITechniqueFxSink s,IKitFxScene scene,float time,float q,bool low)
        {
            if(s==null||scene?.Actors==null||NativeTechniques.Source==null)return;var actors=scene.Actors;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];var k=NativeTechniques.Of(a);if(k?.Kind==null||time<k.At||time>k.End+Tail)continue;long seed=a.Id*7919L+(long)Math.Round(k.At*240);
                switch(k.Kind)
                {
                    case NativeTechniques.Riposte:Riposte(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.SmokeVeil:SmokeVeil(s,k,time,seed,q,low);break;
                    case NativeTechniques.Bastion:Bastion(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.AnchorThrow:Anchor(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.RicochetCoin:Coin(s,k,time,seed,q,low);break;
                    case NativeTechniques.SentryDrop:Sentry(s,k,time,seed,q,low);break;
                    case NativeTechniques.GrappleLine:Grapple(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.GaleArrow:Gale(s,k,time,seed,q,low);break;
                    case NativeTechniques.IgnitionLeap:Ignition(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.HealingSpring:Spring(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.StaticRecall:Recall(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.ShadeSwap:Shade(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.PartingLine:Parting(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.Tripwire:Tripwire(s,k,time,seed,q,low);break;
                    case NativeTechniques.ShieldSurf:Surf(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.IronVow:Vow(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.BountyRound:Bounty(s,scene,k,time,seed,q,low);break;
                    case NativeTechniques.RecoilBurst:Recoil(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.SonarDart:Sonar(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.SpiritHawk:Hawk(s,k,time,seed,q,low);break;
                    case NativeTechniques.WildfireRun:Wildfire(s,scene,a,k,time,seed,q,low);break;
                    case NativeTechniques.Slipstream:Slip(s,k,time,seed,q,low);break;
                    case NativeTechniques.LightningRod:Rod(s,k,time,seed,q,low);break;
                    case NativeTechniques.GraveWalk:Grave(s,scene,a,k,time,seed,q,low);break;
                    default:Third(s,scene,a,k,time,seed,q,low);break;
                }
            }
        }

        // ---- shared pieces ----------------------------------------------------------------------------------------------
        // A flat ring on the floor: its inner and outer soft edges, from corner bands.
        private static void Ring(ITechniqueFxSink s,Vector3 c,float radius,float width,Vector3 color,int segments,float b=1)
        {
            float inner=Math.Max(0,radius-width*.5f),outer=radius+width*.5f;
            for(int i=0;i<segments;i++)
            {
                float a0=6.2831853f*i/segments,a1=6.2831853f*(i+1)/segments;var d0=new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0));var d1=new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1));
                s.Band(false,c+d0*inner,c+d1*inner,c+d1*outer,c+d0*outer,b,b,color);
            }
        }
        private static void Sparks(ITechniqueFxSink s,Vector3 at,float age,float life,int n,float speed,Vector3 color,long seed,float gravity=6,float width=.03f,Vector3 bias=default)
        {
            if(age<0||age>life)return;
            for(int i=0;i<n;i++)
            {
                float own=life*(.55f+.45f*R(seed,i*3+1));if(age>own)continue;float u=age/own,sp=speed*(.45f+.55f*R(seed,i*3+2));var v=Vector3.Normalize(RandomDir(seed,i*5)+bias)*sp;
                float t=age;var head=at+v*t*(1-.35f*u)-Up*(.5f*gravity*t*t);var vel=v-Up*(gravity*t);s.Streak(head,vel,color*(1.6f*(1-u*u)),width,.05f,.03f,.8f,u,seed+i);
            }
        }
        private static void Dust(ITechniqueFxSink s,Vector3 at,float age,float life,int n,float radius,Vector3 albedo,float opacity,long seed)
        {
            if(age<0||age>life)return;float u=age/life;
            for(int i=0;i<n;i++){float a=6.2831853f*(i+R(seed,i))/n,r=radius*OutCubic(u)*(.5f+.5f*R(seed,i+40));var p=at+new Vector3((float)Math.Cos(a)*r,.12f+.35f*u*R(seed,i+80),(float)Math.Sin(a)*r);s.Smoke(p,albedo,opacity*(1-u)*(1-u),(.5f+.9f*u)*(.8f+.5f*R(seed,i+60)),.15f+.6f*u,seed+i,at.Y-.1f);}
        }
        // Radial cracks in the floor, glowing out.
        private static void Cracks(ITechniqueFxSink s,Vector3 at,float age,float life,int n,float reach,Vector3 color,long seed)
        {
            if(age<0||age>life)return;float u=age/life,grow=OutCubic(age/.1f);
            for(int i=0;i<n;i++){float a=6.2831853f*(i+.6f*R(seed,i+7))/n,len=reach*(.45f+.55f*R(seed,i+9))*grow;var d=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));var side=RightOf(d);var bend=at+d*len*.55f+side*(len*.12f*(R(seed,i+11)-.5f));s.Line(false,at+Up*.02f,bend+Up*.02f,color*(1.4f*(1-u)),.035f,true);s.Line(false,bend+Up*.02f,at+d*len+side*(len*.18f*(R(seed,i+13)-.5f))+Up*.02f,color*(.9f*(1-u)),.022f,true);}
        }
        // A hot point of light: a bright core and a soft halo.
        private static void Flash(ITechniqueFxSink s,Vector3 at,float age,float life,Vector3 color,float size,long seed)
        {
            if(age<0||age>life)return;float u=age/life,fade=(1-u)*(1-u);s.Glow(at,color*(1.5f*fade),0,size*(.6f+.5f*u),Core,u,seed);s.Glow(at,(color+Vector3.One)*(1.1f*fade),0,size*.32f,Core,u,seed+1);s.Glow(at,color*(.7f*fade),0,size*(.7f+1.4f*OutCubic(u)),RingGlow,u,seed+2);
        }
        // A jagged bolt, re-rolled a few times a second.
        private static void Bolt(ITechniqueFxSink s,Vector3 from,Vector3 to,Vector3 color,float width,float wander,int segments,long seed)
        {
            var d=to-from;float len=d.Length();if(len<1e-3f)return;var side=Vector3.Normalize(Vector3.Cross(d,Up).LengthSquared()>1e-5f?Vector3.Cross(d,Up):Vector3.UnitX);var up2=Vector3.Normalize(Vector3.Cross(side,d));
            Vector3 last=from;
            for(int i=1;i<=segments;i++)
            {
                float u=i/(float)segments,w=i==segments?0:wander*(float)Math.Sin(3.14159f*u)*Math.Min(1,len*.35f);var p=from+d*u+side*(w*(R(seed,i*2)-.5f))+up2*(w*(R(seed,i*2+1)-.5f));
                s.Line(false,last,p,color,width,false);last=p;
            }
        }

        // ---- Sword: Riposte --------------------------------------------------------------------------------------------
        // The guard: a thin crescent of cold light before the chest, the blade glinting along its length. The parry: a star at the contact,
        // sparks thrown off it, the crescent flaring white, and the counter thrust as a hard line toward the attacker.
        private static void Riposte(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var chest=scene.Chest(a,out var c)?c:feet+Up*1.25f;var at=N(k.Dir);bool hit=k.Hit(t);var f=hit?-Flat(at,Vector3.UnitZ):Flat(N(V3.Forward(a.Yaw)),Vector3.UnitZ);var r=RightOf(f);bool view=scene.Viewer==a.Id;
            float until=hit?k.HitAt:k.Until;float env=Smooth((t-k.At)/.08f)*(1-Smooth((t-until)/.1f));
            if(env>0)
            {
                float breathe=.75f+.25f*(float)Math.Sin(t*30);
                for(int j=0;j<3;j++)GuardArc(s,chest,f,r,-.3f+.3f*j,.72f+.05f*(j==1?1:0),j==1?.035f:.02f,env*breathe*(j==1?1:.6f)*(view?.45f:1),Steel*2.2f,low?8:14);
                bool has=view?scene.ViewBlade(false,out var b0,out var b1):scene.Blade(a,false,out b0,out b1);
                if(has){float run=(t*2.6f)%1;s.Sprite(view,Vector3.Lerp(b0,b1,run),(Steel+Vector3.One)*(2.2f*env),view?.06f:.16f,Sparkle);s.Sprite(view,b1,Steel*(2.4f*env),view?.08f:.22f,Hot);}
            }
            if(!hit)return;float age=t-k.HitAt;var point=N(k.Point);
            Flash(s,point,age,.26f,Steel,.85f,seed);
            s.Sprite(false,point,(Steel+Vector3.One)*(3.2f*(1-Clamp01(age/.14f))),.8f,Sparkle);
            Sparks(s,point,age,.5f,Count(26,q,low),7.5f,Steel+new Vector3(.3f,.2f,0),seed,7,.03f,f*.8f);
            // The arcs flaring out.
            float flare=1-Clamp01(age/.2f);if(flare>0&&!view)for(int j=0;j<3;j++)GuardArc(s,chest,f,r,-.3f+.3f*j,.72f+.45f*(1-flare),.05f*flare+.01f,flare,(Steel+Vector3.One)*1.6f,low?8:14);
            // The counter: a hard thrust line along the way the attack came.
            float thrust=Clamp01((age-.05f)/.09f),gone=1-Clamp01((age-.14f)/.18f);
            if(thrust>0&&gone>0){var start=chest+f*.3f;var end=start+f*(.4f+1.5f*thrust);s.Line(false,start,end,(Steel+Vector3.One)*(2.4f*gone),.07f*gone,true);s.Line(false,start,end,Steel*(1.4f*gone),.2f*gone,false);}
        }

        // A thin arc of light before the chest, faded at its ends.
        private static void GuardArc(ITechniqueFxSink s,Vector3 chest,Vector3 f,Vector3 r,float y,float rad,float width,float b,Vector3 color,int n)
        {
            for(int i=0;i<n;i++)
            {
                float a0=-1.05f+2.1f*i/n,a1=-1.05f+2.1f*(i+1)/n;Vector3 P(float ang,float h)=>chest+f*(rad*(float)Math.Cos(ang))+r*(rad*(float)Math.Sin(ang))+Up*(y+h+.05f*(float)Math.Cos(ang*2));float e0=1-Math.Abs(a0)/1.05f,e1=1-Math.Abs(a1)/1.05f;
                s.Band(false,P(a0,-width),P(a1,-width),P(a1,width),P(a0,width),e0*b,e1*b,color);
            }
        }

        // ---- Daggers: Smoke Veil ---------------------------------------------------------------------------------------
        // The pellet's little arc, the pop (a ring along the floor and quick puffs), then a dome of slow toon smoke that hides what is in it,
        // with violet sparks caught inside and a last thinning before it goes.
        private const float VeilRadius=4;
        private static void SmokeVeil(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var c=N(k.Point);
            if(t<k.Release){float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));var p=Vector3.Lerp(N(k.From),c,u)+Up*(.55f*(float)Math.Sin(3.14159f*u));s.Sprite(false,p,Violet*.7f,.14f,Hot);s.Glow(p,Ink,.9f,.12f,Core,0,seed);return;}
            float age=t-k.Release,life=k.Until-k.Release;if(age>life)return;
            Flash(s,c+Up*.2f,age,.3f,Violet,2.6f,seed);
            float pop=Clamp01(age/.4f);if(pop<1)Ring(s,c+Up*.04f,VeilRadius*OutCubic(pop),.5f,new Vector3(.75f,.65f,.95f)*(1.1f*(1-pop)),low?14:24);
            Dust(s,c,age,.7f,Count(14,q,low),VeilRadius*.8f,new Vector3(.22f,.2f,.26f),.8f,seed);
            float env=Smooth(age/.28f)*(1-Smooth((age-(life-1.1f))/1.1f));if(env<=0)return;float grow=OutCubic(age/.45f);
            int n=Count(34,q,low);
            for(int i=0;i<n;i++)
            {
                float own=(i+.5f)/n,ang=2.39996f*i,rad=(float)Math.Sqrt(own)*VeilRadius*.92f*grow,dome=1-(rad*rad)/(VeilRadius*VeilRadius*1.05f),y=.25f+2.6f*dome*R(seed,i+200);
                float drift=t*.14f;var p=c+new Vector3((float)Math.Cos(ang+drift*(.4f+.3f*R(seed,i)))*rad,y+.12f*(float)Math.Sin(t*.8f+i),(float)Math.Sin(ang+drift*(.4f+.3f*R(seed,i)))*rad);
                float tone=.15f+.11f*R(seed,i+300);var col=new Vector3(tone,tone*.92f,tone*1.14f);s.Smoke(p,col,.76f*env,(2+1.3f*R(seed,i+400))*(.8f+.2f*grow),.14f+.3f*Clamp01(age/life)+.1f*R(seed,i+500),seed+i,c.Y-.1f);
            }
            // Violet sparks caught inside it.
            int m=low?4:9;for(int i=0;i<m;i++){float ph=(t*.5f+R(seed,i+700))%1;float ang=6.2831853f*R(seed,i+710),rad=VeilRadius*.8f*R(seed,i+720);var p=c+new Vector3((float)Math.Cos(ang)*rad,.4f+2.2f*ph,(float)Math.Sin(ang)*rad);float b=(float)Math.Sin(3.14159f*ph);s.Glow(p,Violet*(1.6f*b*env),0,.14f,Core,ph,seed+i);}
        }

        // ---- Shield: Bastion -------------------------------------------------------------------------------------------
        // Where the shield bites the floor: cracks, sparks and dust. The wall opens out of it, a lattice of glass cells with a scan climbing it;
        // a stopped shot ripples through the cells from where it hit; as its health falls it dims; at the end it folds back into the shield.
        public const float WallWidth=2.6f,WallHeight=1.8f;
        private static void Bastion(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var c=N(k.Point);var f=Flat(N(k.Dir),Vector3.UnitZ);var r=RightOf(f);bool view=scene.Viewer==a.Id;
            if(t<k.Release){float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Glow(c+Up*.05f,Guard*(.8f*u),0,1.2f*u,RingGlow,u,seed);return;}
            float age=t-k.Release,life=k.Until-k.Release;
            Cracks(s,c,age,1.1f,low?7:12,1.5f,Guard+new Vector3(.3f,.3f,.2f),seed);Sparks(s,c+Up*.1f,age,.5f,Count(22,q,low),5.5f,GuardCore,seed,8,.03f,Up*.9f);Dust(s,c,age,.9f,Count(10,q,low),1.9f,new Vector3(.3f,.3f,.32f),.7f,seed);Flash(s,c+Up*.2f,age,.26f,Guard,2.2f,seed);
            if(age>life)return;
            float open=OutCubic(age/.24f),fold=Smooth((age-(life-.26f))/.26f),show=open*(1-fold);if(show<=0)return;float health=k.Arm>0&&k.Arm<=1?k.Arm:1;
            int cols=low?7:11,rows=low?4:6;float w=WallWidth*show,h=WallHeight*(.4f+.6f*show),scan=(age*.55f)%1.1f;float rip=k.Hit(t)?t-k.HitAt:-1;var hitAt=N(k.To);
            float size=Math.Min(w/(cols*1.732f),h/(rows*1.5f+.5f));float dim=.5f+.5f*health;
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
            {
                float u=(x+(y%2==0?0:.5f)-(cols-.5f)*.5f)*1.732f*size,v=size+.05f+y*1.5f*size;if(Math.Abs(u)>w*.5f)continue;var mid=c+r*u+Up*v;
                float edge=1-Math.Abs(u)/(w*.5f+1e-3f);float noise=.5f+.5f*(float)Math.Sin(t*2.2f+x*1.7f+y*2.3f);float lit=.3f+.16f*noise+.6f*Math.Max(0,1-Math.Abs(v/h-scan)*5);
                if(rip>=0&&rip<.5f){float dd=Vector3.Distance(mid,hitAt),front=rip*5.5f;lit+=2.4f*Math.Max(0,1-Math.Abs(dd-front)*2.6f)*(1-rip*2);}
                float b=lit*show*dim*(.55f+.45f*edge);if(view)b*=.6f;
                Vector3 last=mid+r*0+Up*(size*.94f);
                for(int e=1;e<=6;e++)
                {
                    float ang=e*1.0471976f;var p=mid+r*((float)Math.Sin(ang)*size*.94f)+Up*((float)Math.Cos(ang)*size*.94f);
                    s.Band(false,mid,mid,p,last,b*.42f,b*.42f,Guard*1.7f);s.Line(false,last,p,(Guard*1.4f+GuardCore*.5f)*(.55f*b+.25f*show*dim),.011f,false);last=p;
                }
            }
            // The soft sheet behind the cells, and the light along its foot.
            s.Glow(c+Up*(h*.5f),Guard*(.5f*show*dim),0,Math.Max(w,h)*1.25f,Core,0,seed+11);
            s.Line(false,c-r*(w*.5f)+Up*.04f,c+r*(w*.5f)+Up*.04f,Guard*(1.6f*show*dim),.05f,false);s.Glow(c+Up*.05f,Guard*(.9f*show*dim),0,1.2f,Core,0,seed+12);
            if(rip>=0){Flash(s,hitAt,rip,.22f,GuardCore,1.1f,seed+1);Sparks(s,hitAt,rip,.3f,Count(10,q,low),4,GuardCore,seed+2,6,.022f,-f*.7f);}
            // The motes it breathes off.
            int m=low?4:10;for(int i=0;i<m;i++){float ph=(t*.4f+R(seed,i+50))%1,u=R(seed,i+60)-.5f;var p=c+r*(u*w)+Up*(.1f+h*ph)-f*(.05f+.1f*ph);s.Glow(p,Guard*(1.1f*(float)Math.Sin(3.14159f*ph)*show),0,.09f,Core,ph,seed+i);}
        }

        // ---- Greatsword: Anchor Throw ----------------------------------------------------------------------------------
        // The blade's spin carves amber crescents along its line. Where it bites: a shock of cracks, sparks and dust, then embers breathing off it,
        // and a thread from the pilot to the blade while the leap is open. The leap's streak and the landing's ring; else its flight back.
        private static void Anchor(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release){var feet0=scene.Feet(a);float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));if(scene.Blade(a,false,out var b0,out var b1)){s.Sprite(false,b1,Amber*(2.4f*u),.2f+.3f*u,Hot);s.Line(false,b0,b1,Amber*(1.2f*u),.05f,false);}s.Glow(feet0+Up*.04f,Amber*(.5f*u),0,1.2f*u,RingGlow,u,seed);return;}
            var feet=scene.Feet(a);var chest=scene.Chest(a,out var c)?c:feet+Up*1.25f;var at=NativeTechniques.BladeAt(k,t,chest,out float flight);var point=N(k.Point);bool flying=t<k.Arm;bool back=!k.Recast(t)&&t>=k.Until-NativeTechniques.BladeReturn&&t<k.Until;
            if(flying||back)
            {
                var dir=flying?Vector3.Normalize(point-N(k.From)):Vector3.Normalize(chest-point);var side=Vector3.Normalize(Vector3.Cross(dir,Up));float spin=t*26;
                // Three crescents the spinning blade has just cut, older ones behind.
                for(int j=0;j<3;j++)
                {
                    float ang=spin-j*.9f,fade=(1-j/3f)*.85f;int n=low?5:8;
                    for(int i=0;i<n;i++)
                    {
                        float a0=ang+1.4f*i/n,a1=ang+1.4f*(i+1)/n;Vector3 P(float an,float rad)=>at-dir*(j*.28f)+side*(rad*(float)Math.Cos(an))+Up*(rad*(float)Math.Sin(an));float b0=fade*(i/(float)n),b1=fade*((i+1)/(float)n);
                        s.Band(false,P(a0,.55f),P(a1,.55f),P(a1,1.05f),P(a0,1.05f),b0,b1,Amber*1.6f);
                    }
                }
                s.Glow(at,Amber*1.6f,0,.9f,Core,0,seed);Sparks(s,at,.08f,.5f,low?3:6,3,Amber,seed+(long)(t*40),3,.025f);
            }
            if(t<k.Arm)return;float age=t-k.Arm;
            Flash(s,point+Up*.4f,age,.32f,Amber,2.8f,seed);Cracks(s,point,age,1.2f,low?8:13,2.2f,Amber,seed);Sparks(s,point+Up*.15f,age,.65f,Count(30,q,low),8,FlameCore,seed,9,.035f,Up);Dust(s,point,age,1,Count(12,q,low),2.4f,new Vector3(.33f,.29f,.25f),.8f,seed);
            float stuck=k.Recast(t)?k.RecastAt:k.Until-NativeTechniques.BladeReturn;
            if(t<stuck)
            {
                float breathe=.6f+.4f*(float)Math.Sin(t*6);s.Glow(point+Up*.65f,Amber*(.9f*breathe),0,1.5f,Core,0,seed);var lean=Flat(N(k.Dir),Vector3.UnitZ);s.Line(false,point,point+Up*1.24f+lean*(-.34f),Amber*(1.8f*breathe),.035f,false);s.Glow(point+Up*.03f,Amber*(1.1f*breathe),0,.9f,RingGlow,0,seed+3);int m=low?3:7;for(int i=0;i<m;i++){float ph=(t*.55f+R(seed,i+30))%1;var p=point+new Vector3(.2f*(R(seed,i+31)-.5f),.1f+1.4f*ph,.2f*(R(seed,i+32)-.5f));s.Glow(p,Amber*(1.4f*(float)Math.Sin(3.14159f*ph)),0,.08f,Core,ph,seed+i);}
                // The thread: a broken line from the pilot to the blade, running toward it.
                var line=point+Up*.8f-chest;int seg=low?10:18;float crawl=(t*2.4f)%1;for(int i=0;i<seg;i++){float u0=(i+crawl)/seg,u1=(i+crawl+.45f)/seg;if(u1>1)continue;float b=.6f*(float)Math.Sin(3.14159f*u0);s.Line(false,chest+line*u0,chest+line*u1,Amber*(1+b),.018f,false);}
            }
            if(k.Recast(t))
            {
                float leap=t-k.RecastAt,dur=Math.Max(.1f,k.Until-k.RecastAt);
                if(leap<dur){var from=feet+Up*.9f;float u=leap/dur;s.Line(false,from,Vector3.Lerp(from,point+Up*.5f,OutCubic(u)),Amber*(2*(1-u)),.08f*(1-u)+.015f,false);}
                float land=t-k.Until;if(land>=0){Flash(s,point+Up*.3f,land,.3f,Amber,3.2f,seed+5);if(land<.5f)Ring(s,point+Up*.04f,2.2f*OutCubic(land/.4f),.45f*(1-land*2),Amber*(1.8f*(1-land*2)),low?14:24);Sparks(s,point+Up*.1f,land,.6f,Count(24,q,low),7,FlameCore,seed+6,8,.035f,Up*.6f);Dust(s,point,land,.9f,Count(10,q,low),2.8f,new Vector3(.33f,.29f,.25f),.7f,seed+7);}
            }
        }

        // ---- Pistol: Ricochet Coin -------------------------------------------------------------------------------------
        // A real little disc turning over and over, flashing when it faces you, a thin gold thread behind. The bullet's hit: a star, and a hard
        // gold tracer snapping off to whoever it found.
        private static void Coin(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release)return;bool hit=k.Hit(t);float end=hit?k.HitAt:k.Until;
            if(t<end)
            {
                var p=NativeTechniques.CoinAt(k,t);var f=Flat(N(k.Dir),Vector3.UnitZ);var r=RightOf(f);float spin=(t-k.Release)*44,c=(float)Math.Cos(spin),sn=(float)Math.Sin(spin);const float size=.075f;var v=f*(c*size)+Up*(sn*size);
                float gleam=(float)Math.Pow(Math.Abs(sn),8);var col=Gold*(1.6f+3.5f*gleam);int n=low?5:8;
                for(int i=0;i<n;i++){float a0=6.2831853f*i/n,a1=6.2831853f*(i+1)/n;var e0=r*(size*(float)Math.Cos(a0))+v*(float)Math.Sin(a0);var e1=r*(size*(float)Math.Cos(a1))+v*(float)Math.Sin(a1);s.Band(false,p,p,p+e1,p+e0,1,1,col);}
                s.Glow(p,Gold*(.35f+1.1f*gleam),0,.12f+.16f*gleam,Core,0,seed);if(gleam>.5f)s.Sprite(false,p,(Gold+Vector3.One)*(2.2f*gleam),.32f,Sparkle);
                int m=low?4:9;for(int i=1;i<=m;i++){float back=t-i*.024f;if(back<k.Release)break;var q0=NativeTechniques.CoinAt(k,back);var q1=NativeTechniques.CoinAt(k,back+.024f);s.Line(false,q0,q1,Gold*(.9f*(1-i/(float)(m+1))),.012f,false);}
            }
            if(!hit)return;float age=t-k.HitAt;var at=N(k.Point);var to=N(k.To);
            Flash(s,at,age,.22f,Gold,.9f,seed);if(age<.12f)s.Sprite(false,at,(Gold+Vector3.One)*3.2f,.7f,Sparkle);Sparks(s,at,age,.4f,Count(18,q,low),6,Gold,seed,6,.024f);
            float line=1-Clamp01(age/.26f);if(line>0){float head=Clamp01(age/.05f);var tip=Vector3.Lerp(at,to,head);s.Line(false,at,tip,(Gold+Vector3.One)*(2.6f*line),.05f*line+.008f,true);s.Line(false,at,tip,Gold*(1.2f*line),.16f*line,false);}
            float land=age-.05f;if(land>=0){Flash(s,to,land,.24f,Gold,1.2f,seed+3);Sparks(s,to,land,.4f,Count(14,q,low),5,Gold,seed+4,7,.024f);}
        }

        // ---- Machine gun: Sentry Drop ----------------------------------------------------------------------------------
        // Deploy dust, a thin red laser sweeping to its target, a hard muzzle flash and a tracer for each round, spent casings, its status light,
        // and sparks and a wisp of smoke when it powers down.
        public const float SentryInterval=.5f;
        public static Vector3 SentryMuzzle(NativeTechnique k)=>N(k.Point)+Up*.86f+Flat(N(k.Dir),Vector3.UnitZ)*.72f;
        private static void Sentry(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release)return;float age=t-k.Release;var c=N(k.Point);Dust(s,c,age,.7f,Count(8,q,low),1.1f,new Vector3(.3f,.3f,.3f),.6f,seed);Sparks(s,c+Up*.1f,age,.3f,Count(10,q,low),3.5f,Gold,seed,8,.02f,Up);
            var muzzle=SentryMuzzle(k);var to=N(k.To);float armed=t-k.Arm;
            if(t<k.Until)
            {
                // The status light: amber arming, red armed.
                float pulse=.6f+.4f*(float)Math.Sin(t*(armed<0?22:7));s.Glow(c+Up*.98f-Flat(N(k.Dir),Vector3.UnitZ)*.1f,(armed<0?Gold:Red)*(2.6f*pulse),0,.2f,Core,0,seed);
                if(armed>=0)
                {
                    // The laser, its dot at the target.
                    var swing=to+new Vector3(.25f*(float)Math.Sin(t*2.1f),.12f*(float)Math.Sin(t*3.3f),.25f*(float)Math.Cos(t*1.7f));float settle=OutCubic(armed/.3f);var aim=Vector3.Lerp(muzzle+Flat(N(k.Dir),Vector3.UnitZ)*2,swing,settle);
                    s.Line(false,muzzle,aim,Red*(1.6f*settle),.018f,false);s.Glow(aim,Red*(2.4f*settle),0,.24f,Core,0,seed+1);
                    int shot=(int)(armed/SentryInterval);float since=armed-shot*SentryInterval;long ss=seed+shot*31;
                    if(since<.07f){float u=since/.07f;s.Sprite(false,muzzle,(FlameCore+Vector3.One*.3f)*(3.5f*(1-u)),.34f,Hot);s.Glow(muzzle,Flame*(2.5f*(1-u)),0,.6f,Core,u,ss);for(int i=0;i<3;i++){var d=Vector3.Normalize(aim-muzzle+RandomDir(ss,i)*.5f);s.Line(false,muzzle,muzzle+d*(.5f*(1-u)),FlameCore*(2.6f*(1-u)),.05f*(1-u),true);}}
                    if(since<.1f){float u=since/.1f;var head=Vector3.Lerp(muzzle,swing,Math.Min(1,u*1.6f));var tail=Vector3.Lerp(muzzle,swing,Math.Max(0,u*1.6f-.35f));s.Line(false,tail,head,FlameCore*2.4f,.03f,true);}
                    Sparks(s,swing,since-.05f,.3f,low?3:6,3.5f,FlameCore,ss,7,.02f);
                    // The casing, thrown off the side.
                    if(since<.4f){var side=RightOf(Flat(N(k.Dir),Vector3.UnitZ));var p=muzzle-Flat(N(k.Dir),Vector3.UnitZ)*.3f+side*(.9f*since)+Up*(1.5f*since-4.9f*since*since);s.Line(false,p,p+side*.03f+Up*.02f,Gold*1.1f,.014f,false);}
                }
            }
            else{float off=t-k.Until;Sparks(s,c+Up*.6f,off,.5f,Count(12,q,low),3,Gold,seed+9,8,.02f);if(off<Tail)s.Smoke(c+Up*(.6f+off*.8f),new Vector3(.2f,.2f,.22f),.5f*(1-off/Tail),.5f+off*.6f,.2f+.5f*off,seed+10,c.Y);}
        }

        // ---- Rifle: Grapple Line ---------------------------------------------------------------------------------------
        // The hook leaves on a whipping line. It bites with a spark and a ring; the line snaps taut and hums; speed lines stream past the pilot as
        // it reels; at the end the line lets go and flicks away.
        private static void Grapple(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release)return;var feet=scene.Feet(a);var chest=scene.Chest(a,out var c)?c:feet+Up*1.25f;var anchor=N(k.Point);var f=Flat(anchor-chest,Vector3.UnitZ);var hand=chest+f*.35f+RightOf(f)*-.22f-Up*.1f;
            float fly=Clamp01((t-k.Release)/Math.Max(.03f,k.Arm-k.Release));bool bit=t>=k.Arm;float end=t-k.Until;if(end>.18f)return;float gone=Clamp01(end/.18f);
            var head=bit?anchor:Vector3.Lerp(hand,anchor,fly);var line=head-hand;float len=line.Length();if(len>.01f)
            {
                var dir=line/len;var side=Vector3.Normalize(Vector3.Cross(dir,Up).LengthSquared()>1e-5f?Vector3.Cross(dir,Up):Vector3.UnitX);var lift=Vector3.Normalize(Vector3.Cross(side,dir));
                float whip=bit?(float)Math.Exp(-(t-k.Arm)*9)*.5f:.32f*(1-fly*.5f),hum=bit&&end<0?.012f*(float)Math.Sin(t*90):0;int n=low?10:20;Vector3 last=hand;float reach=1-gone;
                for(int i=1;i<=n;i++)
                {
                    float u=i/(float)n;if(u>reach)break;float env=(float)Math.Sin(3.14159f*u);var p=hand+line*u+(side*((float)Math.Sin(u*9-t*34)*whip)+lift*((float)Math.Cos(u*6-t*27)*whip*.6f+hum))*env+(end>0?Up*(-1.4f*gone*gone*u):Vector3.Zero);
                    s.Line(false,last,p,Cyan*(1.9f*(1-gone)),.026f,false);last=p;
                }
                s.Line(false,hand,bit?anchor:head,Cyan*(.35f*(1-gone)),.05f,false);
            }
            // The hook.
            s.Sprite(false,head,Cyan*2.4f,.2f,Hot);s.Glow(head,Cyan*1.2f,0,.45f,Core,0,seed);
            if(!bit)return;float age=t-k.Arm;Flash(s,anchor,age,.24f,Cyan,1.6f,seed);Sparks(s,anchor,age,.45f,Count(18,q,low),6,Cyan+new Vector3(.3f,.1f,0),seed,6,.024f);
            float ringU=Clamp01(age/.3f);if(ringU<1)s.Glow(anchor,Cyan*(1.6f*(1-ringU)),0,.4f+2*ringU,RingGlow,ringU,seed+1);
            // Reeling: speed lines around the pilot, sparks off the hand.
            if(end<0)
            {
                int m=Count(12,q,low);for(int i=0;i<m;i++){float ph=(t*2.6f+R(seed,i+80))%1;float ang=6.2831853f*R(seed,i+81),rad=.5f+.6f*R(seed,i+82);var side=RightOf(f);var off=side*((float)Math.Cos(ang)*rad)+Up*((float)Math.Sin(ang)*rad);var p=chest+off+f*(1.6f-3.2f*ph);s.Line(false,p,p-f*(.5f+.4f*R(seed,i+83)),new Vector3(.75f,.95f,1)*(.8f*(float)Math.Sin(3.14159f*ph)),.012f,false);}
                Sparks(s,hand,(t*11)%.3f,.3f,low?2:4,2.5f,Cyan,seed+(long)(t*11),5,.016f);
            }
        }

        // ---- Bow: Gale Arrow -------------------------------------------------------------------------------------------
        // The arrow spins two pale ribbons of wind round its line. The burst: rings racing out along the floor, a swirl climbing out of the middle,
        // dust and bits thrown flat and fast.
        public const float GaleRadius=5;
        private static void Gale(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.At)return;var from=N(k.From);var to=N(k.Point);
            if(t<k.Release){float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));int m=low?5:10;for(int i=0;i<m;i++){float ang=6.2831853f*(i/(float)m)+t*7,rad=.5f*(1-u)+.08f;var p=from+new Vector3((float)Math.Cos(ang)*rad,(float)Math.Sin(ang*1.3f)*rad*.5f,(float)Math.Sin(ang)*rad);s.Glow(p,Wind*(1.2f*u),0,.08f,Core,u,seed+i);}return;}
            if(t<k.Arm)
            {
                float u=Clamp01((t-k.Release)/Math.Max(.03f,k.Arm-k.Release));var at=Vector3.Lerp(from,to,u);var dir=Vector3.Normalize(to-from);var side=Vector3.Normalize(Vector3.Cross(dir,Up).LengthSquared()>1e-5f?Vector3.Cross(dir,Up):Vector3.UnitX);var lift=Vector3.Normalize(Vector3.Cross(side,dir));
                s.Line(false,at-dir*.6f,at,Wind*2,.03f,true);s.Glow(at,Wind*1.2f,0,.45f,Core,u,seed);int n=low?9:18;float reach=Math.Min(Vector3.Distance(at,from),3);
                for(int j=0;j<2;j++)for(int i=0;i<n;i++)
                {
                    float u0=i/(float)n,u1=(i+1)/(float)n,turn=j*3.14159f-t*30;Vector3 P(float w)=>at-dir*(w*reach)+(side*(float)Math.Cos(turn+w*11)+lift*(float)Math.Sin(turn+w*11))*(.1f+.26f*w);float w0=.09f*(1-u0),w1=.09f*(1-u1);
                    s.Band(false,P(u0)-lift*w0,P(u1)-lift*w1,P(u1)+lift*w1,P(u0)+lift*w0,(1-u0)*.9f,(1-u1)*.9f,Wind*1.25f);
                }
                return;
            }
            float age=t-k.Arm;var c=to;Flash(s,c+Up*.3f,age,.3f,Wind,3.4f,seed);
            // Rings racing out along the floor, each a little later.
            for(int j=0;j<3;j++){float u=Clamp01((age-j*.07f)/.42f);if(u<=0||u>=1)continue;Ring(s,c+Up*(.05f+.05f*j),GaleRadius*OutCubic(u)*(1-.1f*j),.8f*(1-u)+.12f,Wind*(2.4f*(1-u)),low?16:30);}
            // A swirl climbing from the middle.
            float sw=Clamp01(age/.7f);if(sw<1){int n=low?12:24;for(int j=0;j<3;j++){Vector3 last=c;for(int i=1;i<=n;i++){float w=i/(float)n,ang=j*2.094f+w*7+age*9,rad=(.25f+2.2f*w)*OutCubic(sw);var p=c+new Vector3((float)Math.Cos(ang)*rad,w*3.4f*OutCubic(sw),(float)Math.Sin(ang)*rad);s.Line(false,last,p,Wind*(2.2f*(1-sw)*(1-w*.6f)),.09f*(1-w*.5f),false);last=p;}}}
            // Dust and bits blown flat and fast.
            Dust(s,c,age,.9f,Count(20,q,low),GaleRadius*.95f,new Vector3(.5f,.5f,.46f),.85f,seed);
            if(age<.7f){int m=Count(40,q,low);float u=age/.7f;for(int i=0;i<m;i++){float ang=6.2831853f*R(seed,i+200),sp=GaleRadius*(.55f+.65f*R(seed,i+201));float d=sp*OutCubic(u);var dirF=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var p=c+dirF*d+Up*(.15f+1.1f*R(seed,i+202)*(float)Math.Sin(3.14159f*u));s.Streak(p,dirF*(sp*4*(1-u))+Up*.3f,Wind*(2*(1-u)),.034f,.12f,.06f,.8f,u,seed+i);}}
        }

        // ---- Fire: Ignition Leap ---------------------------------------------------------------------------------------
        // Heat gathers at the feet; it goes off: a pillar of fire and tongues licking up, a burning ring, sparks fountaining. Wings of ember stream off
        // the pilot in the air; the scorched circle smoulders where it left.
        public const float LeapBurn=3;
        private static void Ignition(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var c=t<k.Release?feet:N(k.Point);if(t>=k.Release&&c==default)c=feet;
            if(t<k.Release){float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Glow(feet+Up*.06f,Flame*(1.2f*u),0,1.6f*u,RingGlow,u,seed);s.Glow(feet+Up*.1f,FlameCore*(1.4f*u),0,1.2f*u,Core,u,seed+1);Sparks(s,feet,(t-k.At),.2f,low?4:8,1.5f,FlameCore,seed,-3,.02f);return;}
            float age=t-k.Release;var floor=c.Y-.05f;
            Flash(s,c+Up*.6f,age,.22f,Flame,2.4f,seed);
            if(age<.7f)
            {
                float u=age/.7f;int n=Count(14,q,low);
                for(int i=0;i<n;i++){float w=i/(float)n;float h=.2f+4.2f*w*OutCubic(u*1.5f);float wob=.5f*(1-w)*(R(seed,i+1)-.5f);var p=c+new Vector3(wob,h,wob*(R(seed,i+2)-.5f)*2);s.Fire(p,new Vector3(1,.34f,.07f),(1-u*u)*(.85f+.15f*(1-w)),(1.5f-.8f*w)*(.75f+.6f*u),.04f+.3f*u,seed+i,floor);}
                int m=low?4:9;for(int i=0;i<m;i++){float ang=6.2831853f*(i/(float)m+R(seed,i+30)*.1f),rad=.3f+1.2f*OutCubic(u);var base0=c+new Vector3((float)Math.Cos(ang)*rad,0,(float)Math.Sin(ang)*rad);s.Tongue(false,base0,base0+Up*(1.2f+1.6f*R(seed,i+31))*(1-u)+new Vector3(0,.2f,0),Flame*(1.5f*(1-u)),.2f*(1-u*.6f));}
            }
            if(age<.55f){float u=age/.55f;Ring(s,c+Up*.05f,LeapBurn*OutCubic(u),.6f*(1-u)+.1f,Flame*(2*(1-u)),low?16:28);}
            Sparks(s,c+Up*.2f,age,1.1f,Count(46,q,low),8.5f,FlameCore,seed,7,.03f,Up*1.3f);
            // The scorched circle smoulders.
            float scorch=(1-Smooth((age-.5f)/1.1f))*Smooth(age/.25f);if(scorch>0){int m=low?5:12;for(int i=0;i<m;i++){float ang=6.2831853f*R(seed,i+60),rad=LeapBurn*.85f*(float)Math.Sqrt(R(seed,i+61));var p=c+new Vector3((float)Math.Cos(ang)*rad,.06f,(float)Math.Sin(ang)*rad);float fl=.6f+.4f*(float)Math.Sin(t*11+i*2);s.Glow(p,Flame*(1.3f*fl*scorch),0,.32f+.28f*R(seed,i+62),Core,0,seed+i);if(i%3==0&&!low)s.Smoke(p+Up*(.3f+(age*.5f)),new Vector3(.12f,.11f,.1f),.45f*scorch,.5f+.5f*age,.25f+.3f*age,seed+i,floor);}}
            // Wings of ember off the pilot in the air.
            if(age<k.Until-k.Release&&scene.Viewer!=a.Id)
            {
                var chest=scene.Chest(a,out var ch)?ch:feet+Up*1.25f;var f=Flat(N(k.Dir),Vector3.UnitZ);var r=RightOf(f);float env=Smooth(age/.12f)*(1-Smooth((age-.75f)/.5f));
                if(env>0)for(int side=-1;side<=1;side+=2){int n=low?5:12;for(int i=0;i<n;i++){float u0=i/(float)n,u1=(i+1)/(float)n;Vector3 P(float w,float dn)=>chest+r*(side*(.12f+.85f*w))-f*(.2f+.75f*w)+Up*(.2f+(.75f+.12f*(float)Math.Sin(age*14))*w-dn*(1-w*.6f));s.Band(false,P(u0,.0f),P(u1,.0f),P(u1,.5f*(1-u1)+.03f),P(u0,.5f*(1-u0)+.03f),(1-u0*u0)*env,(1-u1*u1)*env,Flame*1.15f);}}
                Sparks(s,feet+Up*.4f,(t*6)%.4f,.4f,low?3:6,2,FlameCore,seed+(long)(t*6),5,.022f,-Up);
            }
        }

        // ---- Water: Healing Spring -------------------------------------------------------------------------------------
        // The cast's arc of water; the splash where it lands. A pool that stays: its bright rim, ripples opening from the middle, caustic glints across
        // it, droplets rising and falling, and green motes lifting off anyone standing in it.
        public const float SpringRadius=4;
        private static void Spring(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var c=N(k.Point);var from=N(k.From);const int Shade=0x0a5f7a;var lit=new Vector3(.45f,.95f,1);float floor=c.Y-.05f;
            if(t<k.Release)
            {
                float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));int n=low?4:8;for(int i=0;i<n;i++){float w=Clamp01(u*1.25f-i*.04f);var p=Vector3.Lerp(from,c,w)+Up*(1.2f*(float)Math.Sin(3.14159f*w));s.Water(p,lit,Shade,.8f,.26f-.012f*i,(c-from)*.03f,.2f,seed+i,floor);}return;
            }
            float age=t-k.Release,life=k.Until-k.Release;
            Sparks(s,c+Up*.1f,age,.5f,Count(16,q,low),4,lit,seed,9,.02f,Up);
            if(age<.6f){int n=Count(12,q,low);float u=age/.6f;for(int i=0;i<n;i++){var d=RandomDir(seed,i+90);d.Y=Math.Abs(d.Y)+.6f;var v=d*(3+1.5f*R(seed,i+91));var p=c+v*age-Up*(4.9f*age*age);if(p.Y<c.Y)continue;s.Drop(p,v*.05f-Up*(9.8f*age*.05f),lit*1.2f,.9f,.05f,u,seed+i);}}
            if(age>life)return;float env=OutCubic(age/.35f)*(1-Smooth((age-(life-.6f))/.6f));if(env<=0)return;float R0=SpringRadius*OutCubic(age/.5f);
            // The water's sheet: soft rings filling it, the rim bright.
            int seg=low?18:36;Ring(s,c+Up*.03f,R0*.5f,R0,Aqua*(.3f*env),seg);Ring(s,c+Up*.05f,R0,.26f,Aqua*(1.5f*env),seg);Ring(s,c+Up*.045f,R0-.32f,.12f,lit*(.7f*env),seg);
            // Ripples from the middle.
            for(int j=0;j<3;j++){float ph=(age*.42f+j/3f)%1;Ring(s,c+Up*.06f,R0*ph,.16f*(1-ph)+.03f,lit*(.9f*(1-ph)*env),low?18:40);}
            // Caustic glints drifting across it.
            int g=Count(26,q,low);for(int i=0;i<g;i++){float ang=6.2831853f*R(seed,i+120)+t*.2f*(R(seed,i+121)-.5f),rad=R0*.95f*(float)Math.Sqrt(R(seed,i+122)),tw=(float)Math.Pow(Math.Abs(Math.Sin(t*(1.6f+R(seed,i+123))+i)),6);var p=c+new Vector3((float)Math.Cos(ang)*rad,.08f,(float)Math.Sin(ang)*rad);s.Glow(p,lit*(1.8f*tw*env),0,.12f+.1f*R(seed,i+124),Core,0,seed+i);}
            // Droplets lifting from the water and falling back.
            int d2=Count(16,q,low);for(int i=0;i<d2;i++){float dur=.9f+.6f*R(seed,i+150),ph=(t/dur+R(seed,i+151))%1,ang=6.2831853f*R(seed,i+152),rad=R0*.8f*(float)Math.Sqrt(R(seed,i+153)),vy=2.6f+1.2f*R(seed,i+154),tt=ph*dur;float y=vy*tt-4.9f*tt*tt;if(y<0)continue;var p=c+new Vector3((float)Math.Cos(ang)*rad,.06f+y,(float)Math.Sin(ang)*rad);s.Drop(p,Up*((vy-9.8f*tt)*.04f),lit*1.1f,.85f*env,.04f,ph,seed+i);}
            // Green motes rising through the column, more over anyone it is healing.
            int m=Count(24,q,low);for(int i=0;i<m;i++){float ph=(t*.32f+R(seed,i+180))%1,ang=6.2831853f*R(seed,i+181)+ph*1.5f,rad=R0*.85f*(float)Math.Sqrt(R(seed,i+182));var p=c+new Vector3((float)Math.Cos(ang)*rad,.1f+2.6f*ph,(float)Math.Sin(ang)*rad);s.Glow(p,new Vector3(.3f,1,.55f)*(1.5f*(float)Math.Sin(3.14159f*ph)*env),0,.09f+.05f*R(seed,i+183),Core,ph,seed+i);}
            var actors=scene.Actors;for(int i=0;i<actors.Count;i++)
            {
                var o=actors[i];if(o==null||!o.Alive)continue;var pf=scene.Feet(o);if(Vector3.DistanceSquared(new Vector3(pf.X,c.Y,pf.Z),c)>R0*R0)continue;
                int n=low?3:7;for(int j=0;j<n;j++){float ph=(t*.8f+R(seed,j+230+i*11))%1,ang=6.2831853f*R(seed,j+231)+ph*3;var p=pf+new Vector3((float)Math.Cos(ang)*.4f,.1f+1.9f*ph,(float)Math.Sin(ang)*.4f);s.Glow(p,new Vector3(.4f,1,.6f)*(1.9f*(float)Math.Sin(3.14159f*ph)*env),0,.12f,Core,ph,seed+j);}
            }
        }

        // ---- Lightning: Static Recall ----------------------------------------------------------------------------------
        // The anchor: a burnt ring with arcs crawling up a body-tall column (the pilot's ghost stands in it, drawn by the runtime) and a thin live thread
        // to the pilot. The recast: a hard bolt from where the pilot was to the anchor, branching, both ends flashing.
        private static void Recall(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var anchor=N(k.Point);if(t<k.Release)return;float age=t-k.Release;var feet=scene.Feet(a);var chest=scene.Chest(a,out var c)?c:feet+Up*1.25f;bool bolt=k.Recast(t);
            Flash(s,anchor+Up*.9f,age,.22f,Shock,2.2f,seed);
            if(!bolt&&t<k.Until)
            {
                float env=Smooth(age/.15f),jit=(float)Math.Floor(t*16);long js=seed+(long)jit*17;Ring(s,anchor+Up*.04f,.75f+.04f*(float)Math.Sin(t*30),.1f,Shock*(1.5f*env),low?12:22);Ring(s,anchor+Up*.04f,.4f,.5f,Shock*(.25f*env),low?10:18);
                int n=low?3:6;for(int i=0;i<n;i++){float ang=6.2831853f*R(js,i),rad=.42f+.25f*R(js,i+9);var bottom=anchor+new Vector3((float)Math.Cos(ang)*.75f,.05f,(float)Math.Sin(ang)*.75f);var top=anchor+new Vector3((float)Math.Cos(ang+1.2f)*rad,.7f+1.1f*R(js,i+18),(float)Math.Sin(ang+1.2f)*rad);Bolt(s,bottom,top,(Shock+Vector3.One*.4f)*1.9f,.016f,.3f,low?4:7,js+i);}
                int m=low?3:7;for(int i=0;i<m;i++){float ph=(t*.9f+R(seed,i+40))%1,ang=6.2831853f*R(seed,i+41),rad=.25f+.35f*R(seed,i+42);var p=anchor+new Vector3((float)Math.Cos(ang)*rad,.1f+1.7f*ph,(float)Math.Sin(ang)*rad);s.Glow(p,Shock*(1.5f*(float)Math.Sin(3.14159f*ph)),0,.08f,Core,ph,seed+i);}
                // The thread back to the pilot: thin and alive, flickering.
                float flick=R(js,99)>.28f?1:.25f;Bolt(s,anchor+Up*1,chest,Shock*(.9f*flick*env),.008f,.4f,low?6:12,js+70);
                // Warns of its end: it stutters in its last half second.
                return;
            }
            if(!bolt)return;float since=t-k.RecastAt;var from=N(k.From)+Up*1;var to=anchor+Up*1;
            Flash(s,from,since,.26f,Shock,2.4f,seed+1);Flash(s,to,since,.3f,Shock,3,seed+2);Sparks(s,to,since,.5f,Count(22,q,low),6,Shock+new Vector3(.3f,.2f,0),seed+3,6,.024f);Sparks(s,from,since,.4f,Count(12,q,low),4,Shock,seed+4,6,.02f);
            float fade=1-Clamp01(since/.28f);if(fade>0)
            {
                long bs=seed+(long)(since*30)*13;Bolt(s,from,to,(Shock+Vector3.One)*(3*fade),.06f*fade+.01f,.8f,low?7:14,bs);Bolt(s,from,to,Shock*(1.6f*fade),.14f*fade,.8f,low?7:14,bs);
                int bn=low?1:3;for(int i=0;i<bn;i++){float u=.2f+.6f*R(bs,i+5);var o=Vector3.Lerp(from,to,u);var end=o+RandomDir(bs,i+7)*(1+1.4f*R(bs,i+8));Bolt(s,o,end,Shock*(1.6f*fade),.03f*fade,.5f,5,bs+i+20);}
            }
            if(since<.3f)Ring(s,anchor+Up*.05f,.6f+2*since*3,.3f*(1-since/.3f),Shock*(1.6f*(1-since/.3f)),low?12:22);
        }

        // ---- Darkness: Shade Swap --------------------------------------------------------------------------------------
        // A slow knot of black with a violet rim and ribbons of ink streaming behind it. On a hit: an ink burst at both ends, and two ribbons arcing past each
        // other as the bodies trade places.
        private static void Shade(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var to=N(k.Point);
            if(t<k.Release){float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Glow(from,Ink,.9f*u,.3f*u,Core,0,seed);s.Sprite(false,from,Violet*(2*u),.22f*u+.05f,RingSprite);return;}
            bool hit=k.Hit(t);float flyEnd=hit?k.HitAt:k.Until;
            if(t<flyEnd)
            {
                float u=Clamp01((t-k.Release)/Math.Max(.05f,(hit||k.HitAt>-50?k.HitAt:k.Until)-k.Release));var at=Vector3.Lerp(from,to,u);var dir=Vector3.Normalize(to-from);var side=Vector3.Normalize(Vector3.Cross(dir,Up).LengthSquared()>1e-5f?Vector3.Cross(dir,Up):Vector3.UnitX);var lift=Vector3.Normalize(Vector3.Cross(side,dir));
                s.Glow(at,Ink,.96f,.8f,Core,0,seed);s.Glow(at,Violet*2.1f,0,1.15f,RingGlow,0,seed+1);s.Sprite(false,at,Violet*2.2f,.3f,RingSprite);s.Glow(at,Violet*.8f,0,1.2f,Core,0,seed+2);
                int rb=low?3:5,seg=low?8:14;float reach=Math.Min(Vector3.Distance(at,from),2.6f);
                for(int j=0;j<rb;j++)for(int i=0;i<seg;i++)
                {
                    float u0=i/(float)seg,u1=(i+1)/(float)seg,base0=j*6.2831853f/rb-t*7;Vector3 P(float w)=>at-dir*(w*reach)+(side*(float)Math.Cos(base0+w*5)+lift*(float)Math.Sin(base0+w*5))*(.1f+.34f*w);float w0=.07f*(1-u0),w1=.07f*(1-u1);
                    s.Band(false,P(u0)-lift*w0,P(u1)-lift*w1,P(u1)+lift*w1,P(u0)+lift*w0,(1-u0)*.9f,(1-u1)*.9f,Violet*.95f);
                }
                int m=low?3:7;for(int i=0;i<m;i++){float ph=(t*2+R(seed,i+60))%1;var p=at-dir*(ph*reach*1.2f)+RandomDir(seed,i+61)*(.4f*ph);s.Smoke(p,Ink*3,.5f*(1-ph),.35f+.3f*ph,.25f+.4f*ph,seed+i,from.Y-5);}
            }
            if(!hit)return;float age=t-k.HitAt;var feet=scene.Feet(a);var mine=feet+Up*1;var theirs=to;
            Flash(s,mine,age,.2f,Violet,1.3f,seed+3);Flash(s,theirs,age,.2f,Violet,1.3f,seed+4);Sparks(s,theirs,age,.45f,Count(16,q,low),5,Violet+Vector3.One*.2f,seed+5,3,.024f);Sparks(s,mine,age,.45f,Count(16,q,low),5,Violet+Vector3.One*.2f,seed+6,3,.024f);
            for(int e=0;e<2;e++){var c=e==0?mine:theirs;float u=Clamp01(age/.4f);if(u<1)s.Glow(c,Violet*(1.5f*(1-u)),0,1.1f+.9f*u,RingGlow,u,seed+7+e);int n=Count(7,q,low);for(int i=0;i<n;i++){var d=RandomDir(seed,i*3+e*40);s.Smoke(c+d*(1.1f*OutCubic(age/.35f)),Ink*3,.75f*(1-Clamp01(age/.55f)),.5f+.4f*age,.2f+.8f*age,seed+i,c.Y-5);}}
            // The two arcs: each body's way across, passing above and below.
            float span=Math.Max(.1f,k.Until-k.HitAt),h=Clamp01(age/span),off=1-Clamp01((age-span)/.2f);if(off>0)
            {
                var line=theirs-mine;var side=Vector3.Normalize(Vector3.Cross(line,Up).LengthSquared()>1e-5f?Vector3.Cross(line,Up):Vector3.UnitX);int n=low?10:20;
                for(int e=0;e<2;e++)
                {
                    float sign=e==0?1:-1;Vector3 Arc(float w){float sinw=(float)Math.Sin(3.14159f*w);return (e==0?mine+line*w:theirs-line*w)+Up*(sign*.5f*sinw+1*sinw)+side*(sign*1.5f*sinw);}
                    for(int i=0;i<n;i++){float u0=i/(float)n,u1=(i+1)/(float)n;if(u1>h)break;var p0=Arc(u0);var p1=Arc(u1);float fade=off*(.25f+.75f*u1/Math.Max(.01f,h));s.Band(false,p0-Up*.1f,p1-Up*.1f,p1+Up*.1f,p0+Up*.1f,fade,fade,Violet*1.5f);}
                }
            }
        }
    }
}

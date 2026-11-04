using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The second set's effects (docs/WEAPON-TECHNIQUES.md), on the same terms as the first: pure functions of the replicated record and the clock,
    // thinned by Reduced and the low tier, reaching nothing but the drawer.
    public sealed partial class NativeTechniqueFx
    {
        private static readonly Vector3 CutLight=new Vector3(.62f,.86f,1),WireLight=new Vector3(.82f,1,.38f),Brass=new Vector3(1,.72f,.28f),PingLight=new Vector3(.3f,1,.66f),HawkLight=new Vector3(.5f,1,.92f),Current=new Vector3(.22f,.75f,1),RodLight=new Vector3(.66f,.56f,1),GraveLight=new Vector3(.42f,.2f,1),SurfLight=new Vector3(.35f,.7f,1);
        /// <summary>How far either side of Parting Line its burst reaches; Iron Vow's shove; Lightning Rod's height and reach; Sonar Dart's reach and how often it pings.</summary>
        public const float CutReach=1.2f,VowPush=3.5f,RodHeight=1.75f,RodReach=3,SonarReach=12,SonarEvery=1.2f,GraveDeep=2.3f;

        private static Actor ActorOf(IKitFxScene scene,int id){if(id<=0)return null;var l=scene.Actors;for(int i=0;i<l.Count;i++)if(l[i]!=null&&l[i].Id==id)return l[i];return null;}
        private static float Pulse(float t,float rate,int power){return (float)Math.Pow(.5f+.5f*Math.Sin(t*rate),power);}

        // ---- Sword: Parting Line ---------------------------------------------------------------------------------------
        // A hair of white hanging where the pilot stood, tightening and trembling, the floor below lit where it will cut; then it bursts in two crescents thrown apart.
        private static void Parting(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var c=N(k.Point);var f=Flat(N(k.Dir),Vector3.UnitZ);var r=RightOf(f);const float Half=1.5f,High=1.12f;var mid=c+Up*High;
            if(t<k.Release){if(scene.Blade(a,false,out var b0,out var b1)){float g=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Sprite(false,Vector3.Lerp(b0,b1,g),(CutLight+Vector3.One)*1.6f,.18f,Sparkle);}return;}
            float age=t-k.Release,span=Math.Max(.05f,k.Arm-k.Release);
            if(t<k.Arm)
            {
                float tense=Clamp01(age/span);tense*=tense;float open=OutCubic(age/.09f),jit=tense*.02f*(float)Math.Sin(t*90);var e0=mid-r*(Half*open)+Up*jit;var e1=mid+r*(Half*open)-Up*jit;
                s.Line(false,e0,e1,CutLight*(.5f+.8f*tense),.08f+.05f*tense,false);s.Line(false,e0,e1,(CutLight+Vector3.One)*(1.1f+1.6f*tense),.012f+.012f*tense,true);
                s.Sprite(false,e0,(CutLight+Vector3.One)*(1+tense),.16f,Sparkle);s.Sprite(false,e1,(CutLight+Vector3.One)*(1+tense),.16f,Sparkle);
                var fl=c+Up*.03f;float b=.08f+.2f*tense;s.Band(false,fl-r*Half-f*CutReach,fl+r*Half-f*CutReach,fl+r*Half+f*CutReach,fl-r*Half+f*CutReach,b,b,CutLight);
                int n=Count(12,q,low);for(int i=0;i<n;i++){float x=R(seed,i+5)*2-1,ph=(t*1.2f+R(seed,i+6))%1;var p=mid+r*(x*Half*open)+Up*(.22f*ph)+f*(.16f*(R(seed,i+7)-.5f)*ph);s.Glow(p,CutLight*(1.5f*(1-ph)),0,.07f,Core,0,seed+i);}
                return;
            }
            float after=t-k.Arm;if(after>.75f)return;
            Flash(s,mid,after,.22f,CutLight,1.8f,seed);
            float go=OutCubic(after/.32f),fade=1-Clamp01(after/.34f);
            if(fade>0)for(int side=-1;side<=1;side+=2)
            {
                int n=low?6:14;
                for(int i=0;i<n;i++)
                {
                    float x0=i/(float)n*2-1,x1=(i+1)/(float)n*2-1;Vector3 P(float x,float dy)=>mid+r*(x*Half*(1+.25f*go))+f*(side*(CutReach+.9f)*go*(1-.45f*x*x))+Up*dy;float h=.17f*fade;
                    s.Band(false,P(x0,-h),P(x1,-h),P(x1,h),P(x0,h),(1-x0*x0)*fade,(1-x1*x1)*fade,(CutLight+Vector3.One*.3f)*1.7f);
                }
            }
            Sparks(s,mid,after,.5f,Count(24,q,low),6,CutLight+new Vector3(.3f,.2f,0),seed+1,8,.026f);
            Dust(s,c,after,.7f,Count(8,q,low),1.7f,new Vector3(.36f,.36f,.42f),.5f,seed+9);
        }

        // ---- Daggers: Tripwire -----------------------------------------------------------------------------------------
        // The dagger flies trailing a hair of wire; it sings when it pulls taut; a glint runs along it. Tripped, it whips back to both ends and a coil closes on the victim.
        private static void Tripwire(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release)return;var e0=N(k.From);var e1=N(k.Point);float age=t-k.Release,fly=Math.Max(.05f,(k.Arm-k.Release)*.62f),g=Clamp01(age/fly);var along=e1-e0;float len=along.Length();if(len<.05f)return;along/=len;
            if(g<1){var head=Vector3.Lerp(e0,e1,g);s.Line(false,e0,head,WireLight*.9f,.007f,true);s.Line(false,head-along*.26f,head,new Vector3(.8f,.84f,.88f)*1.5f,.032f,true);s.Sprite(false,head,WireLight*1.5f,.14f,Hot);return;}
            float landed=age-fly;Sparks(s,e1,landed,.3f,low?4:9,3,WireLight,seed,7,.018f);Flash(s,e1,landed,.16f,WireLight,.4f,seed+1);
            if(!k.Hit(t))
            {
                if(t>=k.Until)return;float armed=Clamp01((t-k.Arm)/.15f),end=1-Smooth((t-(k.Until-.25f))/.25f),glow=(.35f+.65f*armed)*end;
                s.Line(false,e0,e1,WireLight*(.2f*glow),.05f,false);
                // Pulled taut it thrums for a moment.
                float thrum=t>k.Arm?Math.Max(0,1-(t-k.Arm)/.3f):0;
                if(thrum>0&&!low){var side=Vector3.Normalize(Vector3.Cross(along,Up).LengthSquared()>1e-5f?Vector3.Cross(along,Up):Vector3.UnitX);Vector3 last=e0;for(int i=1;i<=8;i++){float u=i/8f;var p=e0+along*(len*u)+side*(thrum*.07f*(float)Math.Sin(3.14159f*u)*(float)Math.Sin(t*120+u*9));s.Line(false,last,p,WireLight*(1.2f*glow),.0065f,true);last=p;}}
                else s.Line(false,e0,e1,WireLight*(1.1f*glow),.0065f,true);
                float run=(t*.8f+R(seed,1))%1;s.Sprite(false,e0+along*(len*run),(WireLight+Vector3.One)*(1.8f*glow),.15f,Sparkle);s.Sprite(false,e0+along*(len*((run+.5f)%1)),WireLight*(1.2f*glow),.09f,Sparkle);
                s.Sprite(false,e0,WireLight*(1.3f*glow),.11f,Hot);s.Sprite(false,e1,WireLight*(1.3f*glow),.11f,Hot);
                return;
            }
            float since=t-k.HitAt;var at=N(k.To);float back=Clamp01(since/.16f);
            if(back<1){float b=1-back;s.Line(false,e0,Vector3.Lerp(at,e0,back)+Up*(.2f*back),WireLight*(1.5f*b),.009f,true);s.Line(false,e1,Vector3.Lerp(at,e1,back)-Up*(.15f*back),WireLight*(1.5f*b),.009f,true);}
            Flash(s,at,since,.2f,WireLight,1,seed+2);Sparks(s,at,since,.4f,Count(18,q,low),5,WireLight,seed+3,8,.02f);
            float coil=1-Clamp01(since/.8f);if(coil>0){int seg=low?12:20;for(int j=0;j<3;j++){float rad=(.5f-.12f*j)*(.55f+.45f*coil)+.25f*(float)Math.Max(0,1-since/.14f);Ring(s,at-Up*(.72f-.2f*j),rad,.035f,WireLight*(1.4f*coil),seg);}}
        }

        // ---- Shield: Shield Surf ---------------------------------------------------------------------------------------
        // The shield's rim on the floor throws a rooster tail of sparks, dust and speed lines stream past, and the hop off rings the floor.
        private static void Surf(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release)return;var feet=scene.Feet(a);var f=Flat(N(k.Dir),Vector3.UnitZ);var r=RightOf(f);float age=t-k.Release,floor=feet.Y-.2f;bool hopped=k.Recast(t);
            if(age<.45f){if(age<.3f)Ring(s,feet+Up*.05f,1.5f*OutCubic(age/.3f),.16f,SurfLight*(1.3f*(1-age/.3f)),low?14:24);Dust(s,feet,age,.45f,Count(8,q,low),1.1f,new Vector3(.34f,.33f,.34f),.5f,seed);}
            if(!hopped&&t<k.Until)
            {
                float env=Smooth(age/.12f);var tail=feet-f*.62f+Up*.03f;var kick=-f*1.7f+Up*.55f;
                Sparks(s,tail,(t*6)%.35f,.35f,Count(12,q,low),5.5f,new Vector3(1,.72f,.3f),seed+(long)(t*6),7,.024f,kick);
                Sparks(s,tail+r*.2f,(t*6+.17f)%.35f,.35f,Count(10,q,low),5,new Vector3(1,.6f,.24f),seed+13+(long)(t*6+.17f),7,.022f,kick);
                Sparks(s,tail-r*.2f,(t*6+.09f)%.35f,.35f,Count(10,q,low),5,new Vector3(1,.6f,.24f),seed+29+(long)(t*6+.09f),7,.022f,kick);
                s.Glow(tail,new Vector3(1,.55f,.18f)*(1.3f*env),0,.75f,Core,0,seed+1);s.Glow(feet+Up*.05f,SurfLight*(.42f*env),0,1.5f,Core,0,seed+2);
                int n=Count(16,q,low);
                for(int i=0;i<n;i++){float ph=(t*2.6f+R(seed,i+40))%1,side=R(seed,i+41)*2-1;var p=feet+r*(side*(.45f+.5f*Math.Abs(side)))+Up*(.12f+1.4f*R(seed,i+42))-f*(2.2f*ph);s.Line(false,p,p-f*(.8f+.6f*R(seed,i+43)),SurfLight*(.85f*(1-ph)*env),.012f,true);}
                int m=Count(6,q,low);for(int i=0;i<m;i++){float ph=(t*1.6f+R(seed,i+60))%1;s.Smoke(feet-f*(.6f+1.4f*ph)+r*(.5f*(R(seed,i+61)-.5f))+Up*(.12f+.25f*ph),new Vector3(.3f,.29f,.3f),.32f*(1-ph)*env,.5f+.7f*ph,.2f+.5f*ph,seed+i,floor);}
            }
            if(hopped){float since=t-k.RecastAt;Flash(s,feet+Up*.1f,since,.2f,SurfLight,1.2f,seed+4);if(since<.35f)Ring(s,feet+Up*.04f,1.7f*OutCubic(since/.35f),.18f,SurfLight*(1.4f*(1-since/.35f)),low?14:24);Dust(s,feet,since,.5f,Count(8,q,low),1.2f,new Vector3(.34f,.33f,.34f),.55f,seed+5);}
        }

        // ---- Greatsword: Iron Vow --------------------------------------------------------------------------------------
        // The blade bites the floor; a circle of amber turns round the pilot, embers climbing inside it; what it absorbs flashes and ripples the circle; the end shoves outward.
        private static void Vow(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release)return;var c=N(k.Point);var feet=scene.Feet(a);float age=t-k.Release,life=k.Until-k.Release;
            Flash(s,c+Up*.15f,age,.22f,Amber,1.7f,seed);Cracks(s,c,age,1.4f,low?7:13,2.6f,Amber,seed+1);Dust(s,c,age,.6f,Count(12,q,low),2,new Vector3(.3f,.27f,.24f),.7f,seed+2);Sparks(s,c+Up*.1f,age,.5f,Count(24,q,low),6.5f,Gold,seed+3,9,.028f,Up);
            int seg=low?20:44;
            if(age<life)
            {
                float env=Smooth(age/.2f)*(1-Smooth((age-(life-.15f))/.15f));const float Rim=1.3f;var fl=feet+Up*.04f;
                Ring(s,fl,Rim,.05f,Amber*(1.5f*env),seg);Ring(s,fl,Rim-.28f,.02f,Gold*env,seg);Ring(s,fl,Rim*.5f,Rim,Amber*(.1f*env),low?14:28);
                int ticks=low?6:12;for(int i=0;i<ticks;i++){float ang=t*.7f+i*6.2831853f/ticks;var d=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));s.Line(false,fl+d*(Rim-.2f),fl+d*(Rim+.16f),Gold*(1.2f*env),.02f,true);}
                int n=Count(20,q,low);for(int i=0;i<n;i++){float ang=6.2831853f*R(seed,i+30)+t*.25f,h=(t*.55f+R(seed,i+31))%1,rad=Rim*(.55f+.45f*R(seed,i+32));s.Glow(feet+new Vector3((float)Math.Cos(ang)*rad,h*2.2f,(float)Math.Sin(ang)*rad),Gold*(1.5f*(1-h)*env),0,.07f,Core,0,seed+i);}
                s.Glow(feet+Up*1.05f,Amber*(.2f*env),0,2.5f,Core,0,seed+8);
                // The hilt holds a slow bright pulse.
                s.Sprite(false,c+Up*1.25f,Gold*((.7f+.5f*Pulse(t,5,2))*env),.3f,Hot);
            }
            if(k.Hit(t)&&t-k.HitAt<.4f){float since=t-k.HitAt;var at=N(k.To);Flash(s,at,since,.22f,Gold,1,seed+11);Sparks(s,at,since,.35f,Count(16,q,low),6,Gold,seed+12,8,.024f);if(since<.3f)Ring(s,feet+Up*.06f,1.3f+1.2f*OutCubic(since/.3f),.12f,Gold*(1.4f*(1-since/.3f)),seg);}
            float after=t-k.Until;if(after>=0&&after<.6f){float u=Clamp01(after/.45f);Ring(s,feet+Up*.06f,VowPush*OutCubic(u),.4f*(1-u)+.06f,Amber*(1.8f*(1-u)),seg);Dust(s,feet,after,.6f,Count(12,q,low),VowPush,new Vector3(.3f,.27f,.24f),.6f,seed+14);Flash(s,feet+Up*.9f,after,.2f,Amber,1.8f,seed+15);}
        }

        // ---- Pistol: Bounty Round --------------------------------------------------------------------------------------
        // A brass tracer; a star where it strikes; then the mark turns over the target's head until it runs out. The through-walls marker is the HUD's.
        private static void Bounty(ITechniqueFxSink s,IKitFxScene scene,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release)return;float age=t-k.Release;var from=N(k.From);var end=N(k.Point);Flash(s,from,age,.14f,Brass,.7f,seed);
            float flight=k.HitAt>-50?Math.Max(.03f,k.HitAt-k.Release):.1f,g=Clamp01(age/flight);
            if(age<flight+.12f){float b=1-Clamp01((age-flight)/.12f);s.Line(false,Vector3.Lerp(from,end,Math.Max(0,g-.45f)),Vector3.Lerp(from,end,g),(Brass+Vector3.One*.4f)*(2*b),.03f,true);s.Sprite(false,Vector3.Lerp(from,end,g),Brass*(2*b),.2f,Hot);}
            if(!k.Hit(t))return;float since=t-k.HitAt;var at=N(k.To);var v=ActorOf(scene,k.Target);if(v!=null&&v.Alive&&scene.Chest(v,out var ch))at=ch;
            Flash(s,at,since,.26f,Brass,1.4f,seed+1);Sparks(s,at,since,.45f,Count(20,q,low),5.5f,Brass,seed+2,8,.026f);if(t>=k.Until||(v!=null&&!v.Alive))return;
            float env=Smooth(since/.2f)*(1-Smooth((since-(k.Until-k.HitAt-.3f))/.3f)),spin=t*1.6f,rad=.46f+.06f*(float)Math.Sin(t*5);var top=at+Up*1.05f;
            for(int i=0;i<4;i++){float a0=spin+i*1.5707963f-.42f,a1=a0+.84f;Vector3 P(float ang,float rr)=>top+new Vector3((float)Math.Cos(ang)*rr,0,(float)Math.Sin(ang)*rr);s.Band(false,P(a0,rad-.03f),P(a1,rad-.03f),P(a1,rad+.03f),P(a0,rad+.03f),env,env,Brass*1.7f);}
            s.Glow(top,Brass*(1.2f*env),0,.26f,Core,0,seed+3);s.Sprite(false,top,Brass*(1.5f*env),.3f,RingSprite);s.Line(false,top,top+Up*.5f,Brass*(.8f*env),.012f,true);
        }

        // ---- Machine gun: Recoil Burst ---------------------------------------------------------------------------------
        // The whole magazine as one long tongue of fire off the muzzle, brass raining back, smoke hanging on the line it fired.
        private static void Recoil(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var chest=scene.Chest(a,out var c)?c:scene.Feet(a)+Up*1.25f;var d=N(k.Dir);d=d.LengthSquared()>1e-5f?Vector3.Normalize(d):Vector3.UnitZ;var muzzle=chest+d*.85f;float floor=N(k.From).Y-2;
            if(t<k.Release){float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Glow(muzzle,Flame*(1.2f*u),0,.4f*u,Core,u,seed);return;}
            float age=t-k.Release,life=k.Until-k.Release;
            if(age<life)
            {
                float env=1-.35f*Clamp01(age/life);long roll=seed+(long)(t*30);int n=Count(14,q,low);
                for(int i=0;i<n;i++){float w=i/(float)n;var p=muzzle+d*(.2f+3.4f*w)+RandomDir(roll,i)*(.1f+.3f*w);s.Fire(p,new Vector3(1,.42f,.1f),env*(1-.8f*w),.5f+.5f*w,.05f+.5f*w,seed+i,floor);}
                int tongues=low?3:6;for(int j=0;j<tongues;j++)s.Tongue(false,muzzle,muzzle+d*(2.2f+R(roll,j+60))+RandomDir(roll,j+40)*.22f,FlameCore*(2.2f*env),.12f);
                s.Glow(muzzle,(FlameCore+Vector3.One*.2f)*(2.2f*env),0,1.1f,Core,0,seed+7);
                Sparks(s,chest+d*.25f+Up*.1f,(t*5)%.4f,.4f,Count(10,q,low),4.5f,Brass,seed+(long)(t*5),11,.03f,-d*.5f+Up*.9f);
            }
            Flash(s,muzzle,age,.18f,FlameCore,1.8f,seed+2);
            int m=Count(8,q,low);float o=.45f*(1-Smooth((age-.2f)/.9f))*Smooth(age/.12f);if(o>0)for(int i=0;i<m;i++){float w=i/(float)m;s.Smoke(N(k.From)+d*(1.2f+3.6f*w)+RandomDir(seed,i+70)*.25f+Up*(age*.3f),new Vector3(.2f,.19f,.18f),o,.6f+.5f*age,.2f+.5f*Clamp01(age),seed+i,floor);}
        }

        // ---- Rifle: Sonar Dart -----------------------------------------------------------------------------------------
        // The dart blinks where it stuck; each ping runs out across the floor and round the dart. The enemies it shows are the HUD's.
        private static void Sonar(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release)return;var from=N(k.From);var at=N(k.Point);float fly=Math.Max(.05f,(k.Arm-k.Release)*.5f),age=t-k.Release,g=Clamp01(age/fly);
            if(g<1){var head=Vector3.Lerp(from,at,g);s.Line(false,Vector3.Lerp(from,at,Math.Max(0,g-.3f)),head,PingLight*1.6f,.02f,true);s.Sprite(false,head,PingLight*1.8f,.18f,Hot);return;}
            float landed=age-fly;Sparks(s,at,landed,.25f,low?4:8,2.8f,PingLight,seed,7,.016f);
            if(k.Hit(t)){float since=t-k.HitAt;Flash(s,at,since,.25f,PingLight,1.1f,seed+1);Sparks(s,at,since,.4f,Count(14,q,low),4.5f,PingLight,seed+2,8,.02f);return;}
            if(t>=k.Until)return;float blink=Pulse(t,9,6);s.Sprite(false,at,PingLight*(1+1.8f*blink),.14f+.06f*blink,Hot);s.Glow(at,PingLight*(.5f*blink),0,.5f,Core,0,seed+3);
            var fl=new Vector3(at.X,scene.Feet(a).Y+.05f,at.Z);
            for(int n=0;n<3;n++)
            {
                float p=t-(k.Arm+n*SonarEvery);if(p<0||p>.85f)continue;float w=p/.85f,rad=SonarReach*OutCubic(w),b=1-w;
                Flash(s,at,p,.2f,PingLight,.9f,seed+10+n);Ring(s,fl,rad,.16f+.12f*w,PingLight*(1.4f*b),low?24:48);Ring(s,fl,rad*.86f,.05f,PingLight*(.6f*b),low?24:48);Ring(s,at,rad*.6f,.05f,PingLight*(.5f*b),low?18:32);
            }
        }

        // ---- Bow: Spirit Hawk ------------------------------------------------------------------------------------------
        /// <summary>Where the hawk is at `t`: out along the line, then down the curve to its strike, or on and up if it found no one.</summary>
        public static Vector3 HawkAt(NativeTechnique k,float t)
        {
            var from=N(k.From);var to=N(k.Point);if(t<k.Arm)return Vector3.Lerp(from,to,Clamp01((t-k.Release)/Math.Max(.05f,k.Arm-k.Release)));
            if(k.HitAt>-50){float u=Clamp01((t-k.Arm)/Math.Max(.05f,k.HitAt-k.Arm));var strike=N(k.To);var mid=(to+strike)*.5f+Up*1.2f;return to*((1-u)*(1-u))+mid*(2*u*(1-u))+strike*(u*u);}
            var d=to-from;d=d.LengthSquared()>1e-5f?Vector3.Normalize(d):Vector3.UnitZ;float over=t-k.Arm;return to+d*(over*14)+Up*(over*over*3);
        }
        // Two wings of light, flapping and folding for the dive, a body, a ribbon of tail, feathers shed behind it; a burst of them where it strikes.
        private static void Hawk(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release){s.Sprite(false,N(k.From),HawkLight*(1.4f*Clamp01((t-k.At)/.3f)),.22f,Hot);return;}
            bool struck=k.Hit(t);float last=struck?k.HitAt:k.Until;
            if(t<last)
            {
                float fade=struck||k.HitAt>-50?1:1-Smooth((t-(k.Until-.3f))/.3f),flap=(float)Math.Sin(t*24),fold=t>=k.Arm&&k.HitAt>-50?.5f:1;var p=HawkAt(k,t);var d=p-HawkAt(k,t-.03f);d=d.LengthSquared()>1e-6f?Vector3.Normalize(d):Vector3.UnitZ;
                var cross=Vector3.Cross(d,Up);var side=cross.LengthSquared()>1e-5f?Vector3.Normalize(cross):Vector3.UnitX;var up=Vector3.Normalize(Vector3.Cross(side,d));
                for(int sd=-1;sd<=1;sd+=2)
                {
                    var shoulder=p+side*(sd*.08f);var elbow=shoulder+side*(sd*.44f*fold)+up*(flap*.22f)-d*.06f;var tip=elbow+side*(sd*.56f*fold)+up*(flap*.36f)-d*.34f;
                    s.Band(false,shoulder+d*.05f,elbow+d*.02f,elbow-d*.24f,shoulder-d*.34f,fade,fade*.85f,HawkLight*1.6f);s.Band(false,elbow+d*.02f,tip,tip-d*.06f,elbow-d*.24f,fade*.85f,fade*.2f,HawkLight*1.4f);
                }
                s.Line(false,p-d*.32f,p+d*.22f,(HawkLight+Vector3.One*.5f)*(1.9f*fade),.07f,true);s.Glow(p,HawkLight*(1.1f*fade),0,.75f,Core,0,seed);s.Sprite(false,p+d*.26f,Vector3.One*(2*fade),.12f,Hot);
                int n=low?6:12;var prev=p-d*.2f;for(int i=1;i<=n;i++){float tt=t-i*.028f;if(tt<k.Release)break;var o=HawkAt(k,tt);float b=1-i/(float)n,w0=.12f*(b+1f/n),w1=.12f*b;s.Band(false,prev-side*w0,o-side*w1,o+side*w1,prev+side*w0,(b+1f/n)*fade,b*fade,HawkLight*1.1f);prev=o;}
                int m=Count(10,q,low);for(int i=0;i<m;i++){float ph=(t*2+R(seed,i+50))%1,born=t-ph*.7f;if(born<k.Release)continue;var o=HawkAt(k,born)+RandomDir(seed,i+51)*.2f-Up*(1.1f*ph);s.Sprite(false,o,HawkLight*(1.2f*(1-ph)*fade),.07f,Sparkle);}
            }
            if(!struck)return;float since=t-k.HitAt;var at=N(k.To);Flash(s,at,since,.24f,HawkLight,1.4f,seed+3);Sparks(s,at,since,.5f,Count(24,q,low),6,HawkLight,seed+4,2.5f,.028f);
            float left=1-Clamp01(since/.9f);if(left>0){int f=Count(12,q,low);for(int i=0;i<f;i++){var p=at+RandomDir(seed,i+80)*(1.2f*OutCubic(since/.4f))-Up*(.5f*since*since+.2f*since);s.Sprite(false,p,HawkLight*(1.4f*left),.09f,Sparkle);}}
        }

        // ---- Fire: Wildfire Run ----------------------------------------------------------------------------------------
        // Flames stand on the path as the pilot lays it and die back three seconds on, smouldering; the runner's feet burn while it lasts.
        private static void Wildfire(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);if(t<k.Release){float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Glow(feet+Up*.06f,Flame*(1.4f*u),0,1.2f*u,RingGlow,u,seed);return;}
            float age=t-k.Release;if(age<.3f){Flash(s,feet+Up*.4f,age,.22f,Flame,1.7f,seed+1);Ring(s,feet+Up*.05f,1.6f*OutCubic(age/.3f),.2f,Flame*(1.6f*(1-age/.3f)),low?14:24);}
            var path=k.Path;
            if(path!=null)
            {
                int step=low||q<.5f?2:1;
                for(int i=0;i<path.Length;i+=step)
                {
                    float born=k.Release+i*NativeTechniques.PathStep;if(t<born)break;float since=t-born;const float Life=3;if(since>Life)continue;float env=Smooth(since/.12f)*(1-Smooth((since-(Life-.6f))/.6f)),fl=(float)Math.Sin(t*13+i*2.3f);var p=N(path[i]);
                    s.Glow(p+Up*.05f,Flame*(.7f*env),0,1.1f,Core,0,seed+i);
                    int puffs=low?1:2;for(int j=0;j<puffs;j++){float ph=(t*1.5f+R(seed,i*3+j))%1;var o=p+new Vector3((R(seed,i*5+j)-.5f)*.7f,.12f+.85f*ph,(R(seed,i*7+j)-.5f)*.7f);s.Fire(o,new Vector3(1,.38f,.07f),env*(1-ph)*.9f,.5f+.25f*fl*(1-ph),.1f+.6f*ph,seed+i*11+j,p.Y-.05f);}
                    if(!low&&i%2==0)s.Tongue(false,p+Up*.02f,p+Up*((.55f+.25f*fl)*env),FlameCore*(1.4f*env),.1f);
                    if(!low&&i%3==0&&since>Life*.35f)s.Smoke(p+Up*(.8f+.4f*((t+i*.13f)%1)),new Vector3(.16f,.14f,.13f),.18f*env,.7f,.4f,seed+i,p.Y-.05f);
                }
            }
            if(t<k.Arm){float env=Smooth(age/.15f)*(1-Smooth((t-(k.Arm-.2f))/.2f));s.Fire(feet+Up*.16f,new Vector3(1,.42f,.1f),.8f*env,.7f,.2f,seed+3,feet.Y-.05f);s.Glow(feet+Up*.1f,Flame*(.9f*env),0,1,Core,0,seed+4);Sparks(s,feet+Up*.3f,(t*7)%.4f,.4f,low?3:7,2.4f,FlameCore,seed+(long)(t*7),3,.022f,Up*.5f);}
        }

        // ---- Water: Slipstream -----------------------------------------------------------------------------------------
        // A sheet of moving water runs out along the floor: chevrons and flow lines going its way, foam on its edges, spray thrown off them.
        private static void Slip(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var e0=N(k.From);var e1=N(k.To);var line=e1-e0;float L=line.Length();if(L<.1f)return;var f=line/L;var r=RightOf(Flat(f,Vector3.UnitZ));var lit=new Vector3(.6f,.92f,1);
            if(t<k.Release){float u=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Glow(e0+Up*.1f,Current*(1.2f*u),0,1.1f*u,Core,u,seed);return;}
            float age=t-k.Release,life=k.Until-k.Release;if(age>life)return;float reach=L*OutCubic(age/.4f),env=1-Smooth((age-(life-.5f))/.5f);const float W=1.2f;var lift=Up*.04f;
            int n=low?6:14;for(int i=0;i<n;i++){float u0=i/(float)n,u1=(i+1)/(float)n,a0=Math.Min(1,u0*6)*Math.Min(1,(1-u0)*6),a1=Math.Min(1,u1*6)*Math.Min(1,(1-u1)*6);var p0=e0+f*(reach*u0)+lift;var p1=e0+f*(reach*u1)+lift;s.Band(false,p0-r*W,p1-r*W,p1+r*W,p0+r*W,.42f*a0*env,.42f*a1*env,Current);}
            for(int side=-1;side<=1;side+=2){var edge=r*(side*W);s.Line(false,e0+edge+lift,e0+f*reach+edge+lift,lit*(.9f*env),.035f,true);}
            int c=Count(7,q,low);for(int i=0;i<c;i++){float x=(t*6+i*(L/c))%L;if(x>reach)continue;float e=Math.Min(1,x*.7f)*Math.Min(1,(reach-x)*.7f)*env;var tip=e0+f*x+Up*.06f;s.Line(false,tip-f*.75f+r*.75f,tip,lit*(1.4f*e),.07f,true);s.Line(false,tip-f*.75f-r*.75f,tip,lit*(1.4f*e),.07f,true);}
            int m=Count(18,q,low);for(int i=0;i<m;i++){float x=(t*(7+3*R(seed,i+20))+R(seed,i+21)*L)%L;if(x>reach)continue;var p=e0+f*x+r*((R(seed,i+22)*2-1)*W*.85f)+Up*.05f;s.Line(false,p-f*(.6f+.6f*R(seed,i+23)),p,lit*(.7f*env),.012f,true);}
            int dr=Count(14,q,low);for(int i=0;i<dr;i++){float ph=(t*1.4f+R(seed,i+40))%1,x=R(seed,i+41)*reach,side=R(seed,i+42)<.5f?-1:1;var v=Up*(2.2f*(1-ph))+r*(side*.7f);var p=e0+f*x+r*(side*(W+.3f*ph))+Up*(1.6f*ph-1.7f*ph*ph+.03f);s.Drop(p,v*.05f,lit*(1.1f*env),.8f,.035f,ph,seed+i);}
            if(age<.45f){var head=e0+f*reach;Sparks(s,head+Up*.1f,age,.45f,Count(14,q,low),3.5f,lit,seed+5,9,.02f,f+Up);}
        }

        // ---- Lightning: Lightning Rod ----------------------------------------------------------------------------------
        // The rod lands with a crack; arcs crawl off its tip and its reach is ringed on the floor; a shot it draws is a bolt from where the shot was to the tip.
        private static void Rod(ITechniqueFxSink s,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release)return;var from=N(k.From);var foot=N(k.Point);var tip=foot+Up*RodHeight;
            if(t<k.Arm){float u=Clamp01((t-k.Release)/Math.Max(.05f,k.Arm-k.Release));var p=Vector3.Lerp(from,foot+Up*.6f,u)+Up*(.7f*(float)Math.Sin(3.14159f*u));var d=foot-from;d=d.LengthSquared()>1e-5f?Vector3.Normalize(d):Vector3.UnitZ;s.Line(false,p-d*.6f,p,RodLight*1.6f,.04f,true);s.Sprite(false,p,RodLight*1.8f,.24f,Hot);return;}
            float age=t-k.Arm;Flash(s,foot+Up*.3f,age,.24f,RodLight,1.6f,seed);Cracks(s,foot,age,.8f,low?5:9,1.6f,RodLight,seed+1);Sparks(s,foot+Up*.2f,age,.4f,Count(18,q,low),5,RodLight,seed+2,8,.024f,Up);
            if(t>=k.Until){float after=t-k.Until;Flash(s,tip,after,.3f,RodLight,1.2f,seed+3);Sparks(s,tip,after,.4f,Count(14,q,low),4,RodLight,seed+4,9,.02f);return;}
            float env=1-Smooth((t-(k.Until-.3f))/.3f);s.Sprite(false,tip,(RodLight+Vector3.One*.4f)*((1.6f+.6f*(float)Math.Sin(t*17))*env),.22f,Hot);s.Glow(tip,RodLight*(.6f*env),0,.8f,Core,0,seed+5);
            Ring(s,foot+Up*.04f,RodReach*OutCubic(age/.25f),.04f,RodLight*(.75f*env),low?20:40);
            long roll=seed+(long)(t*12);int arcs=low?1:3;for(int i=0;i<arcs;i++){var d=RandomDir(roll,i);d.Y=-Math.Abs(d.Y)-.3f;Bolt(s,tip,tip+Vector3.Normalize(d)*(1+.9f*R(roll,i+20)),RodLight*(1.5f*env),.014f,.5f,5,roll+i);}
            if(k.Hit(t)){float since=t-k.HitAt;if(since<.22f){var at=N(k.To);float b=1-since/.22f;Bolt(s,at,tip,(RodLight+Vector3.One*.4f)*(2.2f*b),.035f,.8f,low?6:10,seed+(long)(t*30));Flash(s,at,since,.2f,RodLight,1,seed+6);Flash(s,tip,since,.2f,RodLight,.8f,seed+7);}}
        }

        // ---- Darkness: Grave Walk --------------------------------------------------------------------------------------
        /// <summary>How far under the floor the pilot's body is drawn at `t` (the rules hold the real capsule).</summary>
        public static float GraveDepth(NativeTechnique k,float t)
        {
            if(k==null||t<k.At||t>=k.RecoverUntil)return 0;if(t<k.Release)return GraveDeep*Smooth((t-k.At)/Math.Max(.05f,k.Release-k.At));if(t<k.Until)return GraveDeep;return GraveDeep*(1-Smooth((t-k.Until)/Math.Max(.05f,k.RecoverUntil-k.Until)));
        }
        // A pool of black on the floor with a violet rim, sliding with the pilot under it, tendrils and smoke climbing off it. It opens at the sink and closes at the rise.
        private static void Grave(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var at=new Vector3(feet.X,k.From.y,feet.Z);float floor=k.From.y-.05f,size=t<k.Release?Smooth((t-k.At)/Math.Max(.05f,k.Release-k.At)*1.4f):t<k.Until?1:1-Smooth((t-k.Until)/Math.Max(.05f,k.RecoverUntil-k.Until));
            if(size>0)
            {
                float rim=1.05f*size;int n=Count(14,q,low);
                for(int i=0;i<n;i++){float ang=6.2831853f*R(seed,i+1)+t*.3f*(R(seed,i+2)-.5f),rad=rim*(float)Math.Sqrt(R(seed,i+3));s.Smoke(at+new Vector3((float)Math.Cos(ang)*rad,.06f+.1f*R(seed,i+4),(float)Math.Sin(ang)*rad),Ink,.75f*size,.7f+.4f*R(seed,i+5),.25f,seed+i,floor);}
                Ring(s,at+Up*.05f,rim,.1f,GraveLight*(1.3f*size),low?14:28);Ring(s,at+Up*.05f,rim*.72f,.04f,GraveLight*(.6f*size),low?12:24);
                int m=low?4:9;for(int i=0;i<m;i++){float ph=(t*1.1f+R(seed,i+20))%1,ang=6.2831853f*R(seed,i+21)+t*.4f;var b=at+new Vector3((float)Math.Cos(ang)*rim*.85f,.02f,(float)Math.Sin(ang)*rim*.85f);s.Line(false,b,b+Up*(.7f*(1-ph*.2f))+new Vector3((float)Math.Sin(t*3+i)*.08f,0,(float)Math.Cos(t*3+i)*.08f),GraveLight*(.9f*size*(1-ph)),.02f,true);}
                int w=Count(10,q,low);for(int i=0;i<w;i++){float ph=(t*.8f+R(seed,i+40))%1,ang=6.2831853f*R(seed,i+41);s.Glow(at+new Vector3((float)Math.Cos(ang)*rim*.6f*(1+ph*.4f),.1f+1.3f*ph,(float)Math.Sin(ang)*rim*.6f*(1+ph*.4f)),GraveLight*(1.2f*size*(1-ph)),0,.08f,Core,0,seed+i);}
            }
            // Smoke climbing the body as it goes down and as it comes up.
            if(t<k.Release||(t>=k.Until&&t<k.RecoverUntil)){int n=Count(8,q,low);for(int i=0;i<n;i++){float ph=(t*1.8f+R(seed,i+60))%1;s.Smoke(at+new Vector3((R(seed,i+61)-.5f)*.5f,.2f+1.5f*ph,(R(seed,i+62)-.5f)*.5f),Ink,.55f*(1-ph),.5f+.3f*ph,.3f,seed+i,floor);}}
            float after=t-k.Until;if(after>=0){Flash(s,at+Up*.4f,after,.26f,GraveLight,1.5f,seed+7);Sparks(s,at+Up*.2f,after,.5f,Count(16,q,low),4,GraveLight,seed+8,3,.02f,Up);}
        }
    }
}

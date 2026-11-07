using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The boss relics in the world (docs/BOSS-CHIPS.md): every relic's moment, read from the ChipProc events the rules emit with the relic's id
    /// (Runtime NativeBossRelicRules), drawn in its boss's colours: the Siren's song rolling out in rings and a ribbon of it pulling them in; THE 6:47's
    /// headlight and speed lines, and a live third rail crackling along a roll; THE CONVERTER's molten release and a pool of slag; THE HUM's fluorescent
    /// tubes buzzing into a burst, and a glitch where a roll goes through someone; THE DEEP END's water dragging a target in, and the warm pool light at a
    /// pilot's feet; NOCTIEL's ring of light on landing with feathers coming down, and a beam of light falling on the one it saves.
    /// A pure function of the event and the presentation clock (host, guests, spectators and the killcam draw the same). Reduced effects thins it and never
    /// flickers; the low tier halves it. Nothing here reaches the rules.
    /// </summary>
    public sealed class NativeBossRelicFx
    {
        private struct Proc{public int Id,Source,Target,Relic;public float Time,Amount;public Vector3 At,From,Dir;}
        private readonly List<Proc> procs=new List<Proc>();private int cursor;private float clock=float.NegativeInfinity;
        public const int MaxProcs=64;public const float Longest=3.3f;
        public int Count=>procs.Count;
        public void Clear(){procs.Clear();cursor=0;clock=float.NegativeInfinity;}
        /// <summary>Reads the frame's fresh relic procs (idempotent per frame; a new arena or the killcam's replica starts over).</summary>
        public void Observe(IReadOnlyList<ArenaEvent> events,float time)
        {
            if(events==null)return;int newest=0;for(int i=0;i<events.Count;i++)if(events[i].Id>newest)newest=events[i].Id;
            if(newest>0&&newest<cursor||time<clock-.25f){procs.Clear();cursor=0;}
            clock=time;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=cursor||e.Kind!=EventKind.ChipProc)continue;int r=NativeBossChips.Find(e.Detail);if(r<0||time-e.Time>Longest)continue;
                if(procs.Count>=MaxProcs)procs.RemoveAt(0);
                procs.Add(new Proc{Id=e.Id,Source=e.Source,Target=e.Target,Relic=r,Time=e.Time,Amount=e.Amount,At=N(e.Position),From=N(e.From),Dir=N(e.Normal)});
            }
            if(newest>cursor)cursor=newest;
            for(int i=procs.Count-1;i>=0;i--)if(time-procs[i].Time>Life(procs[i]))procs.RemoveAt(i);
        }
        private static Vector3 N(V3 v)=>new Vector3(v.x,v.y,v.z);
        private static float Life(in Proc p)
        {
            switch(NativeBossChips.Relics[p.Relic].Id)
            {
                case NativeBossChips.LastVerse:return 1.3f;case NativeBossChips.Lullaby:return .65f;case NativeBossChips.Express:return .6f;
                case NativeBossChips.MindGap:return p.Target==0?2.1f:.5f;case NativeBossChips.SlagHeart:return .7f;case NativeBossChips.Pour:return 3.25f;
                case NativeBossChips.MaxHum:return p.Target==0?.8f:.4f;case NativeBossChips.Noclip:return .6f;case NativeBossChips.Undertow:return p.Amount>=3?.8f:.5f;
                case NativeBossChips.Warm:return 1.8f;case NativeBossChips.Feather:return 1.1f;case NativeBossChips.Lux:return 1.6f;
            }
            return 1;
        }
        /// <summary>Every live relic moment at `time`. `q` effects quality (Reduced .3), `low` the low graphics tier.</summary>
        public void Draw(IKitFxSink s,float time,float q,bool low)
        {
            if(s==null)return;
            for(int i=0;i<procs.Count;i++){var p=procs[i];float age=time-p.Time;if(age<0||age>Life(p))continue;Draw(s,p,age,q,low);}
        }

        // ---- helpers --------------------------------------------------------------------------------------------------------
        private const int Mote=NativeMeleeKitFx.Mote,Core=NativeMeleeKitFx.Core,RingP=NativeMeleeKitFx.Ring;
        private static float H(long k,uint salt){unchecked{uint h=(uint)k*0x9e3779b1u^salt*0x85ebca6bu;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;h*=0x297a2d39u;h^=h>>15;return (h&0xffffff)/16777216f;}}
        private static float C01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=C01(x);return x*x*(3-2*x);}
        private static Vector3 Lin(int hex,float gain)=>NativeMeleeKitFx.Lin(hex,gain);
        private static Vector3 Floor(Vector3 body)=>new Vector3(body.X,body.Y-.865f,body.Z);
        private static Vector3 Flat(Vector3 v){v.Y=0;return v.LengthSquared()>1e-6f?Vector3.Normalize(v):Vector3.UnitZ;}
        private static Vector3 Side(Vector3 d)=>new Vector3(d.Z,0,-d.X);
        private static int Many(int n,float q,bool low){int c=q<.5f?Math.Max(2,n/2):n;return low?Math.Max(2,c/2):c;}
        /// <summary>A fluorescent buzz: a slow sway, and with full effects a tube's stutter now and then (never a strobe).</summary>
        private static float Buzz(float age,long seed,float q){float b=.85f+.15f*(float)Math.Sin(age*47+seed%7);if(q>=.5f&&H(seed+(long)(age*18),91)<.12f)b*=.45f;return b;}
        private static void Rings(IKitFxSink s,Vector3 floor,float radius,float width,Vector3 color,float bright,int segments){NativeMeleeKitFx.GroundRing(s,floor,radius,width,color,bright,segments);}

        private static void Draw(IKitFxSink s,in Proc p,float age,float q,bool low)
        {
            var set=NativeBossChips.Sets[NativeBossChips.Relics[p.Relic].Set];var deep=Lin(set.Rarity,1);var light=Lin(set.Label,1);long seed=p.Id*7919L+13;
            switch(NativeBossChips.Relics[p.Relic].Id)
            {
                case NativeBossChips.LastVerse:LastVerse(s,p,age,deep,light,seed,q,low);break;
                case NativeBossChips.Lullaby:Lullaby(s,p,age,deep,light,seed,q,low);break;
                case NativeBossChips.Express:Express(s,p,age,deep,light,seed,q,low);break;
                case NativeBossChips.MindGap:if(p.Target==0)Rail(s,p,age,deep,seed,q,low);else Shock(s,p,age,deep,seed,q,low);break;
                case NativeBossChips.SlagHeart:Slag(s,p,age,deep,light,seed,q,low);break;
                case NativeBossChips.Pour:Pour(s,p,age,deep,light,seed,q,low);break;
                case NativeBossChips.MaxHum:Hum(s,p,age,deep,light,seed,q,low);break;
                case NativeBossChips.Noclip:Glitch(s,p,age,deep,light,seed,q,low);break;
                case NativeBossChips.Undertow:Undertow(s,p,age,deep,light,seed,q,low);break;
                case NativeBossChips.Warm:Warm(s,p,age,deep,light,seed,q,low);break;
                case NativeBossChips.Feather:Feather(s,p,age,deep,light,seed,q,low);break;
                case NativeBossChips.Lux:Lux(s,p,age,deep,light,seed,q,low);break;
            }
        }

        // ---- THE SIREN ---------------------------------------------------------------------------------------------------------
        private static void LastVerse(IKitFxSink s,in Proc p,float age,Vector3 deep,Vector3 light,long seed,float q,bool low)
        {
            // Her song rolls out over the floor in three soft rings to 6 m, the kill's place lit, and notes of light spiralling up from it.
            var floor=Floor(p.At);int seg=low?24:40;
            for(int k=0;k<3;k++){float u=(age-.12f*k)/.9f;if(u<=0||u>=1)continue;Rings(s,floor,.4f+5.6f*Smooth(u),.22f+.1f*(1-u),Vector3.Lerp(light,deep,u)*1.6f,(float)Math.Pow(1-u,1.4)*.85f,seg);}
            if(age<.45f){float u=age/.45f;s.Glow(p.At,light*2.2f*(1-u),0,1.4f+u,Core,u,seed);}
            int n=Many(10,q,low);for(int i=0;i<n;i++){float h=H(seed+i,3),born=h*.35f,u=(age-born)/.85f;if(u<=0||u>=1)continue;float a=(h*6.283f)+u*3.2f,r=.35f+1.3f*u;
                var at=floor+new Vector3((float)Math.Cos(a)*r,.3f+1.9f*u,(float)Math.Sin(a)*r);s.Glow(at,Vector3.Lerp(light,deep,u)*2.4f*(1-u),0,.22f,Mote,u,seed+i);}
        }
        private static void Lullaby(IKitFxSink s,in Proc p,float age,Vector3 deep,Vector3 light,long seed,float q,bool low)
        {
            // A ribbon of her song from the pilot to the one it calls, its wave running toward the pilot as they're drawn in.
            if(p.From==Vector3.Zero)return;var a=p.At;var b=p.From;var d=b-a;float len=d.Length();if(len<.2f)return;var side=Side(Flat(d));float fade=1-Smooth(age/.65f);int n=Many(22,q,low);Vector3 last=a;
            for(int i=0;i<=n;i++){float t=i/(float)n,w=(float)Math.Sin(t*12.5f-age*16)*.28f*(float)Math.Sin(t*3.1416f);var at=a+d*t+side*w+Vector3.UnitY*(.15f*(float)Math.Sin(t*3.1416f));
                if(i>0)s.Line(false,last,at,Vector3.Lerp(deep,light,t)*1.8f*fade,.045f,false);if(i%3==0)s.Glow(at,light*1.6f*fade,0,.32f,Mote,age,seed+i);last=at;}
            s.Glow(a,deep*2*fade,0,.9f,Core,age,seed);
        }

        // ---- THE 6:47 ----------------------------------------------------------------------------------------------------------
        private static void Express(IKitFxSink s,in Proc p,float age,Vector3 deep,Vector3 light,long seed,float q,bool low)
        {
            var dir=Flat(p.Dir);var white=new Vector3(1.6f,1.5f,1.3f);int n=Many(12,q,low);
            if(p.Amount<=0)
            {
                // Full speed: the headlight comes on ahead of them and the air tears past, a ring kicked off the platform.
                float u=age/.6f;s.Glow(p.At+dir*.9f+Vector3.UnitY*.25f,white*2.6f*(1-u),0,1.3f,Core,u,seed);Rings(s,Floor(p.At),.3f+1.6f*Smooth(u*1.3f),.14f,deep*2,(1-u)*.8f,low?16:28);
                for(int i=0;i<n;i++){float h=H(seed+i,5),born=h*.2f,v=(age-born)/.4f;if(v<=0||v>=1)continue;var side=Side(dir)*((h-.5f)*1.4f);var at=p.At+side+Vector3.UnitY*((H(seed+i,6)-.4f)*1.1f)-dir*(v*1.8f);
                    s.Streak(at,-dir*14,Vector3.Lerp(white,deep,h)*1.8f*(1-v),.025f,.5f,.04f,.8f,v,seed+i);}
                return;
            }
            // The express hit: a white-hot strike, sparks thrown on along its way, a ring off them.
            float k=age/.55f;s.Glow(p.At,white*3.2f*(1-k)*(1-k),0,1.2f+k,Core,k,seed);Rings(s,Floor(p.At),.4f+2.2f*Smooth(k*1.4f),.18f,deep*2.4f,(1-k)*.9f,low?16:28);
            for(int i=0;i<n+4;i++){float h=H(seed+i,7),a=(h-.5f)*.9f,v=C01(age/.45f);var d=Vector3.Normalize(dir*(float)Math.Cos(a)+Side(dir)*(float)Math.Sin(a)+Vector3.UnitY*(H(seed+i,8)*.6f));
                s.Streak(p.At+d*(.3f+3.2f*v),d*(16+8*h),Vector3.Lerp(white,deep,v)*2.2f*(1-v),.03f,.3f,.04f,.85f,v,seed+i);}
        }
        private static void Rail(IKitFxSink s,in Proc p,float age,Vector3 deep,long seed,float q,bool low)
        {
            // A live third rail laid along the roll: it lights from the start as they go, hums for 1.5 s, crackling between its two strips, and dies.
            var dir=Flat(p.Dir);float len=Math.Max(.5f,p.Amount),grown=len*Smooth(age/.35f),fade=age>1.75f?1-Smooth((age-1.75f)/.35f):1;if(grown<.05f||fade<=0)return;
            var side=Side(dir)*.17f;var a=p.From+Vector3.UnitY*.05f;var b=a+dir*grown;var spark=new Vector3(.75f,.9f,1.6f);float hum=Buzz(age,seed,q);
            s.Line(false,a+side,b+side,deep*2.2f*fade*hum,.06f,false);s.Line(false,a-side,b-side,deep*2.2f*fade*hum,.06f,false);
            s.Band(false,a+side,b+side,b-side,a-side,.35f*fade,.35f*fade,deep*.9f);
            int arcs=Many(5,q,low);long slot=(long)(age*14);
            for(int i=0;i<arcs;i++){float h=H(seed+slot*31+i,11);if(h>.75f)continue;float t=H(seed+slot*31+i,12)*grown;var m=a+dir*t;var mid=m+Side(dir)*((h-.4f)*.2f)+Vector3.UnitY*(.1f+.15f*h);
                s.Line(false,m+side,mid,spark*2.4f*fade,.025f,true);s.Line(false,mid,m-side,spark*2.4f*fade,.025f,true);s.Glow(mid,spark*1.4f*fade,0,.3f,Mote,h,seed+i);}
            s.Glow(b,spark*2*fade,0,.6f,Core,age,seed);
        }
        private static void Shock(IKitFxSink s,in Proc p,float age,Vector3 deep,long seed,float q,bool low)
        {
            // An enemy crossed it: the rail's current jumps up into them.
            float u=age/.5f;var spark=new Vector3(.8f,.95f,1.7f);var a=p.From==Vector3.Zero?Floor(p.At):p.From;var b=p.At;int n=Many(7,q,low);Vector3 last=a;
            for(int i=1;i<=n;i++){float t=i/(float)n;var at=Vector3.Lerp(a,b,t)+new Vector3(H(seed+i,13)-.5f,0,H(seed+i,14)-.5f)*.35f*(1-t*t);s.Line(false,last,at,spark*2.8f*(1-u),.035f,true);last=at;}
            s.Glow(b,spark*2.6f*(1-u),0,1.1f,Core,u,seed);s.Glow(a,deep*2*(1-u),0,.7f,Core,u,seed+1);
        }

        // ---- THE CONVERTER -----------------------------------------------------------------------------------------------------
        private static void Slag(IKitFxSink s,in Proc p,float age,Vector3 deep,Vector3 light,long seed,float q,bool low)
        {
            // The stored heat let go: a white-hot core in them and molten drops flung out, arcing down.
            float size=.5f+C01(p.Amount/40f),u=age/.7f;var hot=new Vector3(2.4f,1.7f,.9f);s.Glow(p.At,Vector3.Lerp(hot,deep,u)*3*(1-u)*(1-u),0,(1.2f+u)*size,Core,u,seed);
            int n=Many(10+(int)(p.Amount/4),q,low);for(int i=0;i<n;i++){float h=H(seed+i,17),a=h*6.283f,up=1.5f+3*H(seed+i,18),sp=3.5f+3*H(seed+i,19);
                var v0=new Vector3((float)Math.Cos(a)*sp,up,(float)Math.Sin(a)*sp)*size;float t=age;var at=p.At+v0*t+new Vector3(0,-4.9f*t*t,0);var vel=v0+new Vector3(0,-9.8f*t,0);
                s.Streak(at,vel,Vector3.Lerp(hot,deep,u)*2.4f*(1-u),.035f,.12f,.03f,.9f,u,seed+i);}
        }
        private static void Pour(IKitFxSink s,in Proc p,float age,Vector3 deep,Vector3 light,long seed,float q,bool low)
        {
            // Molten slag spilled where they fell: a glowing pool with a bright rim, crusting dark at the edges as it cools, bubbling and smoking.
            float r=Math.Max(.6f,p.Amount),grow=Smooth(age/.25f),fade=age>2.75f?1-Smooth((age-2.75f)/.5f):1;if(fade<=0)return;var c=p.At+Vector3.UnitY*.03f;var hot=new Vector3(2.2f,1.2f,.4f);
            Rings(s,c,r*grow,.2f,hot*1.8f*fade,.9f*fade,low?18:30);Rings(s,c,r*.72f*grow,r*.72f*grow,deep*1.2f,.55f*fade,low?14:24);
            int n=Many(7,q,low);for(int i=0;i<n;i++){float a=H(seed+i,21)*6.283f,rr=r*.7f*(float)Math.Sqrt(H(seed+i,22))*grow;s.Glow(c+new Vector3((float)Math.Cos(a)*rr,.05f,(float)Math.Sin(a)*rr),hot*(1.2f+.4f*(float)Math.Sin(age*5+i))*fade,0,.7f,Mote,age,seed+i);}
            long slot=(long)(age*6);for(int b=0;b<Many(3,q,low);b++){float h=H(seed+slot*17+b,23),u=age*6-slot;float a=h*6.283f,rr=r*.6f*H(seed+slot*17+b,24);
                var at=c+new Vector3((float)Math.Cos(a)*rr,.05f+.25f*u,(float)Math.Sin(a)*rr);s.Glow(at,hot*2*(1-u)*fade,0,.18f,Mote,u,seed+slot+b);}
            if(!low&&q>=.5f)s.Smoke(c+Vector3.UnitY*.4f,new Vector3(.12f,.1f,.09f),.35f*fade,1.1f,C01(age/3.2f),seed,c.Y);
        }

        // ---- THE HUM -----------------------------------------------------------------------------------------------------------
        private static void Hum(IKitFxSink s,in Proc p,float age,Vector3 deep,Vector3 light,long seed,float q,bool low)
        {
            var tube=new Vector3(1.5f,1.55f,1.25f);
            if(p.Target!=0)
            {
                // The hum building in them: a fluorescent tube's light over them, brighter with every hit (four, then the fifth bursts).
                float k=C01(p.Amount/4f),u=age/.4f,b=Buzz(age,seed,q)*(1-u);var top=p.At+Vector3.UnitY*(1.1f+.1f*k);var l=new Vector3(.32f+.25f*k,0,0);
                s.Line(false,top-l,top+l,tube*(1.2f+1.6f*k)*b,.06f,false);s.Glow(top,light*(1+2*k)*b,0,.6f+.6f*k,Core,u,seed);return;
            }
            // Max Hum-Buzz: a flash of dead white tube light, its ring buzzing out to 3 m, bars of light flung around it.
            float t=age/.8f,f=Buzz(age,seed,q);s.Glow(p.At,tube*3.4f*(1-t)*(1-t)*f,0,2.4f+1.4f*t,Core,t,seed);
            Rings(s,Floor(p.At),.3f+(p.Amount>0?p.Amount:3)*Smooth(t*1.3f),.3f,tube*1.6f*f,(1-t)*.9f,low?18:32);
            int n=Many(10,q,low);for(int i=0;i<n;i++){float a=i/(float)n*6.283f+t*.6f,r0=.4f+2.2f*Smooth(t*1.2f);var d=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));var at=p.At+d*r0+Vector3.UnitY*(.2f*(float)Math.Sin(a*3));
                s.Line(false,at-Side(d)*.3f,at+Side(d)*.3f,tube*1.8f*(1-t)*f,.05f,false);}
        }
        private static void Glitch(IKitFxSink s,in Proc p,float age,Vector3 deep,Vector3 light,long seed,float q,bool low)
        {
            // Noclip: the world glitches where the roll goes through — slices of space knocked sideways, split into cyan and red, then gone.
            bool hit=p.Target!=0;var at=hit?p.At:(p.From==Vector3.Zero?p.At:p.From);var dir=Flat(p.Dir.LengthSquared()>.01f?p.Dir:Vector3.UnitZ);var side=Side(dir);float u=age/.6f,fade=(1-u)*(1-u);
            int n=Many(hit?8:6,q,low);long slot=q>=.5f?(long)(age*20):0;
            for(int i=0;i<n;i++)
            {
                float h=H(seed+i*13+slot,27),y=-.7f+1.7f*H(seed+i,28),w=.35f+.7f*H(seed+i,29),shift=(h-.5f)*(hit?.9f:.6f);var c=at+Vector3.UnitY*y+side*shift;var half=side*w;var up=Vector3.UnitY*(.04f+.06f*H(seed+i,30));
                s.Band(false,c-half-up,c+half-up,c+half+up,c-half+up,.6f*fade,.6f*fade,light*1.4f);
                s.Line(false,c-half+side*.05f,c+half+side*.05f,new Vector3(.2f,1.4f,1.6f)*fade,.02f,false);s.Line(false,c-half-side*.05f,c+half-side*.05f,new Vector3(1.6f,.25f,.35f)*fade,.02f,false);
            }
            if(hit){s.Glow(at,light*2.4f*fade,0,1.2f,Core,u,seed);Rings(s,Floor(at),.3f+1.4f*Smooth(u*1.5f),.12f,light*2,fade,low?14:24);}
        }

        // ---- THE DEEP END ------------------------------------------------------------------------------------------------------
        private static void Undertow(IKitFxSink s,in Proc p,float age,Vector3 deep,Vector3 light,long seed,float q,bool low)
        {
            // Water takes hold of them and drags them in: spray streaming toward the pilot, and on the 4th a swirl round them.
            bool big=p.Amount>=3;float life=big?.8f:.5f,u=age/life,fade=1-u;var to=p.From==Vector3.Zero?p.At:p.From;var d=Flat(to-p.At);int n=Many(big?14:7,q,low);
            for(int i=0;i<n;i++){float h=H(seed+i,33),born=h*.15f,v=(age-born)/(life*.8f);if(v<=0||v>=1)continue;var at=p.At+Side(d)*((H(seed+i,34)-.5f)*.9f)+Vector3.UnitY*(-.6f+1.1f*H(seed+i,35))+d*(v*(big?2.6f:1.2f));
                s.Streak(at,d*(9+5*h)+Vector3.UnitY*1.5f,Vector3.Lerp(light,deep,v)*1.8f*(1-v),.03f,.25f,.05f,.85f,v,seed+i);}
            s.Glow(p.At,light*1.6f*fade,0,.9f,Core,u,seed);
            if(big){var f=Floor(p.At);Rings(s,f,.5f+.4f*u,.18f,deep*2,fade*.9f,low?14:24);for(int i=0;i<Many(10,q,low);i++){float a=i*.628f+age*9,r=.9f-.4f*u;s.Glow(f+new Vector3((float)Math.Cos(a)*r,.15f+.3f*u,(float)Math.Sin(a)*r),light*1.4f*fade,0,.25f,Mote,u,seed+i);}}
        }
        private static void Warm(IKitFxSink s,in Proc p,float age,Vector3 deep,Vector3 light,long seed,float q,bool low)
        {
            // Still for a second: the water's warmth comes up around their feet, a pool lamp's soft ring and bubbles rising.
            float u=age/1.8f,fade=Smooth(age/.3f)*(1-Smooth((age-1.1f)/.7f));var f=Floor(p.At);var warm=Vector3.Lerp(light,new Vector3(1.4f,1.3f,1f),.35f);
            Rings(s,f,1.1f+.12f*(float)Math.Sin(age*3.2f),.45f,warm*1.2f,.55f*fade,low?18:30);s.Glow(f+Vector3.UnitY*.1f,deep*1.4f*fade,0,1.8f,Core,u,seed);
            int n=Many(10,q,low);for(int i=0;i<n;i++){float h=H(seed+i,37),born=h*.9f,v=(age-born)/.9f;if(v<=0||v>=1)continue;float a=H(seed+i,38)*6.283f,r=.25f+.7f*H(seed+i,39);
                var at=f+new Vector3((float)Math.Cos(a)*r+.06f*(float)Math.Sin(age*7+i),.1f+1.4f*v,(float)Math.Sin(a)*r);s.Glow(at,light*1.6f*(1-v)*fade,0,.14f+.08f*h,Mote,v,seed+i);}
        }

        // ---- NOCTIEL -----------------------------------------------------------------------------------------------------------
        private static void Feathers(IKitFxSink s,Vector3 centre,float age,float life,float spread,Vector3 color,long seed,int n)
        {
            for(int i=0;i<n;i++){float h=H(seed+i,41),born=h*.3f,v=(age-born)/(life-born);if(v<=0||v>=1)continue;float a=H(seed+i,42)*6.283f,r=spread*(.3f+.7f*H(seed+i,43));
                var at=centre+new Vector3((float)Math.Cos(a)*r+.3f*(float)Math.Sin(v*6+i),2.6f-2.4f*v,(float)Math.Sin(a)*r);float tilt=(float)Math.Sin(v*5+h*6)*.6f;
                var along=new Vector3((float)Math.Cos(a+tilt)*.16f,.05f,(float)Math.Sin(a+tilt)*.16f);s.Line(false,at-along,at+along,color*1.8f*(1-v),.05f,false);}
        }
        private static void Feather(IKitFxSink s,in Proc p,float age,Vector3 deep,Vector3 light,long seed,float q,bool low)
        {
            // A soft landing that rings with light: a ring of it out to 4 m, a flash where they touch down, feathers drifting down after.
            float u=age/1.1f;var gold=new Vector3(1.9f,1.6f,1.05f);var f=p.At;
            Rings(s,f,.3f+3.7f*Smooth(u*1.25f),.3f,Vector3.Lerp(gold,light,u)*2,(float)Math.Pow(1-u,1.3)*.95f,low?20:36);Rings(s,f,.2f+2.6f*Smooth(u*1.5f),.12f,light*2,(1-u)*.6f,low?16:28);
            if(age<.35f)s.Glow(f+Vector3.UnitY*.3f,gold*3*(1-age/.35f),0,1.8f,Core,u,seed);
            Feathers(s,f,age,1.1f,2.2f,light,seed,Many(8,q,low));
        }
        private static void Lux(IKitFxSink s,in Proc p,float age,Vector3 deep,Vector3 light,long seed,float q,bool low)
        {
            // The light falls once: a beam down onto them from far above, a halo opening over their head, a ring of light out to 5 m, feathers coming down through it.
            float u=age/1.6f,beam=Smooth(age/.12f)*(1-Smooth((age-.5f)/.9f));var gold=new Vector3(2f,1.75f,1.2f);var f=Floor(p.At);var top=f+Vector3.UnitY*16;
            for(int k=0;k<2;k++){var w=(k==0?Vector3.UnitX:Vector3.UnitZ)*(.55f+.35f*(1-beam));s.Band(false,top-w,top+w,f+w*.7f,f-w*.7f,.15f*beam,.9f*beam,gold);}
            s.Glow(p.At,gold*3.4f*beam,0,2.2f,Core,u,seed);
            var halo=p.At+Vector3.UnitY*1.15f;float hr=.32f+.25f*Smooth(age/.6f);int n=Many(18,q,low);
            for(int i=0;i<n;i++){float a=i/(float)n*6.283f+age*.8f;s.Glow(halo+new Vector3((float)Math.Cos(a)*hr,0,(float)Math.Sin(a)*hr),gold*1.8f*(1-u),0,.16f,Mote,u,seed+i);}
            Rings(s,f,.4f+4.6f*Smooth(u*1.4f),.35f,Vector3.Lerp(gold,light,u)*2.2f,(float)Math.Pow(1-u,1.2)*.95f,low?20:36);
            Feathers(s,f,age,1.6f,2.6f,light,seed+99,Many(12,q,low));
        }
    }
}

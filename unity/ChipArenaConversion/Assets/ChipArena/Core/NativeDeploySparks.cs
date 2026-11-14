using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Deployment Station's slot sparks (Claude, 2026-11-02; owner: "add like spark effects when rolling and landing on
    // things? (Maybe a little effect if something benefits the players, or in a sense.. screws them)"). Presentation only and
    // stateless: every spark is a function of this client's time since it saw the roll (the reels' own clock, so a late joiner
    // sees none and any frame of a roll is reproducible); NativeDeployStage draws them as camera-facing additive streaks.
    //  - While a reel spins: grinder sparks, thin as it spins up, a stream at cruise, a spray as the brake bites (StopTime -
    //    Decel to StopTime). The instance machine's are hot orange off each window's lower lip; the mutator machine's are
    //    short blue-white crackles off each dial's hub.
    //  - As a destination reel lands: a burst off its window's rim in the destination's colour, a flash in the window.
    //  - As a mutator card lands, by polarity: a BOON throws a fountain of gold and green with slow glints rising after it; a
    //    BANE shorts out (red sparks spraying down hard, arcs crackling round the card, the flash and the cabinet stuttering);
    //    CHAOS spins a violet vortex out of the dome, its colours cycling. Unknown cards a small grey burst.
    //  - All three cards of one polarity: the sign over the dome erupts in it at SweepAt (a jackpot, or a disaster).
    //  - Reduced effects: about a third of the sparks, no arcs, no stutter (the flashes ease out instead).
    //  - Schedule/Heard give the same moments for sound (Astra's audio plays them; nothing here makes a sound).
    public static class NativeDeploySparks
    {
        public enum Kind:byte{Grinder,Burst,Glint,Arc,Flash}
        /// <summary>One spark this frame (station frame): its centre, its streak (its motion over Streak s, or an arc's segment;
        /// zero: round), its width and its colour (linear HDR, additive).</summary>
        public struct Spark{public Vector3 At,Axis;public float Width;public Vector4 Color;public Kind Kind;}
        public enum CueKind:byte{Spin,Brake,Stop,Card,Sweep}
        /// <summary>A moment of a roll for sound: the machine (0 instance, 1 mutator), the reel (-1 the whole machine), a card's or
        /// the sweep's polarity, and its time after the roll was seen.</summary>
        public struct Cue{public CueKind Kind;public int Machine,Reel;public NativeDeployPresentation.Polarity Polarity;public float At;}

        /// <summary>The most sparks a frame draws (both machines), and the share reduced effects keeps.</summary>
        public const int Limit=160;public const float Reduce=.34f;
        public const float Gravity=9.8f,Streak=.035f,Slot=1/60f;
        /// <summary>The instance machine's windows (local, front -z): centre height, size and the lip's plane.</summary>
        public const float WindowY=1.72f,WindowWidth=.48f,WindowHeight=.56f,LipZ=-.55f;
        /// <summary>The mutator machine's dome front (local z at the cards' height), the dials' hubs, the sign over the dome.</summary>
        public const float DomeZ=-.37f,HubY=1.16f,HubZ=.03f,SignY=2.34f,SignTop=2.58f,SignZ=.66f;
        /// <summary>When the sweep erupts after a mutator roll is seen (all three cards of one polarity).</summary>
        public static float SweepAt=>NativeDeployPresentation.StopTime(NativeDeployPresentation.Reels-1)+.12f;
        /// <summary>The latest any spark of a roll lives (after this the machines draw none).</summary>
        public static float End=>SweepAt+2.2f;

        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Frac(float x)=>x-(float)Math.Floor(x);
        // A hash of three ints in [0,1): every spark's chances, fixed per roll frame.
        private static float H(int a,int b,int c)
        {
            unchecked{uint h=(uint)a*0x9E3779B1u+(uint)b*0x85EBCA77u+(uint)c*0xC2B2AE3Du+0x27D4EB2Fu;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return (h>>8)*(1f/16777216f);}
        }
        private static Vector4 C(float r,float g,float b,float level)=>new Vector4(r*level,g*level,b*level,1);
        private static Vector4 Hex(int hex,float level){var c=WeaponPaint.Linear(hex);return new Vector4(c.X*level,c.Y*level,c.Z*level,1);}
        private static Vector4 Mix(Vector4 a,Vector4 b,float t){t=Clamp01(t);return a+(b-a)*t;}
        // A spark cooling over its life a (0..1): white-yellow, orange, dull red.
        private static Vector4 Hot(float a,float level)
        {
            var c=a<.3f?Mix(C(1,.92f,.7f,1),C(1,.6f,.18f,1),a/.3f):a<.7f?Mix(C(1,.6f,.18f,1),C(1,.3f,.06f,1),(a-.3f)/.4f):Mix(C(1,.3f,.06f,1),C(.7f,.1f,.02f,1),(a-.7f)/.3f);
            float i=level*(1-a*a*a);return new Vector4(c.X*i,c.Y*i,c.Z*i,1);
        }
        private static int Put(Spark[] into,int count,in Matrix4x4 root,Vector3 p,Vector3 axis,float width,Vector4 color,Kind kind)
        {
            if(count>=into.Length||color.X+color.Y+color.Z<=1e-3f)return count;
            into[count]=new Spark{At=Vector3.Transform(p,root),Axis=Vector3.TransformNormal(axis,root),Width=width,Color=color,Kind=kind};return count+1;
        }
        // A ballistic spark `age` s after leaving `o` at `v` (local; the machines only turn about y, so local up is up).
        private static int Fly(Spark[] into,int count,in Matrix4x4 root,Vector3 o,Vector3 v,float age,float fall,float width,Vector4 color,Kind kind)
        {
            float g=Gravity*fall;var p=o+v*age+new Vector3(0,-.5f*g*age*age,0);var vel=v+new Vector3(0,-g*age,0);
            return Put(into,count,root,p,vel*Streak,width,color,kind);
        }
        private static int N(int full,bool reduced)=>reduced?Math.Max(1,(int)Math.Round(full*Reduce)):full;

        // ---- spinning: the grinder ----------------------------------------------------------------------------------------
        /// <summary>Grinder sparks per second off reel `reel` at `t` s into a roll: up with the spin, a stream at cruise, a spray
        /// while the brake bites (fading as the reel slows), none once it lands.</summary>
        public static float GrindRate(int reel,float t,bool mutator)
        {
            float stop=NativeDeployPresentation.StopTime(reel),brake=stop-NativeDeployPresentation.Decel;
            if(!(t>=0)||t>=stop)return 0;float cruise=mutator?7:11,bite=mutator?30:46;
            if(t<NativeDeployPresentation.SpinUp)return cruise*t/NativeDeployPresentation.SpinUp;
            if(t<brake)return cruise;float u=(t-brake)/NativeDeployPresentation.Decel;return cruise*(1-u)+bite*(float)Math.Sqrt(Math.Max(0,4*u*(1-u)));
        }
        private static int Grind(Spark[] into,int count,in Matrix4x4 root,int machine,int reel,float since,bool reduced)
        {
            bool mutator=machine==1;float life0=mutator?.1f:.26f,life1=mutator?.12f:.24f;int salt=machine*16+reel;
            int k0=Math.Max(0,(int)Math.Floor((since-life0-life1)/Slot)-1),k1=(int)Math.Floor(since/Slot);
            for(int k=k0;k<=k1;k++)
            {
                float tb=(k+H(salt,k,1))*Slot,age=since-tb;if(age<0)continue;float life=life0+life1*H(salt,k,2);if(age>=life)continue;
                if(H(salt,k,3)>=GrindRate(reel,tb,mutator)*Slot*(reduced?Reduce:1))continue;float a=age/life;
                if(!mutator)
                {
                    var o=new Vector3(NativeDeployModels.ReelAt(reel).X+(H(salt,k,4)-.5f)*(WindowWidth-.04f),WindowY-WindowHeight/2-.012f,LipZ);
                    var v=new Vector3((H(salt,k,5)-.5f)*1.2f,-.4f+2f*H(salt,k,6),-(1f+1.6f*H(salt,k,7)));
                    count=Fly(into,count,root,o,v,age,1,.028f,Hot(a,8),Kind.Grinder);
                }
                else
                {
                    var c=NativeDeployModels.CardAt(reel);float phi=(H(salt,k,4)-.5f)*2.6f,sp=1.2f+1.2f*H(salt,k,5);
                    var o=new Vector3(c.X+(H(salt,k,6)-.5f)*.18f,HubY,HubZ);var v=new Vector3((float)Math.Sin(phi)*sp,.3f+1.2f*H(salt,k,7),-(float)Math.Cos(phi)*sp);
                    count=Fly(into,count,root,o,v,age,.3f,.016f,Mix(C(.7f,.92f,1,6),C(.3f,.5f,1,2),a)*(1-a),Kind.Grinder);
                }
            }
            return count;
        }

        // ---- landing ---------------------------------------------------------------------------------------------------------
        private static int Flash(Spark[] into,int count,in Matrix4x4 root,Vector3 at,float age,float span,float size,Vector4 color)
        {
            if(age<0||age>=span)return count;float u=age/span;return Put(into,count,root,at,Vector3.Zero,size*(.75f+.45f*u),color*((1-u)*(1-u)),Kind.Flash);
        }
        // A destination reel's landing: sparks off its window's rim, outward and toward the room, in the destination's colour.
        private static int Land(Spark[] into,int count,in Matrix4x4 root,int reel,float since,int color,bool reduced)
        {
            float t0=NativeDeployPresentation.StopTime(reel),cx=NativeDeployModels.ReelAt(reel).X;int n=N(20,reduced),salt=64+reel;var tone=Hex(color,1);
            for(int j=0;j<n;j++)
            {
                float age=since-(t0+.05f*H(salt,j,1)),life=.45f+.35f*H(salt,j,2);if(age<0||age>=life)continue;float a=age/life;
                float e=H(salt,j,3)*4,f=Frac(e)-.5f;int side=(int)e;
                var rim=side==0?new Vector2(f*WindowWidth,-WindowHeight/2):side==1?new Vector2(f*WindowWidth,WindowHeight/2):side==2?new Vector2(-WindowWidth/2,f*WindowHeight):new Vector2(WindowWidth/2,f*WindowHeight);
                var out2=Vector2.Normalize(rim+new Vector2(0,1e-3f));float sp=.8f+1.4f*H(salt,j,4);
                var v=new Vector3(out2.X*sp,out2.Y*sp+.6f,-(.8f+1.2f*H(salt,j,5)));
                count=Fly(into,count,root,new Vector3(cx+rim.X,WindowY+rim.Y,LipZ),v,age,1,.024f,Mix(C(1,1,1,1),tone,a*2)*(6*(float)Math.Pow(1-a,1.5)),Kind.Burst);
            }
            return Flash(into,count,root,new Vector3(cx,WindowY,LipZ-.02f),since-t0,.35f,.9f,tone*2.5f);
        }
        // A BOON: a fountain of gold and green, then slow glints rising and twinkling.
        private static int Fountain(Spark[] into,int count,in Matrix4x4 root,Vector3 o,float spread,float t0,float since,int salt,int full,bool reduced)
        {
            var gold=C(1,.8f,.3f,1);var green=Hex(NativeDeployPresentation.PolarityColor(NativeDeployPresentation.Polarity.Boon),1);int n=N(full,reduced);
            for(int j=0;j<n;j++)
            {
                float age=since-(t0+.14f*H(salt,j,1)),life=.7f+.4f*H(salt,j,2);if(age<0||age>=life)continue;float a=age/life;
                var v=new Vector3((H(salt,j,3)-.5f)*1.4f,2.6f+1.6f*H(salt,j,4),-(.3f+.9f*H(salt,j,5)));var at=o+new Vector3((H(salt,j,6)-.5f)*spread,0,0);
                count=Fly(into,count,root,at,v,age,1,.024f,Mix(C(1,1,.9f,1),j%2==0?gold:green,a*2.5f)*(5*(float)Math.Pow(1-a,1.2)),Kind.Burst);
            }
            int g=N(full*3/11,reduced);
            for(int j=0;j<g;j++)
            {
                float age=since-(t0+.06f+.35f*H(salt,j,11)),life=1.1f+.5f*H(salt,j,12);if(age<0||age>=life)continue;float a=age/life;
                var at=o+new Vector3((H(salt,j,13)-.5f)*(spread+.8f),(H(salt,j,14)-.5f)*.3f,-.05f);var v=new Vector3((H(salt,j,15)-.5f)*.25f,.35f+.4f*H(salt,j,16),-(.05f+.15f*H(salt,j,17)));
                float twinkle=.35f+.65f*Math.Abs((float)Math.Sin(age*16+j*1.7f)),fade=Clamp01(age/.1f)*(1-a);
                count=Put(into,count,root,at+v*age,Vector3.Zero,.06f,C(1,.85f,.4f,3*twinkle*fade),Kind.Glint);
            }
            return count;
        }
        // A BANE: a short circuit. Red sparks spray out of `o` and fall hard; arcs crackle round `arc` (re-struck 20 times a second).
        private static int Short(Spark[] into,int count,in Matrix4x4 root,Vector3 o,float spread,Vector3 arc,float radius,float t0,float since,int salt,int full,bool reduced)
        {
            int n=N(full,reduced);
            for(int j=0;j<n;j++)
            {
                float age=since-(t0+.03f*H(salt,j,1)),life=.35f+.3f*H(salt,j,2);if(age<0||age>=life)continue;float a=age/life;
                var v=new Vector3((H(salt,j,3)-.5f)*3.2f,-1.2f+2.2f*H(salt,j,4),-(1f+1.4f*H(salt,j,5)));var at=o+new Vector3((H(salt,j,6)-.5f)*spread,0,0);
                var c=a<.4f?Mix(C(1,.75f,.5f,1),C(1,.22f,.08f,1),a/.4f):Mix(C(1,.22f,.08f,1),C(.55f,.03f,.02f,1),(a-.4f)/.6f);
                count=Fly(into,count,root,at,v,age,1.4f,.022f,c*(6*(1-a)),Kind.Burst);
            }
            float since0=since-t0;if(reduced||since0<0||since0>=.55f)return count;
            int step=(int)Math.Floor(since0*20);float fade=1-since0/.55f;
            for(int b=0;b<3;b++)
            {
                if(H(salt,step,40+b)<.3f)continue;float a0=6.2832f*H(salt,step,50+b),a1=a0+.9f+1.6f*H(salt,step,60+b);
                var p=arc+new Vector3((float)Math.Cos(a0)*radius,(float)Math.Sin(a0)*radius,0);var q=arc+new Vector3((float)Math.Cos(a1)*radius,(float)Math.Sin(a1)*radius,0);
                var last=p;var side=Vector3.Normalize(Vector3.Cross(q-p,Vector3.UnitZ)+new Vector3(0,0,1e-4f));
                for(int s=1;s<=4;s++)
                {
                    var next=s==4?q:Vector3.Lerp(p,q,s/4f)+side*((H(salt,step,70+b*8+s)-.5f)*.1f);
                    count=Put(into,count,root,(last+next)/2,next-last,.014f,C(1,.45f,.5f,5*fade),Kind.Arc);last=next;
                }
            }
            return count;
        }
        // CHAOS: a vortex spinning out of `o` (alternate reels turn opposite ways), its colours cycling violet, magenta, cyan.
        private static int Vortex(Spark[] into,int count,in Matrix4x4 root,Vector3 o,float grow,float t0,float since,int salt,int turn,int full,bool reduced)
        {
            int n=N(full,reduced);
            for(int j=0;j<n;j++)
            {
                float age=since-(t0+.12f*H(salt,j,1)),life=.6f+.35f*H(salt,j,2);if(age<0||age>=life)continue;float a=age/life;
                float th=j*6.2832f/n+.4f*H(salt,j,3)+turn*age*7,rad=.06f+grow*age*(1+.2f*H(salt,j,4)),fwd=.3f+.6f*H(salt,j,5);
                float cs=(float)Math.Cos(th),sn=(float)Math.Sin(th);
                var p=o+new Vector3(cs*rad,sn*rad-.5f*Gravity*.25f*age*age,-fwd*age);
                var vel=new Vector3(-sn,cs,0)*(rad*7*turn)+new Vector3(cs,sn,0)*grow+new Vector3(0,-Gravity*.25f*age,-fwd);
                float hue=Frac(j/3f+age*1.5f)*3;var c=hue<1?Mix(C(.78f,.45f,1,1),C(1,.28f,.86f,1),hue):hue<2?Mix(C(1,.28f,.86f,1),C(.35f,.88f,1,1),hue-1):Mix(C(.35f,.88f,1,1),C(.78f,.45f,1,1),hue-2);
                count=Put(into,count,root,p,vel*Streak,.022f,c*(5*(1-a)),Kind.Burst);
            }
            return count;
        }
        // An unknown card: a small grey burst.
        private static int Fizz(Spark[] into,int count,in Matrix4x4 root,Vector3 o,float t0,float since,int salt,bool reduced)
        {
            int n=N(10,reduced);
            for(int j=0;j<n;j++)
            {
                float age=since-t0,life=.4f+.25f*H(salt,j,2);if(age<0||age>=life)continue;float a=age/life,th=6.2832f*H(salt,j,3),sp=.6f+.8f*H(salt,j,4);
                count=Fly(into,count,root,o,new Vector3((float)Math.Cos(th)*sp,(float)Math.Sin(th)*sp,-.6f),age,.6f,.02f,C(.75f,.8f,.9f,4*(1-a)),Kind.Burst);
            }
            return count;
        }
        // A card's landing by its polarity, out of the dome in front of card `card` (local), and its flash.
        private static int Card(Spark[] into,int count,in Matrix4x4 root,NativeDeployPresentation.Polarity p,Vector3 card,float t0,float since,int salt,int turn,bool reduced)
        {
            var o=new Vector3(card.X,card.Y,DomeZ);float age=since-t0;var tone=Hex(NativeDeployPresentation.PolarityColor(p),2.6f);
            switch(p)
            {
                case NativeDeployPresentation.Polarity.Boon:count=Fountain(into,count,root,o,.1f,t0,since,salt,22,reduced);break;
                case NativeDeployPresentation.Polarity.Bane:count=Short(into,count,root,o,.1f,card-new Vector3(0,0,.06f),.22f,t0,since,salt,20,reduced);if(!reduced&&age<.3f&&Frac(age*14)>=.5f)tone*=.2f;break;
                case NativeDeployPresentation.Polarity.Chaos:count=Vortex(into,count,root,o,1f,t0,since,salt,turn,24,reduced);if(!reduced)tone*=.75f+.25f*(float)Math.Sin(age*30);break;
                default:count=Fizz(into,count,root,o,t0,since,salt,reduced);break;
            }
            return Flash(into,count,root,o,age,.4f,.85f,tone);
        }

        // ---- per machine ---------------------------------------------------------------------------------------------------
        /// <summary>The instance machine's sparks `since` s after its roll was seen (machine root `root`; `colors` the three
        /// destinations' colours): adds them to `into` from `count` and returns the new count (never past into.Length).</summary>
        public static int Instance(Spark[] into,int count,float since,in Matrix4x4 root,int[] colors,int reels,bool reduced)
        {
            if(!(since>=0)||since>=End||into==null)return count;
            for(int r=0;r<reels&&r<NativeDeployPresentation.Reels;r++){count=Grind(into,count,root,0,r,since,reduced);count=Land(into,count,root,r,since,colors!=null&&r<colors.Length?colors[r]:NativeDeployPresentation.UnknownColor,reduced);}
            return count;
        }
        /// <summary>The mutator machine's sparks `since` s after its roll was seen (`cards` the rolled cards' polarities, `reels`
        /// how many were rolled): the dials' crackle, each card's landing by its polarity, and the sweep.</summary>
        public static int Mutator(Spark[] into,int count,float since,in Matrix4x4 root,NativeDeployPresentation.Polarity[] cards,int reels,bool reduced)
        {
            if(!(since>=0)||since>=End||into==null||cards==null)return count;reels=Math.Min(reels,Math.Min(cards.Length,NativeDeployPresentation.Reels));
            for(int r=0;r<reels;r++)
            {
                count=Grind(into,count,root,1,r,since,reduced);var c=NativeDeployModels.CardAt(r);
                count=Card(into,count,root,cards[r],c,NativeDeployPresentation.StopTime(r),since,96+r,r%2==0?1:-1,reduced);
            }
            if(Sweep(cards,reels,out var p))
            {
                float t0=SweepAt;
                switch(p)
                {
                    case NativeDeployPresentation.Polarity.Boon:count=Fountain(into,count,root,new Vector3(0,SignTop,SignZ-.08f),1.8f,t0,since,120,36,reduced);break;
                    case NativeDeployPresentation.Polarity.Bane:count=Short(into,count,root,new Vector3(0,SignY,SignZ-.1f),1.8f,new Vector3(0,SignY,SignZ-.03f),.6f,t0,since,120,36,reduced);break;
                    default:count=Vortex(into,count,root,new Vector3(0,SignY,SignZ-.1f),1.6f,t0,since,120,1,40,reduced);break;
                }
                count=Flash(into,count,root,new Vector3(0,SignY,SignZ-.12f),since-t0,.5f,2.2f,Hex(NativeDeployPresentation.PolarityColor(p),2.2f));
            }
            return count;
        }
        /// <summary>Whether all `reels` cards (three) share one known polarity: the sweep and its polarity.</summary>
        public static bool Sweep(NativeDeployPresentation.Polarity[] cards,int reels,out NativeDeployPresentation.Polarity polarity)
        {
            polarity=NativeDeployPresentation.Polarity.Unknown;
            if(cards==null||reels<NativeDeployPresentation.Reels||cards.Length<NativeDeployPresentation.Reels)return false;var p=cards[0];
            for(int r=1;r<NativeDeployPresentation.Reels;r++)if(cards[r]!=p)return false;
            if(p==NativeDeployPresentation.Polarity.Unknown)return false;polarity=p;return true;
        }
        /// <summary>The cabinet's light this frame (1 as lit): a thump as each destination lands; a mutator card's polarity
        /// lifts it (BOON), stutters it (BANE) or wobbles it (CHAOS), the sweep more so. Reduced: the stutter eases out instead.</summary>
        public static float Kick(int machine,float since,NativeDeployPresentation.Polarity[] cards,int reels,bool reduced)
        {
            if(!(since>=0)||since>=End)return 1;float k=1;int n=Math.Min(reels,NativeDeployPresentation.Reels);if(machine==1)n=cards==null?0:Math.Min(n,cards.Length);
            for(int r=0;r<n;r++)
            {
                float age=since-NativeDeployPresentation.StopTime(r);if(age<0||age>=.6f)continue;float fade=(1-age/.6f)*(1-age/.6f);
                k=Polar(k,machine==0?NativeDeployPresentation.Polarity.Unknown:cards[r],age,fade,.35f,machine==0?.45f:.9f,reduced);
            }
            if(machine==1&&Sweep(cards,n,out var p)){float age=since-SweepAt;if(age>=0&&age<1){float fade=(1-age)*(1-age);k=Polar(k,p,age,fade,.6f,1.2f,reduced);}}
            return Math.Max(.1f,k);
        }
        private static float Polar(float k,NativeDeployPresentation.Polarity p,float age,float fade,float stutter,float lift,bool reduced)
        {
            switch(p)
            {
                case NativeDeployPresentation.Polarity.Bane:return reduced?k*(1-.35f*fade):age<stutter?k*(Frac(age*14)<.5f?.3f:1.1f):k;
                case NativeDeployPresentation.Polarity.Chaos:return reduced?k+.3f*fade:k*(1+.5f*fade*(float)Math.Sin(age*40));
                default:return k+lift*fade;
            }
        }

        // ---- sound -----------------------------------------------------------------------------------------------------------
        /// <summary>A roll's moments in time order (at most 12): the spin, each reel's brake and stop, each mutator card's polarity
        /// sting as it stops, and the sweep. Fills `into` and returns how many.</summary>
        public static int Schedule(Cue[] into,int machine,int reels,NativeDeployPresentation.Polarity[] cards)
        {
            int n=0;if(machine==1)reels=cards==null?0:Math.Min(reels,cards.Length);reels=Math.Min(reels,NativeDeployPresentation.Reels);if(reels<=0||into==null)return 0;
            void Add(CueKind kind,int reel,NativeDeployPresentation.Polarity p,float at){if(n<into.Length)into[n++]=new Cue{Kind=kind,Machine=machine,Reel=reel,Polarity=p,At=at};}
            var U=NativeDeployPresentation.Polarity.Unknown;Add(CueKind.Spin,-1,U,0);
            for(int r=0;r<reels;r++){float stop=NativeDeployPresentation.StopTime(r);Add(CueKind.Brake,r,U,stop-NativeDeployPresentation.Decel);Add(CueKind.Stop,r,U,stop);if(machine==1)Add(CueKind.Card,r,cards[r],stop);}
            if(machine==1&&Sweep(cards,reels,out var p))Add(CueKind.Sweep,-1,p,SweepAt);
            for(int i=1;i<n;i++){var c=into[i];int j=i-1;while(j>=0&&into[j].At>c.At){into[j+1]=into[j];j--;}into[j+1]=c;}
            return n;
        }
        /// <summary>The cues heard between two frames' roll times (`before` exclusive, `now` inclusive), added to `into` from
        /// `count`. A new roll (now before `before`) starts from its spin; an unknown `before` (the first frame seen) hears
        /// only the last quarter second, never a stale roll; an unknown `now` (a late joiner) hears nothing.</summary>
        public static int Heard(Cue[] schedule,int scheduled,float before,float now,Cue[] into,int count)
        {
            if(!(now>=0)||into==null)return count;if(!(before>=0))before=now-.25f;else if(before>now)before=-1e-4f;
            for(int i=0;i<scheduled;i++)if(schedule[i].At>before&&schedule[i].At<=now&&count<into.Length)into[count++]=schedule[i];
            return count;
        }
    }
}

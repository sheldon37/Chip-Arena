using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Belvedere Camp's light and fire each frame (Core NativeCamp's live layout; docs/expedition/BELVEDERE-CAMP.md "Light"):
    /// <list type="bullet">
    /// <item>the fires' flames, halos, embers and smoke;</item>
    /// <item>a soft halo round every lit bulb and lamp head near the eye;</item>
    /// <item>every lamp's, flood's and fire's light on the fence, the plaza and the tents round it through the city's lamp grid
    /// (<see cref="CityLamps"/>, <see cref="CityLevels"/>: Runtime NativeCityLampLight; 2026-11-21: a flat pool disc under each read as
    /// light laid over the floor, not on it, and only the three in the slots lit anything);</item>
    /// <item>the camp's share of the practical light slots (<see cref="LightPos"/>, <see cref="LightRgb"/>, at most three, two with
    /// reduced effects, one on the low graphics tier: NativeLighting.KitLights merges them into the map's weakest slots at the eye),
    /// picked by the hazard kit's own score (NativeKitLightPool.Score) and eased in and out so nothing pops;</item>
    /// <item>the war board's desk lamp, which clicks on while the eye's pilot is within 4 m (<see cref="BoardLamp"/>).</item>
    /// </list>
    /// The photosensitivity rule: nothing here flashes. A fire's light breathes at under 2.4 Hz, and with reduced effects under 0.7 Hz
    /// with no embers; the lamps and bulbs are steady (the camp is home). Pure (the same clock, eye and settings give the same quads)
    /// and allocation free per frame.
    /// </summary>
    public sealed class NativeCampFx
    {
        public const int Capacity=1800,BulbHalos=280,SmokeMax=64;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public const int MaxLights=NativeKitLightPool.MaxBudget;
        public readonly float[] LightPos=new float[MaxLights*4],LightRgb=new float[MaxLights*3];public int LightCount;
        /// <summary>This frame's counts (budget lines and CampChecks).</summary>
        public int Flames,Embers,Smoke,Halos,Pools;
        /// <summary>The war board's desk lamp: on while the eye's pilot stands within <see cref="NativeCamp.BoardLampReach"/> (with 0.3 m
        /// of hysteresis); <see cref="BoardLampClicked"/> is true on the frame it changes (the click's cue).</summary>
        public bool BoardLamp,BoardLampClicked;
        /// <summary>The DEV preview's brazier in the fountain (A1's mess ring; the walk only): its fire and its light join the camp's.</summary>
        public bool Brazier;public static readonly V3 BrazierAt=new V3(0,.92f,0);
        private readonly NativeCamp.Layout layout;
        private readonly NativeKitLight[] sources;private readonly byte[] kind;private readonly int[] seed;private readonly float[] weight,inX,inZ;private readonly int[] slot={-1,-1,-1};
        private readonly V3 board;private readonly int brazier;
        private readonly int[] smokeOrder=new int[SmokeMax];private readonly double[] smokeKey=new double[SmokeMax],sx=new double[SmokeMax],sy=new double[SmokeMax],sz=new double[SmokeMax],ss=new double[SmokeMax],sa=new double[SmokeMax],sw=new double[SmokeMax];private int smokes;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,hx,hz;
        public const byte KindLamp=0,KindFire=1,KindBoard=2;

        public NativeCampFx(NativeCamp.Layout l)
        {
            layout=l??throw new ArgumentNullException(nameof(l));int n=1;foreach(var p in l.Pieces)if(p.Lit)n++;
            sources=new NativeKitLight[n];kind=new byte[n];seed=new int[n];weight=new float[n];inX=new float[n];inZ=new float[n];int i=0;
            foreach(var p in l.Pieces)
            {
                if(!p.Lit)continue;Rgb(p.Hex,out float r,out float g,out float b);
                sources[i]=new NativeKitLight{X=p.Light.x,Y=p.Light.y,Z=p.Light.z,Range=p.Range,R=r*p.Gain,G=g*p.Gain,B=b*p.Gain,Piece=i};
                kind[i]=p.Kind==NativeCamp.Kind.Fire?KindFire:p.Kind==NativeCamp.Kind.WarLamp?KindBoard:KindLamp;seed[i]=p.Seed;
                // into the camp from the fence the piece stands on
                if(p.Side>=0){var nn=NativeCamp.Normal(p.Side);inX[i]=-nn.x;inZ[i]=-nn.z;}
                i++;
                if(p.Kind==NativeCamp.Kind.WarLamp)board=new V3(p.Light.x,0,p.Light.z);
            }
            // the last source: the preview's brazier (off unless Brazier)
            Rgb(NativeCamp.FireHex,out float fr,out float fg,out float fb);brazier=i;
            sources[i]=new NativeKitLight{X=BrazierAt.x,Y=BrazierAt.y+.4f,Z=BrazierAt.z,Range=11,R=fr*1.5f,G=fg*1.5f,B=fb*1.5f,Piece=i};kind[i]=KindFire;seed[i]=97;
        }
        /// <summary>The camp's lights (lamps, floods, the fires, the board lamp; the preview's brazier not counted): what the pick chooses from.</summary>
        public int Sources=>sources.Length-1;
        private static void Rgb(int hex,out float r,out float g,out float b){r=Lin((hex>>16)&255);g=Lin((hex>>8)&255);b=Lin(hex&255);}
        private static float Lin(int c){double v=c/255.0;return (float)(v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4));}
        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        private static double H(int a,int b=0,int c=0){unchecked{return Mix(Mix(Mix((uint)a+0x9E3779B9U)^(uint)b*0x85EBCA6BU)+(uint)c*0xC2B2AE35U)/4294967296.0;}}
        private static double Frac(double v)=>v-Math.Floor(v);

        /// <summary>A fire's light this frame (about 0.7-1.05): two slow swells, under 2.4 Hz; reduced effects, one under 0.7 Hz.</summary>
        public static float FireLevel(int fire,double t,bool reduced)
        {
            double p=H(fire,3)*6.28,q=H(fire,4)*6.28;
            if(reduced)return (float)(.9+.05*Math.Sin(2*Math.PI*.55*t+p));
            return (float)(.88+.09*Math.Sin(2*Math.PI*(1.05+.2*H(fire,5))*t+p)+.06*Math.Sin(2*Math.PI*(2.1+.25*H(fire,6))*t+q));
        }
        /// <summary>The fastest swell any camp light makes (Hz), by setting: what CampChecks holds under 3 Hz.</summary>
        public static double FastestSwell(bool reduced)=>reduced?.55:2.35;

        /// <summary>Fills the frame: the eye (position, right, up), the eye's pilot's feet (null: none, the board lamp stays off), the effects
        /// quality, the low tier and reduced effects.</summary>
        public void Build(double time,double eyeX,double eyeY,double eyeZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,V3? pilot,double quality,bool low,bool reduced,float dt)
        {
            Quads.Clear();Flames=Embers=Smoke=Halos=Pools=0;ex=eyeX;ey=eyeY;ez=eyeZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;
            double hl=Math.Sqrt(rx*rx+rz*rz);hx=hl>1e-6?rx/hl:1;hz=hl>1e-6?rz/hl:0;
            // the board lamp: on within 4 m of the board (with hysteresis), a click on each change
            bool was=BoardLamp;
            if(pilot.HasValue){var d=new V3(pilot.Value.x-board.x,0,pilot.Value.z-board.z).Length;BoardLamp=d<NativeCamp.BoardLampReach-(was?-.3f:0);}else BoardLamp=false;
            BoardLampClicked=was!=BoardLamp;
            Fires(time,quality,low,reduced);
            Lamps(time,low);
            if(!low)Bulbs();
            Pick(time,low,reduced,dt);
        }

        // ---------------- primitives ----------------
        private void Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive)
        {
            double h=size/2,ax=rx*h,ay=ry*h,az=rz*h,bx=ux*h,by=uy*h,bz=uz*h;
            Quads.Add(x-ax-bx,y-ay-by,z-az-bz,x+ax-bx,y+ay-by,z+az-bz,x+ax+bx,y+ay+by,z+az+bz,x-ax+bx,y-ay+by,z-az+bz,r,g,b,a,1,additive,1);
        }
        private void Upright(double x,double y,double z,double w,double h,double r,double g,double b,double a)
        {double ax=hx*w/2,az=hz*w/2;Quads.Add(x-ax,y,z-az,x+ax,y,z+az,x+ax,y+h,z+az,x-ax,y+h,z-az,r,g,b,a,1,true,1);}
        private void Flat(double x,double y,double z,double ax,double az,double bx,double bz,double r,double g,double b,double a)
        {Quads.Add(x-ax-bx,y,z-az-bz,x+ax-bx,y,z+az-bz,x+ax+bx,y,z+az+bz,x-ax+bx,y,z-az+bz,r,g,b,a,1,true,1);}
        private double Dist(double x,double y,double z)=>Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));

        // ---------------- the fires ----------------
        private void Fires(double time,double quality,bool low,bool reduced)
        {
            smokes=0;int embers=reduced?0:NativeBrickfallAtmosphere.Count(9,quality,low),puffs=low?2:NativeBrickfallAtmosphere.Count(4,quality,low);
            for(int i=0;i<sources.Length;i++)
            {
                if(kind[i]!=KindFire||i==brazier&&!Brazier)continue;var s=sources[i];double d=Dist(s.X,s.Y,s.Z);if(d>95)continue;int f=seed[i];bool bowl=i==brazier;
                float k=FireLevel(f,time,reduced);
                // into the camp from the fence the drum stands against
                double nx=inX[i],nz=inZ[i];
                // the flames: a core and four tongues licking up out of the drum's mouth
                for(int t=0;t<5;t++)
                {
                    double ph=Frac(time*(1.5+.4*H(f,t,1))+H(f,t,2)),w=.42*(.62-.08*t)*(1-.3*ph),hgt=.62*(.75+.5*H(f,t,3))*(.7+.6*Math.Sin(ph*Math.PI));
                    double ox=(H(f,t,4)-.5)*.22+(reduced?0:Math.Sin(time*3+t)*.03),oz=(H(f,t,5)-.5)*.12;bool core=t==0;
                    double big=bowl?2.2:1;Upright(s.X+ox*big+nx*.02,s.Y-(bowl?.38:.08),s.Z+oz*big+nz*.02,w*big,hgt*big,1.0,core?.62:.4,core?.22:.08,(core?.78:.52)*k);Flames++;
                }
                Sprite(s.X+nx*.15,s.Y+.25,s.Z+nz*.15,1.5,.95*k,.36*k,.08*k,.17,true);Halos++;
                if(d<45)for(int e=0;e<embers;e++)
                {
                    double life=1.4+H(f,e,6)*1.4,ph=Frac(time/life+H(f,e,7)),y=s.Y+.15+ph*2.4,x=s.X+(H(f,e,8)-.5)*.3+nx*ph*.3+Math.Sin(time*2.1+e)*.18,z=s.Z+(H(f,e,9)-.5)*.3+nz*ph*.3+Math.Cos(time*1.7+e)*.18;
                    double a=(1-ph)*Math.Min(1,ph*8);Sprite(x,y,z,Math.Max(.04,d*.0015),1,.55,.16,a,true);Embers++;
                }
                // smoke: a few soft puffs climbing the fence and leaning off it (queued, drawn far to near)
                for(int k2=0;k2<puffs&&smokes<SmokeMax;k2++)
                {
                    double life=6+H(f,k2,11)*3,ph=Frac(time/life+H(f,k2,12)),rise=ph*5.5,size=.55+ph*2.4,a=.2*(1-ph)*Math.Min(1,ph*5);
                    int q=smokes++;sx[q]=s.X+nx*(.2+ph*1.2)+.35*Math.Sin(ph*4+k2);sy[q]=s.Y+.6+rise;sz[q]=s.Z+nz*(.2+ph*1.2);ss[q]=size;sa[q]=a;sw[q]=Math.Max(0,1-ph*3);
                    smokeKey[q]=Dist(sx[q],sy[q],sz[q]);smokeOrder[q]=q;
                }
            }
            // far to near, premultiplied: the puffs over the plaza read against the lamps
            for(int i=1;i<smokes;i++){int v=smokeOrder[i];double key=smokeKey[v];int j=i-1;while(j>=0&&smokeKey[smokeOrder[j]]<key){smokeOrder[j+1]=smokeOrder[j];j--;}smokeOrder[j+1]=v;}
            for(int n=0;n<smokes;n++){int i=smokeOrder[n];double w=sw[i],r=.05+w*.22,g=.045+w*.08,b=.05+w*.02;Sprite(sx[i],sy[i],sz[i],ss[i],r*sa[i],g*sa[i],b*sa[i],sa[i],false);Smoke++;}
        }

        // ---------------- the lamps' halos and pools ----------------
        private void Lamps(double time,bool low)
        {
            for(int i=0;i<sources.Length;i++)
            {
                if(kind[i]==KindFire)continue;if(kind[i]==KindBoard&&!BoardLamp)continue;var s=sources[i];double d=Dist(s.X,s.Y,s.Z);if(d>140)continue;
                double m=Math.Max(s.R,Math.Max(s.G,s.B));if(m<=0)continue;double r=s.R/m,g=s.G/m,b=s.B/m;
                Sprite(s.X,s.Y,s.Z,Math.Max(s.Range*.09,d*.012),r,g,b,.24,true);Halos++;
            }
        }

        // ---------------- the bulbs' halos (the nearest, up to BulbHalos) ----------------
        private void Bulbs()
        {
            var bulbs=layout.Bulbs;var lit=layout.BulbLit;int n=0;
            Rgb(NativeCamp.BulbHex,out float br,out float bg,out float bb);double m=Math.Max(br,Math.Max(bg,bb));
            for(int i=0;i<bulbs.Count&&n<BulbHalos;i++)
            {
                if(!lit[i])continue;var p=bulbs[i];double d=Dist(p.x,p.y,p.z);if(d>55)continue;
                double fade=d<40?1:(55-d)/15;Sprite(p.x,p.y,p.z,Math.Max(.32,d*.011),br/m,bg/m,bb/m,.3*fade,true);Halos++;n++;
            }
        }

        // ---------------- the city's lamp grid ----------------
        /// <summary>Every light of the camp (lamps, floods, the fires, the board lamp, the preview's brazier last) on the city's lamp
        /// grid, at its place, reach and colour (made once, when the drawer adds the camp to the grid).</summary>
        public NativeCityLamps.Lamp[] CityLamps()
        {
            var o=new NativeCityLamps.Lamp[sources.Length];
            // a lamp, flood or the board's lamp throws its light down (NativeCityLamps.Overhead: sideways its light as before); a fire glows all round
            for(int i=0;i<o.Length;i++){var s=sources[i];var at=new V3(s.X,s.Y,s.Z);float k=1/NativeCityLamps.DownFloor;
                o[i]=kind[i]==KindFire?new NativeCityLamps.Lamp{At=at,Range=s.Range,R=s.R,G=s.G,B=s.B}:NativeCityLamps.Overhead(at,s.Range,s.R*k,s.G*k,s.B*k);}
            return o;
        }
        /// <summary>Each light's level this frame on the grid (`into` as <see cref="CityLamps"/>): the lamps steady, a fire breathing
        /// (<see cref="FireLevel"/>), the board lamp while on, the brazier only in the preview. Allocation free.</summary>
        public void CityLevels(double time,bool reduced,float[] into)
        {
            for(int i=0;i<sources.Length&&i<into.Length;i++)
                into[i]=i==brazier&&!Brazier||kind[i]==KindBoard&&!BoardLamp?0:kind[i]==KindFire?FireLevel(seed[i],time,reduced):1;
        }

        // ---------------- the practical slots ----------------
        private readonly float[] score=new float[64];
        private void Pick(double time,bool low,bool reduced,float dt)
        {
            int budget=low?1:reduced?2:MaxLights,n=Math.Min(sources.Length,score.Length);
            for(int i=0;i<n;i++)score[i]=kind[i]==KindBoard&&!BoardLamp||i==brazier&&!Brazier?0:NativeKitLightPool.Score(sources[i],ex,ey,ez);
            float step=dt<=0?1:Math.Min(1,dt*6);
            // the slots keep their light while it is still among the best; the rest ease out, and the best new ones ease in
            Span<int> want=stackalloc int[MaxLights];
            for(int k=0;k<MaxLights;k++)
            {
                want[k]=-1;if(k>=budget)continue;int best=-1;float bs=1e-4f;
                for(int i=0;i<n;i++){if(score[i]<=bs)continue;bool taken=false;for(int j=0;j<k;j++)if(want[j]==i){taken=true;break;}if(!taken){bs=score[i];best=i;}}
                want[k]=best;
            }
            for(int s=0;s<MaxLights;s++)
            {
                int i=slot[s];if(i<0)continue;bool keep=false;for(int k=0;k<MaxLights;k++)if(want[k]==i){keep=true;want[k]=-1;break;}
                if(keep)weight[i]=Math.Min(1,weight[i]+step);else{weight[i]-=step;if(weight[i]<=0){weight[i]=0;slot[s]=-1;}}
            }
            for(int k=0;k<MaxLights;k++){int c=want[k];if(c<0)continue;for(int s=0;s<MaxLights;s++)if(slot[s]<0){slot[s]=c;weight[c]=dt<=0?1:step;break;}}
            LightCount=0;
            for(int s=0;s<MaxLights;s++)
            {
                int i=slot[s];if(i<0||weight[i]<=0)continue;var l=sources[i];float w=weight[i]*(kind[i]==KindFire?FireLevel(seed[i],time,reduced):1f);int o=LightCount*4,c=LightCount*3;
                LightPos[o]=l.X;LightPos[o+1]=l.Y;LightPos[o+2]=l.Z;LightPos[o+3]=l.Range;LightRgb[c]=l.R*w;LightRgb[c+1]=l.G*w;LightRgb[c+2]=l.B*w;LightCount++;
            }
        }
    }
}

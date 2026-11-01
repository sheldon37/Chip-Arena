using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The topmost surface over each 1 m column of a map (the blocks at least .6 m across, rubble left out: Cyber City's roof
    /// map, Core NativeCyberFxScene, for any map), so rain and snow stop on roofs, awnings and decks and never fall indoors.
    /// Outside the map's box (widened by 4 m) the ground at 0.
    /// </summary>
    public sealed class NativeWeatherCover
    {
        public int MapX,MapZ;public float OriginX,OriginZ;public float[] Roof=Array.Empty<float>();
        public float Top(double x,double z){int i=(int)Math.Floor(x-OriginX),j=(int)Math.Floor(z-OriginZ);if(i<0||j<0||i>=MapX||j>=MapZ)return 0;return Roof[j*MapX+i];}
        public static NativeWeatherCover Build(ContentBlock[] blocks,MapBounds bounds)
        {
            float x0=(float)Math.Floor((bounds?.minX??-40)-4),z0=(float)Math.Floor((bounds?.minZ??-32)-4),x1=(bounds?.maxX??40)+4,z1=(bounds?.maxZ??32)+4;
            var c=new NativeWeatherCover{OriginX=x0,OriginZ=z0,MapX=Math.Max(1,(int)Math.Ceiling(x1-x0)),MapZ=Math.Max(1,(int)Math.Ceiling(z1-z0))};c.Roof=new float[c.MapX*c.MapZ];
            for(int k=0;k<c.Roof.Length;k++)c.Roof[k]=float.NegativeInfinity;
            foreach(var b in blocks??Array.Empty<ContentBlock>())
            {
                if(b==null||b.role=="rubble"||b.size.x<.6f||b.size.z<.6f)continue;float top=b.pos.y+b.size.y/2,bx0=b.pos.x-b.size.x/2,bx1=b.pos.x+b.size.x/2,bz0=b.pos.z-b.size.z/2,bz1=b.pos.z+b.size.z/2;
                for(int j=Math.Max(0,(int)Math.Floor(bz0-z0));j<Math.Min(c.MapZ,(int)Math.Ceiling(bz1-z0));j++)for(int i=Math.Max(0,(int)Math.Floor(bx0-x0));i<Math.Min(c.MapX,(int)Math.Ceiling(bx1-x0));i++)
                {float cx=x0+i+.5f,cz=z0+j+.5f;if(cx>=bx0&&cx<=bx1&&cz>=bz0&&cz<=bz1&&top>c.Roof[j*c.MapX+i])c.Roof[j*c.MapX+i]=top;}
            }
            for(int k=0;k<c.Roof.Length;k++)if(float.IsNegativeInfinity(c.Roof[k]))c.Roof[k]=0;
            return c;
        }
    }

    /// <summary>
    /// The weather (docs/LIGHTING-ADVANCED.md "Weather"; MapLooks `advanced.weather`): Cyber City's rain (Core NativeCyberCityFx
    /// RainFx and SplashFx) made a system any look can use, with drizzle and snow beside it, and the lightning.
    /// <list type="bullet">
    /// <item>Fall (<see cref="Build"/>): streaks (rain, drizzle) or flakes (snow) round the eye, each wrapped into a box of
    /// <see cref="Kind.Radius"/> about the camera so the fall follows it without popping, drifting with the wind, none under
    /// cover (the map's <see cref="NativeWeatherCover"/>) and none too close to the eye; rain's splashes ring out on the topmost
    /// surfaces round the eye. Lit by the scene's ambient and key (NativeFxLight). Counts are the kind's full fall times the
    /// look's amount (the tier's and Reduced effects' shares already in it: NativeAdvancedLooks.Resolve).</item>
    /// <item>Lightning (<see cref="Lightning"/>): about one strike every `interval` seconds (some slots stay dark), each two or
    /// three pulses within half a second; Reduced effects keeps one soft pulse (no strobe). The runtime lights the scene with it
    /// (NativeAdvancedLight.cginc: what sees the sky) and the sky.</item>
    /// </list>
    /// Pure: the same clock, camera, light, state and cover give the same quads (the killcam and every client see the same
    /// rain). Allocation free.
    /// </summary>
    public sealed class NativeWeatherFx
    {
        /// <summary>A kind's fall: full count, box radius and height about the eye, fall speed (m/s), streak length (m), width
        /// (m at 1 m, and per metre of distance), opacity, splashes.</summary>
        public readonly struct Kind
        {
            public readonly int Full,Splashes;public readonly double Radius,Height,Speed,Length,Width,PerMetre,Alpha;public readonly bool Flakes;
            public Kind(int full,double radius,double height,double speed,double length,double width,double perMetre,double alpha,int splashes,bool flakes){Full=full;Radius=radius;Height=height;Speed=speed;Length=length;Width=width;PerMetre=perMetre;Alpha=alpha;Splashes=splashes;Flakes=flakes;}
        }
        /// <summary>Rain (Cyber City's: 720 streaks .55 m long at 11 m/s in a 16 m box, 96 splashes), drizzle and snow.</summary>
        public static readonly Kind RainKind=new Kind(720,16,24,11,.55,.012,.0012,.2,96,false),DrizzleKind=new Kind(900,14,18,4.5,.2,.008,.0009,.13,0,false),SnowKind=new Kind(640,14,16,1.1,0,.06,.0025,.55,0,true);
        public const int Capacity=1800;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        public int Fall,Splashes;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz;
        public static Kind KindOf(string kind)=>kind==NativeAdvancedLooks.Snow?SnowKind:kind==NativeAdvancedLooks.Drizzle?DrizzleKind:RainKind;
        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352dU;x^=x>>15;x*=0x846ca68bU;x^=x>>16;return x;}}
        private static double H(int a,int b=0,int c=0){unchecked{return Mix(Mix(Mix((uint)a+0x9E3779B9U)^(uint)b*0x85EBCA6BU)+(uint)c*0xC2B2AE35U)/4294967296.0;}}
        private static double Frac(double v)=>v-Math.Floor(v);

        /// <summary>
        /// Fills <see cref="Quads"/> with the fall for `kind` at `amount` (0..1) and `wind` (m/s along x, a third of it along z)
        /// round the camera (position, right and up), lit by `light`, stopping at `cover` (null: nothing covers).
        /// </summary>
        public void Build(string kind,double amount,double wind,double time,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,NativeFxLight light,NativeWeatherCover cover)
        {
            Quads.Clear();Fall=Splashes=0;ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;if(string.IsNullOrEmpty(kind)||!(amount>0))return;
            var k=KindOf(kind);int n=(int)Math.Round(k.Full*Math.Min(1,amount));double span=2*k.Radius,wx=wind,wz=wind/3;
            // The fall's colour: the ambient's sky and equator and a little of the key (additive streaks; flakes premultiplied).
            double lr=.05,lg=.06,lb=.09;if(light!=null){lr+=(light.Ambient[0]+light.Ambient[3])*1.4+light.Sr*.22;lg+=(light.Ambient[1]+light.Ambient[4])*1.4+light.Sg*.22;lb+=(light.Ambient[2]+light.Ambient[5])*1.4+light.Sb*.22;}
            for(int i=0;i<n;i++)
            {
                double ox=H(i,1)*span,oz=H(i,2)*span,phase=H(i,3);
                double x=Math.Floor((ex-ox)/span+.5)*span+ox,z=Math.Floor((ez-oz)/span+.5)*span+oz,fall=Frac(time*k.Speed/k.Height+phase),y=ey+k.Height/2-fall*k.Height;
                double drift=fall*k.Height/k.Speed;x+=wx*drift;z+=wz*drift;
                if(k.Flakes){double sway=time*.9+phase*40;x+=Math.Sin(sway)*.35;z+=Math.Cos(sway*.8)*.35;}
                if(cover!=null&&y<cover.Top(x,z)+.05||y<-4)continue;
                double dx=x-ex,dy=y-ey,dz=z-ez,d=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(d<.6||d>k.Radius)continue;
                double a=k.Alpha*Math.Min(1,(d-.6)/1.5)*Math.Min(1,(k.Radius-d)/4);
                if(k.Flakes)
                {
                    double s=Math.Max(k.Width*(.7+.6*H(i,4)),d*k.PerMetre);double fr=Math.Min(1,lr*2.2),fg=Math.Min(1,lg*2.2),fb=Math.Min(1,lb*2.2);
                    Sprite(x,y,z,s,fr*a,fg*a,fb*a,a,false);Fall++;continue;
                }
                double vx=wx,vy=-k.Speed,vz=wz,vl=Math.Sqrt(vx*vx+vy*vy+vz*vz);
                Line(x,y,z,x+vx/vl*k.Length,y+vy/vl*k.Length,z+vz/vl*k.Length,Math.Max(k.Width,d*k.PerMetre),lr*a,lg*a,lb*a,1);Fall++;
            }
            int sp=(int)Math.Round(k.Splashes*Math.Min(1,amount));
            for(int i=0;i<sp;i++)
            {
                double cyc=time/.42+H(i,4),c=Math.Floor(cyc),age=cyc-c;
                double x=ex+(H(i,(int)c,5)-.5)*20,z=ez+(H(i,(int)c,6)-.5)*20,top=cover?.Top(x,z)??0;
                double d=Math.Sqrt((x-ex)*(x-ex)+(top-ey)*(top-ey)+(z-ez)*(z-ez));if(d<.8||d>12)continue;
                double s=.06+age*.32,a=.35*(1-age);Flat(x,top+.025,z,s,lr*1.2*a,lg*1.2*a,lb*1.2*a,1);Splashes++;
            }
        }
        private void Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive)
        {
            double h=size/2,ax=rx*h,ay=ry*h,az=rz*h,bx=ux*h,by=uy*h,bz=uz*h;
            Quads.Add(x-ax-bx,y-ay-by,z-az-bz,x+ax-bx,y+ay-by,z+az-bz,x+ax+bx,y+ay+by,z+az+bz,x-ax+bx,y-ay+by,z-az+bz,r,g,b,a,1,additive,1);
        }
        private void Flat(double x,double y,double z,double half,double r,double g,double b,double a)=>Quads.Add(x-half,y,z-half,x+half,y,z-half,x+half,y,z+half,x-half,y,z+half,r,g,b,a,1,true,1);
        private void Line(double ax,double ay,double az,double bx,double by,double bz,double width,double r,double g,double b,double a)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,mx=(ax+bx)/2-ex,my=(ay+by)/2-ey,mz=(az+bz)/2-ez;
            double sx=dy*mz-dz*my,sy=dz*mx-dx*mz,sz=dx*my-dy*mx,sl=Math.Sqrt(sx*sx+sy*sy+sz*sz);if(sl<1e-9)return;sx*=width/2/sl;sy*=width/2/sl;sz*=width/2/sl;
            Quads.Add(ax-sx,ay-sy,az-sz,bx-sx,by-sy,bz-sz,bx+sx,by+sy,bz+sz,ax+sx,ay+sy,az+sz,r,g,b,a,0,true,1);
        }

        /// <summary>The share of a slot (an `interval`-second stretch) that strikes.</summary>
        public const double StrikeShare=.75;
        /// <summary>
        /// The lightning's level at `time` (0..1): in each `interval`-second slot that strikes (<see cref="StrikeShare"/>, by the
        /// slot's hash with `seed`), a strike at a hashed moment, its pulses each dropping by e every 1/18 s; `reduced` keeps one
        /// pulse that eases in and out over half a second (no strobe).
        /// </summary>
        public static double Lightning(double time,double interval,int seed,bool reduced)
        {
            if(!(interval>.5))return 0;double best=0;double slot=Math.Floor(time/interval);
            for(int back=0;back<2;back++)
            {
                int s=(int)slot-back;if(H(s,seed,11)>=StrikeShare)continue;double t0=(s+.1+.75*H(s,seed,12))*interval,t=time-t0;if(t<0||t>1.2)continue;
                if(reduced){double e=t/.5;best=Math.Max(best,e<1?.6*Math.Sin(Math.PI*e):0);continue;}
                int pulses=H(s,seed,13)<.45?2:3;
                for(int p=0;p<pulses;p++){double tp=p==0?0:p==1?.11+.08*H(s,seed,14):.3+.12*H(s,seed,15),u=t-tp;if(u<0)continue;double lv=(p==0?1:.7)*Math.Exp(-u*18);if(lv>best)best=lv;}
            }
            return best;
        }
    }
}

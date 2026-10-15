using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A backdrop box drawn through NativeBlockLit (world-fixed): centre, half extents, a turn about y (degrees), look and
    /// flags, sRGB colour, emission gain, window light level and its kind (0 structure, 1 a lit window band, 2 a burning floor,
    /// 3 a beacon).</summary>
    public struct NativeCityBackdropBox {public float X,Y,Z,Hx,Hy,Hz,Yaw,Gain,Level;public int Look,Flags,Color;public byte Kind;}
    /// <summary>A distant fire the atmosphere feeds a smoke column and a glow: world place (the burning floor), size.</summary>
    public struct NativeCityBackdropFire {public float X,Y,Z,Size;}

    /// <summary>
    /// The Expedition town's skyline (docs/EXPEDITION-LAYOUT.md "Town dressing"): a dead city at dusk round the town (its blocks
    /// run over x -98..98, z -98..62 since the hub's wilds, exp-layout-3), built once from integer hashes so every client and the
    /// killcam draw the same horizon. Nothing here is sim data and
    /// nothing collides: Runtime/NativeExpeditionCityPresentation.cs draws it as instanced NativeBlockLit boxes on
    /// NativeSceneryBuilder.BackdropLayer (no shadows cast) for map id "expedition-city" only.
    ///  - The ground: street plates from the town's edge out to 320 m, flush with the slab.
    ///  - Blocks on a 24 m street grid from 70 m out to 230 m: mid-rise ruins near the town, towers further out (tallest
    ///    furthest), many with broken crowns; a few windows lit warm, the rest dark.
    ///  - Burning towers: whole floors glowing orange (their smoke columns and flare are the atmosphere's,
    ///    from the fires <see cref="Build(out NativeCityBackdropFire[],bool)"/> returns), aviation beacons on the tallest masts, two cranes stopped mid-job.
    /// Everything stands <see cref="Margin"/> m or more beyond the town's edge (its perimeter blocks), outside its bounds. Unity frame (x east, z
    /// north), metres. Pure and deterministic; allocates only in Build.
    /// </summary>
    public static class NativeExpeditionCityScenery
    {
        /// <summary>The town's edge (its perimeter blocks' outer faces) and how far beyond it the skyline starts.</summary>
        public const double EdgeMinX=-98,EdgeMaxX=98,EdgeMinZ=-98,EdgeMaxZ=62,Margin=4,Ground=320,Reach=250;public const int MaxFires=7;
        private const int Concrete=NativeBlockLooks.ConcreteBoard,Brick=NativeBlockLooks.BrickRed,Plain=NativeBlockLooks.Plain,Tube=NativeCyberCityLooks.Tube,Bank=NativeCyberCityLooks.Bank,Asphalt=NativeBlockLooks.Asphalt;
        private static readonly int[] Tones={0x34303A,0x3C3438,0x2E2E36,0x403634,0x36303C,0x463A36};
        private static readonly int[] Bricks={0x44282A,0x3A2624,0x4E3030};
        private static double H(int a,int b=0,int c=0)=>NativeCyberCityScenery.Hash(a+7919,b,c);
        private static double R(double lo,double hi,int a,int b=0,int c=0)=>lo+(hi-lo)*H(a,b,c);

        private sealed class Sink
        {
            public readonly List<NativeCityBackdropBox> Boxes=new List<NativeCityBackdropBox>();public readonly List<NativeCityBackdropFire> Fires=new List<NativeCityBackdropFire>();
            public void Add(double x,double y,double z,double sx,double sy,double sz,int look,int flags,int color,double gain=1,double level=1,double yaw=0,byte kind=0)
            {if(sx>0&&sy>0&&sz>0)Boxes.Add(new NativeCityBackdropBox{X=(float)x,Y=(float)y,Z=(float)z,Hx=(float)(sx/2),Hy=(float)(sy/2),Hz=(float)(sz/2),Yaw=(float)yaw,Look=look,Flags=flags,Color=color,Gain=(float)gain,Level=(float)level,Kind=kind});}
        }
        /// <summary>The whole static backdrop in build order (ground, blocks, cranes); faces that share a plane with another box's
        /// are drawn apart (NativeSurfaceLayers, backdrop settings).</summary>
        public static NativeCityBackdropBox[] Build(bool layers=true)=>Build(out _,layers);
        /// <summary>The backdrop and its distant fires (each a burning tower's top floor and size).</summary>
        public static NativeCityBackdropBox[] Build(out NativeCityBackdropFire[] fires,bool layers=true)
        {
            var s=new Sink();GroundPlates(s);Blocks(s);Cranes(s);var o=s.Boxes.ToArray();fires=s.Fires.ToArray();if(layers)Layers(o);return o;
        }
        /// <summary>Whether a box's footprint (turned) reaches inside the town's edge grown by `margin` (the checks hold every
        /// standing box beyond <see cref="Margin"/>, the ground plates beyond the edge itself).</summary>
        public static bool Inside(in NativeCityBackdropBox b,double margin=Margin)
        {
            double t=b.Yaw*Math.PI/180,c=Math.Abs(Math.Cos(t)),n=Math.Abs(Math.Sin(t)),ex=c*b.Hx+n*b.Hz,ez=n*b.Hx+c*b.Hz;
            return b.X+ex>EdgeMinX-margin&&b.X-ex<EdgeMaxX+margin&&b.Z+ez>EdgeMinZ-margin&&b.Z-ez<EdgeMaxZ+margin;
        }
        private static bool Clear(double x,double z,double hx,double hz,double margin)=>x+hx<=EdgeMinX-margin||x-hx>=EdgeMaxX+margin||z+hz<=EdgeMinZ-margin||z-hz>=EdgeMaxZ+margin;
        private static void Layers(NativeCityBackdropBox[] o)
        {
            var boxes=new NativeSurfaceLayers.Box[o.Length];
            for(int i=0;i<o.Length;i++){var b=o[i];double h=b.Yaw*Math.PI/360;boxes[i]=new NativeSurfaceLayers.Box{X=b.X,Y=b.Y,Z=b.Z,Hx=b.Hx,Hy=b.Hy,Hz=b.Hz,Qy=Math.Sin(h),Qw=Math.Cos(h),Material=b.Gain==1?NativeSurfaceLayers.Key(b.Look,b.Color,b.Flags):-1};}
            var lifts=NativeSurfaceLayers.Solve(boxes,0,null,false,out _,NativeSurfaceLayers.BackdropLift,NativeSurfaceLayers.Far);
            for(int i=0;i<o.Length;i++)
            {
                NativeSurfaceLayers.Apply(lifts,i,out float sx,out float sy,out float sz,out float gx,out float gy,out float gz);if(gx==0&&gy==0&&gz==0)continue;
                double t=o[i].Yaw*Math.PI/180,c=Math.Cos(t),n=Math.Sin(t);o[i].X+=(float)(c*sx+n*sz);o[i].Y+=sy;o[i].Z+=(float)(-n*sx+c*sz);o[i].Hx+=gx;o[i].Hy+=gy;o[i].Hz+=gz;
            }
        }
        private static void GroundPlates(Sink s)
        {
            // Four plates round the town's slab (its blocks run to the edge), flush with the street.
            const double G=Ground;
            s.Add(0,-.25,(EdgeMaxZ+G)/2,2*G,.5,G-EdgeMaxZ,Asphalt,0,0x2E2C30);s.Add(0,-.25,(EdgeMinZ-G)/2,2*G,.5,G+EdgeMinZ,Asphalt,0,0x2E2C30);
            s.Add((EdgeMaxX+G)/2,-.25,(EdgeMinZ+EdgeMaxZ)/2,G-EdgeMaxX,.5,EdgeMaxZ-EdgeMinZ,Asphalt,0,0x2E2C30);s.Add((EdgeMinX-G)/2,-.25,(EdgeMinZ+EdgeMaxZ)/2,G+EdgeMinX,.5,EdgeMaxZ-EdgeMinZ,Asphalt,0,0x2E2C30);
        }
        private static void Blocks(Sink s)
        {
            // A 24 m grid (12 m streets between 12-20 m lots); lots near the town's edge (and their streets) are the town.
            int id=0;
            for(int gx=-11;gx<11;gx++)for(int gz=-11;gz<11;gz++,id++)
            {
                double cx=gx*24+12,cz=gz*24+12;double outX=Math.Max(0,Math.Max(EdgeMinX-cx,cx-EdgeMaxX)),outZ=Math.Max(0,Math.Max(EdgeMinZ-cz,cz-EdgeMaxZ)),d=Math.Max(outX,outZ);
                if(d<Margin+10||Math.Sqrt(cx*cx+cz*cz)>Reach+40)continue;
                double far=Math.Min(1,d/(Reach-60));int lots=H(id,1)<.35?1:2;
                // Near the town the lots thin out and stay low, so the town's own edge reads against the skyline.
                if(d<34&&H(id,11)<.45)continue;
                for(int l=0;l<lots;l++)
                {
                    double w=lots==1?R(12,18,id,2):R(7,9.5,id,2,l),dep=R(10,18,id,3,l);double ox=lots==1?0:(l==0?-5:5);double bx=cx+ox+R(-1,1,id,4,l),bz=cz+R(-2,2,id,5,l);
                    // Keep the town clear: the nearest face stands Margin beyond its edge.
                    if(!Clear(bx,bz,w/2,dep/2,Margin))continue;
                    double h=R(8,20,id,6,l)+far*far*R(10,95,id,7,l);if(H(id,8,l)<.12)h*=.45;bool brick=h<30&&H(id,9,l)<.55;
                    int tone=brick?Bricks[(int)(H(id,10,l)*Bricks.Length)%Bricks.Length]:Tones[(int)(H(id,10,l)*Tones.Length)%Tones.Length];tone=Shade(tone,1-.4*far);
                    Tower(s,id*4+l,bx,bz,w,dep,h,brick?Brick:Concrete,tone,far);
                }
            }
        }
        // A colour darkened toward the horizon (the far blocks read as silhouettes against the dusk).
        private static int Shade(int hex,double k){int r=(int)(((hex>>16)&255)*k),g=(int)(((hex>>8)&255)*k),b=(int)((hex&255)*k);return r<<16|g<<8|b;}
        private static void Tower(Sink s,int id,double x,double z,double w,double d,double h,int look,int tone,double far)
        {
            // The body; a ruined crown (a stepped stub off one corner) on some; a setback and a mast on the tall ones.
            bool ruin=H(id,20)<.4;double body=ruin?h*R(.7,.9,id,21):h;s.Add(x,body/2,z,w,body,d,look,0,tone);
            if(ruin){double rw=w*R(.35,.6,id,22),rd=d*R(.35,.6,id,23),rx=x+(H(id,24)<.5?-1:1)*(w-rw)/2,rz=z+(H(id,25)<.5?-1:1)*(d-rd)/2;s.Add(rx,body+(h-body)/2,rz,rw,h-body,rd,look,0,tone);
                s.Add(rx+(x-rx)*.4,body+(h-body)*.3,rz,rw*.6,(h-body)*.6,rd*.8,look,0,tone);}
            else if(h>45){double sw=w*.65,sd=d*.65;s.Add(x,h+h*.08,z,sw,h*.16,sd,look,0,tone);if(H(id,26)<.6){s.Add(x,h*1.16+6,z,.6,12,.6,NativeBlockLooks.SteelGreen,0,0x3A3A3C);s.Add(x,h*1.16+12.4,z,.8,.8,.8,Tube,Bank,0xFF2A20,2.4,1,0,3);}}
            // Lit windows: a few warm bands on the faces toward the town (the rest of the city is dark).
            double toward=Math.Abs(x)>Math.Abs(z)?-Math.Sign(x):-Math.Sign(z);bool xFace=Math.Abs(x)>Math.Abs(z);
            int bands=(int)(body/3.4);for(int f=1;f<bands;f++)
            {
                if(H(id,30,f)>.16)continue;double fy=f*3.4+1.4,span=(xFace?d:w)*R(.06,.3,id,31,f),off=((xFace?d:w)-span)/2*(H(id,32,f)*2-1);
                if(xFace)s.Add(x+toward*(w/2+.06),fy,z+off,.1,1.1,span,Tube,Bank,0xFFB060,R(.35,.9,id,33,f),1,0,1);else s.Add(x+off,fy,z+toward*(d/2+.06),span,1.1,.1,Tube,Bank,0xFFB060,R(.35,.9,id,33,f),1,0,1);
            }
            // Burning: one tower in nine, mid-distance and not too tall: two or three floors alight on every face, a fire record.
            if(H(id,40)<.045&&far<.7&&body>14&&s.Fires.Count<MaxFires)
            {
                int top=Math.Max(2,bands-1-(int)(H(id,41)*3));for(int f=top-1;f<=top;f++){double fy=f*3.4+1.2;
                    s.Add(x,fy,z+d/2+.08,w*R(.5,.95,id,42,f),1.6,.12,Tube,Bank,0xFF6A1E,2.2,1,0,2);s.Add(x,fy,z-d/2-.08,w*R(.5,.95,id,43,f),1.6,.12,Tube,Bank,0xFF6A1E,2.2,1,0,2);
                    s.Add(x+w/2+.08,fy,z,.12,1.6,d*R(.5,.95,id,44,f),Tube,Bank,0xFF7A2A,2.0,1,0,2);s.Add(x-w/2-.08,fy,z,.12,1.6,d*R(.5,.95,id,45,f),Tube,Bank,0xFF7A2A,2.0,1,0,2);}
                s.Fires.Add(new NativeCityBackdropFire{X=(float)x,Y=(float)(top*3.4+2.0),Z=(float)z,Size=(float)Math.Max(w,d)});
            }
        }
        private static void Cranes(Sink s)
        {
            // Two tower cranes over unfinished blocks, jibs frozen; red lights at their tips.
            double[][] at={new[]{-150.0,110,30},new[]{140.0,-150,215}};
            foreach(var c in at)
            {
                double x=c[0],z=c[1],yaw=c[2],h=62;var steel=0x8A6A2A;s.Add(x,h/2,z,1.6,h,1.6,NativeBlockLooks.SteelGreen,0,steel);
                double t=yaw*Math.PI/180,jx=Math.Sin(t),jz=Math.Cos(t);s.Add(x+jx*14,h+.6,z+jz*14,1.0,1.2,40,NativeBlockLooks.SteelGreen,0,steel,1,1,yaw);s.Add(x-jx*8,h+.9,z-jz*8,2.2,1.8,6,Concrete,0,0x5A5650,1,1,yaw);
                s.Add(x,h+4,z,.4,8,.4,NativeBlockLooks.SteelGreen,0,steel);s.Add(x+jx*33,h+.6,z+jz*33,.7,.7,.7,Tube,Bank,0xFF2A20,2.6,1,0,3);
            }
        }
    }
}

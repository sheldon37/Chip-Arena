using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A light the town's picker can hand to NativeBlockLit's eight practical slots: place, range, linear colour times
    /// intensity, the kind it came from (NativeExpeditionCityKit Glow*, or 255 a MapLooks practical) and its phase.</summary>
    public struct NativeCityLight {public float X,Y,Z,Range,R,G,B,Param;public byte Kind;public short Look;}

    /// <summary>
    /// The town's practical lights (docs/EXPEDITION-LAYOUT.md "Town dressing"). NativeBlockLit lights blocks and kit from eight
    /// point lights; the town has dozens of sources: the look's own eight (the brazier, the sanctuary lamps, the Meridian portico,
    /// the gate, the burning bus, the ring's sodium lamps) and the kit's (wall lamps, the sanctuary posts, fires, the floods,
    /// beacons, the police bar, neon). Each frame the eight that matter most to the camera take the slots (their strength times
    /// the shader's fall-off at the camera, plus a share for strong lights further off), easing in and out so a change never pops,
    /// as Ward Zero's picker does. Colours follow each source's drive: fire beats, lamps stutter, beacons sweep, the police bar
    /// alternates red and blue. Kit sources within 1.5 m of a look light give way to it. Allocation free after Build.
    /// </summary>
    public sealed class NativeExpeditionCityLights
    {
        public const int Slots=NativeMapLooks.MaxLights;
        public NativeCityLight[] Candidates=Array.Empty<NativeCityLight>();
        public readonly int[] Slot=new int[Slots];public readonly float[] Weight=new float[Slots];public readonly float[] Pos=new float[Slots*4],Rgb=new float[Slots*3];
        private MapLookPractical[] looks=Array.Empty<MapLookPractical>();private float[] score=Array.Empty<float>();private readonly int[] want=new int[Slots];

        public NativeExpeditionCityLights(){for(int i=0;i<Slots;i++)Slot[i]=-1;}
        public static NativeExpeditionCityLights Build(NativeExpeditionCityKit kit,MapLook look)
        {
            var l=new NativeExpeditionCityLights();var list=new List<NativeCityLight>();l.looks=look?.lights??Array.Empty<MapLookPractical>();
            for(short i=0;i<l.looks.Length;i++)
            {
                var p=l.looks[i];if(p?.pos==null)continue;
                list.Add(new NativeCityLight{X=p.pos.x,Y=p.pos.y,Z=p.pos.z,Range=p.range,R=(float)NativeSceneryBuilder.Channel(p.color,16)*p.intensity,G=(float)NativeSceneryBuilder.Channel(p.color,8)*p.intensity,B=(float)NativeSceneryBuilder.Channel(p.color,0)*p.intensity,Kind=255,Look=i});
            }
            int fixedCount=list.Count;
            foreach(var g in kit?.Glows??Array.Empty<NativeCityGlow>())
            {
                if(g.Kind==NativeExpeditionCityKit.GlowNeon&&g.Range<4||g.Kind==NativeExpeditionCityKit.GlowBulbs)continue;bool near=false;
                for(int k=0;k<fixedCount;k++){var c=list[k];double dx=c.X-g.X,dy=c.Y-g.Y,dz=c.Z-g.Z;if(dx*dx+dy*dy+dz*dz<2.25){near=true;break;}}if(near)continue;
                float y=g.Y+(g.Kind==NativeExpeditionCityKit.GlowFire||g.Kind==NativeExpeditionCityKit.GlowBrazier||g.Kind==NativeExpeditionCityKit.GlowWindowFire?.8f:0);
                list.Add(new NativeCityLight{X=g.X,Y=y,Z=g.Z,Range=g.Range,R=g.R,G=g.G,B=g.B,Param=g.Param,Kind=g.Kind,Look=-1});
            }
            l.Candidates=list.ToArray();l.score=new float[l.Candidates.Length];return l;
        }
        /// <summary>A candidate's linear colour times intensity at `time` (pure).</summary>
        public void Color(in NativeCityLight c,double time,out float r,out float g,out float b)
        {
            r=c.R;g=c.G;b=c.B;float k;
            if(c.Kind==255){k=c.Look>=0&&c.Look<looks.Length?(float)NativeMapLooks.Flicker(looks[c.Look],time):1;}
            else switch(c.Kind)
            {
                case NativeExpeditionCityKit.GlowFire:case NativeExpeditionCityKit.GlowBrazier:case NativeExpeditionCityKit.GlowWindowFire:k=NativeExpeditionCityKit.Level(NativeExpeditionCityKit.DriveFire,c.Param,time);break;
                case NativeExpeditionCityKit.GlowLampFlicker:k=NativeExpeditionCityKit.Level(NativeExpeditionCityKit.DriveFlicker,c.Param,time);break;
                case NativeExpeditionCityKit.GlowBeacon:k=.35f+.65f*NativeExpeditionCityKit.Level(NativeExpeditionCityKit.DriveStrobe,c.Param,time)/1.42f;break;
                case NativeExpeditionCityKit.GlowPolice:{bool red=NativeExpeditionCityKit.Level(NativeExpeditionCityKit.DrivePolice,c.Param,time)>.5f;float m=Math.Max(r,Math.Max(g,b));if(red){r=m;g=m*.08f;b=m*.06f;}else{r=m*.1f;g=m*.25f;b=m;}k=1;break;}
                default:k=1;break;
            }
            r*=k;g*=k;b*=k;
        }
        /// <summary>Picks and eases the slots for a camera at (x, y, z); dt 0 snaps (stills). Allocation free.</summary>
        public void Update(double x,double y,double z,double time,float dt,float rate=4)
        {
            int n=Candidates.Length;
            for(int i=0;i<n;i++)
            {
                var c=Candidates[i];Color(c,time,out float r,out float g,out float b);float lum=r*.3f+g*.55f+b*.15f;
                double dx=c.X-x,dy=(c.Y-y)*1.4,dz=c.Z-z,d=Math.Sqrt(dx*dx+dy*dy+dz*dz),u=d/Math.Max(.1,c.Range),near=u<1?(1-u*u)*(1-u*u):0;
                score[i]=lum*(float)(near+.35/(1+d*d/(c.Range*c.Range*.6)));
            }
            for(int k=0;k<Slots;k++){int best=-1;float bs=1e-4f;for(int i=0;i<n;i++){if(score[i]<=bs)continue;bool taken=false;for(int j=0;j<k;j++)if(want[j]==i){taken=true;break;}if(!taken){bs=score[i];best=i;}}want[k]=best;}
            float step=dt<=0?1:Math.Min(1,dt*rate);
            for(int s=0;s<Slots;s++)
            {
                int c=Slot[s];if(c<0)continue;bool keep=false;for(int k=0;k<Slots;k++)if(want[k]==c){keep=true;break;}
                Weight[s]=keep?Math.Min(1,Weight[s]+step):Weight[s]-step;if(Weight[s]<=0){Weight[s]=0;Slot[s]=-1;}
            }
            for(int k=0;k<Slots;k++)
            {
                int c=want[k];if(c<0)continue;bool slotted=false;for(int s=0;s<Slots;s++)if(Slot[s]==c){slotted=true;break;}if(slotted)continue;
                for(int s=0;s<Slots;s++)if(Slot[s]<0){Slot[s]=c;Weight[s]=dt<=0?1:Math.Min(1,step);break;}
            }
            for(int s=0;s<Slots;s++)
            {
                int c=Slot[s];if(c<0){Pos[s*4]=0;Pos[s*4+1]=-10000;Pos[s*4+2]=0;Pos[s*4+3]=1;Rgb[s*3]=Rgb[s*3+1]=Rgb[s*3+2]=0;continue;}
                var l=Candidates[c];Color(l,time,out float r,out float g,out float b);float w=Weight[s];
                Pos[s*4]=l.X;Pos[s*4+1]=l.Y;Pos[s*4+2]=l.Z;Pos[s*4+3]=l.Range;Rgb[s*3]=r*w;Rgb[s*3+1]=g*w;Rgb[s*3+2]=b*w;
            }
        }
        /// <summary>Snaps the slots to the steady pick for a still (renders, a cut).</summary>
        public void Settle(double x,double y,double z,double time){for(int i=0;i<Slots;i++){Slot[i]=-1;Weight[i]=0;}Update(x,y,z,time,0);}
    }
}

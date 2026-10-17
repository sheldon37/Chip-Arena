using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A light the Ironworks picker can hand to the eight practical slots: the kit glow it came from (place, moving frame,
    /// colour times intensity, reach, kind, hazard, phase) and whether it is one of the cool lamps.</summary>
    public struct NativeIronLight {public float X,Y,Z,Range,R,G,B,Param;public byte Kind,Frame;public short Hazard;public bool Cool;}

    /// <summary>
    /// The City Ironworks' practical lights (docs/EXPEDITION-CITY-IRONWORKS.md sections 3.2 and 9.2). NativeBlockLit and
    /// NativeMolten light from eight point lights; the works has dozens of molten sources. The steady ones (furnace mouths,
    /// tapholes, the runners, coke seams, the converters, the pit, the pig bed) are baked into the light field
    /// (NativeIronworksField); the slots carry what moves and what matters at the camera. Each frame <see cref="Warm"/> slots
    /// go to the warm sources that matter most (their strength times the shader's fall-off at the camera, plus a share for strong
    /// lights further off) and <see cref="Cool"/> slots always go to the nearest mercury lamps, whichever furnace is near, so
    /// the floor, the pilots and the enemies' shapes stay readable against the glow (readability rules 1 and 2). Slots ease in
    /// and out so a change never pops. Colours follow each source's drive and its hazard's view (a runner taps and runs, the
    /// pour pours, the slab runs, beacons sweep only while their hazard warns or runs). Allocation free after Build.
    /// </summary>
    public sealed class NativeIronworksLights
    {
        public const int Slots=NativeMapLooks.MaxLights,Cool=2,Warm=Slots-Cool;
        public NativeIronLight[] Candidates=Array.Empty<NativeIronLight>();
        public readonly int[] Slot=new int[Slots];public readonly float[] Weight=new float[Slots];public readonly float[] Pos=new float[Slots*4],Rgb=new float[Slots*3],Beam=new float[Slots];
        private float[] score=Array.Empty<float>();private readonly int[] want=new int[Slots];

        public NativeIronworksLights(){for(int i=0;i<Slots;i++)Slot[i]=-1;}
        /// <summary>The candidates from the kit's glows (deterministic order); the LED digits and the field-only seam glows stay out.</summary>
        public static NativeIronworksLights Build(NativeIronworksKit kit)
        {
            var l=new NativeIronworksLights();var list=new List<NativeIronLight>();
            foreach(var g in kit?.Glows??Array.Empty<NativeIronGlow>())
            {
                if(g.Kind==NativeIronworksKit.GlowLed||g.Kind==NativeIronworksKit.GlowCokeSeam||(g.Kind==NativeIronworksKit.GlowRunner||g.Kind==NativeIronworksKit.GlowReheat)&&g.Channel>=0)continue;
                list.Add(new NativeIronLight{X=g.X,Y=g.Y,Z=g.Z,Range=g.Range,R=g.R,G=g.G,B=g.B,Param=g.Param,Kind=g.Kind,Frame=g.Frame,Hazard=g.Hazard,Cool=g.Kind==NativeIronworksKit.GlowMercury});
            }
            l.Candidates=list.ToArray();l.score=new float[l.Candidates.Length];return l;
        }
        /// <summary>A candidate's level this frame (0 dark .. about 1.3) from its kind, drive and hazard view (pure).</summary>
        public static float Level(in NativeIronLight c,IronworksHazardView[] views,double time)
        {
            var v=c.Hazard>=0&&views!=null&&c.Hazard<views.Length?views[c.Hazard]:default;
            switch(c.Kind)
            {
                case NativeIronworksKit.GlowFurnace:case NativeIronworksKit.GlowCokeDoor:case NativeIronworksKit.GlowConverter:case NativeIronworksKit.GlowHearth:return NativeIronworksKit.Level(NativeIronworksKit.DriveFire,c.Param,time);
                case NativeIronworksKit.GlowTaphole:return NativeIronworksKit.TapGlow(v,time,c.Param);
                case NativeIronworksKit.GlowRunner:return v.Phase==NativeHazardPhase.Active?1:v.Phase==NativeHazardPhase.Warn?.25f+.75f*v.Level:v.Phase==NativeHazardPhase.Clear?.2f+.8f*v.Level:v.Rate==NativeIronworksHazards.RateOff?.12f:.2f;
                case NativeIronworksKit.GlowPour:if(v.Parked)return 0;return v.Phase==NativeHazardPhase.Active?.92f+.08f*NativeIronworksKit.Level(NativeIronworksKit.DriveFire,c.Param,time*2.4):v.Phase==NativeHazardPhase.Clear?v.Level:v.Phase==NativeHazardPhase.Warn?.12f*v.Level:0;
                case NativeIronworksKit.GlowMold:{if(v.Phase==NativeHazardPhase.Active)return 1;double since=v.Phase==NativeHazardPhase.Clear?v.Since:v.Phase==NativeHazardPhase.Idle?v.Since+2:v.Since+20;return .25f+.75f*(float)Math.Exp(-since/24);}
                case NativeIronworksKit.GlowReheat:return NativeIronworksKit.HazardGlow(v,.45f);
                case NativeIronworksKit.GlowSlab:return c.Hazard<0?1:NativeIronworksKit.SlabShown(v)?(v.Phase==NativeHazardPhase.Warn?.4f+.6f*v.Level:1):0;
                case NativeIronworksKit.GlowBillet:return NativeIronworksKit.HazardGlow(v,.7f)*(v.Phase==NativeHazardPhase.Active?1.3f:1);
                case NativeIronworksKit.GlowSlag:return c.Hazard<0?.9f+.1f*NativeIronworksKit.Level(NativeIronworksKit.DriveFire,c.Param,time*.5):v.Phase==NativeHazardPhase.Active?1:v.Phase==NativeHazardPhase.Clear?v.Level*v.Level:v.Phase==NativeHazardPhase.Warn?.15f*v.Level:0;
                case NativeIronworksKit.GlowSodium:return c.Param>0?NativeIronworksKit.Level(NativeIronworksKit.DriveSodium,c.X*.1+c.Z*.07,time):1;
                case NativeIronworksKit.GlowOffice:return NativeIronworksKit.Level(NativeIronworksKit.DriveFlicker,c.Param,time);
                case NativeIronworksKit.GlowBeacon:{bool on=v.Phase==NativeHazardPhase.Warn||v.Phase==NativeHazardPhase.Active;return on?.55f+.45f*(float)Math.Abs(Math.Cos((time*1.1+c.Param)*Math.PI*2)):0;}
                default:return 1;
            }
        }
        /// <summary>
        /// Picks and eases the slots for a camera at (x, y, z): the Warm strongest warm candidates and the Cool best mercury lamps
        /// (a light on the other floor counts for less), ease rate `rate` per second (dt 0 snaps, for stills). Allocation free.
        /// </summary>
        public void Update(double x,double y,double z,IronworksHazardView[] views,double time,float dt,float rate=4)
        {
            int n=Candidates.Length;
            for(int i=0;i<n;i++)
            {
                var c=Candidates[i];float k=Level(c,views,time);if(k<=0){score[i]=0;continue;}Where(c,views,time,out float px,out float py,out float pz);
                float lum=(c.R*.3f+c.G*.55f+c.B*.15f)*k;double dx=px-x,dy=(py-y)*1.6,dz=pz-z,d=Math.Sqrt(dx*dx+dy*dy+dz*dz);
                float floor=Math.Abs((py>7.5f?1:0)-(y>7.5?1:0))>0?.5f:1;double u=d/Math.Max(.1,c.Range),near=u<1?(1-u*u)*(1-u*u):0;
                score[i]=lum*floor*(float)(near+.35/(1+d*d/(c.Range*c.Range*.6)));if(c.Cool)score[i]+=1e-3f;
            }
            for(int k=0;k<Slots;k++)
            {
                bool cool=k>=Warm;int best=-1;float bs=cool?0:1e-4f;
                for(int i=0;i<n;i++){if(Candidates[i].Cool!=cool||score[i]<=bs)continue;bool taken=false;for(int j=0;j<k;j++)if(want[j]==i){taken=true;break;}if(!taken){bs=score[i];best=i;}}
                want[k]=best;
            }
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
                int c=Slot[s];Beam[s]=0;if(c<0){Pos[s*4]=0;Pos[s*4+1]=-10000;Pos[s*4+2]=0;Pos[s*4+3]=1;Rgb[s*3]=Rgb[s*3+1]=Rgb[s*3+2]=0;continue;}
                var l=Candidates[c];float k=Level(l,views,time)*Weight[s];Where(l,views,time,out float px,out float py,out float pz);
                Pos[s*4]=px;Pos[s*4+1]=py;Pos[s*4+2]=pz;Pos[s*4+3]=l.Range;Rgb[s*3]=l.R*k;Rgb[s*3+1]=l.G*k;Rgb[s*3+2]=l.B*k;
            }
        }
        /// <summary>Snaps the slots to the steady pick for a still (renders, a cut).</summary>
        public void Settle(double x,double y,double z,IronworksHazardView[] views,double time){for(int i=0;i<Slots;i++){Slot[i]=-1;Weight[i]=0;}Update(x,y,z,views,time,0);}
        /// <summary>How many of the slots hold a cool lamp now (the rule wants Cool).</summary>
        public int CoolSlots(){int n=0;for(int s=0;s<Slots;s++)if(Slot[s]>=0&&Candidates[Slot[s]].Cool)n++;return n;}
        private static void Where(in NativeIronLight c,IronworksHazardView[] views,double time,out float x,out float y,out float z)
        {
            if(c.Frame==NativeIronworksKit.FrameStatic){x=c.X;y=c.Y;z=c.Z;return;}
            var g=new NativeIronGlow{X=c.X,Y=c.Y,Z=c.Z,Frame=c.Frame};NativeIronworksKit.GlowAt(g,views,time,out x,out y,out z);
        }
    }
}

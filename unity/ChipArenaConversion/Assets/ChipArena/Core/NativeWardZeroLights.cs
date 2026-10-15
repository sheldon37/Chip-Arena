using System;

namespace ChipArena.UnityConversion
{
    /// <summary>A light the picker can hand to NativeBlockLit's eight practical slots: place, range, linear colour times
    /// intensity, and what drives it.</summary>
    public struct NativeWardLight {public float X,Y,Z,Range,R,G,B,Param;public byte Kind,Alarm;public short Hazard;public int Count;}

    /// <summary>
    /// Ward Zero's practical lights (docs/arenas/WARDZERO_PROPOSAL.md section 10.2). NativeBlockLit lights blocks and kit from
    /// eight point lights (MapLooks' `lights`, NativeLighting); one building with two floors has hundreds of fixtures. With the
    /// ceiling light field (<see cref="Build"/> `field` true, NativeWardZeroField: every troffer and high-bay baked) the slots
    /// carry only what moves: the Spine over the Cradle, the Cradle's ink, the specimen tanks, the furnaces, the emergency
    /// beacons, the shutters' and the decon locks' alarms. Without it the troffers are pooled into 8 m cells per floor and the
    /// high-bays join them, as before. Each frame the eight that matter most to the camera take the slots (score: the light's
    /// strength times its fall-off at the camera, plus a share for strong lights further off), easing in and out so a change
    /// never pops. Colours follow the mood (white dims and fails, red beacons turn in an emergency, violet ink grows with the
    /// corruption) and the hazard states; a rotating beacon throws its beam round the room (<see cref="Beam"/>, the shader's
    /// WardBeam). Pure scoring, colour and beam (<see cref="Color"/>, <see cref="BeamOf"/>); the slot easing is this object's
    /// only state. Allocation free after construction.
    /// </summary>
    public sealed class NativeWardZeroLights
    {
        public const int Slots=NativeMapLooks.MaxLights;
        public NativeWardLight[] Candidates=Array.Empty<NativeWardLight>();
        /// <summary>This frame's slots: candidate index (-1 empty), ease weight 0..1, and the output (position, range; colour).</summary>
        public readonly int[] Slot=new int[Slots];public readonly float[] Weight=new float[Slots];
        public readonly float[] Pos=new float[Slots*4],Rgb=new float[Slots*3];
        /// <summary>Each slot's beam this frame (NativeWardLight.cginc WardBeam: 0 a point light, 1 + yaw one beam, 8 + yaw two).</summary>
        public readonly float[] Beam=new float[Slots];
        private float[] score=Array.Empty<float>();private readonly int[] want=new int[Slots];

        public NativeWardZeroLights(){for(int i=0;i<Slots;i++)Slot[i]=-1;}
        /// <summary>Pools the kit's glows into candidates (deterministic order). `field`: the ceiling light field lights the
        /// troffers' and high-bays' pools, so they are left out of the slots.</summary>
        public static NativeWardZeroLights Build(NativeWardZeroKit kit,bool field=false)
        {
            var l=new NativeWardZeroLights();if(kit==null)return l;var list=new System.Collections.Generic.List<NativeWardLight>();
            // Troffers into 8 m cells per floor (the pooled light hangs 0.5 m under the cell's ceiling at its centroid).
            var cells=new System.Collections.Generic.SortedDictionary<long,NativeWardLight>();
            foreach(var g in kit.Glows)
            {
                if(field&&(g.Kind==NativeWardZeroKit.GlowFluor||g.Kind==NativeWardZeroKit.GlowHighBay))continue;
                if(g.Kind==NativeWardZeroKit.GlowFluor)
                {
                    long key=((long)Math.Floor(g.X/8)+100)*1000000+((long)Math.Floor(g.Z/8)+100)*1000+(g.Floor>3?1:0);
                    cells.TryGetValue(key,out var c);c.X+=g.X;c.Y+=g.Y;c.Z+=g.Z;c.Count++;c.Kind=NativeWardZeroKit.GlowFluor;c.Param+=g.Param;cells[key]=c;continue;
                }
                byte k=g.Kind;if(k==NativeWardZeroKit.GlowScreen||k==NativeWardZeroKit.GlowSign)continue;
                float range=k==NativeWardZeroKit.GlowHighBay?16:k==NativeWardZeroKit.GlowSpine?24:k==NativeWardZeroKit.GlowTank?7:k==NativeWardZeroKit.GlowFurnace?(g.Size>4?11:7):k==NativeWardZeroKit.GlowInk?8:k==NativeWardZeroKit.GlowStrobe?(g.Hazard>=0?12:BeaconRange):12;
                list.Add(new NativeWardLight{X=g.X,Y=k==NativeWardZeroKit.GlowFurnace?g.Y+1.2f:g.Y-(k==NativeWardZeroKit.GlowSpine?1.5f:.3f),Z=g.Z,Range=range,R=g.R,G=g.G,B=g.B,Kind=k,Alarm=(byte)(k==NativeWardZeroKit.GlowStrobe&&g.Hazard>=0?(int)g.Param:0),Hazard=g.Hazard,Param=g.Param,Count=1});
            }
            foreach(var c in cells.Values){var x=c;x.X/=c.Count;x.Y=x.Y/c.Count-.5f;x.Z/=c.Count;x.Param/=c.Count;x.Range=c.Y/c.Count>8?13:9.5f;var f=NativeWardZeroLooks.FluorHex;x.R=(float)NativeSceneryBuilder.Channel(f,16);x.G=(float)NativeSceneryBuilder.Channel(f,8);x.B=(float)NativeSceneryBuilder.Channel(f,0);list.Add(x);}
            l.Candidates=list.ToArray();l.score=new float[l.Candidates.Length];return l;
        }
        /// <summary>A candidate's linear colour times intensity this frame (pure).</summary>
        public static void Color(in NativeWardLight c,in NativeWardMood m,NativeHazardState[] states,double time,out float r,out float g,out float b)
        {
            var s=c.Hazard>=0&&states!=null&&c.Hazard<states.Length?states[c.Hazard]:default;float k;r=c.R;g=c.G;b=c.B;
            switch(c.Kind)
            {
                case NativeWardZeroKit.GlowFluor:{k=.42f*(c.Y>8?1.35f:1)*(float)Math.Sqrt(c.Count)*m.Fluor*(1-.35f*m.Flicker)*(1+.15f*m.Beat);float red=m.Red*.7f;r=r*(1-red)+1f*red;g*=1-red*.9f;b*=1-red*.85f;break;}
                case NativeWardZeroKit.GlowHighBay:k=.75f*m.Fluor*(1+.2f*m.Beat);break;
                case NativeWardZeroKit.GlowSpine:{k=1.0f*(.55f+.45f*m.Fluor)+.8f*m.Beat*m.Groove;float ink=m.Ink*.6f;r=r*(1-ink)+.55f*ink;g*=1-ink*.75f;b=b*(1-ink)+1f*ink;break;}
                case NativeWardZeroKit.GlowInk:{k=(.2f+3.2f*m.Ink)*(1+.6f*m.Beat);var v=NativeWardZeroLooks.InkHex;r=(float)NativeSceneryBuilder.Channel(v,16);g=(float)NativeSceneryBuilder.Channel(v,8);b=(float)NativeSceneryBuilder.Channel(v,0);break;}
                case NativeWardZeroKit.GlowTank:k=.6f*(.6f+.4f*m.Fluor)+.3f*m.Beat*m.Groove;break;
                case NativeWardZeroKit.GlowFurnace:k=s.Phase==NativeHazardPhase.Active?2.4f+.4f*(float)Math.Sin(time*19+c.X):s.Phase==NativeHazardPhase.Warn?.7f+1.1f*s.Level:s.Phase==NativeHazardPhase.Clear?.5f+1.2f*s.Level:c.Range<10?.55f:.05f;break;
                case NativeWardZeroKit.GlowStrobe:
                {
                    if(c.Hazard>=0)
                    {
                        // A hazard alarm: a shutter's amber then red, a decon lock's amber then gas green.
                        bool gas=c.Alarm==2;var ph=s.Phase;if(ph==NativeHazardPhase.Idle){k=0;break;}
                        if(ph==NativeHazardPhase.Warn){r=1;g=.55f;b=.05f;k=(Frac(time*3)<.5?1.6f:.3f);}
                        else if(gas){r=.5f;g=1;b=.65f;k=ph==NativeHazardPhase.Active?1.6f:.8f*s.Level;}else{r=1;g=.1f;b=.08f;k=ph==NativeHazardPhase.Clear?.8f:1.7f;}
                        break;
                    }
                    // An emergency beacon: its beam turns round the room (BeamOf), a steady red that pulses on the beat.
                    k=m.Strobe*BeaconPower*(1+.25f*m.Beat);break;
                }
                default:k=.5f;break;
            }
            r*=k;g*=k;b*=k;
        }
        /// <summary>
        /// Picks and eases the slots for a camera at (x, y, z): the eight strongest candidates over distance (a light on another
        /// floor counts for less), ease rate `rate` per second (dt 0 or a large rate snaps, for stills). Allocation free.
        /// </summary>
        public void Update(double x,double y,double z,in NativeWardMood m,NativeHazardState[] states,double time,float dt,float rate=4)
        {
            int n=Candidates.Length;
            for(int i=0;i<n;i++)
            {
                var c=Candidates[i];Color(c,m,states,time,out float r,out float g,out float b);float lum=r*.3f+g*.55f+b*.15f;
                double dx=c.X-x,dy=(c.Y-y)*1.6,dz=c.Z-z,d=Math.Sqrt(dx*dx+dy*dy+dz*dz);float floorPenalty=Math.Abs((c.Y>7.5f?1:0)-(y>7?1:0))>0?.45f:1;
                // What it gives the camera's surroundings: its fall-off at the camera (the shader's), plus a share for a strong
                // light lighting what the camera sees further off.
                double u=d/c.Range,near=u<1?(1-u*u)*(1-u*u):0;
                score[i]=lum*floorPenalty*(float)(near+.35/(1+d*d/(c.Range*c.Range*.6)));
            }
            // The wanted set: the top Slots by score (selection without sorting the whole list).
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
                int c=Slot[s];if(c<0){Pos[s*4]=0;Pos[s*4+1]=-10000;Pos[s*4+2]=0;Pos[s*4+3]=1;Rgb[s*3]=Rgb[s*3+1]=Rgb[s*3+2]=0;Beam[s]=0;continue;}
                var l=Candidates[c];Color(l,m,states,time,out float r,out float g,out float b);float w=Weight[s];
                Pos[s*4]=l.X;Pos[s*4+1]=l.Y;Pos[s*4+2]=l.Z;Pos[s*4+3]=l.Range;Rgb[s*3]=r*w;Rgb[s*3+1]=g*w;Rgb[s*3+2]=b*w;Beam[s]=BeamOf(l,states,time);
            }
        }
        /// <summary>Snaps the slots to the steady pick for a still (the renders, the menu).</summary>
        public void Settle(double x,double y,double z,in NativeWardMood m,NativeHazardState[] states,double time){for(int i=0;i<Slots;i++){Slot[i]=-1;Weight[i]=0;}Update(x,y,z,m,states,time,0);}
        /// <summary>The shader's beam (NativeWardLight.cginc WardBeam, WB_FLOOR) for a slot coded `w` at a point (dx, dz) from
        /// the light: 1 for a point light; else the lamp's glow all round (<see cref="BeamFloor"/>) plus (cos)^6 of the angle
        /// off the beam (one beam at yaw w - 1, or two opposite ones at w - 8).</summary>
        public const float BeamFloor=.12f;
        public static float BeamFactor(float w,double dx,double dz)
        {
            if(w<.5f)return 1;bool two=w>=7.5f;double yaw=w-(two?8:1),l=Math.Max(Math.Sqrt(dx*dx+dz*dz),.0001),c=(dx*Math.Sin(yaw)+dz*Math.Cos(yaw))/l;c=two?Math.Abs(c):Math.Max(c,0);
            double c2=c*c;return BeamFloor+(1-BeamFloor)*(float)(c2*c2*c2);
        }
        /// <summary>An emergency beacon's reach and strength (its beam carries the light round the room).</summary>
        public const float BeaconRange=22,BeaconPower=1.9f;
        /// <summary>A candidate's beam this frame (NativeWardLight.cginc WardBeam): an emergency beacon's one beam, turning as its
        /// effects' beam does (NativeWardZeroFx.BeaconFx); a shutter's alarm turns two beams while it warns, closes or is shut;
        /// every other light is a point light (0). Both turn 1.1 times a second, so a wall sees at most 2.2 sweeps a second
        /// (under the 3 Hz flash limit the hazard kit keeps).</summary>
        public static float BeamOf(in NativeWardLight c,NativeHazardState[] states,double time)
        {
            if(c.Kind!=NativeWardZeroKit.GlowStrobe)return 0;
            if(c.Hazard<0)return 1+(float)(Frac(time*1.1+c.Param)*Math.PI*2);
            var s=states!=null&&c.Hazard<states.Length?states[c.Hazard]:default;if(c.Alarm!=1||s.Phase==NativeHazardPhase.Idle)return 0;
            return 8+(float)(Frac(time*1.1)*Math.PI*2);
        }
        private static double Frac(double v)=>v-Math.Floor(v);
    }
}

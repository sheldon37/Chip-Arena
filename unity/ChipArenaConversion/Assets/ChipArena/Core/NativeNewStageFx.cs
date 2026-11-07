using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The five M1 stages' camera-facing effects for one frame (docs/arenas/NEW-STAGES.md "Stage looks (presentation)") into one
    /// <see cref="NativeFxQuads"/> (Runtime draws it as one effects NativeFxDraw batch) and, while the look's volumetric beams are
    /// on, the frame's <see cref="NativeBeamList"/> (null: their billboard shafts):
    /// <list type="bullet">
    /// <item>Breakwater: the lighthouse's lamp and its two sweeping beams, the spray thrown over the windward (north and west)
    /// parapets by the swell, foam at the wall's foot, the lamps' halos, the buoys' blinks, the glints on the water toward the
    /// last light in the west and toward the lantern.</item>
    /// <item>Pier 9: the floodlights' halos and cones, the cranes' aviation lights, the gantry's telegraph (a red cone over the
    /// slot it serves, strobing through the warning, solid as the load falls) and the landing's dust, the lights' reflections
    /// on the basin, the mist over it.</item>
    /// <item>Skyframe: the haze over the city far below, the safety netting on the open frame under the slab's edges snapping in
    /// the wind, dust motes in the sun, sun glints along the crane's jib, the tower crane's telegraph and landing dust.</item>
    /// <item>Deepwell: the flare's roar (layered flame, its smoke, its glow), the burners (pilot flames and the glowing grate
    /// through the 1.5 s warning, fire columns to the lane's height while they burn, smoke after), the blowdown vents (the
    /// hiss, the steam column, the drifting cloud), spray thrown up the deck's edges, deck lamp halos, the distant flares.</item>
    /// <item>Polaris: the aurora's rayed curtains (green below, violet above, pulses running along them), ground drift streaming
    /// with the wind, the porch and garage lamps' halos, aviation lights.</item>
    /// </list>
    /// Telegraphs (the burners', vents' and cranes' warnings) are never reduced by the effects quality or the low tier
    /// (ARENA_PROPOSAL.md 2.1 rule 4); atmosphere is. The hazards' moments come only from the replicated state (the same
    /// functions NativeNewStageRig poses from). Pure: the same clock, camera, quality and map give the same quads. Allocation free
    /// after <see cref="Prepare"/> (per map).
    /// </summary>
    public sealed class NativeNewStageFx
    {
        public const int Capacity=3200;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        /// <summary>The volumetric beams (null: billboards).</summary>
        public NativeBeamList Beams;
        public int Telegraph,Atmosphere,Lights;
        private int stage=-1;private string prepared;
        // Lamps (x, y, z, sRGB colour, halo size, blink period: 0 steady) and reflections on the water (x, z, colour, height).
        private readonly List<double> lamps=new List<double>();private double[] lamp=Array.Empty<double>();private int lampCount;
        private double ex,ey,ez,rx,ry,rz,ux,uy,uz,quality;private bool low;
        private static readonly double[] Unit={-1,1},CraneLights={-70,72,0},HazeY={-36,-62,-90},HazeA={.10,.14,.18},FarX={-446,534},FarY={53,53},FarZ={560,420};
        private static double H(int a,int b=0,int c=0)=>NativeNewStageLooks.Hash(a,b,c);
        private static double Frac(double v)=>v-Math.Floor(v);
        private static double L(int hex,int shift)=>NativeSceneryBuilder.Channel(hex,shift);

        /// <summary>The map's fixed lamps, once per map (allocates; nothing for another map).</summary>
        public void Prepare(ContentMap map)
        {
            if(map?.id==prepared)return;prepared=map?.id;stage=NativeNewStageLooks.Index(map?.id);lamps.Clear();
            void Lamp(double x,double y,double z,int hex,double size,double blink=0){lamps.Add(x);lamps.Add(y);lamps.Add(z);lamps.Add(hex);lamps.Add(size);lamps.Add(blink);}
            switch(stage)
            {
                case 0:
                    for(int k=0;k<6;k++)foreach(int side in new[]{-1,1})Lamp(-15+k*6,3.68,side*14.38,0xFFC27A,1.1);
                    foreach(double z in new[]{-11.0,-5.5,5.5,11.0})Lamp(21.48,3.68,z,0xFFC27A,1.1);
                    for(double x=-30;x>=-150;x-=15)foreach(int side in new[]{-1,1})Lamp(x,4.38,side*6.45,0xFFC27A,1.0);
                    Lamp(-60,-1.6+2.9,38,0xFF4030,.8,3);Lamp(-60,-1.6+2.9,-40,0x40FF70,.8,3.4);Lamp(70,-1.6+2.9,46,0xFF4030,.8,2.6);Lamp(80,-1.6+2.9,-52,0x40FF70,.8,3.1);
                    Lamp(5.4,-1.6+8.0,-21.5,0xFFF4D8,.6);Lamp(5.4,-1.6+5.2,-19.9,0x30FF60,.4);Lamp(5.4,-1.6+5.2,-23.1,0xFF3020,.4);Lamp(-205,38.4,-30,0xFF3020,1.6,1.5);
                    break;
                case 1:
                    foreach(int side in new[]{-1,1}){for(int k=0;k<3;k++){Lamp(side*(56+k*30),29.7,16,0xFFA64A,4.5);Lamp(side*(56+k*30),25.7,-38,0xFFA64A,4.0);}}
                    for(int i=0;i<6;i++)Lamp(-100+i*40,17.7,-46,0xFFA64A,3.5);
                    foreach(double x in new[]{-70.0,72.0}){Lamp(x,44.6,20,0xFF3020,1.4,1.2);Lamp(x,34.4,58,0xFF3020,1.2,1.2);}
                    Lamp(0,34,23.3,0xFF3020,1.2,1.0);Lamp(0,33,76,0xFF3020,1.0,1.0);
                    Lamp(60,31.8,47.6,0xFFF4D8,.8);Lamp(-87,21.2,47.6,0xFFF4D8,.8);
                    break;
                case 2:
                    Lamp(0,29.6,0,0xFF3020,.9,1.4);Lamp(150,StreetTop(60)+6.3,-110,0xFF3020,1.2,1.4);Lamp(-210,StreetTop(40)+6.3,160,0xFF3020,1.2,1.6);
                    break;
                case 3:
                    foreach(var (x,z) in new[]{(-20.0,-12.0),(-20.0,12.0),(20.0,-12.0),(20.0,12.0),(0.0,-16.0),(0.0,16.0),(-12.0,0.0),(12.0,0.0)})Lamp(x,7.6,z,0xFFA64A,2.2);
                    Lamp(0,23.0,0,0xFF3020,.9,1.3);Lamp(-20.45+.18,2.7,19.82,0x40FF70,.5);Lamp(20.28,2.7,-19.82,0x40FF70,.5);Lamp(10.72,2.7,19.82,0x40FF70,.5);Lamp(-10.72,2.7,-19.82,0x40FF70,.5);
                    Lamp(86,-5+14.2,-90,0xFFF4D8,1.0);Lamp(-412,-5+54.4,520,0xFF3020,2.4,1.5);Lamp(568,-5+54.4,380,0xFF3020,2.4,1.7);
                    break;
                case 4:
                    foreach(var b in map.blocks??Array.Empty<ContentBlock>())
                    {
                        if(b?.id==null)continue;
                        if(b.id.StartsWith("polar-module-",StringComparison.Ordinal)){bool alongX=b.size.x>=b.size.z;double ex0=alongX?(b.pos.x<0?b.pos.x+b.size.x/2+.08:b.pos.x-b.size.x/2-.08):b.pos.x,ez0=alongX?b.pos.z:(b.pos.z<0?b.pos.z+b.size.z/2+.08:b.pos.z-b.size.z/2-.08);Lamp(ex0,b.pos.y-b.size.y/2+2.32,ez0,0xFFC88A,1.0);}
                        if(b.id.StartsWith("polar-garage-",StringComparison.Ordinal)&&b.id.EndsWith("-roof",StringComparison.Ordinal))for(double z=b.pos.z-b.size.z/2+2;z<b.pos.z+b.size.z/2-1;z+=3.5)Lamp(b.pos.x,b.pos.y-b.size.y/2-.1,z,0xFFE0B0,1.4);
                    }
                    Lamp(0,16.6,0,0xFF3020,.9,1.5);Lamp(48,24.4,52,0xFF3020,1.0,1.8);for(int k=0;k<5;k++)Lamp(-120+k*16,18.3,70+H(108,k)*10,0xFF3020,1.2,1.4+k*.13);
                    break;
            }
            lamp=lamps.ToArray();lampCount=lamp.Length/6;
        }
        private static double StreetTop(double above)=>NativeNewStageScenery.StreetY+above;

        /// <summary>
        /// Fills <see cref="Quads"/> for map `mapId` (prepared): `hazards` and the replicated state (round, phase, FightAt) at the
        /// field clock `now`, the render clock `render` for the atmosphere, the camera and its right and up vectors, effects
        /// `quality` (0..1) and the low tier. Telegraphs ignore the quality and the tier.
        /// </summary>
        public void Build(MapHazard[] hazards,int round,NativePhase phase,double fightAt,double now,double render,double camX,double camY,double camZ,double rightX,double rightY,double rightZ,double upX,double upY,double upZ,double q,bool lowTier)
        {
            Quads.Clear();Telegraph=Atmosphere=Lights=0;if(stage<0)return;ex=camX;ey=camY;ez=camZ;rx=rightX;ry=rightY;rz=rightZ;ux=upX;uy=upY;uz=upZ;quality=Math.Max(0,Math.Min(1,q));low=lowTier;
            double clock=NativeFactoryHazards.Clock(phase,fightAt,now);
            // Telegraphs first (they claim the buffer before the atmosphere).
            for(int i=0;i<(hazards?.Length??0);i++)
            {
                var h=hazards[i];if(h==null)continue;
                if(h.kind==NativeFactoryHazards.Crane)CraneFx(h,round,clock,i);
                else if(h.kind==NativeFactoryHazards.Vent)VentFx(h,round,clock,now,i);
                else if(h.kind==NativeWardZeroHazards.Burner)BurnerFx(h,NativeWardZeroHazards.At(h,phase,round,fightAt,now),now,i);
            }
            switch(stage)
            {
                case 0:Lighthouse(render);Spray(render,0);Glints(render,0);break;
                case 1:Floods(render);Glints(render,1);Mist(render);break;
                case 2:Haze(render);Netting(render);Motes(render);CraneGlints(hazards,round,clock,render);break;
                case 3:Flare(render);Spray(render,3);FarFlares(render);Whitecaps(render);break;
                case 4:Aurora(render);Drift(render);break;
            }
            LampHalos(render);
        }
        private int Count(int full)=>low?Math.Max(1,full/3):Math.Max(1,(int)Math.Round(full*(.35+.65*quality)));

        // ---------------- quad helpers ----------------
        private bool Sprite(double x,double y,double z,double size,double r,double g,double b,double a,bool additive,int mode=1)
        {
            double h=size/2,ax=(rx+ux)*h,ay=(ry+uy)*h,az=(rz+uz)*h,bx=(rx-ux)*h,by=(ry-uy)*h,bz=(rz-uz)*h;
            return Quads.Add(x-ax,y-ay,z-az,x+bx,y+by,z+bz,x+ax,y+ay,z+az,x-bx,y-by,z-bz,r,g,b,a,mode,additive,1);
        }
        private bool Hex(double x,double y,double z,double size,int hex,double a,bool additive,double gain=1)=>Sprite(x,y,z,size,L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,a,additive);
        /// <summary>A camera-facing ribbon from a to b, `w0` wide at a and `w1` at b.</summary>
        private bool Streak(double ax,double ay,double az,double bx,double by,double bz,double w0,double w1,double r,double g,double b,double a,bool additive,int mode=1)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ay+by)/2-ey,vy=0,vz=0;vx=(ax+bx)/2-ex;vy=(ay+by)/2-ey;vz=(az+bz)/2-ez;
            double cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,l=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(l<1e-6)return false;cx/=l;cy/=l;cz/=l;
            return Quads.Add(ax-cx*w0/2,ay-cy*w0/2,az-cz*w0/2,bx-cx*w1/2,by-cy*w1/2,bz-cz*w1/2,bx+cx*w1/2,by+cy*w1/2,bz+cz*w1/2,ax+cx*w0/2,ay+cy*w0/2,az+cz*w0/2,r,g,b,a,mode,additive,1);
        }
        /// <summary>A flat quad in the horizontal plane, half sizes hx along x and hz along z (turned by `yaw`).</summary>
        private bool Flat(double x,double y,double z,double hx,double hz,double yaw,double r,double g,double b,double a,bool additive,int mode=1)
        {
            double c=Math.Cos(yaw),s=Math.Sin(yaw),ax=c*hx,az=-s*hx,bx=s*hz,bz=c*hz;
            return Quads.Add(x-ax-bx,y,z-az-bz,x+ax-bx,y,z+az-bz,x+ax+bx,y,z+az+bz,x-ax+bx,y,z-az+bz,r,g,b,a,mode,additive,1);
        }
        private void Beam(double ax,double ay,double az,double bx,double by,double bz,double r0,double r1,int hex,double gain,double alpha,double seed)
        {
            double r=L(hex,16)*gain,g=L(hex,8)*gain,b=L(hex,0)*gain;
            if(Beams!=null){Beams.Add(ax,ay,az,bx,by,bz,r0,r1,r*alpha*NativeBeamList.FromBillboard,g*alpha*NativeBeamList.FromBillboard,b*alpha*NativeBeamList.FromBillboard,.5,seed);return;}
            // billboards (the low tier): five strips across the beam, soft at its edges, three lengths fading out along it
            double dx=bx-ax,dy=by-ay,dz=bz-az,vx=(ax+bx)/2-ex,vy=(ay+by)/2-ey,vz=(az+bz)/2-ez,cx=dy*vz-dz*vy,cy=dz*vx-dx*vz,cz=dx*vy-dy*vx,cl=Math.Sqrt(cx*cx+cy*cy+cz*cz);if(cl<1e-6)return;cx/=cl;cy/=cl;cz/=cl;
            for(int k=0;k<3;k++)for(int j=0;j<5;j++)
            {
                double t0=k/3.0,t1=(k+1)/3.0,w0=r0+(r1-r0)*t0,w1=r0+(r1-r0)*t1,o=(j-2)*.4,wt=Math.Exp(-o*o*2.2)*(1-.3*k);
                double px0=ax+dx*t0+cx*o*w0,py0=ay+dy*t0+cy*o*w0,pz0=az+dz*t0+cz*o*w0,px1=ax+dx*t1+cx*o*w1,py1=ay+dy*t1+cy*o*w1,pz1=az+dz*t1+cz*o*w1;
                Quads.Add(px0-cx*w0*.2,py0-cy*w0*.2,pz0-cz*w0*.2,px1-cx*w1*.2,py1-cy*w1*.2,pz1-cz*w1*.2,px1+cx*w1*.2,py1+cy*w1*.2,pz1+cz*w1*.2,px0+cx*w0*.2,py0+cy*w0*.2,pz0+cz*w0*.2,r,g,b,alpha*wt,0,true,1);
            }
        }

        // ---------------- telegraphs ----------------
        private void CraneFx(MapHazard h,int round,double clock,int index)
        {
            var st=NativeFactoryHazards.State(h,round,clock);if(st.Zone<0)return;var zn=h.zones[st.Zone];double fy=zn.center.y;
            double hx=st.X,hz=st.Z;if(stage==2){NativeNewStageRig.Reach(h,st,round,clock,out double a,out double r);hx=Math.Sin(a)*r;hz=Math.Cos(a)*r;}
            if(st.Phase==NativeFactoryHazards.Warning||st.Phase==NativeFactoryHazards.Falling)
            {
                bool on=st.Phase==NativeFactoryHazards.Falling||NativeFactoryRig.Strobe(clock,NativeFactoryRig.ZoneHz);double k=on?1:.25;
                Beam(hx,st.LoadY,hz,zn.center.x,fy+.05,zn.center.z,.5,Math.Max(zn.size.x,zn.size.z)*.4,0xFF3020,.9,.12*k,index);Telegraph++;
                Flat(zn.center.x,fy+.03,zn.center.z,zn.size.x/2+.4,zn.size.z/2+.4,0,L(0xFF3020,16)*.9,L(0xFF3020,8)*.9,L(0xFF3020,0)*.9,.32*k,true,0);Telegraph++;
                Hex(hx,st.LoadY+.2,hz,1.4,0xFF5030,.8*k,true);Telegraph++;
            }
            // the landing's dust: a ring of puffs from the impact for 1.6 s
            if(!double.IsNaN(st.ImpactAt)&&clock>=st.ImpactAt&&clock<st.ImpactAt+1.6)
            {
                double t=(clock-st.ImpactAt)/1.6;int n=Count(18);
                for(int i=0;i<n;i++){double a=i*2*Math.PI/n+H(index,i),rad=1.2+t*(3.2+H(index,i,2)*1.4),px=zn.center.x+Math.Cos(a)*rad*1.3,pz=zn.center.z+Math.Sin(a)*rad*.6;Hex(px,fy+.3+t*1.2,pz,1.2+t*2.4,0x8A8478,.42*(1-t)*(1-t),false);Atmosphere++;}
            }
        }
        private void VentFx(MapHazard h,int round,double clock,double now,int index)
        {
            var st=NativeFactoryHazards.State(h,round,clock);double x=h.a.x,y=h.a.y,z=h.a.z,tall=h.size.y;
            if(st.Phase==NativeFactoryHazards.Warning)
            {
                for(int i=0;i<6;i++){double t=Frac(now*2.2+i/6.0);Hex(x+(H(index,i)-.5)*.8,y+.1+t*.9,z+(H(index,i,1)-.5)*.8,.25+t*.5,0xDDE6EE,.35*st.Warn*(1-t),false);Telegraph++;}
                Flat(x,y+.04,z,h.size.x/2,h.size.z/2,0,.9,.6,.2,.25*st.Warn,true,0);Telegraph++;
            }
            else if(st.Phase==NativeFactoryHazards.Bursting)
            {
                for(int i=0;i<14;i++){double t=Frac(now*1.6+H(index,i,4)),py=y+.2+t*tall*(.4+.6*st.U);Hex(x+(H(index,i,5)-.5)*(.4+t*1.4),py,z+(H(index,i,6)-.5)*(.4+t*1.4),.8+t*2.0,0xE8EEF2,.6*st.U*(1-t*.7),false);Telegraph++;}
                Hex(x,y+.5,z,2.4,0xFFFFFF,.35*st.U,false);Telegraph++;
            }
            // after the burst: the cloud drifting off on the wind for 2 s
            if(NativeFactoryHazards.Cycle(h,clock,out _,out double u))
            {
                double end=Math.Max(0,h.period-h.warn-h.dwell)+h.warn+h.dwell,after=u-end;
                if(after>=0&&after<2){double k=1-after/2;for(int i=0;i<Count(6);i++){double px=x+after*1.6+(H(index,i,9)-.5)*2;Hex(px,y+tall*.7+after*.8,z+(H(index,i,10)-.5)*2,2.4+after*1.4,0xD8E0E8,.28*k,false);Atmosphere++;}}
            }
        }
        private void BurnerFx(MapHazard h,in NativeHazardState st,double time,int index)
        {
            foreach(var z in h.zones??Array.Empty<MapVolume>())
            {
                double y=z.center.y-z.size.y/2,x0=z.center.x-z.size.x/2,w=z.size.x,cz=z.center.z,depth=z.size.z,tall=z.size.y;int cols=4,rows=5;
                if(st.Phase==NativeHazardPhase.Idle){for(int i=0;i<cols;i++){double x=x0+w*(i+.5)/cols;Sprite(x,y+.06,cz+(H(index,i)-.5)*depth*.6,.14,1,.5,.15,.3,true);Telegraph++;}continue;}
                if(st.Phase==NativeHazardPhase.Warn)
                {
                    for(int i=0;i<cols;i++)for(int j=0;j<rows;j++){double x=x0+w*(i+.5)/cols,pz=cz-depth/2+depth*(j+.5)/rows,fl=.25+.55*st.Level*(.7+.3*Math.Sin(time*25+i+j*3));Sprite(x,y+.08+fl*.35,pz,.22+fl*.35,1,.55,.18,.65,true);Telegraph++;}
                    Flat(z.center.x,y+.04,cz,w/2,depth/2,0,1,.38,.08,.3*st.Level,true,0);Telegraph++;
                    for(int i=0;i<3;i++){double t=Frac(time*1.3+i/3.0);Sprite(x0+w*(i+.5)/3,y+.4+t*2.4,cz,1.4,1,.5,.2,.06*st.Level*(1-t),true);Telegraph++;}
                    continue;
                }
                if(st.Phase==NativeHazardPhase.Active)
                {
                    Flat(z.center.x,y+.05,cz,w/2+.4,depth/2+.4,0,1,.5,.15,.5,true,0);Telegraph++;
                    for(int i=0;i<cols;i++)for(int j=0;j<rows;j++)for(int k=0;k<3;k++)
                    {
                        double x=x0+w*(i+.5)/cols,pz=cz-depth/2+depth*(j+.5)/rows,t=Frac(time*1.9+H(index,i*7+j,k)),py=y+.15+t*tall,size=(1.3-t*.5)*.9,flick=.8+.2*Math.Sin(time*31+i*3+j*5+k);
                        Sprite(x+(H(index,i,j+k*9)-.5)*.4,py,pz+(H(index,j,i+k*7)-.5)*.5,size,1,.68-.44*t,Math.Max(0,.24-.22*t),.5*(1-t)*flick,true);Telegraph++;
                        if(k==0&&t<.45){Streak(x,y+.1,pz,x+(H(index,i,j)-.5)*.3,y+.25+tall*(.55+.3*flick),pz,.5,.12,1,.92,.62,.55*flick,true);Telegraph++;}
                    }
                    for(int i=0;i<10;i++){double t=Frac(time*.9+H(index,i,50));Sprite(x0+H(index,i,51)*w,y+tall*.85+t*2.2,cz+(H(index,i,52)-.5)*depth,.07,1,.7,.3,.9*(1-t),true);Telegraph++;}
                    continue;
                }
                for(int i=0;i<cols;i++){double t=Frac(time*.6+H(index,i,70));Sprite(x0+w*(i+.5)/cols,y+.4+t*2.8,cz,1+t*1.6,.14,.13,.12,.32*st.Level*(1-t),false);Telegraph++;}
            }
        }

        // ---------------- atmosphere ----------------
        private void LampHalos(double t)
        {
            for(int i=0;i<lampCount;i++)
            {
                int o=i*6;double x=lamp[o],y=lamp[o+1],z=lamp[o+2],size=lamp[o+4],blink=lamp[o+5];int hex=(int)lamp[o+3];
                if(blink>0&&Frac(t/blink+H(i,3))>.18)continue;double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));double grow=1+Math.Min(3,d/60);
                Hex(x,y,z,size*grow,hex,.55,true);if(size>1.5&&!low)Hex(x,y-size*.3,z,size*2.6*grow,hex,.12,true);Lights++;
            }
        }
        private void Lighthouse(double t)
        {
            double x=NativeNewStageLooks.LighthouseX,y=NativeNewStageLooks.LanternY,z=NativeNewStageLooks.LighthouseZ,a=NativeNewStageLooks.BeamHeading(t),len=NativeNewStageLooks.BeamReach;
            Hex(x,y,z,3.2,0xFFF0C8,.9,true);Hex(x,y,z,9,0xFFE0A0,.18,true);Lights+=2;
            foreach(double side in Unit){double dx=Math.Sin(a)*side,dz=Math.Cos(a)*side;Beam(x+dx*.6,y,z+dz*.6,x+dx*len,y-3.5,z+dz*len,.3,3.6,0xFFE6B0,1.0,.06,side);Lights++;}
            // when a beam faces the camera, its flash: a bright star at the lantern
            double vx=ex-x,vz=ez-z,vl=Math.Sqrt(vx*vx+vz*vz)+1e-6;double face=Math.Abs((vx*Math.Sin(a)+vz*Math.Cos(a))/vl);if(face>.97)Hex(x,y,z,14*(face-.97)/.03,0xFFF4E0,.6,true);
        }
        /// <summary>Spray: Breakwater's swell breaking on the windward parapets; Deepwell's seas thrown up the cellar deck's edges.</summary>
        private void Spray(double t,int which)
        {
            int bursts=Count(which==0?6:8);
            for(int k=0;k<bursts;k++)
            {
                const double life=2.2;double period=which==0?4.2:3.1,phase=H(which*97+k,1)*period;double cyc=Math.Floor((t+phase)/period),age=(t+phase)-cyc*period;if(age>life)continue;int c=(int)cyc;
                double px,py,pz,nx,nz;
                if(which==0){bool north=H(k,c,2)<.65;if(north){px=-19+H(k,c,3)*38;pz=14.6;nx=0;nz=-1;}else{px=-21.6;pz=-12+H(k,c,3)*24;nx=1;nz=0;}py=.2;}
                else{int edge=(int)(H(k,c,2)*4);double u=H(k,c,3)*2-1;px=edge<2?u*27:(edge==2?-28.6:28.6);pz=edge==0?20.6:edge==1?-20.6:u*19;nx=edge==2?1:edge==3?-1:0;nz=edge==0?-1:edge==1?1:0;py=-2.2;}
                double s=age/life,rise=which==0?3.6:5.2;int n=10;
                for(int i=0;i<n;i++)
                {
                    double f=(i+.5)/n,h=H(k*13+i,c,5),up=Math.Sin(Math.Min(1,s*1.35)*Math.PI)*rise*(.55+.45*h),drift=s*s*(which==0?2.2:1.4)*(.6+.6*h);
                    double x=px+nx*drift+(nz!=0?(f-.5)*2.4:0)+(h-.5)*.6,z=pz+nz*drift+(nx!=0?(f-.5)*2.4:0)+(H(k*13+i,c,6)-.5)*.6;
                    Hex(x,py+up,z,.9+s*1.8,0xDCE8F0,.38*(1-s)*(1-s*.4),false);Atmosphere++;
                }
                if(which==0){Flat(px+nx*.4,-1.55,pz+nz*.4,nx!=0?.6:2.4,nz!=0?.6:2.4,0,.8,.85,.9,.35*(1-s),false);Atmosphere++;}
            }
        }
        /// <summary>Glints on the water toward the stage's lights: Breakwater's afterglow in the west and the lantern; Pier 9's
        /// reflections under the ship's and cranes' lamps.</summary>
        private void Glints(double t,int which)
        {
            double sea=NativeNewStageLooks.SeaLevel(which)+.04;
            void Toward(double lx,double lz,int hex,double gain,int n,double spread,int seed)
            {
                double dx=lx-ex,dz=lz-ez,d=Math.Sqrt(dx*dx+dz*dz);if(d<1)return;dx/=d;dz/=d;double nx=-dz,nz=dx;
                for(int i=0;i<n;i++)
                {
                    double f=Math.Pow(H(seed,i),.7),dist=20+f*Math.Min(d,500),lat=(H(seed,i,1)-.5)*spread*(.3+f),tw=Frac(t*(.6+H(seed,i,2)*1.2)+H(seed,i,3));if(tw>.55)continue;
                    double px=ex+dx*dist+nx*lat,pz=ez+dz*dist+nz*lat;if(which==0&&Math.Abs(px)<23&&Math.Abs(pz)<16)continue;if(which==1&&pz<30.6)continue;
                    double a=Math.Sin(tw/.55*Math.PI);Flat(px,sea,pz,.6+f*2.4,.06+f*.12,Math.Atan2(nx,nz),L(hex,16)*gain,L(hex,8)*gain,L(hex,0)*gain,.6*a,true,0);Atmosphere++;
                }
            }
            if(which==0){Toward(-600,-40,0xFFA060,1.1,Count(70),60,301);Toward(NativeNewStageLooks.LighthouseX,NativeNewStageLooks.LighthouseZ,0xFFE6B0,.9,Count(24),8,302);}
            else{foreach(double x in CraneLights)Toward(x,30.5,0xFF4030,.6,Count(10),3,303+(int)x);Toward(60,36,0xFFE0A8,.9,Count(30),10,305);Toward(-87,36,0xFFF4D8,.7,Count(14),4,306);Toward(30,465,0xFFD08A,.7,Count(40),120,307);}
        }
        private void Floods(double t)
        {
            for(int side=-1;side<=1;side+=2)for(int k=0;k<3;k++){double x=side*(56+k*30);Beam(x,29.6,16,x-side*9,0,12,.8,9,0xFFA64A,.8,.07,k);Lights++;}
            for(int i=0;i<6;i++){double x=-100+i*40;Beam(x,17.6,-46,x,0,-42,.6,6,0xFFA64A,.8,.06,i+10);Lights++;}
        }
        private void Mist(double t)
        {
            int n=Count(14);for(int i=0;i<n;i++){double x=-80+H(401,i)*160+Math.Sin(t*.05+i)*8,z=36+H(401,i,1)*60;Hex(x,-1.6+H(401,i,2)*.8,z,14+H(401,i,3)*10,0x3A4A5C,.16,false);Atmosphere++;}
        }
        private void Haze(double t)
        {
            for(int layer=0;layer<HazeY.Length;layer++){double y=HazeY[layer],a=HazeA[layer];for(int k=0;k<4;k++){double cx=(k&1)==0?-260:260,cz=(k&2)==0?-260:260;Flat(cx+Math.Sin(t*.02+k)*12,y,cz,300,300,0,.80,.86,.92,a,false);Atmosphere++;}}
            int n=Count(10);for(int i=0;i<n;i++){double a=H(402,i)*2*Math.PI,r=90+H(402,i,1)*300;Hex(Math.Cos(a)*r,-20-H(402,i,2)*70,Math.Sin(a)*r,60+H(402,i,3)*60,0xD8E2EC,.12,false);Atmosphere++;}
        }
        private void Netting(double t)
        {
            for(int side=-1;side<=1;side+=2)for(int f=1;f<=2;f++)for(double z=-23.5;z<23.5;z+=4)
            {
                double y0=-f*3.6+.1,y1=y0+2.9,wave=Math.Sin(t*3.1+z*.7+f)*.18+Math.Sin(t*7.3+z*1.9)*.06,x=side*(28.25+.12+wave);
                Quads.Add(side*28.3,y1,z,side*28.3,y1,z+4,x,y0,z+4,x,y0,z,.10,.26,.16,.42,0,false,1);Atmosphere++;
            }
        }
        private void Motes(double t)
        {
            int n=Count(60);const double box=14;
            for(int i=0;i<n;i++)
            {
                double fx=Frac(H(403,i)+t*.013),fy=Frac(H(403,i,1)+t*.021),fz=Frac(H(403,i,2)-t*.009);double x=ex+(fx-.5)*2*box,y=ey-2+fy*6,z=ez+(fz-.5)*2*box;
                double d=Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez));if(d<1.5)continue;Hex(x,y,z,.05+.03*H(403,i,3),0xFFF4D8,.6*Math.Min(1,(box-d)/4),true);Atmosphere++;
            }
        }
        private void CraneGlints(MapHazard[] hazards,int round,double clock,double t)
        {
            foreach(var h in hazards??Array.Empty<MapHazard>())
            {
                if(h?.kind!=NativeFactoryHazards.Crane)continue;var st=NativeFactoryHazards.State(h,round,clock);NativeNewStageRig.Reach(h,st,round,clock,out double a,out _);
                for(int k=0;k<6;k++){double u=4+k*4.6,tw=Frac(t*.5+k*.37);if(tw>.2)continue;Hex(Math.Sin(a)*u,h.a.y+NativeNewStageRig.JibDepth+.1,Math.Cos(a)*u,1.2*Math.Sin(tw/.2*Math.PI),0xFFFFFF,.9,true);Atmosphere++;}
            }
        }
        private void Flare(double t)
        {
            double x=NativeNewStageLooks.FlareX,y=NativeNewStageLooks.FlareY+.5,z=NativeNewStageLooks.FlareZ;const double lean=.55;
            Hex(x,y+4,z,26,0xFF7A2A,.22,true);Hex(x,y+2,z,10,0xFFB040,.4,true);Lights+=2;
            int n=Count(26);
            for(int i=0;i<n;i++)
            {
                double f=Frac(t*1.4+H(404,i)),h=f*9,sway=Math.Sin(t*3.3+i)*.6+Math.Sin(t*7.1+i*2.3)*.3,px=x+h*lean+sway*f,pz=z+Math.Sin(t*2.1+i)*.4*f,size=(1.8+f*3.4)*(1-f*.35);
                double r=1,g=.85-.55*f,b=Math.Max(0,.45-.45*f*1.6);Sprite(px,y+h,pz,size,r,g,b,.75*(1-f)*(.85+.15*Math.Sin(t*29+i)),true);Atmosphere++;
            }
            Sprite(x,y+.6,z,1.4,1,.98,.9,.9,true);
            int m=Count(12);for(int i=0;i<m;i++){double f=Frac(t*.35+H(405,i));Hex(x+9*lean+f*14,y+9+f*12,z+(H(405,i,1)-.5)*4,4+f*8,0x1A1614,.35*(1-f),false);Atmosphere++;}
        }
        /// <summary>Deepwell's whitecaps: foam streaks breaking and fading on the swell round the eye (the storm sea reads at night).</summary>
        private void Whitecaps(double t)
        {
            int n=Count(70);double sea=NativeNewStageLooks.SeaLevel(3)+.06;const double life=3.2;
            for(int i=0;i<n;i++)
            {
                double phase=H(408,i)*life,cyc=Math.Floor((t+phase)/life),age=(t+phase)-cyc*life;int c=(int)cyc;
                double a=H(409,i,c)*2*Math.PI,r=12+Math.Pow(H(410,i,c),.6)*120,x=ex+Math.Cos(a)*r,z=ez+Math.Sin(a)*r;if(Math.Abs(x)<29&&Math.Abs(z)<23)continue;
                double s=age/life,k=Math.Sin(Math.Min(1,s*3)*Math.PI*.5)*(1-s);Flat(x+s*3,sea,z,2.0+s*3.6,.45+s*.6,.3,.86,.92,.96,.6*k,false,1);Atmosphere++;
            }
        }
        private void FarFlares(double t)
        {
            for(int i=0;i<FarX.Length;i++){double x=FarX[i],y=FarY[i],z=FarZ[i];Hex(x,y+1.5+Math.Sin(t*5)*.4,z,7+Math.Sin(t*7.3)*.8,0xFF8A30,.8,true);Hex(x,y+2,z,22,0xFF6A20,.2,true);Lights+=2;}
        }
        private void Aurora(double t)
        {
            int bands=3;
            for(int k=0;k<bands;k++)
            {
                double dist=230+k*110,alt=78+k*14,tall=70+k*24,arc=1.9-k*.3,center=(k==1?Math.PI:0)+Math.Sin(t*.013+k)*.08;int rays=Count(64-k*12);
                for(int i=0;i<rays;i++)
                {
                    double f=(double)i/(rays-1),a=center-arc/2+arc*f,wob=Math.Sin(f*9.4+t*.11+k*2)*18+Math.Sin(f*23+t*.23)*6,r=dist+wob;
                    double x=Math.Sin(a)*r,z=Math.Cos(a)*r,pulse=.55+.45*Math.Sin(f*14-t*.6+k*1.7)*Math.Sin(f*5.3+t*.17),edge=Math.Sin(Math.PI*f);double g=Math.Max(0,pulse)*edge;if(g<.05)continue;
                    double hgt=tall*(.7+.5*H(406+k,i));double w=r*arc/rays*2.4;
                    Streak(x,alt,z,x,alt+hgt*.62,z,w,w*.9,.10*g,.95*g,.55*g,.55,true);
                    Streak(x,alt+hgt*.5,z,x,alt+hgt,z,w*.9,w*.6,.45*g,.22*g,.95*g,.4,true);Atmosphere+=2;
                }
            }
        }
        private void Drift(double t)
        {
            int n=Count(90);const double box=18;double wx=1,wz=.35,wl=Math.Sqrt(wx*wx+wz*wz);wx/=wl;wz/=wl;
            for(int i=0;i<n;i++)
            {
                double fx=Frac(H(407,i)+t*.11*(.7+.6*H(407,i,3))),fz=Frac(H(407,i,1)+t*.04);double x=ex+(fx-.5)*2*box,z=ez+(fz-.5)*2*box,y=.05+H(407,i,2)*.6;
                double len=1.2+H(407,i,4)*2.4;Streak(x,y,z,x+wx*len,y+.05,z+wz*len,.08,.02,.85,.9,.95,.22*Math.Sin(fx*Math.PI),false);Atmosphere++;
            }
        }
    }
}

using System;

namespace ChipArena.UnityConversion
{
    /// <summary>One Ward Zero floor vent's dressing state: the eruption jet accumulator, whether the burst fired, the entry smoke.</summary>
    public sealed class NativeStageVentState
    {
        public double Acc,Smoke;public bool Burst;
        public void Clear(){Acc=Smoke=0;Burst=false;}
    }

    /// <summary>
    /// Pure port of the Ward Zero floor-vent dressing in client/siren/stage.ts WardZeroVisuals at 05950ea: buildVents' rim (a
    /// glowMaterial RingGeometry(1.15, 1.4, 4) turned into a square frame under the grate), update's "Vents" block (the rim
    /// fades from the warn red to white, black wisps while a vent warns, a burst and the black flame jet while it erupts) and
    /// updateEntries' vent branch (the open-vent rim flicker, the warning lid rattle and the smoke spilling out while a lid
    /// swings). Flames go into a <see cref="NativeFlameField"/> fed the source's random draws in the source's order, so a run
    /// replays the source frame for frame (Tests/StageVentChecks.cs). Positions here are the flame pool's frame (the runtime
    /// passes -x of a Unity vent, as every other emitter of the shared Siren pool). Allocation free.
    /// </summary>
    public static class NativeStageVentFx
    {
        public const int Count=8;
        /// <summary>The rim: RingGeometry(inner, outer, 4 theta segments, 1 phi segment), Euler(-π/2, 0, π/4), .01 above the grate group (.015).</summary>
        public const double RimInner=1.15,RimOuter=1.4,RimTurn=Math.PI/4,RimLift=.025;public const int RimSegments=4,RimVertices=(RimSegments+1)*2;
        /// <summary>PALETTE.warn while arming, white while erupting.</summary>
        public const int RimWarn=0xff2f44,RimOn=0xffffff;
        public const double OnOpacity=.95,WarnOpacity=.9,OpenGlowBase=.25,OpenGlowSwing=.35,OpenGlowDim=.4,OpenGlowRate=10;
        /// <summary>VENT_SWING, and the warning rattle of the lid (.06 rad at 34 rad/s).</summary>
        public const double Swing=1.95,RattleAmp=.06,RattleRate=34;
        /// <summary>ENTRY_CLOSE.vent: a PvE vent counts as active until this long after it starts to close.</summary>
        public const double EntryClose=.4;
        /// <summary>A Ward Zero (outside PvE) pit counts as active from this drawn opening.</summary>
        public const double PitActiveFrom=.02;
        private static readonly int[] triangles=BuildTriangles();
        /// <summary>RingGeometry's index list (a, b, d), (b, c, d) per segment.</summary>
        public static int[] RimTriangles=>(int[])triangles.Clone();
        private static int[] BuildTriangles()
        {
            var t=new int[RimSegments*6];int k=0;
            for(int i=0;i<RimSegments;i++){int a=i,b=i+RimSegments+1,c=i+RimSegments+2,d=i+1;t[k++]=a;t[k++]=b;t[k++]=d;t[k++]=b;t[k++]=c;t[k++]=d;}
            return t;
        }
        /// <summary>update's q: effects quality below .5 halves every count and rate.</summary>
        public static double Quality(double effects)=>effects<.5?.5:1;
        /// <summary>update's clock step: dt = clamp(time - last, 0, .05).</summary>
        public static double Step(double last,double time){double d=time-last;return double.IsNaN(d)?0:Math.Min(.05,Math.Max(0,d));}
        /// <summary>
        /// Rim vertex i (0..9) relative to the vent centre on the floor, Y up: RingGeometry vertex (ring i/5, theta i%5) through the
        /// mesh's Euler(-π/2, 0, π/4) and its .025 lift. The four theta steps make a square frame with straight edges.
        /// </summary>
        public static void RimVertex(int i,out double x,out double y,out double z)
        {
            double r=i<RimSegments+1?RimInner:RimOuter,a=(i%(RimSegments+1))*Math.PI*2/RimSegments,px=r*Math.Cos(a),py=r*Math.Sin(a);
            // Rz(π/4) then Rx(-π/2): (x, y, 0) -> (x', 0, -y') with x' = x cos - y sin, y' = x sin + y cos.
            double c=Math.Cos(RimTurn),s=Math.Sin(RimTurn),rx=px*c-py*s,ry=px*s+py*c;
            x=rx;y=RimLift;z=-ry;
        }
        /// <summary>The rim's colour and opacity: white at .95 while erupting, else the warn red at max(warn·.9, open glow).</summary>
        public static void Rim(bool on,double warn,double glow,out int hex,out double opacity)
        {hex=on?RimOn:RimWarn;opacity=on?OnOpacity:Math.Max(warn*WarnOpacity,glow);}
        /// <summary>updateEntries: an open vent's rim flickers .25 + .35·open, dimmed to .4 on the off half of a 10 rad/s square wave.</summary>
        public static double OpenGlow(double open,double time)=>OpenGlowBase+OpenGlowSwing*open*(Math.Sin(time*OpenGlowRate)>0?1:OpenGlowDim);
        /// <summary>Ward Zero outside PvE: the pit glows while its drawn opening is above .02.</summary>
        public static bool PitActive(double drawn)=>drawn>PitActiveFrom;
        /// <summary>PvE: the vent glows from openAt until ENTRY_CLOSE.vent after closeAt.</summary>
        public static bool EntryActive(double openAt,double closeAt,double time)=>time>=openAt&&time<closeAt+EntryClose;
        /// <summary>The lid's warning rattle (Ward Zero warnings only): .06·warn·|sin(34·t)| rad on top of the swing.</summary>
        public static double Rattle(double warn,double time)=>RattleAmp*warn*Math.Abs(Math.Sin(time*RattleRate));
        /// <summary>The lid's angle about its hinge (rotation.z): VENT_SWING·open plus the rattle.</summary>
        public static double LidAngle(double open,double warn,double time)=>Swing*open+Rattle(warn,time);
        /// <summary>
        /// updateEntries: while a lid swings (open between .05 and .98) black and violet smoke spills out at 40·q per second.
        /// Returns the vent's accumulator (left unchanged outside that range, as the source's map entry).
        /// </summary>
        public static double EntrySmoke(NativeFlameField f,double acc,double x,double z,double open,double dt,double q)
        {
            if(!(open>.05&&open<.98))return acc;var R=f.Random;acc+=dt*q*40;
            while(acc>=1)
            {
                acc-=1;double px=x+(R()-.5)*1.8,pz=z+(R()-.5)*1.8,vy=.8+R();var tone=R()<.5?NativeFlameTone.Black:NativeFlameTone.Smoke;
                f.Emit(px,.05,pz,0,vy,0,.6,.08,tone,1,1.5);
            }
            return acc;
        }
        /// <summary>
        /// update's per-vent flames: black wisps rise while it warns (a 20·q·warn per second chance), a 28·q burst on the first
        /// eruption frame, then 110·q jet tongues per second (embers 6%, smoke 20% of the rest) while it stays active.
        /// </summary>
        public static void Vent(NativeFlameField f,NativeStageVentState s,double x,double z,double warn,bool on,double dt,double q)
        {
            var R=f.Random;
            if(warn>0&&R()<dt*q*20*warn){double px=x+(R()-.5)*2,pz=z+(R()-.5)*2;f.Emit(px,.05,pz,0,.5+warn,0,.4,.06,NativeFlameTone.Black,1.5,2);}
            if(!on){s.Burst=false;return;}
            if(!s.Burst){s.Burst=true;f.Burst(x,.1,z,NativeSirenFx.JsRound(28*q),2,.6,.14,NativeFlameTone.Black,4,1);}
            s.Acc+=dt*q*110;
            while(s.Acc>=1)
            {
                s.Acc-=1;double a=R()*Math.PI*2,r=R()*1;double px=x+Math.Cos(a)*r,pz=z+Math.Sin(a)*r,vx=(R()-.5)*.5,vy=6+R()*4,vz=(R()-.5)*.5,life=.38+R()*.2,size=.12+R()*.1;
                var tone=R()<.06?NativeFlameTone.Ember:R()<.2?NativeFlameTone.Smoke:NativeFlameTone.Black;
                f.Emit(px,.05,pz,vx,vy,vz,life,size,tone,-3,1.4);
            }
        }
    }
}

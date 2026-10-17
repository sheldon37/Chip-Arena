using System;
using System.Collections.Generic;
using ChipArena.UnityConversion;

// Engine-free checks for the draft tray's highlight timeline (Core/NativeHoloScan.cs, the holo scan NativeChipArt draws on the
// highlighted card and the pick's lock): the level's targets and rates, re-engaging only from fully off (a quick out-and-back,
// a hop across a gap or the held card's standby never replays the trace) at 30/60/144/240 Hz as NativeDraftScreen.DrawTray
// steps it with the stage's held card (NativeDraftHover); the trace, bracket snap and blink, the single scan pass, the comet,
// the halo's breath, the die's charge, the legs lighting with the trace, the veil, the lift and the lock; everything settled
// and still after the engage but the comet's lap and the swell, Reduced effects still from the start; 0 bytes per frame.
// Unity's drawing of the scan is not exercised.
internal static class DraftHoverChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Draft hover: "+what);}
    private static bool Near(float a,float b,float e=1e-4f)=>Math.Abs(a-b)<=e;
    public static IEnumerable<string> Run()
    {
        // Targets and rates: up over In, down over Out, the same at every frame rate; engages only from Off or below.
        Check(NativeHoloScan.Target(true,false)==1&&NativeHoloScan.Target(true,true)==1&&Near(NativeHoloScan.Target(false,true),.7f)&&NativeHoloScan.Target(false,false)==0,"targets");
        foreach(int hz in new[]{30,60,144,240})
        {
            float dt=1f/hz,v=0;int f=0;while(v<1){v=NativeHoloScan.Step(v,1,dt);f++;Check(f<10000,"never reaches 1");}
            Check(Math.Abs(f*dt-NativeHoloScan.In)<=dt+1e-4f,"rise "+f*dt+" s at "+hz+" Hz");
            f=0;while(v>0){v=NativeHoloScan.Step(v,0,dt);f++;Check(f<10000,"never reaches 0");}Check(Math.Abs(f*dt-NativeHoloScan.Out)<=dt+1e-4f,"fall "+f*dt+" s at "+hz+" Hz");
            v=1;for(int i=0;i<hz;i++)v=NativeHoloScan.Step(v,NativeHoloScan.Held,dt);Check(Near(v,NativeHoloScan.Held),"held level "+v);
        }
        Check(NativeHoloScan.Engages(0,1)&&NativeHoloScan.Engages(.02f,.7f)&&!NativeHoloScan.Engages(.021f,1)&&!NativeHoloScan.Engages(0,0)&&!NativeHoloScan.Engages(.5f,1),"engages only from <= .02 toward a lit target");
        Check(NativeHoloScan.Step(.4f,.4f,.1f)==.4f&&NativeHoloScan.Step(.4f,1,-1)==.4f,"steady and negative dt");
        yield return "Holo scan level: targets 1 / "+NativeHoloScan.Held+" / 0, up in "+NativeHoloScan.In+" s, down in "+NativeHoloScan.Out+" s at 30/60/144/240 Hz; engages only from <= "+NativeHoloScan.Off;

        // The tray's pointer script (DrawTray's rule: the card under the pointer, else the keyboard focus, else the stage's held
        // card at Held while NativeDraftHover reaches): out-and-back, a hop across a gap and the standby never replay the trace.
        foreach(int hz in new[]{30,60,144,240})
        {
            float dt=1f/hz;var hold=new NativeDraftHover();var level=new float[3];var engaged=new int[3];int hover=-1;float lastHeld=0;
            // (until s, pointer card or -1 off / -2 gap): on 0, gap .3 s, back on 0, out to 1 for .08 s and back, off 1.2 s, on 0 again.
            var script=new[]{(1f,0),(1.3f,-2),(2f,0),(2.08f,1),(3f,0),(4.2f,-1),(5f,0)};
            for(float t=0;t<5;t+=dt)
            {
                int under=-1;foreach(var s in script)if(t<s.Item1){under=s.Item2;break;}if(under<0)under=-1;
                hold.Step(hover,dt);int lit=under;bool held=lit<0&&hold.Reaching;hover=lit;
                for(int i=0;i<3;i++){float target=NativeHoloScan.Target(lit==i,held&&hold.Card==i);if(NativeHoloScan.Engages(level[i],target))engaged[i]++;level[i]=NativeHoloScan.Step(level[i],target,dt);}
                if(t>1.2f&&t<1.3f)lastHeld=level[0];
            }
            Check(engaged[0]==2&&engaged[1]==1&&engaged[2]==0,"engages at "+hz+" Hz: "+engaged[0]+"/"+engaged[1]+"/"+engaged[2]+" (want 2/1/0: the gap, the out-and-back and the standby keep card 0 up)");
            Check(Near(lastHeld,NativeHoloScan.Held,1e-3f),"standby across the gap "+lastHeld);
        }
        yield return "Holo scan engage: on, a .3 s gap (standby "+NativeHoloScan.Held+"), back, .08 s on a neighbour and back, then 1.2 s off: card 0 engages twice, the neighbour once, at 30/60/144/240 Hz";

        // The trace, the brackets' snap (6 at rest, 7.8 held, 16 at the snap's start) and L44T's one blink, the scan's one pass.
        Check(NativeHoloScan.TraceReach(0,false)==0&&NativeHoloScan.TraceReach(NativeHoloScan.Trace,false)==1&&NativeHoloScan.TraceReach(9,false)==1&&NativeHoloScan.TraceReach(0,true)==1,"trace reach");
        float prev=-1;for(float a=0;a<=NativeHoloScan.Trace;a+=.004f){float r=NativeHoloScan.TraceReach(a,false);Check(r>=prev-1e-5f,"trace reach not monotone at "+a);prev=r;}
        Check(Near(NativeHoloScan.BracketOffset(5,1,false),6)&&Near(NativeHoloScan.BracketOffset(5,NativeHoloScan.Held,false),7.8f)&&Near(NativeHoloScan.BracketOffset(0,1,false),16)&&Near(NativeHoloScan.BracketOffset(NativeHoloScan.SnapAt,1,false),16),"bracket offsets "+NativeHoloScan.BracketOffset(5,1,false)+" / "+NativeHoloScan.BracketOffset(5,.7f,false)+" / "+NativeHoloScan.BracketOffset(0,1,false));
        float least=99;for(float a=NativeHoloScan.SnapAt;a<.5f;a+=.002f)least=Math.Min(least,NativeHoloScan.BracketOffset(a,1,false));Check(least<6&&least>4.5f,"snap overshoot to "+least);
        Check(Near(NativeHoloScan.BracketOffset(0,1,true),6)&&Near(NativeHoloScan.BracketOffset(0,0,true),12),"calm brackets rest (drifting with the level)");
        Check(NativeHoloScan.BracketOpacity(.04f,false,false)==0&&Near(NativeHoloScan.BracketOpacity(.12f,false,false),1)&&Near(NativeHoloScan.BracketOpacity(.12f,false,true),.25f)&&Near(NativeHoloScan.BracketOpacity(.099f,false,true),NativeHoloScan.BracketOpacity(.099f,false,false))&&Near(NativeHoloScan.BracketOpacity(.16f,false,true),1)&&NativeHoloScan.BracketOpacity(0,true,true)==1,"bracket opacity and L44T's blink (.10-.16 s)");
        Check(!NativeHoloScan.ScanPass(.089f,false,out _)&&NativeHoloScan.ScanPass(.09f,false,out float p0)&&p0==0&&NativeHoloScan.ScanPass(.709f,false,out float p1)&&p1>.99f&&!NativeHoloScan.ScanPass(.7101f,false,out _)&&!NativeHoloScan.ScanPass(3.69f,false,out _)&&!NativeHoloScan.ScanPass(.4f,true,out _),"scan: one pass .09-.71 s, none after, none calm");
        yield return "Holo scan engage: trace "+NativeHoloScan.Trace+" s, brackets 16 -> 6 px (overshoot to "+least.ToString("0.0")+", 7.8 held), L44T blink .10-.16 s, one scan pass .09-.71 s";

        // The comet (half a lap round at the trace's close, one lap per Orbit, a .24 tail), the breath and the die's charge.
        Check(!NativeHoloScan.Comet(.319f,false,out _,out _)&&NativeHoloScan.Comet(.32f,false,out float h0,out float t0)&&Near(h0,.5f)&&t0==0&&!NativeHoloScan.Comet(2,true,out _,out _),"comet start");
        NativeHoloScan.Comet(.32f+NativeHoloScan.Orbit,false,out float h1,out float t1);NativeHoloScan.Comet(.32f+NativeHoloScan.Orbit/4,false,out float h2,out _);Check(Near(h1,.5f,1e-3f)&&Near(t1,.24f)&&Near(h2,.75f,1e-3f),"comet lap "+h1+" / "+h2+" tail "+t1);
        float lo=9,hi=-9;for(float t=0;t<10;t+=.01f){float s=NativeHoloScan.Swell(t,false);lo=Math.Min(lo,s);hi=Math.Max(hi,s);Check(NativeHoloScan.Swell(t,true)==1,"calm swell");}
        Check(lo>=.9f-1e-4f&&hi<=1.1f+1e-4f&&hi-lo>.19f&&Near(NativeHoloScan.Swell(NativeHoloScan.Breath/4,false),1.1f,1e-3f),"swell "+lo+"-"+hi);
        Check(NativeHoloScan.DieCharge(0,false)==0&&Near(NativeHoloScan.DieCharge(.05f,false),1)&&Near(NativeHoloScan.DieCharge(.1f,false),.78f)&&Near(NativeHoloScan.DieCharge(.16f,false),.6f)&&Near(NativeHoloScan.DieCharge(4,false),.6f)&&NativeHoloScan.DieCharge(0,true)==NativeHoloScan.DieRest,"die charge keys");
        yield return "Holo scan steady: comet from .32 s (head .5, a lap per "+NativeHoloScan.Orbit+" s, tail .24), halo breath "+lo.ToString("0.00")+"-"+hi.ToString("0.00")+" per "+NativeHoloScan.Breath+" s, die charge 0 -> 1 -> .78 -> .6 by .16 s";

        // The legs light with the trace: the top row (clockwise arm) before the bottom row, every leg lit at reach 1, for tray
        // and spotlight cards and each corner radius class.
        int cases=0;
        foreach(var (w,h) in new[]{(236f,190f),(272f,240f),(118f,120f),(214f,300f),(440f,320f)})foreach(float R in new[]{4f,8f,10f,12f})
        {
            float topFirst=float.NaN,bottomFirst=float.NaN,topAll=float.NaN,bottomAll=float.NaN;
            for(float reach=0;reach<=1.0001f;reach+=.0005f)
            {
                bool anyTop=false,allTop=true,anyBottom=false,allBottom=true;
                for(float o=0;o<w-32-.01f;o+=15){float x=16+o+Math.Min(7,w-32-o)/2;float a=NativeHoloScan.LegLit(reach,w,h,R,x,false),b=NativeHoloScan.LegLit(reach,w,h,R,x,true);anyTop|=a>.02f;allTop&=a>=1;anyBottom|=b>.02f;allBottom&=b>=1;}
                if(anyTop&&float.IsNaN(topFirst))topFirst=reach;if(anyBottom&&float.IsNaN(bottomFirst))bottomFirst=reach;if(allTop&&float.IsNaN(topAll))topAll=reach;if(allBottom&&float.IsNaN(bottomAll))bottomAll=reach;
            }
            Check(topFirst<bottomFirst,"legs: top row first at "+w+"x"+h+" R "+R);Check(topAll<=1&&bottomAll<=1,"legs not all lit at reach 1 ("+w+"x"+h+" R "+R+")");
            Check(NativeHoloScan.LegLit(0,w,h,R,20,false)==0&&NativeHoloScan.LegLit(1,w,h,R,w-19.5f,true)==1,"legs at the ends");cases++;
        }
        yield return "Holo scan legs: the top row lights before the bottom row and every leg is lit at reach 1 ("+cases+" card sizes and corner radii)";

        // The veil, the lift and the pick's lock.
        Check(NativeHoloScan.Recede(0,1)==1&&NativeHoloScan.Recede(1,1)==0&&Near(NativeHoloScan.Recede(.25f,.8f),.6f)&&NativeHoloScan.Recede(0,0)==0&&NativeHoloScan.Recede(-1,2)==1,"recede = max other x (1 - own)");
        Check(NativeHoloScan.Lift(0)==0&&Near(NativeHoloScan.Lift(1),-10)&&NativeHoloScan.Lift(NativeHoloScan.Held)<-8&&NativeHoloScan.Lift(NativeHoloScan.Held)>-10,"lift");
        Check(Near(NativeHoloScan.LockOffset(0,false),6)&&Near(NativeHoloScan.LockOffset(NativeHoloScan.Lock/1.6f,false),NativeHoloScan.BracketLock)&&Near(NativeHoloScan.LockOffset(.2f,true),6),"lock clamp");
        Check(Near(NativeHoloScan.LockFlare(0,false),NativeHoloScan.Flare)&&NativeHoloScan.LockFlare(NativeHoloScan.Lock,false)==0&&NativeHoloScan.LockFlare(-.01f,false)==0&&NativeHoloScan.LockFlare(.1f,true)==0&&NativeHoloScan.LockFlare(.15f,false)<NativeHoloScan.LockFlare(.05f,false),"lock flare");
        yield return "Holo scan veil (max other x (1 - own), "+NativeHoloScan.Veil+" strong), lift -10 px (held "+NativeHoloScan.Lift(NativeHoloScan.Held).ToString("0.0")+"), pick lock 6 -> 2.5 px with a +.4 die flare over "+NativeHoloScan.Lock+" s (calm: rest, no flare)";

        // Still after the engage (only the comet laps and the halo breathes), and still from the start under Reduced effects;
        // nothing allocates.
        long allocated=0;
        for(float a=1;a<12;a+=.013f)
        {
            long before=GC.GetAllocatedBytesForCurrentThread();
            float o=NativeHoloScan.BracketOffset(a,1,false),op=NativeHoloScan.BracketOpacity(a,false,true),tr=NativeHoloScan.TraceReach(a,false),dc=NativeHoloScan.DieCharge(a,false);bool scan=NativeHoloScan.ScanPass(a,false,out _);
            float c0=NativeHoloScan.BracketOffset(a*.01f,1,true),c1=NativeHoloScan.BracketOpacity(a*.01f,true,true),c2=NativeHoloScan.TraceReach(a*.01f,true),c3=NativeHoloScan.DieCharge(a*.01f,true);bool c4=NativeHoloScan.ScanPass(a*.01f,true,out _),c5=NativeHoloScan.Comet(a,true,out _,out _);
            NativeHoloScan.Comet(a,false,out _,out _);NativeHoloScan.LegLit(.5f,236,190,10,40,true);NativeHoloScan.Swell(a,false);NativeHoloScan.Step(.5f,1,.016f);NativeHoloScan.Lift(.5f);
            allocated+=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(Near(o,6)&&op==1&&tr==1&&Near(dc,.6f)&&!scan,"not settled at "+a+" s");Check(Near(c0,6)&&c1==1&&c2==1&&c3==NativeHoloScan.DieRest&&!c4&&!c5,"calm not still at "+a*.01f+" s");
        }
        Check(allocated==0,"allocates "+allocated+" bytes");
        yield return "Holo scan settles by "+(NativeHoloScan.ScanAt+NativeHoloScan.ScanTime).ToString("0.00")+" s (then only the comet lap and the swell), Reduced effects still from the start, 0 bytes per frame";
    }
}

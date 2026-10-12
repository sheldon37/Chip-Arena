using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// Portrait backdrops (the effect behind the pilot on lobby, results and pilot cards and the Locker stage): the table and its
// normalisation (Core/NativePortraitBacks.cs), every style's deterministic maths (ranges, symmetry, continuity, reduced
// effects), then source checks that the drawers stay clip-safe and allocation-free, that PortraitStage keeps the Tick Ring
// as the default, and that the field travels like the idle pose: prefs -> profile cosmetics -> host seat -> lobby and match
// seat frames -> cards, with the Locker's baseline, revert and Save Pilot. Drawing itself is checked by offline renders.
internal static class PortraitBackChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Portrait backdrops: "+what);count++;}
    private static bool Near(float a,float b,float eps=1e-4f)=>Math.Abs(a-b)<=eps;
    public static void Run(string root)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        string ui=R("NativePortraitBackUI.cs"),portrait=R("NativePortraitUI.cs"),locker=R("NativeLockerUI.cs"),network=R("NativeNetwork.cs"),frame=R("NativeNetworkFrame.cs"),lobby=R("NativeLobbyLogic.cs"),account=R("NativeAccount.cs"),game=R("ConversionGame.cs"),profile=R("NativeProfileUI.cs");
        var icons=new HashSet<string>();using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/Icons.json"))))foreach(var i in doc.RootElement.GetProperty("icons").EnumerateArray())icons.Add(i.GetProperty("id").GetString());
        int n=NativePortraitBacks.Count;

        // 1. The table: at least eight backdrops, the Tick Ring first and the default, each with a label, a one-line
        // description and an icon from the exported set; keys are short, unique and safe in a seat frame.
        Check(n>=8&&n==NativePortraitBacks.Keys.Length&&NativePortraitBacks.Labels.Length==n&&NativePortraitBacks.Texts.Length==n&&NativePortraitBacks.Icons.Length==n,"a key, label, text and icon per backdrop ("+n+")");
        Check(NativePortraitBacks.Default=="tick-ring"&&NativePortraitBacks.Keys[NativePortraitBacks.TickRing]==NativePortraitBacks.Default&&NativePortraitBacks.TickRing==0,"the Tick Ring is the first backdrop and the default");
        Check(NativePortraitBacks.Keys.Distinct().Count()==n&&NativePortraitBacks.Labels.Distinct().Count()==n,"keys and labels are unique");
        for(int i=0;i<n;i++)
        {
            string k=NativePortraitBacks.Keys[i];
            Check(k.Length<=NativePortraitBacks.MaxLength&&Regex.IsMatch(k,"^[a-z]+(-[a-z]+)*$"),"key "+k+" is short lowercase words");
            Check(NativePortraitBacks.Labels[i].Length>2&&NativePortraitBacks.Texts[i].Length>10&&NativePortraitBacks.Texts[i].Length<=60&&NativePortraitBacks.Texts[i].EndsWith("."),"label and one-line text for "+k);
            Check(icons.Contains(NativePortraitBacks.Icons[i]),"icon "+NativePortraitBacks.Icons[i]+" for "+k+" is in Icons.json");
            Check(NativePortraitBacks.Index(k)==i&&NativePortraitBacks.Normalize(k)==k&&ReferenceEquals(NativePortraitBacks.Normalize(k),NativePortraitBacks.Keys[i]),"key "+k+" indexes and normalises to itself");
        }
        Check(NativePortraitBacks.MaxLength==32,"the seat frame's backdrop cap matches the idle pose's (32)");
        foreach(var bad in new[]{null,"","unknown","Sunburst"," sunburst","tick-ring ",new string('x',33)})
            Check(NativePortraitBacks.Normalize(bad)==NativePortraitBacks.Default&&NativePortraitBacks.Index(bad)==NativePortraitBacks.TickRing,"an absent or unknown backdrop ("+(bad??"null")+") is the Tick Ring");
        Check(new NativeSeat().lobbyBack==NativePortraitBacks.Default,"a new seat (bots included) has the default backdrop");
        Check(NativePortraitBacks.HeadAbove(NativeLobbyPoses.Weapon)>NativePortraitBacks.HeadAbove(NativeLobbyPoses.Chair)&&NativePortraitBacks.HeadAbove(NativeLobbyPoses.Chair)>NativePortraitBacks.HeadAbove(NativeLobbyPoses.Sleep)&&NativePortraitBacks.HeadAbove(NativeLobbyPoses.Sleep)>0,"the head sits lowest when sleeping and highest standing");

        // 2. Shared maths: a stable hash in [0, 1), the reduced-effects clock and the neon line profile.
        double sum=0;for(int i=0;i<4000;i++){float h=NativePortraitBacks.Hash(i,7);Check(h>=0&&h<1&&h==NativePortraitBacks.Hash(i,7),"hash "+i+" in [0,1) and stable");sum+=h;}
        Check(Math.Abs(sum/4000-.5)<.03,"the hash spreads evenly (mean "+(sum/4000).ToString("0.000")+")");
        Check(NativePortraitBacks.ReducedRate>0&&NativePortraitBacks.ReducedRate<1&&NativePortraitBacks.Clock(10,false)==10&&Near(NativePortraitBacks.Clock(10,true),10*NativePortraitBacks.ReducedRate),"reduced effects slows the clock");
        Check(NativePortraitBacks.LineAlpha(0,.1f,.05f,0,0)==1&&NativePortraitBacks.LineAlpha(.2f,.1f,.05f,0,0)==0&&Near(NativePortraitBacks.LineAlpha(.2f,0,.05f,.2f,.4f),.4f*(float)Math.Exp(-1)),"neon line: an anti-aliased core and a gaussian glow");

        // 3. Sunburst: the baked turns cover one period of the pattern, so turn SunPhases is turn 0; the phase pair and
        // blend move continuously; nothing is drawn outside the texture's circle.
        int P=NativePortraitBacks.SunPhases;var probes=new[]{(.3f,-.2f),(-.5f,.4f),(.1f,.7f),(.62f,.05f)};
        foreach(var (x,y) in probes)Check(Near(NativePortraitBacks.SunTexel(x,y,P),NativePortraitBacks.SunTexel(x,y,0),1e-3f),"sun pattern repeats after SunPhases turns at ("+x+","+y+")");
        {
            // Turning by one phase step is rotating the probe the other way by SunPeriod/SunPhases degrees.
            float step=NativePortraitBacks.SunPeriod/P*(float)Math.PI/180,x=.4f,y=-.35f,c=(float)Math.Cos(step),s=(float)Math.Sin(step);
            // Clockwise screen rotation (y down) of (x, y) back by one step.
            float rx=x*c+y*s,ry=-x*s+y*c;Check(Near(NativePortraitBacks.SunTexel(x,y,1),NativePortraitBacks.SunTexel(rx,ry,0),2e-3f),"one baked phase is the pattern turned by one step");
        }
        Check(NativePortraitBacks.SunTexel(.8f,.8f,3)==0&&NativePortraitBacks.SunTexel(0,0,0)==0,"no rays outside the circle or at the hot core");
        {
            int lastA=-1;float lastPos=-1;
            for(float t=0;t<40;t+=.05f)
            {
                NativePortraitBacks.SunPhase(t*NativePortraitBacks.SunSpeed,out int a,out int b,out float k);Check(a>=0&&a<P&&b==(a+1)%P&&k>=0&&k<1,"sun phase pair at "+t);
                float pos=a+k;if(lastA>=0){float d=pos-lastPos;if(d<0)d+=P;Check(d<.5f,"the rays turn smoothly ("+t+")");}lastA=a;lastPos=pos;
            }
        }

        // 4. Hex field: adjacent cells sit one cell height apart on the lattice; the outline lights only at the edge, the lit
        // cell fills the inside; the shimmer stays in [0, 1].
        NativePortraitBacks.HexCentre(0,0,out float hx0,out float hy0);NativePortraitBacks.HexCentre(1,0,out float hx1,out float hy1);NativePortraitBacks.HexCentre(0,1,out float hx2,out float hy2);NativePortraitBacks.HexCentre(-1,0,out float hx3,out float hy3);
        float root3=(float)Math.Sqrt(3);
        Check(Near(Dist(hx0,hy0,hx1,hy1),root3)&&Near(Dist(hx0,hy0,hx2,hy2),root3)&&Near(Dist(hx0,hy0,hx3,hy3),root3),"neighbouring hex cells are one cell height (root 3) apart");
        float apothem=.86f*.8660254f;Check(Near(NativePortraitBacks.HexDistance(0,apothem,apothem),0)&&NativePortraitBacks.HexDistance(0,0,apothem)<0&&NativePortraitBacks.HexDistance(.95f,0,apothem)>0,"hex distance: zero on an edge, inside negative");
        Check(NativePortraitBacks.HexTexel(0,apothem,.06f,false)>.9f&&NativePortraitBacks.HexTexel(0,0,.06f,false)<.01f&&NativePortraitBacks.HexTexel(0,0,.06f,true)>=.29f&&NativePortraitBacks.HexTexel(.99f,.99f,.06f,true)==0,"hex outline at the edge, lit cell inside");
        for(int c=-4;c<=4;c++)for(int r=-3;r<=3;r++)for(float t=0;t<6;t+=.37f){float v=NativePortraitBacks.HexShimmer(c,r,Math.Abs(c)*.3f+Math.Abs(r)*.2f,t);Check(v>=0&&v<=1,"hex shimmer in [0,1]");}

        // 5. Circuit: every trace is horizontal and vertical legs inside the extents the drawer fits to the stage (.94 up,
        // 1.16 down, 1.5 each side); the left half mirrors the right; a pulse runs from the body out to the pad.
        Check(NativePortraitBacks.Traces==2*NativePortraitBacks.TraceHalf,"mirrored trace halves");
        for(int i=0;i<NativePortraitBacks.Traces;i++)
        {
            int pts=NativePortraitBacks.TracePoints(i);Check(pts>=2,"trace "+i+" has a leg");
            for(int k=0;k<pts;k++)
            {
                NativePortraitBacks.TracePoint(i,k,out float x,out float y);Check(Math.Abs(x)<=1.5f&&y>=-.94f&&y<=1.16f,"trace "+i+" point "+k+" inside the fitted extents");
                if(k>0){NativePortraitBacks.TracePoint(i,k-1,out float px,out float py);Check(px==x||py==y,"trace "+i+" leg "+k+" is horizontal or vertical");}
                if(i<NativePortraitBacks.TraceHalf){NativePortraitBacks.TracePoint(i+NativePortraitBacks.TraceHalf,k,out float mx,out float my);Check(mx==-x&&my==y,"trace "+i+" is mirrored");}
            }
            NativePortraitBacks.TracePoint(i,0,out float x0,out float y0);NativePortraitBacks.TracePoint(i,pts-1,out float x1,out float y1);
            NativePortraitBacks.TraceAt(i,0,out float a0,out float b0);NativePortraitBacks.TraceAt(i,1,out float a1,out float b1);NativePortraitBacks.TraceAt(i,.5f,out float am,out float bm);
            Check(Near(a0,x0)&&Near(b0,y0)&&Near(a1,x1)&&Near(b1,y1),"trace "+i+" runs from the body to its pad");
            Check(Math.Abs(x0)<Math.Abs(x1)||Math.Abs(y0)<Math.Abs(y1),"trace "+i+" starts nearer the body than its pad");
            Check(TraceDistance(i,am,bm)<1e-3f,"trace "+i+" midpoint lies on the trace");
            for(float t=0;t<8;t+=.21f){float p=NativePortraitBacks.TracePulse(i,t);Check(p>=0&&p<NativePortraitBacks.PulseRest,"trace "+i+" pulse in range");}
        }

        // 6. Equalizer: levels in [0, 1], mirrored about the middle bar, continuous in time (no jumps, the kick included).
        foreach(int bars in new[]{17,25})
            for(float t=0;t<6;t+=.0137f)
                for(int b=0;b<bars;b++)
                {
                    float v=NativePortraitBacks.EqLevel(b,bars,t);Check(v>=0&&v<=1&&v==NativePortraitBacks.EqLevel(bars-1-b,bars,t),"equalizer bar "+b+"/"+bars+" in range and mirrored");
                    Check(Math.Abs(NativePortraitBacks.EqLevel(b,bars,t+.001f)-v)<.05f,"equalizer bar "+b+" moves continuously at "+t);
                }

        // 7. Warp: distinct whole-step directions; progress in [0, 1) fading in and out; the baked streak lies along its
        // direction and ends inside its square.
        var dirs=new HashSet<int>();for(int i=0;i<NativePortraitBacks.Streaks;i++){int d=NativePortraitBacks.StreakDirection(i);Check(d>=0&&d<NativePortraitBacks.StreakSteps,"streak "+i+" direction");dirs.Add(d);}
        Check(dirs.Count==NativePortraitBacks.Streaks,"every streak has its own direction");
        Check(NativePortraitBacks.StreakAlpha(0)==0&&NativePortraitBacks.StreakAlpha(1)<1e-3f&&NativePortraitBacks.StreakAlpha(.5f)==1,"streaks fade in and out");
        for(int i=0;i<NativePortraitBacks.Streaks;i++)for(float t=0;t<5;t+=.3f){float f=NativePortraitBacks.StreakProgress(i,t);Check(f>=0&&f<1,"streak progress in range");}
        for(int a=0;a<NativePortraitBacks.StreakAngles;a++)
        {
            float ang=a*(180f/NativePortraitBacks.StreakAngles)*(float)Math.PI/180,dx=(float)Math.Sin(ang),dy=-(float)Math.Cos(ang);
            Check(NativePortraitBacks.StreakTexel(dx*.3f,dy*.3f,a,.09f)>.5f&&NativePortraitBacks.StreakTexel(-dy*.3f,dx*.3f,a,.09f)==0&&NativePortraitBacks.StreakTexel(dx*.96f,dy*.96f,a,.09f)==0,"streak "+a+" lies along its direction and ends inside its square");
        }

        // 8. Embers: inside their spread, rising, fading in at the floor and out near the top; slower with reduced effects.
        for(int i=0;i<NativePortraitBacks.EmberCount;i++)
            for(float t=0;t<9;t+=.23f)
            {
                NativePortraitBacks.Ember(i,t,out float x,out float rise,out float size,out float alpha);
                Check(x>=-1.15f&&x<=1.15f&&rise>=0&&rise<1&&size>0&&size<=1&&alpha>=0&&alpha<=1,"ember "+i+" in range");
                if(rise<.01f)Check(alpha<.1f,"ember "+i+" fades in at the floor");
            }
        {
            // Over a short span the reduced clock moves an ember a fifth as far.
            NativePortraitBacks.Ember(3,NativePortraitBacks.Clock(2,false),out _,out float r0,out _,out _);NativePortraitBacks.Ember(3,NativePortraitBacks.Clock(2.2f,false),out _,out float r1,out _,out _);
            NativePortraitBacks.Ember(3,NativePortraitBacks.Clock(2,true),out _,out float q0,out _,out _);NativePortraitBacks.Ember(3,NativePortraitBacks.Clock(2.2f,true),out _,out float q1,out _,out _);
            // Rising wraps from the top back to the floor, so compare the distance risen modulo one.
            Check(NativePortraitBacks.Frac(q1-q0)<NativePortraitBacks.Frac(r1-r0)*.5f,"reduced effects slows the embers");
        }

        // 9. Orbitals: electrons on their tilted ellipses, inside each orbit's half extents; the baked ring is bright on it.
        for(int o=0;o<3;o++)
        {
            float tilt=NativePortraitBacks.OrbitTilt[o]*(float)Math.PI/180,c=(float)Math.Cos(tilt),s=(float)Math.Sin(tilt),hw=NativePortraitBacks.OrbitHalfWidth(o),hh=NativePortraitBacks.OrbitHalfHeight(o);
            for(int k=0;k<2;k++)for(float t=0;t<7;t+=.29f)
            {
                NativePortraitBacks.Electron(o,k,t,out float x,out float y,out float depth);float u=x*c+y*s,v=-x*s+y*c,e=u*u/(NativePortraitBacks.OrbitA*NativePortraitBacks.OrbitA)+v*v/(NativePortraitBacks.OrbitB*NativePortraitBacks.OrbitB);
                Check(Near(e,1,1e-3f)&&Math.Abs(x)<=hw+1e-4f&&Math.Abs(y)<=hh+1e-4f&&depth>=-1&&depth<=1,"orbit "+o+" electron "+k+" on its ellipse");
                NativePortraitBacks.OrbitPoint(o,NativePortraitBacks.ElectronAngle(o,k,t),out float px,out float py);Check(px==x&&py==y,"electron angle and point agree");
            }
            Check(NativePortraitBacks.OrbitDirection(o)==1||NativePortraitBacks.OrbitDirection(o)==-1,"orbit direction");
            // The baked ring (long half-axis .9 of the texture) is lit on the curve and dark at its centre.
            float aspect=NativePortraitBacks.OrbitB/NativePortraitBacks.OrbitA;Check(NativePortraitBacks.EllipseTexel(.9f*c,.9f*s,aspect,NativePortraitBacks.OrbitTilt[o],.04f,.1f,.38f)>.9f&&NativePortraitBacks.EllipseTexel(0,0,aspect,NativePortraitBacks.OrbitTilt[o],.04f,.1f,.38f)<.05f,"orbit "+o+" ring texel");
        }

        // 10. Glitch: bands stay inside the panel; outside a burst nothing shows; reduced effects holds them still and faint.
        for(int i=0;i<NativePortraitBacks.GlitchBands;i++)
        {
            for(float t=0;t<12;t+=.041f)
            {
                NativePortraitBacks.GlitchBand(i,t,false,out float y,out float h,out float dx,out float a);Check(y>=-.9f&&y<=.9f&&h>0&&h<=.14f&&Math.Abs(dx)<=.16f&&a>=0&&a<=.85f,"glitch band "+i+" in the panel");
                if(NativePortraitBacks.Frac(t/NativePortraitBacks.GlitchCycle+i*.07f)>=NativePortraitBacks.GlitchBurst)Check(a==0,"glitch band "+i+" only shows in a burst");
            }
            NativePortraitBacks.GlitchBand(i,.3f,true,out float ry0,out _,out float rdx0,out float ra0);NativePortraitBacks.GlitchBand(i,7.9f,true,out float ry1,out _,out float rdx1,out float ra1);
            Check(ry0==ry1&&rdx0==rdx1&&ra0==ra1&&ra0>0&&ra0<.3f,"reduced effects holds glitch band "+i+" still and faint");
        }
        Check(NativePortraitBacks.ScanScroll(3,false)>=0&&NativePortraitBacks.ScanScroll(3,false)<1&&Near(NativePortraitBacks.ScanScroll(1,true),NativePortraitBacks.Frac(NativePortraitBacks.ReducedRate*.6f)),"scanlines scroll, slower with reduced effects");

        // 11. Crown halo: the rays fan symmetrically over the upper half; ray strength and twinkles stay in range.
        for(int k=0;k<NativePortraitBacks.HaloRays;k++)
        {
            int d=NativePortraitBacks.HaloRayDirection(k),m=NativePortraitBacks.HaloRayDirection(NativePortraitBacks.HaloRays-1-k);
            Check((d+m)%NativePortraitBacks.StreakSteps==0,"halo ray "+k+" mirrors ray "+(NativePortraitBacks.HaloRays-1-k));
            float deg=d*360f/NativePortraitBacks.StreakSteps;if(deg>180)deg-=360;Check(Math.Abs(deg)<=90,"halo ray "+k+" points into the upper half");
            for(float t=0;t<6;t+=.3f){NativePortraitBacks.HaloRay(k,t,out float len,out float a);Check(len>0&&len<=1&&a>0&&a<=1,"halo ray "+k+" strength");}
        }
        for(int i=0;i<5;i++)for(float t=0;t<8;t+=.17f){float v=NativePortraitBacks.Twinkle(i,t);Check(v>=0&&v<=1,"twinkle in range");}

        // 12. Neon diamond: sparks on the unit diamond, clockwise from the top vertex; the baked frames light on their line.
        NativePortraitBacks.DiamondPoint(0,out float tx,out float ty);NativePortraitBacks.DiamondPoint(.25f,out float rx2,out float ry2);NativePortraitBacks.DiamondPoint(.5f,out float bx,out float by);
        Check(Near(tx,0)&&Near(ty,-1)&&Near(rx2,1)&&Near(ry2,0)&&Near(bx,0)&&Near(by,1),"diamond points: top, then right, then bottom");
        for(float s=-.5f;s<1.5f;s+=.013f){NativePortraitBacks.DiamondPoint(s,out float x,out float y);Check(Near(Math.Abs(x)+Math.Abs(y),1,1e-4f),"diamond point on the diamond");}
        for(int k=0;k<2;k++)for(float t=0;t<9;t+=.4f){float s=NativePortraitBacks.DiamondSpark(k,t);Check(s>=0&&s<=1,"diamond spark in range");}
        Check(NativePortraitBacks.DiamondSpark(0,1)>NativePortraitBacks.DiamondSpark(0,.5f)&&NativePortraitBacks.DiamondSpark(1,1)<NativePortraitBacks.DiamondSpark(1,.5f),"the two sparks race opposite ways");
        Check(NativePortraitBacks.DiamondTexel(.45f,.45f,.04f,.1f,.42f)>.9f&&NativePortraitBacks.DiamondTexel(0,0,.04f,.1f,.42f)<1e-4f&&NativePortraitBacks.RingTexel(.9f,0,.04f,.1f,.4f)>.9f&&NativePortraitBacks.RingTexel(0,0,.04f,.1f,.4f)<1e-4f,"diamond and ring texels light on their line");

        // 13. The drawers: one per style besides the Tick Ring, clip-safe (no transformed draws), allocation-free per frame
        // (static stop arrays, no strings, no disc helper that allocates), slowed by the reduced clock, and kept in the stage.
        foreach(var name in new[]{"CleanGlow","Sunburst","HexField","Circuit","Equalizer","Warp","Embers","Orbitals","Glitch","CrownHalo","NeonDiamond"})
            Check(ui.Contains("case NativePortraitBacks."+name+":Bk"+name+"(")&&ui.Contains("private static void Bk"+name+"("),"a drawer for "+name);
        Check(!ui.Contains("case NativePortraitBacks.TickRing"),"the Tick Ring is not redrawn here");
        foreach(var bad in new[]{"U.Push(","GUI.matrix","Matrix4x4","U.Disc(","Random","string.Format","ToString(","+\"","\"+"})Check(!ui.Contains(bad),"backdrop drawers avoid "+bad);
        foreach(var line in ui.Split('\n'))if(line.Contains("new[]")||line.Contains("new float[")||line.Contains("new Color["))Check(line.Contains("static readonly")||line.Contains("var colours=new Color[w*h]"),"arrays only in static fields and the one-off bake: "+line.Trim());
        Check(ui.Contains("float tm=NativePortraitBacks.Clock(t,reduced);")&&ui.Contains("float m=Mathf.Clamp(s.height*.055f,2,30);"),"the dispatch slows motion with reduced effects and keeps a margin in the stage");
        Check(ui.Contains("if(backScale!=U.S||backTextures.Count>192)")&&ui.Contains("Object.Destroy(old)"),"baked textures are made again when the kit scale changes, and bounded");
        Check(ui.Contains("U.BeginBand(v);")&&ui.Contains("U.EndBand();"),"Locker card previews are clipped to their well");

        // 14. PortraitStage: the Tick Ring stays the default frame exactly as before; every caller passes its backdrop. The
        // backdrop takes the picked backdrop colour, else the accent as before (NativeHatLooks, HatLookChecks).
        Check(portrait.Contains("Color tint=backColor<0?accent:U.Hex(backColor);")&&portrait.Contains("if(style==NativePortraitBacks.TickRing)PortraitRing(c,ring,tint,e,t+phase,ticks,clipped);")&&portrait.Contains("else PortraitBack(style,stage,c,ring,tint,body,e,t+phase,prefs.reducedEffects,NativePortraitBacks.HeadAbove(pose));"),"PortraitStage keeps the Tick Ring by default and dispatches the others");
        Check(portrait.Contains("private static void PortraitRing(Vector2 c,float ring,Color accent,float e,float t,int ticks,bool clipped)")&&portrait.Contains("bool major=i%7==0;float deg=i/(float)ticks*360+t*12,h=major?9:5;"),"the Tick Ring itself is unchanged");
        Check(portrait.Contains("PortraitStage(stage,1,look,prefs.lobbyBack,"),"the home pilot card shows the saved backdrop");
        Check(portrait.Contains("back=NativePortraitBacks.Normalize(prefs.lobbyBack);")&&portrait.Contains("back=NativePortraitBacks.Normalize(seat.lobbyBack);")&&portrait.Contains("back=NativePortraitBacks.Normalize(s.lobbyBack);"),"cards show your own backdrop, the PvE seat's and the lobby seat's (normalised)");
        Check(locker.Contains("PortraitStage(r,LockerPortraitKey,look,lockerBack??prefs.lobbyBack,"),"the Locker stage shows the hovered backdrop, else the equipped one");

        // 15. The Locker: a BACKDROP section in Appearance; hover tries it on, a click equips it; baseline, dirty and revert.
        Check(locker.Contains("LkHeading(lockerSlots[k++],\"BACKDROP\",\"The effect behind your pilot in lobbies\");")&&locker.Contains("LkReserve(LkGrid(lockerW,NativePortraitBacks.Count,LkItemMin,LkItemH));"),"the BACKDROP section is laid out and drawn");
        Check(locker.Contains("if(hot)lockerBack=NativePortraitBacks.Keys[i];")&&locker.Contains("&&!on)prefs.lobbyBack=NativePortraitBacks.Keys[i];")&&locker.Contains("lockerWeapon=lockerBack=lockerHatColor=lockerBackColor=null;")&&locker.Contains("||lockerWeapon!=null||lockerBack!=null||"),"hover previews (TRYING ON), click equips, the hover resets each repaint");
        Check(Regex.IsMatch(locker,@"b\.Back!=prefs\.lobbyBack\b")&&locker.Contains("prefs.lobbyBack=b.Back;")&&locker.Contains("Back=prefs.lobbyBack,"),"the Locker baseline covers, dirties and reverts the backdrop");

        // 16. The field travels like lobbyPose: prefs (default), profile cosmetics (no initializer, so an account profile
        // without it leaves the local pref alone), host seat (normalised), lobby and match seat frames (capped, normalised).
        Check(game.Contains("public string lobbyBack=NativePortraitBacks.Default;"),"prefs default to the Tick Ring");
        Check(account.Contains("killEffect=\"impact-ragdoll\";public string lobbyBack;")&&account.Contains("public string hatColor,backColor;}"),"profile cosmetics carry the backdrop with no default");
        Check(network.Contains("cosmetics=new NativeCosmetics{lobbyPose=prefs.lobbyPose,lobbyBack=prefs.lobbyBack,")&&network.Contains("lobbyBack=NativePortraitBacks.Normalize(c.lobbyBack),"),"Profile() sends it and the host seat normalises it");
        Check(lobby.Contains("lobbyPose=s.lobbyPose,lobbyBack=s.lobbyBack,"),"the lobby seat frame carries it");
        Check(frame.Contains("introText,lobbyPose,lobbyBack;public string weaponSkins;public string hatColor,backColor;")&&frame.Contains("s.lobbyBack!=null&&s.lobbyBack.Length>NativePortraitBacks.MaxLength")&&frame.Contains("lobbyPose=s.lobbyPose,lobbyBack=s.lobbyBack,")&&frame.Contains("seat.lobbyBack=NativePortraitBacks.Normalize(sf.lobbyBack);"),"the match seat frame carries, caps and normalises it");
        Check(profile.Contains("if(!string.IsNullOrEmpty(c.lobbyBack))prefs.lobbyBack=NativePortraitBacks.Normalize(c.lobbyBack);"),"an account load adopts a backdrop only when the profile has one");

        Console.WriteLine("PASS Portrait backdrops: "+count+" checks ("+n+" backdrops; normalisation, deterministic style maths, reduced effects, clip-safe drawers, Locker section and the lobbyPose-style network path)");
    }
    private static float Dist(float ax,float ay,float bx,float by)=>(float)Math.Sqrt((ax-bx)*(ax-bx)+(ay-by)*(ay-by));
    // Distance from (x, y) to trace i's legs.
    private static float TraceDistance(int trace,float x,float y)
    {
        float best=float.MaxValue;int pts=NativePortraitBacks.TracePoints(trace);
        for(int k=1;k<pts;k++)
        {
            NativePortraitBacks.TracePoint(trace,k-1,out float ax,out float ay);NativePortraitBacks.TracePoint(trace,k,out float bx,out float by);
            float dx=bx-ax,dy=by-ay,l=dx*dx+dy*dy,u=l>0?Math.Max(0,Math.Min(1,((x-ax)*dx+(y-ay)*dy)/l)):0;best=Math.Min(best,Dist(x,y,ax+dx*u,ay+dy*u));
        }
        return best;
    }
}

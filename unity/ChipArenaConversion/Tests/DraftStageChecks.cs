using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

// Engine-free checks for the chip draft's 3D stage (Core/NativeDraftStage.cs as NativeDraftPresentation drives it): the pilot
// framed close, from the solar plexus up (the plexus derived from the rig's spine, chest and neck joints, on or just above the
// tray's top line; the head's top clear of the screen's top edge and above the cards; the face above the first row but on the
// tallest trays, which cover the chest instead) on every aspect ratio (16:9, 16:10, 21:9, 32:9, 4:3, 5:4) and interface
// scale, at least 1.25 times the seated table framing it replaces for bare heads, hats, the Auditor halo and the Siren drafter
// alike (a crown takes at most CrownRoom, so crowned pilots stay near the bare-headed scale); the resting hands and the hips
// inside the lower shade's opaque band (NativeDraftScreen.DraftShades), so nothing under the plexus line shows; every hat's
// brim and lower part (its real vertices up to the crown room) and the Auditor halo's ring in view through the browse and the
// pick; the gaze following each highlighted card with the visor kept toward the camera, spring settling without overshoot,
// the highlighted card held across the gaps between cards, the hand on the highlighted card's side rising out of the shade
// above the tray's top line toward it (one side at a time); the pick in frame (the catching palm above the cards in front of
// the chest on the chosen card's side, the card flying to the solved catch the palm meets at the contact; the wrist plant
// above the plexus) for the PvP draft, the Siren pick, a spectated picker and the Siren drafter; no frame-to-frame snaps at
// 30/60/144/240 Hz through browse, pick, catch, plant and return, the smoothed pick clock, and 0 bytes per frame. The card
// rects follow NativeDraftScreen.LayoutTray / LowerTray with a fixed card height (Unity measures the text).
internal static class DraftStageChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Draft stage: "+what);}
    // A screen (pixels) and interface scale as NativeUiMotion lays out the overlays.
    private struct Screen{public float W,H,Scale;public string Name;}
    private static readonly Screen[] Screens={S(1920,1080,1,"1920x1080"),S(3440,1440,1,"3440x1440"),S(2560,1440,1.25f,"2560x1440 ui 1.25"),S(1280,720,1,"1280x720"),S(1366,768,.8f,"1366x768 ui .8"),S(1024,768,1,"1024x768"),S(1280,1024,1,"1280x1024"),S(1920,1080,1.5f,"1920x1080 ui 1.5"),
        S(1920,1200,1,"1920x1200"),S(5120,1440,1,"5120x1440"),S(2560,1440,1,"2560x1440")};
    private static Screen S(float w,float h,float ui,string name){var l=NativeUiMotion.Measure(w,h,ui);return new Screen{W=l.Width,H=l.Height,Scale=w/h,Name=name};}
    // NativeDraftScreen.LayoutTray (rows, widths, spacing) with a .78 height-to-width card, then LowerTray onto the bottom reserve.
    private static Vector4[] Tray(int n,float W,float H,bool pve,out float top,out float bottom)
    {
        float header=(H<=760?16:22)+21+(H<=760?24:Math.Clamp(W*.025f,24,34))*1.08f+4+(H>760?20.3f:0)+Math.Clamp(H*.024f,4,30);
        int fit=W<=760||H<=480?n:Math.Clamp((int)Math.Floor((W-42)/164),2,6);int[] rows=n<=fit?new[]{n}:new[]{(n+1)/2,n/2};bool large=W>=1600&&H>=900;
        float tray=W-56,gap=14,rowGap=H<=760||rows.Length==2&&H<=820?14:20,cap=rows.Length==2?214:large?272:236,y=header;var cards=new Vector4[n];int at=0;
        foreach(int cols in rows){float w=Math.Min(cap,Math.Max(118,(tray-(cols-1)*gap)/cols)),h=w*.78f;y+=10;for(int i=0;i<cols;i++)cards[at+i]=new Vector4(NativeDraftRules.RowX(i,cols,w,tray,gap,W),y,w,h);at+=cols;y+=h+8+rowGap;}
        float end=y-rowGap,off=Math.Max(0,H-(pve?142:H<=760?54:66)-end);for(int i=0;i<n;i++)cards[i].Y+=off;top=cards[0].Y;bottom=end+off;return cards;
    }
    private static Vector2[] Centres(Vector4[] cards,float W,float H){var c=new Vector2[cards.Length];for(int i=0;i<cards.Length;i++)c[i]=new Vector2((cards[i].X+cards[i].Z/2)/W,(cards[i].Y+cards[i].W/2)/H);return c;}
    private static Vector3 Eye(CapturedRig.Pose p)=>p.WorldPosition[CapturedRig.Head]+Vector3.Transform(Vector3.UnitY*NativeDraftStage.EyeUp,p.WorldRotation[CapturedRig.Head]);
    private static Vector3 Forward(CapturedRig.Pose p,int bone)=>Vector3.Transform(Vector3.UnitZ,p.WorldRotation[bone]);
    private static float Angle(Quaternion a,Quaternion b){float d=Math.Min(1,Math.Abs(Quaternion.Dot(a,b)));return 2*(float)Math.Acos(d);}
    // The plant on the model's wrist device (`bay` 1: the stage's default bay; 2: one as the draw measures it on the model, its
    // normal across the hand); 0 the voxel pilot's cuff.
    private static void Device(NativeDraftStage s,int bay)
    {s.Plant.Device=bay>0;if(bay==2){s.Plant.BayLocal=new Vector3(-.009f,.045f,.055f);s.Plant.BayNormal=Vector3.Normalize(new Vector3(-.27f,0,.96f));}}
    // Where the pressing palm comes down at the plant: on the cuff, or beside the device's bay (NativeDraftPlant.PressLift and
    // PressSide along the forearm).
    private static Vector3 Pressed(NativeDraftStage s)=>s.Plant.Device?s.Plant.At+s.Plant.Normal*NativeDraftPlant.PressLift+s.Plant.Forearm*NativeDraftPlant.PressSide:s.Plant.At;
    private static void Frame(NativeDraftStage s,float dt,float rawAge,int focus,Vector2[] cards,float aspect,float trayTop,float trayBottom,bool siren=false,bool consider=false,float crown=0)
    {float age=s.SmoothAge(rawAge,dt);s.Begin(dt,false);if(siren)SirenIdle(s.Pose,s.Clock);s.Frame(aspect,trayTop,trayBottom,crown,siren,dt);s.Step(dt,age,focus,consider,cards,cards.Length,siren,false);}
    // The highest screen row (fraction from the top) that any vertex of a hat model (head space) reaches on the posed head, up to
    // `below` m over the head joint (the crown room Frame keeps for it; a taller tip may cross the top edge).
    private static float HatRow(NativeDraftStage s,Vector3[] hat,float below=float.MaxValue)
    {var q=s.Pose.WorldRotation[CapturedRig.Head];var o=s.Pose.WorldPosition[CapturedRig.Head];float row=float.MaxValue;foreach(var v in hat)if(v.Y<=below)row=Math.Min(row,s.Project(o+Vector3.Transform(v,q)).Y);return row;}
    // The part of a cosmetic Frame keeps in view: up to its crown room over the bare head's top, less the gap for the bow.
    private static readonly float Kept=NativeDraftStage.HeadTop+NativeDraftStage.CrownRoom-NativeDraftStage.CrownGap;
    // The Auditor halo's ring (its group's centre, NativeAuditorHalo.Evaluate on the stage's clock) as a screen row.
    private static float RingRow(NativeDraftStage s,AuditorHaloFrame f)
    {NativeAuditorHalo.Evaluate(s.Clock,false,f);var q=s.Pose.WorldRotation[CapturedRig.Head];return s.Project(s.Pose.WorldPosition[CapturedRig.Head]+Vector3.Transform(f.GroupPosition,q)).Y;}
    // How much of its forward the head turns to the camera (1 facing the lens).
    private static float FaceCamera(NativeDraftStage s){var p=s.Pose;return Vector3.Dot(Forward(p,CapturedRig.Head),Vector3.Normalize(s.CameraPosition-p.WorldPosition[CapturedRig.Head]));}
    // The same for the Auditor halo's flame tips (NativeAuditorHalo.Evaluate on the stage's clock, the quads' tops).
    private static float HaloRow(NativeDraftStage s,AuditorHaloFrame f)
    {
        NativeAuditorHalo.Evaluate(s.Clock,false,f);var q=s.Pose.WorldRotation[CapturedRig.Head];var o=s.Pose.WorldPosition[CapturedRig.Head];float row=float.MaxValue;
        for(int n=0;n<NativeAuditorHalo.Flames;n++){var tip=f.GroupPosition+Vector3.Transform((f.FlameBase[n]+new Vector3(0,f.FlameHeight[n],0))*f.GroupScale,f.GroupRotation);row=Math.Min(row,s.Project(o+Vector3.Transform(tip,q)).Y);}
        return row;
    }
    // A hat's measured top above the head joint (ArenaPresentation.DraftCrown: the model's head-space bounds).
    private static float Crown(Vector3[] hat){float top=0;foreach(var v in hat)top=Math.Max(top,v.Y);return top;}
    // A stand-in for the Siren drafter's idle (siren-idle-melee is in the Unity resources): the draft pose raised and hunched
    // like her clip (root .23 up, chest forward, arms hanging at her sides), breathing on the clock. The stage seats her lower
    // and rests her claws in her lap.
    private static void SirenIdle(CapturedRig.Pose p,float clock)
    {
        NativeDraftRules.Pose(p,clock*.75f);p.Position[CapturedRig.Body]=new Vector3(0,.233f,0);p.Rotation[CapturedRig.Chest]=CapturedRig.Euler(.35f+.02f*(float)Math.Sin(clock*1.3f));
        p.Rotation[CapturedRig.LClav+1]=CapturedRig.Euler(-.2f,0,.25f);p.Rotation[CapturedRig.RClav+1]=CapturedRig.Euler(-.2f,0,-.25f);
    }
    // NativeDraftPresentation.DraftFrame's considered card: the chosen one, else the held highlight, else the scan; the arm
    // lifts while the held card is being reached for.
    private static int Considered(NativeDraftHover hold,int chosen,int hover,float since,int count,float dt,out bool consider)
    {if(chosen>=0)hold.Reset();else hold.Step(hover,dt);consider=chosen<0&&hold.Reaching;return NativeDraftStage.Focus(chosen,hold.Card,since,count,false);}
    private static float HeadYaw(CapturedRig.Pose p){var f=Forward(p,CapturedRig.Head);return (float)Math.Atan2(f.X,f.Z);}
    private static bool OnScreen(Vector2 v,float margin)=>v.X>=margin&&v.X<=1-margin&&v.Y>=margin&&v.Y<=1-margin;
    // The seated table framing this close-up replaced (NativeDraftStage before 2026-10-11: the 24 degree lens tilted 12 degrees,
    // the head's top (under a crown its measured top and .03 more, the Siren .2 more) .05 from the top, the table top under the
    // resting fingertips (y -.065, z .5) .012 above the first card row, the head-top-to-table span between .44 and .74 of the
    // screen, at least 1.8 m across): the screen share of the resting head (.36 m standing at z 0).
    private static float OldSeatShare(float restHead,float aspect,float trayTop,float crown=0,bool boss=false)
    {
        float tilt=12*(float)Math.PI/180,t=(float)Math.Tan(12*Math.PI/180),sn=(float)Math.Sin(tilt),cs=(float)Math.Cos(tilt);
        float Row(float y,float z,float centre,float height){float d=height*.5f/t,dy=y-(centre+sn*d),dz=z-cs*d,depth=-sn*dy-cs*dz,up=cs*dy-sn*dz;return (1-up/(Math.Max(1e-4f,depth)*t))*.5f;}
        float Solve(float y,float z,float row,float centre,float height){for(int i=0;i<8;i++){float r=Row(y,z,centre,height)-row,d=(Row(y,z,centre+1e-3f,height)-row-r)/1e-3f;if(Math.Abs(d)<1e-7f)break;centre-=r/d;}return centre;}
        float top=restHead+Math.Max(.36f,crown>0?crown+.03f:0)+(boss?.2f:0),hy=-.065f,hand=Math.Clamp(trayTop,.3f,.95f)-.012f,h=(top-hy)/Math.Max(.05f,hand-.05f),c=top-.45f*h;
        for(int i=0;i<8;i++)
        {
            const float e=1e-3f;float r1=Row(top,0,c,h)-.05f,r2=Row(hy,.5f,c,h)-hand,a11=(Row(top,0,c+e,h)-.05f-r1)/e,a12=(Row(top,0,c,h+e)-.05f-r1)/e,a21=(Row(hy,.5f,c+e,h)-hand-r2)/e,a22=(Row(hy,.5f,c,h+e)-hand-r2)/e,det=a11*a22-a12*a21;
            if(Math.Abs(det)<1e-7f)break;c-=(r1*a22-r2*a12)/det;h=Math.Max(.3f,h-(a11*r2-a21*r1)/det);
        }
        float least=Math.Max((top-hy)/.74f,1.8f/aspect),most=Math.Max(least,Math.Min(4,(top-hy)/.44f));
        if(h<least){h=least;c=Solve(hy,.5f,hand,c,h);}else if(h>most){h=most;c=Solve(top,0,.05f,c,h);}
        return Row(restHead,0,c,h)-Row(restHead+.36f,0,c,h);
    }

    public static IEnumerable<string> Run(string root)
    {
        // The considered card: chosen, then hover/focus, then the boot scan following the entrance, then the 1.8 s cycle.
        Check(NativeDraftStage.Focus(2,4,0,5,false)==2&&NativeDraftStage.Focus(-1,4,0,5,false)==4&&NativeDraftStage.Focus(-1,-1,.5f,5,false)==2&&NativeDraftStage.Focus(-1,-1,1.2f,5,false)==0
            &&NativeDraftStage.Focus(-1,-1,1.9f,5,false)==1&&NativeDraftStage.Focus(-1,-1,9,5,true)==0&&NativeDraftStage.Focus(-1,-1,3,0,false)==-1&&NativeDraftStage.Focus(7,-1,0,5,false)==4,"focus order");
        // Tray rows: a short row sits centred with at most RowGutter card widths between cards (owner 2026-10-12), a full row
        // still spans the tray (28 px margins), and every row stays inside the screen without overlapping cards.
        foreach(var sc in Screens)foreach(int n in new[]{2,3,4,5,6,8,12})
        {
            var r=Tray(n,sc.W,sc.H,false,out _,out _);int first=0;
            while(first<n)
            {
                int last=first;while(last+1<n&&Math.Abs(r[last+1].Y-r[first].Y)<.5f)last++;float left=r[first].X,right=r[last].X+r[last].Z,w=r[first].Z;
                Check(Math.Abs((left+right)/2-sc.W/2)<.5f,sc.Name+" "+n+" cards: the row is centred ("+left+".."+right+" of "+sc.W+")");
                Check(left>=27.5f&&right<=sc.W-27.5f,sc.Name+" "+n+" cards: the row stays inside the tray");
                for(int i=first;i<last;i++){float g=r[i+1].X-(r[i].X+r[i].Z);Check(g>=-.01f&&g<=Math.Max(14,w*NativeDraftRules.RowGutter)+.01f,sc.Name+" "+n+" cards: gutter "+g+" for "+w+" px cards");}
                first=last+1;
            }
        }
        {var r=Tray(3,1920,1080,false,out _,out _);Check(r[0].X>400,"three cards on 1920x1080 sit together, not at the tray edges (first at "+r[0].X+")");}
        var rest0=new CapturedRig.Pose();NativeDraftRules.Pose(rest0,0);rest0.World();float restHead=rest0.WorldPosition[CapturedRig.Head].Y;
        // The solar plexus comes from the rig: on the chest's front, halfway up from the spine joint (the navel) to the neck joint
        // (the sternal notch), so above the chest joint (the ribcage's base) and below the shoulders, and the seated lean carries
        // it forward.
        {
            var pl=NativeDraftStage.Plexus(rest0);float mid=(rest0.WorldPosition[CapturedRig.Spine].Y+rest0.WorldPosition[CapturedRig.Neck].Y)*.5f,chest=rest0.WorldPosition[CapturedRig.Chest].Y,shoulder=rest0.WorldPosition[CapturedRig.LClav+1].Y;
            Check(Math.Abs(pl.Y-mid)<.015f&&pl.Y>chest&&pl.Y<shoulder-.05f&&Math.Abs(pl.X)<.005f&&pl.Z>.14f&&pl.Z<.2f,"the solar plexus at "+pl+" (spine-neck midpoint "+mid+", chest joint "+chest+")");
            var s0=new NativeDraftStage();Check(s0.RestPlexus.Z>pl.Z&&Math.Abs(s0.RestPlexus.Y-pl.Y)<.03f,"the seated plexus "+s0.RestPlexus+" against standing "+pl);
        }
        // The exported hats (the Auditor halo's model is only its fallback; the halo is checked from its own geometry), the
        // tallest by its measured top.
        var hats=LobbyPoseChecks.HatVertices(root);hats.Remove(NativeAuditorHalo.Hat);string tallest=null;foreach(var h in hats)if(tallest==null||Crown(h.Value)>Crown(hats[tallest]))tallest=h.Key;
        Check(tallest!=null&&Crown(hats[tallest])>.9f,"the tallest hat ("+tallest+") is not measured");var tall=hats[tallest];float tallCrown=Crown(tall);

        // Composition and gaze on every screen, PvP and the Siren pick, 3 to 12 offers (one or two rows), with and without the
        // tallest hat (its brim and lower part up to the crown room kept in view on every frame), and the Siren drafter.
        float tallest2=9,worstGaze=0,worstHeadroom=1,worstAim=0,maxYaw=0,smallest=9,largest=0,crownedLeast=9,nearBare=9,worstHat=1,worstPlexus=0,lowestPitch=0,worstFace=1,worstShade=1;int cases=0,covered=0;var bare=new Dictionary<int,float>();var ring=new AuditorHaloFrame();
        foreach(var sc in Screens)foreach(int kind in new[]{0,1,2,3,4})foreach(int n in new[]{3,5,6,8,12})
        {
            bool pve=kind==1,siren=kind==2,hat=kind==3,halo=kind==4;
            var rects=Tray(n,sc.W,sc.H,pve,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);float trayTop=top/sc.H,trayBottom=bottom/sc.H;var s=new NativeDraftStage();
            float crown=hat?tallCrown:halo?NativeAuditorHalo.Top:0;string what0=sc.Name+" "+n+" cards";
            void HatInView(string when){if(!hat&&!halo)return;float row=hat?HatRow(s,tall,Kept):RingRow(s,ring);worstHat=Math.Min(worstHat,row);Check(row>=.02f,what0+": the "+(hat?tallest+" hat's brim and lower part":"Auditor halo's ring")+" at "+row+" ("+when+")");}
            for(int f=0;f<30;f++){Frame(s,1/60f,-1,0,cards,sc.Scale,trayTop,trayBottom,siren,false,crown);HatInView("settling");}
            var p=s.Pose;string what=sc.Name+" "+n+" cards"+(pve?" (Siren pick)":siren?" (Siren drafter)":hat?" (hat)":halo?" (Auditor halo)":"");
            // From the solar plexus up: the head's top in view under the top edge and above the cards, the face well above the
            // first row; the plexus on or just above the tray's top line (a tall tray covers it, and the face of a very tall one,
            // instead, the head keeping its room); the resting hands (palms down) and the hips inside the lower shade's opaque band.
            float headTop=s.Project(new Vector3(0,restHead+NativeDraftStage.HeadTop+(siren?NativeDraftStage.BossTop:0),0)).Y;
            Check(headTop>=.02f&&headTop<trayTop-.08f,what+": head top at "+headTop+" with the tray at "+trayTop);worstHeadroom=Math.Min(worstHeadroom,headTop);
            bool capped=s.Project(s.RestPlexus).Y>trayTop-NativeDraftStage.PlexusGap+.004f;float plexus=s.Project(NativeDraftStage.Plexus(p)).Y;
            Check(capped||s.Project(Eye(p)).Y<trayTop-.1f,what+": the face behind the first row ("+s.Project(Eye(p)).Y+" against "+trayTop+")");
            Check(plexus>=trayTop-.045f,what+": the solar plexus at "+plexus+" high above the tray's top line at "+trayTop+" (the stomach shows)");
            if(capped)covered++;else{Check(plexus<=trayTop+.012f,what+": the solar plexus at "+plexus+" behind the cards (top line "+trayTop+")");worstPlexus=Math.Max(worstPlexus,Math.Abs(plexus-trayTop));}
            float line=s.ShadeLine(trayTop),deep=s.ShadeDeep(line);
            Check(s.PlexusRow>=0&&line>=plexus-.005f,what+": the lower shade's line at "+line+" over the plexus at "+plexus);
            foreach(bool left in new[]{true,false})
            {
                var hand=p.WorldPosition[left?CapturedRig.LHand:CapturedRig.RHand];var sp=s.Project(hand);worstShade=Math.Min(worstShade,sp.Y-deep);
                Check(sp.Y>deep+.015f,what+": a resting hand at "+sp.Y+" shows above the shade's opaque band ("+deep+")");Check((hand-NativeDraftStage.RestHand(left)).Length()<.01f,what+": a hand does not rest in the lap ("+hand+")");
                Check(Vector3.Transform(Vector3.UnitZ,p.WorldRotation[left?CapturedRig.LHand:CapturedRig.RHand]).Y<-.75f,what+": a resting palm is not down");
            }
            float hips=s.Project(p.WorldPosition[CapturedRig.Pelvis]).Y;worstShade=Math.Min(worstShade,hips-deep);Check(hips>deep+.015f,what+": the hips at "+hips+" show above the shade's opaque band ("+deep+")");
            // Close: the resting head at least 1.25 times its share in the seated table framing (that framing took the whole crown,
            // and .2 more for the Siren), crowned and Siren pilots too, and near the bare head's share on the same tray; centred.
            {
                float share=s.Project(new Vector3(0,restHead,0)).Y-s.Project(new Vector3(0,restHead+.36f,0)).Y,old=OldSeatShare(restHead,sc.Scale,trayTop,crown,siren),k=share/old;
                // (Two rows of cards: the pilot sits above them, smaller, rather than under them; owner 2026-10-29: "The player is
                // under chips if theres around 12 chips".)
                bool twoRows=trayBottom-trayTop>.36f||trayTop<.62f;Check(k>=(twoRows?(capped?.6f:.8f):capped?.98f:1.25f),what+": the head only "+k+"x its table-framing size (tray "+trayTop.ToString("0.000")+" to "+trayBottom.ToString("0.000")+(capped?", capped":"")+")");
                if(twoRows){tallest2=Math.Min(tallest2,k);Check(capped||s.Project(Eye(p)).Y<trayTop-.12f,what+": two rows, the face above the cards");}
                if(kind==0){bare[n]=share;smallest=Math.Min(smallest,k);largest=Math.Max(largest,k);}else if(!pve){crownedLeast=Math.Min(crownedLeast,k);float near=share/bare[n];nearBare=Math.Min(nearBare,near);Check(near>=.75f,what+": the head only "+near+" of its bare-headed size");}
            }
            float x=s.Project(p.WorldPosition[CapturedRig.Chest]).X;Check(Math.Abs(x-.5f)<.02f,what+": the pilot is not centred ("+x+")");
            // The gaze lands on each card in turn: its point projects onto the card's centre, and the head's aim falls nearest that
            // card among its row (yaw ordered left to right), looking down at the cards.
            float lastYaw=-9;int lastRow=-1;
            for(int i=0;i<n;i++)
            {
                for(int f=0;f<72;f++){Frame(s,1/60f,-1,i,cards,sc.Scale,trayTop,trayBottom,siren,false,crown);HatInView("looking at card "+i);}
                var g=s.Project(s.Gaze);float err=Math.Max(Math.Abs(g.X-cards[i].X)*sc.W,Math.Abs(g.Y-cards[i].Y)*sc.H);worstGaze=Math.Max(worstGaze,err);
                Check(err<1.5f,what+": gaze "+err+" px off card "+i);
                var eye=Eye(p);var hf=Forward(p,CapturedRig.Head);float headYaw=(float)Math.Atan2(hf.X,hf.Z);int row=rects[i].Y>rects[0].Y+1?1:0,nearest=-1;float best=float.MaxValue,own=0;
                for(int j=0;j<n;j++){if((rects[j].Y>rects[0].Y+1?1:0)!=row)continue;var q=s.OnDisplay(cards[j].X,cards[j].Y)-eye;float yj=(float)Math.Atan2(q.X,q.Z),d=Math.Abs(yj-headYaw);if(j==i)own=yj;if(d<best){best=d;nearest=j;}}
                Check(nearest==i,what+": the head turns nearest card "+nearest+", not "+i);if(Math.Abs(own)<NativeDraftStage.YawLimit-.05f)worstAim=Math.Max(worstAim,Math.Abs(own-headYaw));
                Check(worstAim<.09f,what+": the head turns "+Math.Abs(own-headYaw)+" rad off card "+i);
                var fwd=Forward(p,CapturedRig.Head);float yaw=(float)Math.Atan2(fwd.X,fwd.Z),pitch=(float)Math.Asin(Math.Clamp(fwd.Y,-1,1));maxYaw=Math.Max(maxYaw,Math.Abs(yaw));lowestPitch=Math.Min(lowestPitch,pitch);
                Check(capped||pitch<-.03f,what+": the head does not look down at card "+i+" (pitch "+pitch+")");
                if(row==lastRow)Check(yaw>lastYaw,what+": head yaw not ordered at card "+i);lastYaw=yaw;lastRow=row;
                // Close up the face stays to the camera: the visor, not the head's top or side, faces the lens on every card.
                float face=FaceCamera(s);worstFace=Math.Min(worstFace,face);Check(face>=NativeDraftStage.FaceCamera,what+": the head turns its face away from the camera at card "+i+" ("+face+")");
                // The lean: the upper spine and neck bow toward the cards too, not only the head (the Siren is hunched already).
                Check(capped||siren||Forward(p,CapturedRig.Chest).Y<-.01f&&Forward(p,CapturedRig.Neck).Y<-.04f,what+": the chest and neck do not bow (card "+i+", chest "+Forward(p,CapturedRig.Chest).Y+", neck "+Forward(p,CapturedRig.Neck).Y+")");
            }
            cases++;
        }
        yield return "Draft stage close-up on "+Screens.Length+" screens and interface scales (16:9, 16:10, 21:9, 32:9, 4:3, 5:4), PvP draft and Siren pick trays, the Siren drafter, a hat and the Auditor halo, 3-12 offers ("+cases+" layouts): the solar plexus (rig spine-neck midpoint on the chest's front) within "+(worstPlexus*100).ToString("0.0")+"% of the tray's top line ("+covered+" tall trays cover it to keep the head's room; two rows of cards put the pilot above them, its head at least "+tallest2.ToString("0.00")+"x its table framing), the head's top >= "+(worstHeadroom*100).ToString("0")+"% from the top and above the cards, the face well above the first row (but on the tallest trays), the resting hands (palms down) and the hips at least "+(worstShade*100).ToString("0.0")+"% inside the lower shade's opaque band; the "+tallest+" hat ("+tallCrown.ToString("0.00")+" m over the head joint) in view up to "+Kept.ToString("0.00")+" m and the halo's ring, within "+(worstHat*100).ToString("0.0")+"% of the top at worst; the head "+smallest.ToString("0.00")+"-"+largest.ToString("0.00")+"x its size in the seated table framing bare-headed, at least "+crownedLeast.ToString("0.00")+"x under the hat and the halo and as the Siren drafter (at least "+nearBare.ToString("0.00")+" of the bare-headed size); gaze on every card within "+worstGaze.ToString("0.0")+" px, head turned nearest the highlighted card (within "+(worstAim*57.3f).ToString("0.0")+" deg, yaw up to "+(maxYaw*57.3f).ToString("0")+" deg, looking down up to "+(-lowestPitch*57.3f).ToString("0")+" deg) with its face at least "+worstFace.ToString("0.00")+" toward the camera, chest and neck bowed";

        // Every hat (framed on its measured top, as ArenaPresentation.DraftCrown measures it, up to CrownRoom over the bare head)
        // keeps its brim and lower part (its vertices up to the crown room) in view, and the Auditor halo (framed on
        // NativeAuditorHalo.Top, which bounds its flames at every moment and the source halo the reduced one draws) its ring, on
        // every screen through a browse across a 6-card PvP and an 8-card Siren-pick tray and the pick (30 Hz); a taller tip or
        // the flames may cross the top edge.
        {
            var id=new CapturedRig.Pose();for(int b=0;b<CapturedRig.Bones.Length;b++){id.WorldRotation[b]=Quaternion.Identity;id.WorldPosition[b]=Vector3.Zero;}
            var hf=new AuditorHaloFrame();float reach=0;
            for(int k=0;k<1200;k++)
            {
                float t=k*.05f;NativeAuditorHalo.Evaluate(t,false,hf);reach=Math.Max(reach,NativePortraitRules.HaloTop(id,t)-NativePortraitRules.GroupY);
                for(int n=0;n<NativeAuditorHalo.Flames;n++)reach=Math.Max(reach,(hf.GroupPosition+Vector3.Transform((hf.FlameBase[n]+new Vector3(0,hf.FlameHeight[n],0))*hf.GroupScale,hf.GroupRotation)).Y);
            }
            Check(NativeAuditorHalo.Top>=reach&&NativeAuditorHalo.Top<reach+.03f,"the halo's top "+NativeAuditorHalo.Top+" for a reach of "+reach);
            float worst=1;string where="";int whole=0;var names=new List<string>(hats.Keys){NativeAuditorHalo.Hat};
            foreach(string hat in names)
            {
                bool halo=hat==NativeAuditorHalo.Hat,short_=!halo&&Crown(hats[hat])<=Kept;if(short_)whole++;
                foreach(var sc in Screens)foreach(bool pve in new[]{false,true})
                {
                    int n=pve?8:6;var rects=Tray(n,sc.W,sc.H,pve,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);var s=new NativeDraftStage();var hold=new NativeDraftHover();
                    float crown=halo?NativeAuditorHalo.Top:Crown(hats[hat]),dt=1/30f,pickAt=1.2f+.6f*n;
                    for(int f=0;f<=(int)((pickAt+3.5f)*30);f++)
                    {
                        float t=f*dt;int hover=t<1.2f?-1:Math.Min(n-1,(int)((t-1.2f)/.6f));float raw=t>=pickAt?t-pickAt:-1;int focus=Considered(hold,raw>=0?n-1:-1,hover,t,n,dt,out bool consider);
                        Frame(s,dt,raw,focus,cards,sc.Scale,top/sc.H,bottom/sc.H,false,consider,crown);float row=halo?RingRow(s,hf):HatRow(s,hats[hat],Kept);
                        if(row<worst){worst=row;where=hat+" "+sc.Name+(pve?" Siren pick":"")+" t "+t.ToString("0.00");}Check(row>=.02f,"the "+hat+(halo?" ring":" brim")+" at "+row+" on "+sc.Name+(pve?" (Siren pick)":"")+" at t "+t.ToString("0.00"));
                    }
                }
            }
            yield return "Draft stage hats: all "+hats.Count+" hats (framed on their measured tops but at most "+NativeDraftStage.CrownRoom.ToString("0.00")+" m over the bare head, up to "+tallCrown.ToString("0.00")+" m over the head joint) keep their brims and lower parts in view ("+whole+" whole), and the Auditor halo its ring (its top "+NativeAuditorHalo.Top.ToString("0.000")+" m bounds its flames and the source halo, "+reach.ToString("0.000")+" m), through browse and pick on "+Screens.Length+" screens, 6-card PvP and 8-card Siren-pick trays (highest at "+(worst*100).ToString("0.0")+"% from the top: "+where+")";
        }

        // The pick in frame: on every screen, for a one-row PvP tray and the two-row Siren pick, picking the outermost card on
        // either side, as the local picker (the arm was considering it) and as a spectated picker (no highlight), the catching
        // palm is on screen at the contact, in front of the chest on the card's side and, over a one-row tray, above the cards;
        // the card flies to the stage's solved catch (CatchPalm), which holds still on the screen through the flight (so the card
        // never dips toward the hand still rising from the lap) and is where the palm meets it at the contact; the wrist plant
        // (carry end to sink) is on screen above the solar plexus and over a one-row tray above the cards; the Siren drafter's
        // catch is in frame too and held to the return. On the model's wrist device (NativeDraftPlant.Device: the stage's
        // default bay and one as measured on the model), for the local picker: the bay faces the camera from the carry's end to
        // the sink, the chip lies in it at the carry's end (its face as it showed in the palm: through the seat it
        // never turns past edge-on) and has shrunk to the bay's chip, and the pressing palm comes down beside the bay, not over it.
        {
            float worstCatch=1,worstPlantRow=1,worstSide=1,worstDrift=0,worstMeet=0,worstFacing=1,worstLay=1,worstSeat=0,nearestPalm=1;int picks=0,devicePicks=0;
            foreach(var sc in Screens)foreach(int kind in new[]{0,1,2})foreach(bool lastCard in new[]{false,true})foreach(bool local in new[]{true,false})foreach(int bay in kind==2||!local?new[]{0}:new[]{0,1,2})
            {
                bool pve=kind==1,siren=kind==2;int n=pve?8:5,chosen=lastCard?n-1:0;var rects=Tray(n,sc.W,sc.H,pve,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);float trayTop=top/sc.H,dt=1/60f;
                bool oneRow=rects[n-1].Y<=rects[0].Y+1;var s=new NativeDraftStage();var hold=new NativeDraftHover();string what=sc.Name+(pve?" Siren pick":siren?" Siren drafter":" PvP")+" card "+chosen+(local?"":" (spectated)")+(bay==1?" (device)":bay==2?" (model's device)":"");
                Device(s,bay);if(bay>0)devicePicks++;
                Vector2 firstCatch=default;bool flying=false;
                for(int f=0;f<=(int)(4.4f*60);f++)
                {
                    float t=f*dt,raw=t>=2?t-2:-1;int focus=Considered(hold,raw>=0?chosen:-1,local&&t>=1?chosen:-1,t,n,dt,out bool consider);
                    Frame(s,dt,raw,focus,cards,sc.Scale,trayTop,bottom/sc.H,siren,consider&&local);float age=s.Age;
                    if(age>=NativeDraftStage.FlyStart&&age<=NativeDraftPlant.Contact){var c=s.Project(s.CatchPalm);if(!flying){firstCatch=c;flying=true;}float drift=Math.Max(Math.Abs(c.X-firstCatch.X)*sc.Scale,Math.Abs(c.Y-firstCatch.Y));worstDrift=Math.Max(worstDrift,drift);Check(drift<.03f,what+": the catch the card flies to drifts "+drift+" of the screen at "+age.ToString("0.00"));}
                    if(age>=0&&Math.Abs(age-NativeDraftPlant.Contact)<.5f*dt+.001f)
                    {
                        // A pilot's left palm takes every card (just across the middle for one on the right); the Siren's on its side.
                        var palm=s.Project(s.Palm);bool side=siren?(s.Palm.X>=0)==(cards[chosen].X>=.5f):s.Left&&s.Palm.X>=NativeDraftStage.CrossBeside-.03f;float chestZ=s.Pose.WorldPosition[CapturedRig.Chest].Z;picks++;
                        Check(OnScreen(palm,.02f),what+": the catch off the screen ("+palm+")");Check(side,what+": the catch on the wrong side ("+s.Palm.X+")");Check(s.Palm.Z>chestZ+.2f,what+": the catch not in front of the chest ("+s.Palm+")");
                        float meet=(s.Palm-s.CatchPalm).Length();worstMeet=Math.Max(worstMeet,meet);Check(meet<.02f,what+": the palm meets the card "+meet+" m from the catch it flew to");
                        if(oneRow){Check(palm.Y<trayTop-.03f,what+": the catch behind the cards ("+palm.Y+" against "+trayTop+")");worstCatch=Math.Min(worstCatch,trayTop-palm.Y);}
                        worstSide=Math.Min(worstSide,Math.Min(palm.X,1-palm.X));
                    }
                    if(age>=NativeDraftPlant.Contact&&age<=NativeDraftPlant.ReturnStart&&siren)Check(OnScreen(s.Project(s.Palm),.02f)&&(oneRow?s.Project(s.Palm).Y<trayTop-.03f:true),what+": the Siren's held chip leaves the frame at "+age.ToString("0.00"));
                    if(!siren&&age>=NativeDraftPlant.CarryEnd&&age<=NativeDraftPlant.PlantEnd)
                    {
                        var plantAt=s.Project(s.Plant.At);float plexus=s.Project(NativeDraftStage.Plexus(s.Pose)).Y;
                        Check(OnScreen(plantAt,.05f)&&plantAt.Y<plexus-.04f,what+": the plant at "+plantAt+" (plexus row "+plexus+")");if(oneRow){Check(plantAt.Y<trayTop-.05f,what+": the plant behind the cards ("+plantAt.Y+")");worstPlantRow=Math.Min(worstPlantRow,trayTop-plantAt.Y);}
                        if(bay>0){float facing=Vector3.Dot(s.Plant.Normal,Vector3.Normalize(s.CameraPosition-s.Plant.At));worstFacing=Math.Min(worstFacing,facing);Check(facing>.5f,what+": the bay faces "+facing+" toward the camera at "+age.ToString("0.00"));}
                    }
                    if(bay>0&&age>=NativeDraftPlant.CarryEnd-NativeDraftPlant.SeatSpan&&age<=NativeDraftPlant.CarryEnd+.001f)
                    {
                        float lay=Vector3.Dot(Vector3.Transform(Vector3.UnitY,s.Plant.ChipRotation),-s.Plant.Normal);worstLay=Math.Min(worstLay,lay);Check(lay>-.3f,what+": the chip turns over into the bay ("+lay+") at "+age.ToString("0.00"));
                        if(Math.Abs(age-NativeDraftPlant.CarryEnd)<.5f*dt+.001f)
                        {
                            float gap=(s.Plant.Chip-s.Plant.At).Length(),k=NativeDraftPlant.DeviceChipScale(age,0,true)*NativeDraftPlant.PickupWidth;worstSeat=Math.Max(worstSeat,gap);nearestPalm=Math.Min(nearestPalm,(s.Palm-s.Plant.At).Length());
                            Check(gap<.01f&&lay>.95f,what+": the chip not in the bay at the carry's end ("+gap+" m, face "+lay+")");Check(Math.Abs(k-NativeDraftPlant.BayChipWidth)<.003f,what+": the chip "+k+" m wide in the bay");
                            Check((s.Palm-s.Plant.At).Length()>.06f,what+": the palm over the bay ("+(s.Palm-s.Plant.At).Length()+" m)");
                        }
                    }
                }
            }
            yield return "Draft pick in frame on "+Screens.Length+" screens (PvP, Siren pick and Siren drafter, outer cards both sides, local and spectated; "+picks+" picks): the catching palm in front of the chest (a pilot's left palm, just across the middle for a card on the right; the Siren's on the card's side), on screen (>= "+(worstSide*100).ToString("0")+"% from a side) and at least "+(worstCatch*100).ToString("0.0")+"% above a one-row tray; the card flies to the solved catch (still within "+(worstDrift*100).ToString("0.0")+"% of the screen through the flight, met by the palm within "+(worstMeet*1000).ToString("0")+" mm); the wrist plant above the solar plexus and at least "+(worstPlantRow*100).ToString("0.0")+"% above a one-row tray; the Siren drafter's catch held in frame to the return; on the model's wrist device ("+devicePicks+" picks) the bay facing the camera (at least "+worstFacing.ToString("0.00")+"), the chip in it at the carry's end (within "+(worstSeat*1000).ToString("0.0")+" mm, the bay chip's size, never past edge-on through the seat: its face at least "+worstLay.ToString("0.00")+" into the bay) and the palm beside it (at least "+(nearestPalm*100).ToString("0")+" cm off)";
        }

        // Settling: a step across the whole display and a step to a neighbour settle without overshoot (95% in .45 s, 1% in 1.1 s).
        {
            var sc=Screens[0];var rects=Tray(6,sc.W,sc.H,false,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);float worst95=0,worstOver=0,longest=0;
            foreach(var (a,b) in new[]{(0,5),(5,0),(2,3),(4,1)})
            {
                var s=new NativeDraftStage();for(int f=0;f<90;f++)Frame(s,1/60f,-1,a,cards,sc.Scale,top/sc.H,bottom/sc.H);
                float start=s.Gaze.X;Frame(s,0,-1,b,cards,sc.Scale,top/sc.H,bottom/sc.H);float goal=s.OnDisplay(cards[b].X,cards[b].Y).X,span=goal-start,settled95=-1,yawGoal;
                var yaws=new List<float>();
                for(int f=1;f<=120;f++)
                {
                    Frame(s,1/60f,-1,b,cards,sc.Scale,top/sc.H,bottom/sc.H);float u=(s.Gaze.X-start)/span;worstOver=Math.Max(worstOver,u-1);if(settled95<0&&u>=.95f)settled95=f/60f;
                    if(f==66)Check(Math.Abs(1-u)<.01f,"gaze not within 1% at 1.1 s ("+u+")");var fw=Forward(s.Pose,CapturedRig.Head);yaws.Add((float)Math.Atan2(fw.X,fw.Z));
                }
                yawGoal=yaws[yaws.Count-1];float sign=Math.Sign(yawGoal-yaws[0]);for(int k=1;k<yaws.Count;k++){Check(sign*(yaws[k]-yaws[k-1])>-2e-3f,"head yaw turns back while settling "+a+"->"+b);Check(sign*(yaws[k]-yawGoal)<.015f,"head yaw overshoots "+a+"->"+b+" by "+sign*(yaws[k]-yawGoal)+" at frame "+k);}
                // A neighbour settles in .45 s; a longer glance adds its turn at TurnSpeed.
                var e0=Eye(s.Pose);var qa=s.OnDisplay(cards[a].X,cards[a].Y)-e0;var qb=s.OnDisplay(cards[b].X,cards[b].Y)-e0;float turn=Math.Abs((float)(Math.Atan2(qb.X,qb.Z)-Math.Atan2(qa.X,qa.Z))),allowed=.45f+(Math.Abs(a-b)>1?turn/NativeDraftStage.TurnSpeed:0);
                Check(settled95>0&&settled95<=allowed,"gaze 95% at "+settled95+" s, allowed "+allowed+" ("+a+"->"+b+")");worst95=Math.Max(worst95,Math.Abs(a-b)==1?settled95:0);longest=Math.Max(longest,settled95);
            }
            Check(worstOver<1e-3f,"gaze overshoots by "+worstOver);
            yield return "Gaze: a neighbouring card settles to 95% in "+worst95.ToString("0.00")+" s, a glance across the display in "+longest.ToString("0.00")+" s (turn capped at "+NativeDraftStage.TurnSpeed+" rad/s), within 1% by 1.1 s, no overshoot (gaze "+(worstOver*100).ToString("0.000")+"%, head yaw within 1 deg)";
        }

        // The pointer between cards (owner: the pilot looks at whichever chip is highlighted). At 1920x1080 with 5 cards the
        // pointer crosses about 170 px of tray between two cards; meanwhile the scan card can be at the far end. The held
        // highlight keeps the head on the card just left until the next is reached: it never turns the wrong way (toward a
        // third card) or past the next card, the arm does not dip for a short gap and lowers after a long one (the gaze holds).
        {
            var sc=Screens[0];var rects=Tray(5,sc.W,sc.H,false,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);float trayTop=top/sc.H,trayBottom=bottom/sc.H;float worstWrong=0,worstPast=0,rawWrong=0,worstDip=0,longGap=0;
            foreach(int hz in new[]{30,60,144,240})foreach(float gap in new[]{.1f,.15f,.25f,.5f,1.2f})foreach(var (a,b) in new[]{(1,2),(2,3),(0,1),(4,3),(3,1)})foreach(bool held in new[]{true,false})
            {
                // The scan card during the gap: the far end behind the card just left, else the far end past the next one.
                float dt=1f/hz;int far=b>a?(a>0?0:4):(a<4?4:0);double since0=9+1.8*far+.5-1.2;var s=new NativeDraftStage();var hold=new NativeDraftHover();var yaws=new List<float>();float yawA=0,wA=0;bool sideA=false;
                int frames=(int)Math.Round((2.4+gap)*hz);
                for(int f=0;f<=frames;f++)
                {
                    double tt=Math.Max(0,(double)(f-1)/hz);int hover=tt<1.2?a:tt<1.2+gap-1e-6?-1:b;float since=(float)(since0+tt);
                    int focus;bool consider;if(held)focus=Considered(hold,-1,hover,since,5,dt,out consider);else{focus=NativeDraftStage.Focus(-1,hover,since,5,false);consider=hover>=0;}
                    Frame(s,dt,-1,focus,cards,sc.Scale,trayTop,trayBottom,false,consider);float y=HeadYaw(s.Pose);if(f==0)sideA=true;
                    if(tt<1.2){yawA=y;wA=s.Weight(sideA);}else yaws.Add(y);
                    if(held&&tt>=1.2&&tt<1.2+gap-1e-6){if(gap<NativeDraftHover.HoldReach)worstDip=Math.Max(worstDip,wA-s.Weight(sideA));if(gap>1&&tt>1.2+NativeDraftHover.HoldReach+.5)longGap=Math.Max(longGap,s.Weight(sideA));}
                }
                float yawB=yaws[yaws.Count-1],sign=Math.Sign(yawB-yawA),wrong=0,past=0;foreach(float y in yaws){wrong=Math.Max(wrong,sign*(yawA-y));past=Math.Max(past,sign*(y-yawB));}
                if(held){worstWrong=Math.Max(worstWrong,wrong);worstPast=Math.Max(worstPast,past);Check(wrong<.01f,"the head turns "+(wrong*57.3f).ToString("0.0")+" deg the wrong way between cards "+a+" and "+b+" ("+gap+" s gap, "+hz+" Hz)");}
                else if(gap<=.25f)rawWrong=Math.Max(rawWrong,wrong);
            }
            Check(worstPast<.015f,"the head turns "+worstPast+" rad past the next card");Check(worstDip<.01f,"the arm dips by "+worstDip+" between cards");Check(longGap<.1f,"the arm stays lifted ("+longGap+") after the pointer left the cards");
            yield return "Highlight held between cards (1920x1080, 5 cards, .1-1.2 s gaps, 30-240 Hz): the head never turns the wrong way (at most "+(worstWrong*57.3f).ToString("0.0")+" deg; "+(rawWrong*57.3f).ToString("0")+" deg toward the scan card without the hold), nor past the next card, the arm holds through a gap under "+NativeDraftHover.HoldReach+" s and lowers after a longer one";
        }

        // Considering (draft-stage.ts): at 30/60/144/240 Hz, the hand on the highlighted card's side lifts out of the lap and moves
        // toward the card while the other stays resting, on every card of a wide tray; the Siren drafter's claws stay resting. On
        // every screen, over a one-row tray (3 to 6 cards) and the two-row Siren pick, the lifted wrist shows: above the tray's top
        // line (the cards would hide it) and above the lower shade's line.
        {
            var sc=Screens[1];var rects=Tray(6,sc.W,sc.H,false,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);float trayTop=top/sc.H,trayBottom=bottom/sc.H;float worstToward=0,worstUp=1,worstDown=0,leastLift=9,worstRest=0,worstShow=1;
            foreach(int hz in new[]{30,60,144,240})for(int i=0;i<6;i++)foreach(bool siren in new[]{false,true})
            {
                var s=new NativeDraftStage();float dt=1f/hz;for(int f=0;f<(int)(1.2f*hz);f++)Frame(s,dt,-1,i,cards,sc.Scale,trayTop,trayBottom,siren,true);
                // A pilot's left hand considers every card (it takes the chip to the right wrist).
                var goal=s.OnDisplay(cards[i].X,cards[i].Y);const bool left=true;
                if(siren){Check(s.Weight(true)==0&&s.Weight(false)==0,"the Siren drafter reaches for card "+i);continue;}
                worstUp=Math.Min(worstUp,s.Weight(left));worstDown=Math.Max(worstDown,s.Weight(!left));
                var p=s.Pose;var restHand=NativeDraftStage.RestHand(left);var hand=p.WorldPosition[left?CapturedRig.LHand:CapturedRig.RHand];var other=p.WorldPosition[left?CapturedRig.RHand:CapturedRig.LHand];
                var move=hand-restHand;var want=goal-restHand;move.Y=want.Y=0;float toward=(float)Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(move),Vector3.Normalize(want)),-1,1));
                worstToward=Math.Max(worstToward,toward);leastLift=Math.Min(leastLift,hand.Y-restHand.Y);worstRest=Math.Max(worstRest,(other-NativeDraftStage.RestHand(!left)).Length());
            }
            foreach(var sc2 in Screens)foreach(int n in new[]{3,5,6,8})foreach(bool pve in new[]{false,true})
            {
                if(pve!=(n==8))continue;var r2=Tray(n,sc2.W,sc2.H,pve,out float top2,out float bottom2);var c2=Centres(r2,sc2.W,sc2.H);float tray2=top2/sc2.H;bool oneRow=r2[n-1].Y<=r2[0].Y+1;
                foreach(int i in new[]{0,n/2,n-1})
                {
                    var s=new NativeDraftStage();for(int f=0;f<72;f++)Frame(s,1/60f,-1,i,c2,sc2.Scale,tray2,bottom2/sc2.H,false,true);const bool left=true;
                    float wrist=s.Project(s.Pose.WorldPosition[left?CapturedRig.LHand:CapturedRig.RHand]).Y,line=s.ShadeLine(tray2),edge=oneRow?Math.Min(tray2,line):line;worstShow=Math.Min(worstShow,edge-wrist);
                    Check(wrist<edge-.01f,sc2.Name+" "+n+" cards: the considering wrist for card "+i+" at "+wrist+" hides behind the cards or the shade (line "+edge+")");
                }
            }
            Check(worstUp>.97f&&worstDown<.03f,"considering arm at "+worstUp+", the other at "+worstDown);Check(worstToward<.6f,"the considering hand moves "+worstToward+" rad away from its card");
            Check(leastLift>.05f,"the considering hand lifts only "+leastLift+" m out of the lap");Check(worstRest<.01f,"the other hand leaves the lap by "+worstRest+" m");
            yield return "Considering: a pilot's left hand (it takes the chip to the right wrist) lifts "+(leastLift*100).ToString("0")+"+ cm out of the lap and moves toward it (within "+(worstToward*57.3f).ToString("0")+" deg) while the other rests (within "+(worstRest*1000).ToString("0")+" mm) on all 6 cards of a 3440x1440 tray at 30-240 Hz, and on every screen the lifted wrist shows at least "+(worstShow*100).ToString("0.0")+"% above the tray's top line and the lower shade's; the Siren drafter's claws stay resting";
        }

        // Motion at 30/60/144/240 Hz through the boot scan, quick hover changes (far and near, switching sides), the pick on the
        // edge card, the catch, contact, plant and return, for the local picker (the arm considers the highlighted card), a
        // remote picker (no highlight: only the pick's catch) and the Siren drafter (claws resting until her catch): no
        // frame-to-frame snap of the head, chest or hands, never both arms lifted, no dip as a considering arm takes the card,
        // the hands back in the lap after the return, the same motion at every rate.
        float headRate=0,chestRate=0,handRate=0,browseHand=0,bothUp=0,worstPlant=0,worstRate=0,dip=0,settle=0;long allocated=0;string at="";var heads=new Dictionary<int,List<Quaternion>>();
        foreach(int hz in new[]{30,60,144,240})foreach(int mode in new[]{0,1,2,3})
        {
            bool siren=mode==2,local=mode!=1;var hold=new NativeDraftHover();
            var sc=Screens[1];var rects=Tray(6,sc.W,sc.H,false,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);var s=new NativeDraftStage();float dt=1f/hz;double pickAt=4.2;Device(s,mode==3?2:0);
            Quaternion lastHead=default,lastChest=default;Vector3 lastL=default,lastR=default;bool first=true;float lastAge=-1,lastWl=0,lastWr=0;var samples=new List<Quaternion>();
            for(int f=0;f<=(int)(7.0*hz);f++)
            {
                // Inputs are read at the start of each frame's interval (a frame integrates the time since the last one), so
                // every rate sees each change at the same moment.
                double time=(double)f/hz,start=Math.Max(0,(double)(f-1)/hz);float t=(float)time;int hover=!local||start<1.4?-1:start<2.0?0:start<2.3?5:start<3.0?2:start<3.6?3:5;
                float raw=start>=pickAt?(float)(start-pickAt):-1;long before=GC.GetAllocatedBytesForCurrentThread();int focus=Considered(hold,raw>=0?5:-1,hover,(float)(Math.Floor(start*30+1e-6)/30),6,dt,out bool consider);
                // A guest's authoritative clock steps with 15 Hz network frames; the highlighted card changes on 30 Hz marks, so
                // every rate sees the same targets at the same times.
                float stepped=raw<0?-1:(float)(Math.Floor(raw*15+1e-6)/15);
                Frame(s,dt,stepped,focus,cards,sc.Scale,top/sc.H,bottom/sc.H,siren,consider);if(f>3)allocated+=GC.GetAllocatedBytesForCurrentThread()-before;
                var p=s.Pose;var head=p.WorldRotation[CapturedRig.Head];var chest=p.WorldRotation[CapturedRig.Chest];var l=p.WorldPosition[CapturedRig.LHand];var r=p.WorldPosition[CapturedRig.RHand];
                if(!first)
                {
                    float h=Angle(head,lastHead)/dt,c=Angle(chest,lastChest)/dt,m=Math.Max((l-lastL).Length(),(r-lastR).Length())/dt;
                    if(h>headRate){headRate=h;at=hz+" Hz t "+t.ToString("0.000");}chestRate=Math.Max(chestRate,c);handRate=Math.Max(handRate,m);if(raw<0&&mode==0)browseHand=Math.Max(browseHand,m);
                }
                // One arm considers at a time; a remote picker's and the Siren's arms stay resting until the pick.
                float wl=s.Weight(true),wr=s.Weight(false);if(raw<0)bothUp=Math.Max(bothUp,Math.Min(wl,wr));
                if(raw<0&&mode==1||siren)Check(wl==0&&wr==0,(siren?"Siren":"remote picker")+" arm lifted before the pick at "+hz+" Hz t "+t.ToString("0.00"));
                if(mode==0&&raw<0&&start>=1.5)Check(Math.Max(wl,wr)>.02f,"no arm considers the highlighted card at "+hz+" Hz t "+t.ToString("0.00"));
                // The considering arm keeps lifting through the pick: its weight never drops before the contact.
                if(mode==0&&raw>=0&&s.Age<NativeDraftPlant.Contact)dip=Math.Max(dip,(s.Left?lastWl:lastWr)-s.Weight(s.Left));lastWl=wl;lastWr=wr;
                first=false;lastHead=head;lastChest=chest;lastL=l;lastR=r;
                if(raw>=0){Check(s.Age>=0&&Math.Abs(s.Age-raw)<.07f&&s.Age>=lastAge,"smoothed age "+s.Age+" vs "+raw+" (last "+lastAge+")");lastAge=s.Age;}
                if(hz%10==0&&f%(hz/10)==0){samples.Add(head);}
                if(raw>=0)
                {
                    float age=s.Age;
                    // From the catch to the return the reaching palm stays in front of the chest.
                    if(age>=NativeDraftPlant.Contact&&age<NativeDraftPlant.ReturnStart)Check(s.Palm.Z>p.WorldPosition[CapturedRig.Chest].Z+.12f,"the palm falls back against the chest at "+hz+" Hz age "+age.ToString("0.00"));
                    if(!siren&&Math.Abs(age-NativeDraftPlant.CarryEnd)<.5f*dt+.002f)worstPlant=Math.Max(worstPlant,(s.Palm-Pressed(s)).Length());
                    // After the return both hands rest in the lap again.
                    if(age>NativeDraftPlant.ReturnEnd+.3f)settle=Math.Max(settle,Math.Max((l-NativeDraftStage.RestHand(true)).Length(),(r-NativeDraftStage.RestHand(false)).Length()));
                }
            }
            if(mode==0&&hz%10==0)heads[hz]=samples;
        }
        // The same motion at every frame rate: head turns at shared .1 s marks agree closely while browsing (exact springs on
        // a constant-speed turn) and stay close through the pick (whose hand-held targets trail by one frame).
        int marks=Math.Min(heads[30].Count,heads[240].Count);float worstPick=0;
        for(int k=0;k<marks;k++){float d=Math.Max(Angle(heads[30][k],heads[240][k]),Angle(heads[60][k],heads[240][k]));if(k*.1f<4.2f-1e-3f)worstRate=Math.Max(worstRate,d);else worstPick=Math.Max(worstPick,d);}
        Check(headRate<8,"the head turns "+headRate+" rad/s in one frame (a snap) at "+at);Check(chestRate<4,"the chest turns "+chestRate+" rad/s in one frame");Check(handRate<4.5f,"a hand moves "+handRate+" m/s in one frame");Check(browseHand<2.8f,"a considering hand moves "+browseHand+" m/s");Check(bothUp<.5f,"both arms lifted ("+bothUp+") on a switch of sides");Check(dip<1e-4f,"the considering arm dips by "+dip+" as it takes the card");
        Check(worstPlant<.03f,"the palm misses the wrist by "+worstPlant+" m at the plant");Check(settle<.01f,"the hands do not settle back in the lap ("+settle+" m)");Check(worstRate<.02f,"frame rates disagree by "+worstRate+" rad while browsing");Check(worstPick<.15f,"frame rates disagree by "+worstPick+" rad through the pick");Check(allocated==0,"the stage allocated "+allocated+" bytes");
        yield return "Draft stage at 30/60/144/240 Hz (local and remote pilot, the local one on the model's wrist device, and Siren, 15 Hz guest clock): head <= "+headRate.ToString("0.0")+" rad/s, chest <= "+chestRate.ToString("0.0")+" rad/s, hands <= "+handRate.ToString("0.00")+" m/s per frame (no snaps; considering <= "+browseHand.ToString("0.00")+" m/s), one considering arm at a time (the other at most "+bothUp.ToString("0.00")+" up on a switch), no dip into the pick, rates agree within "+(worstRate*57.3f).ToString("0.0")+" deg browsing and "+(worstPick*57.3f).ToString("0")+" deg through the pick, the palm in front of the chest from the catch to the return and on the wrist within "+(worstPlant*1000).ToString("0")+" mm at the plant, hands back in the lap within "+(settle*1000).ToString("0")+" mm, 0 bytes per frame";
    }
}

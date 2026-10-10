using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

// Engine-free checks for the chip draft's 3D stage (Core/NativeDraftStage.cs as NativeDraftPresentation drives it): the pilot
// framed behind and above the low tray, the bowed gaze following each highlighted card on every aspect ratio and interface
// scale, spring settling without overshoot, the highlighted card held across the gaps between cards, the arm lifting toward
// the local picker's highlighted card (one side at a time), no frame-to-frame snaps at 30/60/144/240 Hz through browse,
// pick, reach, plant and return, the reach landing toward the chosen card and the plant on the wrist, the smoothed pick
// clock, and 0 bytes per frame. The card rects follow NativeDraftScreen.LayoutTray / LowerTray with a fixed card height (Unity measures the text).
internal static class DraftStageChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Draft stage: "+what);}
    // A screen (pixels) and interface scale as NativeUiMotion lays out the overlays; `pve` the Siren pick's lower reserve.
    private struct Screen{public float W,H,Scale;public string Name;}
    private static readonly Screen[] Screens={S(1920,1080,1,"1920x1080"),S(3440,1440,1,"3440x1440"),S(2560,1440,1.25f,"2560x1440 ui 1.25"),S(1280,720,1,"1280x720"),S(1366,768,.8f,"1366x768 ui .8"),S(1024,768,1,"1024x768"),S(1280,1024,1,"1280x1024"),S(1920,1080,1.5f,"1920x1080 ui 1.5")};
    private static Screen S(float w,float h,float ui,string name){var l=NativeUiMotion.Measure(w,h,ui);return new Screen{W=l.Width,H=l.Height,Scale=w/h,Name=name};}
    // NativeDraftScreen.LayoutTray (rows, widths, spacing) with a .78 height-to-width card, then LowerTray onto the bottom reserve.
    private static Vector4[] Tray(int n,float W,float H,bool pve,out float top)
    {
        float header=(H<=760?16:22)+21+(H<=760?24:Math.Clamp(W*.025f,24,34))*1.08f+4+(H>760?20.3f:0)+Math.Clamp(H*.024f,4,30);
        int fit=W<=760||H<=480?n:Math.Clamp((int)Math.Floor((W-42)/164),2,6);int[] rows=n<=fit?new[]{n}:new[]{(n+1)/2,n/2};bool large=W>=1600&&H>=900;
        float tray=W-56,gap=14,rowGap=H<=760||rows.Length==2&&H<=820?14:20,cap=rows.Length==2?214:large?272:236,y=header;var cards=new Vector4[n];int at=0;
        foreach(int cols in rows){float w=Math.Min(cap,Math.Max(118,(tray-(cols-1)*gap)/cols)),h=w*.78f,span=cols>1?(tray-cols*w)/(cols-1):0;y+=10;for(int i=0;i<cols;i++)cards[at+i]=new Vector4(cols==1?(W-w)/2:28+i*(w+span),y,w,h);at+=cols;y+=h+8+rowGap;}
        float bottom=y-rowGap,off=Math.Max(0,H-(pve?142:H<=760?54:66)-bottom);for(int i=0;i<n;i++)cards[i].Y+=off;top=cards[0].Y;return cards;
    }
    private static Vector2[] Centres(Vector4[] cards,float W,float H){var c=new Vector2[cards.Length];for(int i=0;i<cards.Length;i++)c[i]=new Vector2((cards[i].X+cards[i].Z/2)/W,(cards[i].Y+cards[i].W/2)/H);return c;}
    private static Vector3 Eye(CapturedRig.Pose p)=>p.WorldPosition[CapturedRig.Head]+Vector3.Transform(Vector3.UnitY*NativeDraftStage.EyeUp,p.WorldRotation[CapturedRig.Head]);
    private static Vector3 Forward(CapturedRig.Pose p,int bone)=>Vector3.Transform(Vector3.UnitZ,p.WorldRotation[bone]);
    private static float Angle(Quaternion a,Quaternion b){float d=Math.Min(1,Math.Abs(Quaternion.Dot(a,b)));return 2*(float)Math.Acos(d);}
    private static void Frame(NativeDraftStage s,float dt,float rawAge,int focus,Vector2[] cards,float aspect,float trayTop,bool siren=false,bool consider=false)
    {float age=s.SmoothAge(rawAge,dt);s.Begin(dt,false);s.Frame(aspect,trayTop,false,siren,dt);s.Step(dt,age,focus,consider,cards,cards.Length,siren,false);}
    // NativeDraftPresentation.DraftFrame's considered card: the chosen one, else the held highlight, else the scan; the arm
    // lifts while the held card is being reached for.
    private static int Considered(NativeDraftHover hold,int chosen,int hover,float since,int count,float dt,out bool consider)
    {if(chosen>=0)hold.Reset();else hold.Step(hover,dt);consider=chosen<0&&hold.Reaching;return NativeDraftStage.Focus(chosen,hold.Card,since,count,false);}
    private static float HeadYaw(CapturedRig.Pose p){var f=Forward(p,CapturedRig.Head);return (float)Math.Atan2(f.X,f.Z);}
    // The angle at the shoulder between the hand and a point.
    private static float ArmAngle(CapturedRig.Pose p,bool left,Vector3 goal){int hand=left?CapturedRig.LHand:CapturedRig.RHand;var sh=p.WorldPosition[hand-2];return (float)Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(p.WorldPosition[hand]-sh),Vector3.Normalize(goal-sh)),-1,1));}

    public static IEnumerable<string> Run()
    {
        // The considered card: chosen, then hover/focus, then the boot scan following the entrance, then the 1.8 s cycle.
        Check(NativeDraftStage.Focus(2,4,0,5,false)==2&&NativeDraftStage.Focus(-1,4,0,5,false)==4&&NativeDraftStage.Focus(-1,-1,.5f,5,false)==2&&NativeDraftStage.Focus(-1,-1,1.2f,5,false)==0
            &&NativeDraftStage.Focus(-1,-1,1.9f,5,false)==1&&NativeDraftStage.Focus(-1,-1,9,5,true)==0&&NativeDraftStage.Focus(-1,-1,3,0,false)==-1&&NativeDraftStage.Focus(7,-1,0,5,false)==4,"focus order");

        // Composition and gaze on every screen, PvP and the Siren pick, 3 to 12 offers (one or two rows).
        float worstGaze=0,worstHeadroom=1,worstAim=0,maxYaw=0;int cases=0;
        foreach(var sc in Screens)foreach(bool pve in new[]{false,true})foreach(int n in new[]{3,5,6,8,12})
        {
            var rects=Tray(n,sc.W,sc.H,pve,out float top);var cards=Centres(rects,sc.W,sc.H);float trayTop=top/sc.H;var s=new NativeDraftStage();
            for(int f=0;f<30;f++)Frame(s,1/60f,-1,0,cards,sc.Scale,trayTop);
            var p=s.Pose;
            // The pilot stands behind the card plane and above the tray: head top on screen below the top edge and above the
            // cards; the tray's top edge no higher than the waist unless a tall tray needs the head's room.
            float headTop=s.Project(p.WorldPosition[CapturedRig.Head]+new Vector3(0,NativeDraftStage.HeadTop,0)).Y,waist=s.Project(new Vector3(0,NativeDraftStage.Cut,0)).Y;
            Check(headTop>=.02f&&headTop<trayTop-.08f,sc.Name+" "+n+" cards: head top at "+headTop+" with the tray at "+trayTop);worstHeadroom=Math.Min(worstHeadroom,headTop);
            Check(waist<=trayTop+.004f||headTop<=NativeDraftStage.Headroom+.004f,sc.Name+" "+n+" cards: the tray's edge ("+trayTop+") is above the waist ("+waist+")");
            float x=s.Project(p.WorldPosition[CapturedRig.Chest]).X;Check(Math.Abs(x-.5f)<.02f,sc.Name+": the pilot is not centred ("+x+")");
            // The gaze lands on each card in turn: its point projects onto the card's centre, and the head's aim falls nearest that
            // card among its row (yaw ordered left to right).
            float lastYaw=-9;int lastRow=-1;
            for(int i=0;i<n;i++)
            {
                for(int f=0;f<72;f++)Frame(s,1/60f,-1,i,cards,sc.Scale,trayTop);
                var g=s.Project(s.Gaze);float err=Math.Max(Math.Abs(g.X-cards[i].X)*sc.W,Math.Abs(g.Y-cards[i].Y)*sc.H);worstGaze=Math.Max(worstGaze,err);
                Check(err<1.5f,sc.Name+" "+n+" cards: gaze "+err+" px off card "+i);
                // The head turns toward the highlighted card: of the turns toward each card in its row (from the eye, on the look
                // plane), the head's is nearest the highlighted one's, and within a few degrees unless the turn limit holds it.
                var eye=Eye(p);var hf=Forward(p,CapturedRig.Head);float headYaw=(float)Math.Atan2(hf.X,hf.Z);int row=rects[i].Y>rects[0].Y+1?1:0,nearest=-1;float best=float.MaxValue,own=0;
                for(int j=0;j<n;j++){if((rects[j].Y>rects[0].Y+1?1:0)!=row)continue;var q=s.OnPlane(cards[j].X,cards[j].Y,s.LookDepth)-eye;float yj=(float)Math.Atan2(q.X,q.Z),d=Math.Abs(yj-headYaw);if(j==i)own=yj;if(d<best){best=d;nearest=j;}}
                Check(nearest==i,sc.Name+" "+n+" cards: the head turns nearest card "+nearest+", not "+i);if(Math.Abs(own)<NativeDraftStage.YawLimit-.05f)worstAim=Math.Max(worstAim,Math.Abs(own-headYaw));
                Check(worstAim<.09f,sc.Name+" "+n+" cards: the head turns "+Math.Abs(own-headYaw)+" rad off card "+i);
                var fwd=Forward(p,CapturedRig.Head);float yaw=(float)Math.Atan2(fwd.X,fwd.Z),pitch=(float)Math.Asin(Math.Clamp(fwd.Y,-1,1));maxYaw=Math.Max(maxYaw,Math.Abs(yaw));
                Check(pitch<-.35f,sc.Name+" "+n+" cards: the head does not look down at card "+i+" (pitch "+pitch+")");
                if(row==lastRow)Check(yaw>lastYaw,sc.Name+" "+n+" cards: head yaw not ordered at card "+i);lastYaw=yaw;lastRow=row;
                // The bow: the upper spine and neck lean toward the cards too, not only the head.
                Check(Forward(p,CapturedRig.Chest).Y<-.1f&&Forward(p,CapturedRig.Neck).Y<-.2f,sc.Name+" "+n+" cards"+(pve?" (Siren)":"")+": the chest and neck do not bow (card "+i+", chest "+Forward(p,CapturedRig.Chest).Y+", neck "+Forward(p,CapturedRig.Neck).Y+")");
            }
            cases++;
        }
        yield return "Draft stage composition on "+Screens.Length+" screens and interface scales, PvP draft and Siren pick trays, 3-12 offers ("+cases+" layouts): pilot centred behind and above the tray (head top >= "+(worstHeadroom*100).ToString("0")+"% from the top, tray edge at or below the waist), gaze on every card within "+worstGaze.ToString("0.0")+" px, head turned nearest the highlighted card (within "+(worstAim*57.3f).ToString("0.0")+" deg inside the turn limit, yaw up to "+(maxYaw*57.3f).ToString("0")+" deg), chest and neck bowed";

        // Settling: a step across the whole tray and a step to a neighbour settle without overshoot (95% in .45 s, 1% in .7 s).
        {
            var sc=Screens[0];var rects=Tray(6,sc.W,sc.H,false,out float top);var cards=Centres(rects,sc.W,sc.H);float worst95=0,worstOver=0,longest=0;
            foreach(var (a,b) in new[]{(0,5),(5,0),(2,3),(4,1)})
            {
                var s=new NativeDraftStage();for(int f=0;f<90;f++)Frame(s,1/60f,-1,a,cards,sc.Scale,top/sc.H);
                float start=s.Gaze.X;Frame(s,0,-1,b,cards,sc.Scale,top/sc.H);float goal=s.OnPlane(cards[b].X,cards[b].Y,s.LookDepth).X,span=goal-start,settled95=-1,yawGoal;
                var yaws=new List<float>();
                for(int f=1;f<=120;f++)
                {
                    Frame(s,1/60f,-1,b,cards,sc.Scale,top/sc.H);float u=(s.Gaze.X-start)/span;worstOver=Math.Max(worstOver,u-1);if(settled95<0&&u>=.95f)settled95=f/60f;
                    if(f==66)Check(Math.Abs(1-u)<.01f,"gaze not within 1% at 1.1 s ("+u+")");var fw=Forward(s.Pose,CapturedRig.Head);yaws.Add((float)Math.Atan2(fw.X,fw.Z));
                }
                yawGoal=yaws[yaws.Count-1];float sign=Math.Sign(yawGoal-yaws[0]);for(int k=1;k<yaws.Count;k++){Check(sign*(yaws[k]-yaws[k-1])>-2e-3f,"head yaw turns back while settling "+a+"->"+b);Check(sign*(yaws[k]-yawGoal)<.015f,"head yaw overshoots "+a+"->"+b+" by "+sign*(yaws[k]-yawGoal)+" at frame "+k);}
                // A neighbour settles in .45 s; a longer glance adds its turn at TurnSpeed.
                var e0=Eye(s.Pose);var qa=s.OnPlane(cards[a].X,cards[a].Y,s.LookDepth)-e0;var qb=s.OnPlane(cards[b].X,cards[b].Y,s.LookDepth)-e0;float turn=Math.Abs((float)(Math.Atan2(qb.X,qb.Z)-Math.Atan2(qa.X,qa.Z))),allowed=.45f+(Math.Abs(a-b)>1?turn/NativeDraftStage.TurnSpeed:0);
                Check(settled95>0&&settled95<=allowed,"gaze 95% at "+settled95+" s, allowed "+allowed+" ("+a+"->"+b+")");worst95=Math.Max(worst95,Math.Abs(a-b)==1?settled95:0);longest=Math.Max(longest,settled95);
            }
            Check(worstOver<1e-3f,"gaze overshoots by "+worstOver);
            yield return "Gaze: a neighbouring card settles to 95% in "+worst95.ToString("0.00")+" s, a glance across the tray in "+longest.ToString("0.00")+" s (turn capped at "+NativeDraftStage.TurnSpeed+" rad/s), within 1% by 1.1 s, no overshoot (gaze "+(worstOver*100).ToString("0.000")+"%, head yaw within 1 deg)";
        }

        // The pointer between cards (owner: the pilot looks at whichever chip is highlighted). At 1920x1080 with 5 cards the
        // pointer crosses about 170 px of tray between two cards; meanwhile the scan card can be at the far end. The held
        // highlight keeps the head on the card just left until the next is reached: it never turns the wrong way (toward a
        // third card) or past the next card, the arm does not dip for a short gap and lowers after a long one (the gaze holds).
        {
            var sc=Screens[0];var rects=Tray(5,sc.W,sc.H,false,out float top);var cards=Centres(rects,sc.W,sc.H);float trayTop=top/sc.H;float worstWrong=0,worstPast=0,rawWrong=0,worstDip=0,longGap=0;
            foreach(int hz in new[]{30,60,144,240})foreach(float gap in new[]{.1f,.15f,.25f,.5f,1.2f})foreach(var (a,b) in new[]{(1,2),(2,3),(0,1),(4,3),(3,1)})foreach(bool held in new[]{true,false})
            {
                // The scan card during the gap: the far end behind the card just left, else the far end past the next one.
                float dt=1f/hz;int far=b>a?(a>0?0:4):(a<4?4:0);double since0=9+1.8*far+.5-1.2;var s=new NativeDraftStage();var hold=new NativeDraftHover();var yaws=new List<float>();float yawA=0,wA=0;bool sideA=false;
                int frames=(int)Math.Round((2.4+gap)*hz);
                for(int f=0;f<=frames;f++)
                {
                    double tt=Math.Max(0,(double)(f-1)/hz);int hover=tt<1.2?a:tt<1.2+gap-1e-6?-1:b;float since=(float)(since0+tt);
                    int focus;bool consider;if(held)focus=Considered(hold,-1,hover,since,5,dt,out consider);else{focus=NativeDraftStage.Focus(-1,hover,since,5,false);consider=hover>=0;}
                    Frame(s,dt,-1,focus,cards,sc.Scale,trayTop,false,consider);float y=HeadYaw(s.Pose);if(f==0)sideA=s.OnPlane(cards[a].X,cards[a].Y,NativeDraftStage.ReachZ).X>=0;
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

        // Considering (draft-stage.ts): at 30/60/144/240 Hz, the arm on the highlighted card's side lifts toward it (pointing
        // at the card on the reach plane) and the other stays down, on every card of a wide tray; the Siren drafter keeps her
        // idle arms.
        {
            var sc=Screens[1];var rects=Tray(6,sc.W,sc.H,false,out float top);var cards=Centres(rects,sc.W,sc.H);float trayTop=top/sc.H;float worstPoint=0,worstUp=1,worstDown=0;
            foreach(int hz in new[]{30,60,144,240})for(int i=0;i<6;i++)foreach(bool siren in new[]{false,true})
            {
                var s=new NativeDraftStage();float dt=1f/hz;for(int f=0;f<(int)(1.2f*hz);f++)Frame(s,dt,-1,i,cards,sc.Scale,trayTop,siren,true);
                var goal=s.OnPlane(cards[i].X,cards[i].Y,NativeDraftStage.ReachZ);bool left=goal.X>=0;
                if(siren){Check(s.Weight(true)==0&&s.Weight(false)==0,"the Siren drafter reaches for card "+i);continue;}
                worstUp=Math.Min(worstUp,s.Weight(left));worstDown=Math.Max(worstDown,s.Weight(!left));worstPoint=Math.Max(worstPoint,ArmAngle(s.Pose,left,goal));
            }
            Check(worstUp>.97f&&worstDown<.03f,"considering arm at "+worstUp+", the other at "+worstDown);Check(worstPoint<.3f,"the considering arm points "+worstPoint+" rad off its card");
            yield return "Considering: the arm on the highlighted card's side lifts toward it (within "+(worstPoint*57.3f).ToString("0")+" deg of the card at a soft elbow) and the other stays down on all 6 cards of a 3440x1440 tray at 30-240 Hz; the Siren drafter keeps her idle arms";
        }

        // Motion at 30/60/144/240 Hz through the boot scan, quick hover changes (far and near, switching sides), the pick on the
        // edge card, the reach, contact, plant and return, for the local picker (the arm considers the highlighted card), a
        // remote picker (no highlight: only the pick's reach) and the Siren drafter (idle arms): no frame-to-frame snap of the
        // head, chest or hands, never both arms lifted, no dip as a considering arm takes the card, the same motion at every rate.
        float headRate=0,chestRate=0,handRate=0,browseHand=0,bothUp=0,worstPlant=0,worstDir=0,worstRate=0,dip=0;long allocated=0;string at="";var heads=new Dictionary<int,List<Quaternion>>();
        foreach(int hz in new[]{30,60,144,240})foreach(int mode in new[]{0,1,2})
        {
            bool siren=mode==2,local=mode!=1;var hold=new NativeDraftHover();
            var sc=Screens[1];var rects=Tray(6,sc.W,sc.H,false,out float top);var cards=Centres(rects,sc.W,sc.H);var s=new NativeDraftStage();float dt=1f/hz;double pickAt=4.2;
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
                Frame(s,dt,stepped,focus,cards,sc.Scale,top/sc.H,siren,consider);if(f>3)allocated+=GC.GetAllocatedBytesForCurrentThread()-before;
                var p=s.Pose;var head=p.WorldRotation[CapturedRig.Head];var chest=p.WorldRotation[CapturedRig.Chest];var l=p.WorldPosition[CapturedRig.LHand];var r=p.WorldPosition[CapturedRig.RHand];
                if(!first)
                {
                    float h=Angle(head,lastHead)/dt,c=Angle(chest,lastChest)/dt,m=Math.Max((l-lastL).Length(),(r-lastR).Length())/dt;
                    if(h>headRate){headRate=h;at=hz+" Hz t "+t.ToString("0.000");}chestRate=Math.Max(chestRate,c);handRate=Math.Max(handRate,m);if(raw<0&&mode==0)browseHand=Math.Max(browseHand,m);
                }
                // One arm considers at a time; a remote picker's and the Siren's arms stay in the idle until the pick.
                float wl=s.Weight(true),wr=s.Weight(false);if(raw<0)bothUp=Math.Max(bothUp,Math.Min(wl,wr));
                if(raw<0&&mode!=0||siren)Check(wl==0&&wr==0,(siren?"Siren":"remote picker")+" arm lifted before the pick at "+hz+" Hz t "+t.ToString("0.00"));
                if(mode==0&&raw<0&&start>=1.5)Check(Math.Max(wl,wr)>.02f,"no arm considers the highlighted card at "+hz+" Hz t "+t.ToString("0.00"));
                // The considering arm keeps lifting through the pick: its weight never drops before the contact.
                if(mode==0&&raw>=0&&s.Age<NativeDraftPlant.Contact)dip=Math.Max(dip,(s.Left?lastWl:lastWr)-s.Weight(s.Left));lastWl=wl;lastWr=wr;
                first=false;lastHead=head;lastChest=chest;lastL=l;lastR=r;
                if(raw>=0){Check(s.Age>=0&&Math.Abs(s.Age-raw)<.07f&&s.Age>=lastAge,"smoothed age "+s.Age+" vs "+raw+" (last "+lastAge+")");lastAge=s.Age;}
                if(hz%10==0&&f%(hz/10)==0){samples.Add(head);}
                // The pilot stays behind the card plane; the reaching palm stops at the reach plane.
                for(int b=0;b<CapturedRig.Bones.Length;b++)Check(p.WorldPosition[b].Z<NativeDraftStage.LookZ-.1f,"bone "+b+" in front of the cards");Check(s.Palm.Z<NativeDraftStage.ReachZ+.03f,"palm past the reach plane");
                if(!siren&&raw>=0)
                {
                    float age=s.Age;int hand=s.Left?CapturedRig.LHand:CapturedRig.RHand;
                    if(Math.Abs(age-NativeDraftPlant.Contact)<.5f*dt+.002f)
                    {   // At the contact the arm points at the chosen card's reach point.
                        var shoulder=p.WorldPosition[hand-2];var goal=s.OnPlane(cards[5].X,cards[5].Y,NativeDraftStage.ReachZ);
                        float cos=Vector3.Dot(Vector3.Normalize(p.WorldPosition[hand]-shoulder),Vector3.Normalize(goal-shoulder));worstDir=Math.Max(worstDir,(float)Math.Acos(Math.Min(1,cos)));
                    }
                    if(Math.Abs(age-NativeDraftPlant.CarryEnd)<.5f*dt+.002f)worstPlant=Math.Max(worstPlant,(s.Palm-s.Plant.At).Length());
                }
            }
            if(mode==0&&hz%10==0)heads[hz]=samples;
        }
        // The same motion at every frame rate: head turns at shared .1 s marks agree closely while browsing (exact springs on
        // a constant-speed turn) and stay close through the pick (whose hand-held targets trail by one frame).
        int marks=Math.Min(heads[30].Count,heads[240].Count);float worstPick=0;
        for(int k=0;k<marks;k++){float d=Math.Max(Angle(heads[30][k],heads[240][k]),Angle(heads[60][k],heads[240][k]));if(k*.1f<4.2f-1e-3f)worstRate=Math.Max(worstRate,d);else worstPick=Math.Max(worstPick,d);}
        Check(headRate<8,"the head turns "+headRate+" rad/s in one frame (a snap) at "+at);Check(chestRate<4,"the chest turns "+chestRate+" rad/s in one frame");Check(handRate<4.5f,"a hand moves "+handRate+" m/s in one frame");Check(browseHand<2.8f,"a considering hand moves "+browseHand+" m/s");Check(bothUp<.5f,"both arms lifted ("+bothUp+") on a switch of sides");Check(dip<1e-4f,"the considering arm dips by "+dip+" as it takes the card");
        Check(worstDir<.2f,"the reach points "+worstDir+" rad off the chosen card");Check(worstPlant<.03f,"the palm misses the wrist by "+worstPlant+" m at the plant");Check(worstRate<.02f,"frame rates disagree by "+worstRate+" rad while browsing");Check(worstPick<.15f,"frame rates disagree by "+worstPick+" rad through the pick");Check(allocated==0,"the stage allocated "+allocated+" bytes");
        yield return "Draft stage at 30/60/144/240 Hz (local and remote pilot and Siren, 15 Hz guest clock): head <= "+headRate.ToString("0.0")+" rad/s, chest <= "+chestRate.ToString("0.0")+" rad/s, hands <= "+handRate.ToString("0.00")+" m/s per frame (no snaps; considering <= "+browseHand.ToString("0.00")+" m/s), one considering arm at a time (the other at most "+bothUp.ToString("0.00")+" up on a switch), no dip into the pick, rates agree within "+(worstRate*57.3f).ToString("0.0")+" deg browsing and "+(worstPick*57.3f).ToString("0")+" deg through the pick, reach within "+(worstDir*57.3f).ToString("0")+" deg of the chosen card, palm on the wrist within "+(worstPlant*1000).ToString("0")+" mm, pilot behind the card plane, 0 bytes per frame";
    }
}

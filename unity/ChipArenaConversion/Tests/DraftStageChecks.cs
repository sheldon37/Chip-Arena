using System;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;

// Engine-free checks for the chip draft's 3D stage (Core/NativeDraftStage.cs as NativeDraftPresentation drives it): the pilot
// seated at the display table and framed from the chest up (head and resting hands in view, hips and legs hidden behind the
// table) on every aspect ratio and interface scale, never smaller than the standing framing it replaces; the table in front of
// the pilot, its back edge above the first card row and its front rim under the last (the cards lie on its display), wider
// than the screen, never cut by the body or the arms; every hat (its real vertices) and the Auditor halo in view through the
// browse and the pick, framed on their measured tops; the bowed gaze following each highlighted card, spring settling without
// overshoot, the highlighted card held across the gaps between cards, the hand on the highlighted card's side lifting off the
// table toward it (one side at a time), no frame-to-frame snaps at 30/60/144/240 Hz through browse, pick, reach across the
// table, plant and return, the reach landing toward the chosen card and the plant on the wrist, the smoothed pick clock, and
// 0 bytes per frame. The card rects follow NativeDraftScreen.LayoutTray / LowerTray with a fixed card height (Unity measures
// the text).
internal static class DraftStageChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Draft stage: "+what);}
    // A screen (pixels) and interface scale as NativeUiMotion lays out the overlays.
    private struct Screen{public float W,H,Scale;public string Name;}
    private static readonly Screen[] Screens={S(1920,1080,1,"1920x1080"),S(3440,1440,1,"3440x1440"),S(2560,1440,1.25f,"2560x1440 ui 1.25"),S(1280,720,1,"1280x720"),S(1366,768,.8f,"1366x768 ui .8"),S(1024,768,1,"1024x768"),S(1280,1024,1,"1280x1024"),S(1920,1080,1.5f,"1920x1080 ui 1.5")};
    private static Screen S(float w,float h,float ui,string name){var l=NativeUiMotion.Measure(w,h,ui);return new Screen{W=l.Width,H=l.Height,Scale=w/h,Name=name};}
    // NativeDraftScreen.LayoutTray (rows, widths, spacing) with a .78 height-to-width card, then LowerTray onto the bottom reserve.
    private static Vector4[] Tray(int n,float W,float H,bool pve,out float top,out float bottom)
    {
        float header=(H<=760?16:22)+21+(H<=760?24:Math.Clamp(W*.025f,24,34))*1.08f+4+(H>760?20.3f:0)+Math.Clamp(H*.024f,4,30);
        int fit=W<=760||H<=480?n:Math.Clamp((int)Math.Floor((W-42)/164),2,6);int[] rows=n<=fit?new[]{n}:new[]{(n+1)/2,n/2};bool large=W>=1600&&H>=900;
        float tray=W-56,gap=14,rowGap=H<=760||rows.Length==2&&H<=820?14:20,cap=rows.Length==2?214:large?272:236,y=header;var cards=new Vector4[n];int at=0;
        foreach(int cols in rows){float w=Math.Min(cap,Math.Max(118,(tray-(cols-1)*gap)/cols)),h=w*.78f,span=cols>1?(tray-cols*w)/(cols-1):0;y+=10;for(int i=0;i<cols;i++)cards[at+i]=new Vector4(cols==1?(W-w)/2:28+i*(w+span),y,w,h);at+=cols;y+=h+8+rowGap;}
        float end=y-rowGap,off=Math.Max(0,H-(pve?142:H<=760?54:66)-end);for(int i=0;i<n;i++)cards[i].Y+=off;top=cards[0].Y;bottom=end+off;return cards;
    }
    private static Vector2[] Centres(Vector4[] cards,float W,float H){var c=new Vector2[cards.Length];for(int i=0;i<cards.Length;i++)c[i]=new Vector2((cards[i].X+cards[i].Z/2)/W,(cards[i].Y+cards[i].W/2)/H);return c;}
    private static Vector3 Eye(CapturedRig.Pose p)=>p.WorldPosition[CapturedRig.Head]+Vector3.Transform(Vector3.UnitY*NativeDraftStage.EyeUp,p.WorldRotation[CapturedRig.Head]);
    private static Vector3 Forward(CapturedRig.Pose p,int bone)=>Vector3.Transform(Vector3.UnitZ,p.WorldRotation[bone]);
    private static float Angle(Quaternion a,Quaternion b){float d=Math.Min(1,Math.Abs(Quaternion.Dot(a,b)));return 2*(float)Math.Acos(d);}
    private static void Frame(NativeDraftStage s,float dt,float rawAge,int focus,Vector2[] cards,float aspect,float trayTop,float trayBottom,bool siren=false,bool consider=false,float crown=0)
    {float age=s.SmoothAge(rawAge,dt);s.Begin(dt,false);if(siren)SirenIdle(s.Pose,s.Clock);s.Frame(aspect,trayTop,trayBottom,crown,siren,dt);s.Step(dt,age,focus,consider,cards,cards.Length,siren,false);}
    // The highest screen row (fraction from the top) that any vertex of a hat model (head space) reaches on the posed head.
    private static float HatRow(NativeDraftStage s,Vector3[] hat)
    {var q=s.Pose.WorldRotation[CapturedRig.Head];var o=s.Pose.WorldPosition[CapturedRig.Head];float row=float.MaxValue;foreach(var v in hat)row=Math.Min(row,s.Project(o+Vector3.Transform(v,q)).Y);return row;}
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
    // and rests her claws on the table.
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
    // Whether the camera's view of a root-frame point is blocked by the table (its top or its front face) or it is off the screen.
    private static bool Hidden(NativeDraftStage s,Vector3 point)
    {
        var sp=s.Project(point);if(sp.X<0||sp.X>1||sp.Y<0||sp.Y>1)return true;if(point.Y>=s.Table)return false;var c=s.CameraPosition;
        float k=(s.Table-c.Y)/(point.Y-c.Y);if(k>0&&k<1){float z=c.Z+(point.Z-c.Z)*k;if(z>=NativeDraftStage.TableBack&&z<=s.TableFront)return true;}
        float face=s.TableFront-.04f;k=(face-c.Z)/(point.Z-c.Z);if(k>0&&k<1){float y=c.Y+(point.Y-c.Y)*k;if(y<=s.Table&&y>=s.Table-NativeDraftStage.TableThick-NativeDraftStage.FaceDepth)return true;}
        return false;
    }
    // How far the deepest corner of any rig box reaching into the table's slab (over its footprint, from above or across it)
    // lies below its top (m; 0 none), and that box. Boxes wholly under the slab (the legs) are under the table, not in it.
    private static float Penetration(NativeDraftStage s,out int box)
    {
        var p=s.Pose;float worst=0;box=-1;
        for(int b=0;b<CapturedRig.Boxes.Length;b++)
        {
            var bx=CapturedRig.Boxes[b];var q=p.WorldRotation[bx.Bone];var c=p.WorldPosition[bx.Bone]+Vector3.Transform(bx.Position,q);float low=float.MaxValue,high=float.MinValue;
            for(int k=0;k<8;k++)
            {
                var corner=c+Vector3.Transform(new Vector3((k&1)==0?-.5f:.5f,(k&2)==0?-.5f:.5f,(k&4)==0?-.5f:.5f)*bx.Size,q);
                if(corner.Z<NativeDraftStage.TableBack||corner.Z>s.TableFront)continue;low=Math.Min(low,corner.Y);high=Math.Max(high,corner.Y);
            }
            if(high<s.Table-NativeDraftStage.TableThick)continue;float d=s.Table-low;if(d>worst){worst=d;box=b;}
        }
        return worst;
    }
    // The standing framing this stage replaced (NativeDraftStage before 2026-10-10: the tray's top edge across the waist, the
    // head's top .07 from the top, the upper body at most .56 of the screen, 1.8 m across): the screen share of the head.
    private static float OldHeadShare(float restHead,float aspect,float trayTop)
    {
        float top=restHead+.36f,body=top+.13f,cut=Math.Clamp(trayTop,.2f,.92f),need=body/Math.Max(.05f,cut-.07f),height=Math.Min(4,Math.Max(need,Math.Max(body/.56f,1.8f/aspect)));
        return .36f/height;
    }

    public static IEnumerable<string> Run(string root)
    {
        // The considered card: chosen, then hover/focus, then the boot scan following the entrance, then the 1.8 s cycle.
        Check(NativeDraftStage.Focus(2,4,0,5,false)==2&&NativeDraftStage.Focus(-1,4,0,5,false)==4&&NativeDraftStage.Focus(-1,-1,.5f,5,false)==2&&NativeDraftStage.Focus(-1,-1,1.2f,5,false)==0
            &&NativeDraftStage.Focus(-1,-1,1.9f,5,false)==1&&NativeDraftStage.Focus(-1,-1,9,5,true)==0&&NativeDraftStage.Focus(-1,-1,3,0,false)==-1&&NativeDraftStage.Focus(7,-1,0,5,false)==4,"focus order");
        var rest0=new CapturedRig.Pose();NativeDraftRules.Pose(rest0,0);rest0.World();float restHead=rest0.WorldPosition[CapturedRig.Head].Y;
        // The exported hats (the Auditor halo's model is only its fallback; the halo is checked from its own geometry), the
        // tallest by its measured top.
        var hats=LobbyPoseChecks.HatVertices(root);hats.Remove(NativeAuditorHalo.Hat);string tallest=null;foreach(var h in hats)if(tallest==null||Crown(h.Value)>Crown(hats[tallest]))tallest=h.Key;
        Check(tallest!=null&&Crown(hats[tallest])>.9f,"the tallest hat ("+tallest+") is not measured");var tall=hats[tallest];float tallCrown=Crown(tall);

        // Composition, table and gaze on every screen, PvP and the Siren pick, 3 to 12 offers (one or two rows), with and without
        // the tallest hat (its real vertices kept in view on every frame), and the Siren drafter.
        float worstGaze=0,worstHeadroom=1,worstAim=0,maxYaw=0,smallest=9,worstRim=0,worstPen=0,worstHat=1;int cases=0,covered=0;
        foreach(var sc in Screens)foreach(int kind in new[]{0,1,2,3})foreach(int n in new[]{3,5,6,8,12})
        {
            bool pve=kind==1,siren=kind==2,hat=kind==3;
            var rects=Tray(n,sc.W,sc.H,pve,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);float trayTop=top/sc.H,trayBottom=bottom/sc.H;var s=new NativeDraftStage();
            float crown=hat?tallCrown:0;string what0=sc.Name+" "+n+" cards";
            void HatInView(string when){if(!hat)return;float row=HatRow(s,tall);worstHat=Math.Min(worstHat,row);Check(row>=.02f,what0+": the "+tallest+" hat's top at "+row+" ("+when+")");}
            for(int f=0;f<30;f++){Frame(s,1/60f,-1,0,cards,sc.Scale,trayTop,trayBottom,siren,false,crown);HatInView("settling");}
            var p=s.Pose;string what=sc.Name+" "+n+" cards"+(pve?" (Siren pick)":siren?" (Siren drafter)":hat?" (hat)":"");
            // Chest up: the head's top in view under the top edge and above the cards; the resting hands in view (above the
            // first row unless the tray is too tall for them, then the head keeps its room); the hips, the waist and the legs
            // hidden behind the table or off the screen.
            float headTop=hat?HatRow(s,tall):s.Project(new Vector3(0,restHead+NativeDraftStage.HeadTop+(siren?NativeDraftStage.BossTop:0),0)).Y;
            Check(headTop>=.02f&&headTop<trayTop-.08f,what+": head top at "+headTop+" with the tray at "+trayTop);worstHeadroom=Math.Min(worstHeadroom,headTop);
            bool capped=s.Project(new Vector3(0,s.Table,NativeDraftStage.AnchorZ)).Y>trayTop-NativeDraftStage.HandGap+.004f;
            foreach(int b in new[]{CapturedRig.Pelvis,CapturedRig.LHip,CapturedRig.RHip,CapturedRig.LKnee,CapturedRig.RKnee,CapturedRig.LFoot,CapturedRig.RFoot})Check(Hidden(s,p.WorldPosition[b]),what+": "+CapturedRig.Bones[b].Name+" in view");
            Check(Hidden(s,new Vector3(0,p.WorldPosition[CapturedRig.Pelvis].Y+.12f,p.WorldPosition[CapturedRig.Pelvis].Z+.15f)),what+": the waist shows in front of the table");
            foreach(bool left in new[]{true,false})
            {
                var hand=p.WorldPosition[left?CapturedRig.LHand:CapturedRig.RHand];var sp=s.Project(hand+new Vector3(0,.05f,0));
                Check(sp.X>.02f&&sp.X<.98f&&sp.Y>headTop,what+": a resting hand off the screen ("+sp+")");Check(capped||sp.Y<trayTop,what+": a resting hand behind the cards ("+sp.Y+" against "+trayTop+")");
                Check(Math.Abs(hand.Y-(s.Table+NativeDraftStage.RestUp))<.02f&&hand.Z>NativeDraftStage.TableBack+.05f&&hand.Z<NativeDraftStage.TableBack+.3f,what+": a hand does not rest on the table near its edge ("+hand+")");
                Check(Vector3.Transform(Vector3.UnitZ,p.WorldRotation[left?CapturedRig.LHand:CapturedRig.RHand]).Y<-.75f,what+": a resting palm is not down");
            }
            if(capped)covered++;
            // The table: in front of the pilot (the torso behind its near edge, nothing cut into its top), its near edge above
            // the first card row (unless the tray is too tall), its lit front rim just under the last row, wider than the screen.
            foreach(int b in new[]{CapturedRig.Spine,CapturedRig.Pelvis})Check(p.WorldPosition[b].Z<NativeDraftStage.TableBack,what+": "+CapturedRig.Bones[b].Name+" past the table's edge");
            float pen=Penetration(s,out int box);worstPen=Math.Max(worstPen,pen);Check(pen<.02f,what+": box "+box+" cuts "+pen+" m into the table");
            float back=s.Project(new Vector3(0,s.Table,NativeDraftStage.TableBack)).Y,rim=s.Project(new Vector3(0,s.Table,s.TableFront)).Y;
            Check(capped||back<trayTop,what+": the table's edge ("+back+") is below the first card row ("+trayTop+")");
            Check(rim>=trayBottom&&rim<=trayBottom+NativeDraftStage.FrontGap+.01f,what+": the front rim at "+rim+" for the last row at "+trayBottom);worstRim=Math.Max(worstRim,Math.Abs(rim-trayBottom-NativeDraftStage.FrontGap));
            foreach(float z in new[]{NativeDraftStage.TableBack,s.TableFront}){var l=s.Project(new Vector3(s.TableHalfWidth,s.Table,z));var r=s.Project(new Vector3(-s.TableHalfWidth,s.Table,z));Check(l.X>1&&r.X<0,what+": the table does not span the screen ("+r.X+".."+l.X+")");}
            // Readable: the head at least as large on the screen as the standing framing showed it, the pilot centred.
            if(!siren&&!hat){var hp=p.WorldPosition[CapturedRig.Head];float share=s.Project(hp).Y-s.Project(hp+new Vector3(0,.36f,0)).Y,old=OldHeadShare(restHead,sc.Scale,trayTop);smallest=Math.Min(smallest,share/old);Check(share>=old*.995f,what+": the head is smaller than before ("+share+" against "+old+")");}
            float x=s.Project(p.WorldPosition[CapturedRig.Chest]).X;Check(Math.Abs(x-.5f)<.02f,what+": the pilot is not centred ("+x+")");
            // The gaze lands on each card in turn: its point projects onto the card's centre, and the head's aim falls nearest that
            // card among its row (yaw ordered left to right), looking down at the table.
            float lastYaw=-9;int lastRow=-1;
            for(int i=0;i<n;i++)
            {
                for(int f=0;f<72;f++){Frame(s,1/60f,-1,i,cards,sc.Scale,trayTop,trayBottom,siren,false,crown);HatInView("looking at card "+i);}
                var g=s.Project(s.Gaze);float err=Math.Max(Math.Abs(g.X-cards[i].X)*sc.W,Math.Abs(g.Y-cards[i].Y)*sc.H);worstGaze=Math.Max(worstGaze,err);
                Check(err<1.5f,what+": gaze "+err+" px off card "+i);
                var eye=Eye(p);var hf=Forward(p,CapturedRig.Head);float headYaw=(float)Math.Atan2(hf.X,hf.Z);int row=rects[i].Y>rects[0].Y+1?1:0,nearest=-1;float best=float.MaxValue,own=0;
                for(int j=0;j<n;j++){if((rects[j].Y>rects[0].Y+1?1:0)!=row)continue;var q=s.OnTable(cards[j].X,cards[j].Y,NativeDraftStage.CardLift)-eye;float yj=(float)Math.Atan2(q.X,q.Z),d=Math.Abs(yj-headYaw);if(j==i)own=yj;if(d<best){best=d;nearest=j;}}
                Check(nearest==i,what+": the head turns nearest card "+nearest+", not "+i);if(Math.Abs(own)<NativeDraftStage.YawLimit-.05f)worstAim=Math.Max(worstAim,Math.Abs(own-headYaw));
                Check(worstAim<.09f,what+": the head turns "+Math.Abs(own-headYaw)+" rad off card "+i);
                var fwd=Forward(p,CapturedRig.Head);float yaw=(float)Math.Atan2(fwd.X,fwd.Z),pitch=(float)Math.Asin(Math.Clamp(fwd.Y,-1,1));maxYaw=Math.Max(maxYaw,Math.Abs(yaw));
                Check(capped||pitch<(siren?-.1f:-.2f),what+": the head does not look down at card "+i+" (pitch "+pitch+")");
                if(row==lastRow)Check(yaw>lastYaw,what+": head yaw not ordered at card "+i);lastYaw=yaw;lastRow=row;
                // The lean: the upper spine and neck bow toward the table too, not only the head (the Siren is hunched already;
                // under the tallest hat the framing shows more of the table, the cards lie farther and the chest bows a little less).
                Check(capped||siren||Forward(p,CapturedRig.Chest).Y<(hat?-.07f:-.08f)&&Forward(p,CapturedRig.Neck).Y<-.15f,what+": the chest and neck do not bow (card "+i+", chest "+Forward(p,CapturedRig.Chest).Y+", neck "+Forward(p,CapturedRig.Neck).Y+")");
                pen=Penetration(s,out box);worstPen=Math.Max(worstPen,pen);Check(pen<.02f,what+": box "+box+" cuts "+pen+" m into the table looking at card "+i);
            }
            cases++;
        }
        yield return "Draft stage seated at the table on "+Screens.Length+" screens and interface scales, PvP draft and Siren pick trays, the Siren drafter and a hat, 3-12 offers ("+cases+" layouts): chest up (head top >= "+(worstHeadroom*100).ToString("0")+"% from the top, resting hands in view and palms down on the table, hips and legs hidden behind it; "+covered+" tall trays cover the hands to keep the head's room; the "+tallest+" hat, "+tallCrown.ToString("0.00")+" m over the head joint, whole within "+(worstHat*100).ToString("0.0")+"% of the top at worst), the head at least "+smallest.ToString("0.00")+"x its standing-framing size, the table in front (torso behind its edge, nothing cuts in deeper than "+(worstPen*1000).ToString("0")+" mm), its edge above the first row and its front rim within "+(worstRim*100).ToString("0.0")+"% of its place under the last, wider than the screen; gaze on every card within "+worstGaze.ToString("0.0")+" px, head turned nearest the highlighted card (within "+(worstAim*57.3f).ToString("0.0")+" deg, yaw up to "+(maxYaw*57.3f).ToString("0")+" deg), chest and neck bowed";

        // Every hat (framed on its measured top, as ArenaPresentation.DraftCrown measures it) and the Auditor halo (framed on
        // NativeAuditorHalo.Top, which bounds its flames at every moment and the source halo the reduced one draws) stay whole
        // in view on every screen through a browse across a 6-card PvP and an 8-card Siren-pick tray and the pick (30 Hz).
        {
            var id=new CapturedRig.Pose();for(int b=0;b<CapturedRig.Bones.Length;b++){id.WorldRotation[b]=Quaternion.Identity;id.WorldPosition[b]=Vector3.Zero;}
            var hf=new AuditorHaloFrame();float reach=0;
            for(int k=0;k<1200;k++)
            {
                float t=k*.05f;NativeAuditorHalo.Evaluate(t,false,hf);reach=Math.Max(reach,NativePortraitRules.HaloTop(id,t)-NativePortraitRules.GroupY);
                for(int n=0;n<NativeAuditorHalo.Flames;n++)reach=Math.Max(reach,(hf.GroupPosition+Vector3.Transform((hf.FlameBase[n]+new Vector3(0,hf.FlameHeight[n],0))*hf.GroupScale,hf.GroupRotation)).Y);
            }
            Check(NativeAuditorHalo.Top>=reach&&NativeAuditorHalo.Top<reach+.03f,"the halo's top "+NativeAuditorHalo.Top+" for a reach of "+reach);
            float worst=1;string where="";var names=new List<string>(hats.Keys){NativeAuditorHalo.Hat};
            foreach(string hat in names)foreach(var sc in Screens)foreach(bool pve in new[]{false,true})
            {
                int n=pve?8:6;var rects=Tray(n,sc.W,sc.H,pve,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);var s=new NativeDraftStage();var hold=new NativeDraftHover();bool halo=hat==NativeAuditorHalo.Hat;
                float crown=halo?NativeAuditorHalo.Top:Crown(hats[hat]),dt=1/30f,pickAt=1.2f+.6f*n;
                for(int f=0;f<=(int)((pickAt+3.5f)*30);f++)
                {
                    float t=f*dt;int hover=t<1.2f?-1:Math.Min(n-1,(int)((t-1.2f)/.6f));float raw=t>=pickAt?t-pickAt:-1;int focus=Considered(hold,raw>=0?n-1:-1,hover,t,n,dt,out bool consider);
                    Frame(s,dt,raw,focus,cards,sc.Scale,top/sc.H,bottom/sc.H,false,consider,crown);float row=halo?HaloRow(s,hf):HatRow(s,hats[hat]);
                    if(row<worst){worst=row;where=hat+" "+sc.Name+(pve?" Siren pick":"")+" t "+t.ToString("0.00");}Check(row>=.02f,"the "+hat+" top at "+row+" on "+sc.Name+(pve?" (Siren pick)":"")+" at t "+t.ToString("0.00"));
                }
            }
            yield return "Draft stage hats: all "+hats.Count+" hats (framed on their measured tops, up to "+tallCrown.ToString("0.00")+" m over the head joint) and the Auditor halo (its top "+NativeAuditorHalo.Top.ToString("0.000")+" m bounds its flames and the source halo, "+reach.ToString("0.000")+" m) stay whole in view through browse and pick on "+Screens.Length+" screens, 6-card PvP and 8-card Siren-pick trays (highest at "+(worst*100).ToString("0.0")+"% from the top: "+where+")";
        }

        // Settling: a step across the whole display and a step to a neighbour settle without overshoot (95% in .45 s, 1% in 1.1 s).
        {
            var sc=Screens[0];var rects=Tray(6,sc.W,sc.H,false,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);float worst95=0,worstOver=0,longest=0;
            foreach(var (a,b) in new[]{(0,5),(5,0),(2,3),(4,1)})
            {
                var s=new NativeDraftStage();for(int f=0;f<90;f++)Frame(s,1/60f,-1,a,cards,sc.Scale,top/sc.H,bottom/sc.H);
                float start=s.Gaze.X;Frame(s,0,-1,b,cards,sc.Scale,top/sc.H,bottom/sc.H);float goal=s.OnTable(cards[b].X,cards[b].Y,NativeDraftStage.CardLift).X,span=goal-start,settled95=-1,yawGoal;
                var yaws=new List<float>();
                for(int f=1;f<=120;f++)
                {
                    Frame(s,1/60f,-1,b,cards,sc.Scale,top/sc.H,bottom/sc.H);float u=(s.Gaze.X-start)/span;worstOver=Math.Max(worstOver,u-1);if(settled95<0&&u>=.95f)settled95=f/60f;
                    if(f==66)Check(Math.Abs(1-u)<.01f,"gaze not within 1% at 1.1 s ("+u+")");var fw=Forward(s.Pose,CapturedRig.Head);yaws.Add((float)Math.Atan2(fw.X,fw.Z));
                }
                yawGoal=yaws[yaws.Count-1];float sign=Math.Sign(yawGoal-yaws[0]);for(int k=1;k<yaws.Count;k++){Check(sign*(yaws[k]-yaws[k-1])>-2e-3f,"head yaw turns back while settling "+a+"->"+b);Check(sign*(yaws[k]-yawGoal)<.015f,"head yaw overshoots "+a+"->"+b+" by "+sign*(yaws[k]-yawGoal)+" at frame "+k);}
                // A neighbour settles in .45 s; a longer glance adds its turn at TurnSpeed.
                var e0=Eye(s.Pose);var qa=s.OnTable(cards[a].X,cards[a].Y,NativeDraftStage.CardLift)-e0;var qb=s.OnTable(cards[b].X,cards[b].Y,NativeDraftStage.CardLift)-e0;float turn=Math.Abs((float)(Math.Atan2(qb.X,qb.Z)-Math.Atan2(qa.X,qa.Z))),allowed=.45f+(Math.Abs(a-b)>1?turn/NativeDraftStage.TurnSpeed:0);
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
                    Frame(s,dt,-1,focus,cards,sc.Scale,trayTop,trayBottom,false,consider);float y=HeadYaw(s.Pose);if(f==0)sideA=s.OnTable(cards[a].X,cards[a].Y,NativeDraftStage.ReachLift).X>=0;
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

        // Considering (draft-stage.ts): at 30/60/144/240 Hz, the hand on the highlighted card's side lifts off the table and moves
        // toward the card while the other stays resting, on every card of a wide tray; the Siren drafter's claws stay resting.
        {
            var sc=Screens[1];var rects=Tray(6,sc.W,sc.H,false,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);float trayTop=top/sc.H,trayBottom=bottom/sc.H;float worstToward=0,worstUp=1,worstDown=0,leastLift=9,worstRest=0;
            foreach(int hz in new[]{30,60,144,240})for(int i=0;i<6;i++)foreach(bool siren in new[]{false,true})
            {
                var s=new NativeDraftStage();float dt=1f/hz;for(int f=0;f<(int)(1.2f*hz);f++)Frame(s,dt,-1,i,cards,sc.Scale,trayTop,trayBottom,siren,true);
                var goal=s.OnTable(cards[i].X,cards[i].Y,NativeDraftStage.ReachLift);bool left=goal.X>=0;
                if(siren){Check(s.Weight(true)==0&&s.Weight(false)==0,"the Siren drafter reaches for card "+i);continue;}
                worstUp=Math.Min(worstUp,s.Weight(left));worstDown=Math.Max(worstDown,s.Weight(!left));
                var p=s.Pose;var restHand=NativeDraftStage.RestHand(left);var hand=p.WorldPosition[left?CapturedRig.LHand:CapturedRig.RHand];var other=p.WorldPosition[left?CapturedRig.RHand:CapturedRig.LHand];
                var move=hand-restHand;var want=goal-restHand;move.Y=want.Y=0;float toward=(float)Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(move),Vector3.Normalize(want)),-1,1));
                worstToward=Math.Max(worstToward,toward);leastLift=Math.Min(leastLift,hand.Y-restHand.Y);worstRest=Math.Max(worstRest,(other-NativeDraftStage.RestHand(!left)).Length());
            }
            Check(worstUp>.97f&&worstDown<.03f,"considering arm at "+worstUp+", the other at "+worstDown);Check(worstToward<.6f,"the considering hand moves "+worstToward+" rad away from its card");
            Check(leastLift>.05f,"the considering hand lifts only "+leastLift+" m off the table");Check(worstRest<.01f,"the other hand leaves the table by "+worstRest+" m");
            yield return "Considering: the hand on the highlighted card's side lifts "+(leastLift*100).ToString("0")+"+ cm off the table and moves toward it (within "+(worstToward*57.3f).ToString("0")+" deg across the table) while the other rests (within "+(worstRest*1000).ToString("0")+" mm) on all 6 cards of a 3440x1440 tray at 30-240 Hz; the Siren drafter's claws stay resting";
        }

        // Motion at 30/60/144/240 Hz through the boot scan, quick hover changes (far and near, switching sides), the pick on the
        // edge card, the reach across the table, contact, plant and return, for the local picker (the arm considers the
        // highlighted card), a remote picker (no highlight: only the pick's reach) and the Siren drafter (claws resting): no
        // frame-to-frame snap of the head, chest or hands, never both arms lifted, no dip as a considering arm takes the card,
        // nothing cutting into the table, the same motion at every rate.
        float headRate=0,chestRate=0,handRate=0,browseHand=0,bothUp=0,worstPlant=0,worstDir=0,worstRate=0,dip=0,motionPen=0,settle=0;long allocated=0;string at="";var heads=new Dictionary<int,List<Quaternion>>();
        foreach(int hz in new[]{30,60,144,240})foreach(int mode in new[]{0,1,2})
        {
            bool siren=mode==2,local=mode!=1;var hold=new NativeDraftHover();
            var sc=Screens[1];var rects=Tray(6,sc.W,sc.H,false,out float top,out float bottom);var cards=Centres(rects,sc.W,sc.H);var s=new NativeDraftStage();float dt=1f/hz;double pickAt=4.2;
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
                if(raw<0&&mode!=0||siren)Check(wl==0&&wr==0,(siren?"Siren":"remote picker")+" arm lifted before the pick at "+hz+" Hz t "+t.ToString("0.00"));
                if(mode==0&&raw<0&&start>=1.5)Check(Math.Max(wl,wr)>.02f,"no arm considers the highlighted card at "+hz+" Hz t "+t.ToString("0.00"));
                // The considering arm keeps lifting through the pick: its weight never drops before the contact.
                if(mode==0&&raw>=0&&s.Age<NativeDraftPlant.Contact)dip=Math.Max(dip,(s.Left?lastWl:lastWr)-s.Weight(s.Left));lastWl=wl;lastWr=wr;
                first=false;lastHead=head;lastChest=chest;lastL=l;lastR=r;
                if(raw>=0){Check(s.Age>=0&&Math.Abs(s.Age-raw)<.07f&&s.Age>=lastAge,"smoothed age "+s.Age+" vs "+raw+" (last "+lastAge+")");lastAge=s.Age;}
                if(hz%10==0&&f%(hz/10)==0){samples.Add(head);}
                // The pilot stays behind the table's edge, nothing cuts into its top, the palm stays over the table.
                foreach(int b in new[]{CapturedRig.Spine,CapturedRig.Pelvis})Check(p.WorldPosition[b].Z<NativeDraftStage.TableBack,"bone "+b+" past the table's edge at "+hz+" Hz t "+t.ToString("0.00"));
                {float pen=Penetration(s,out int box);if(pen>motionPen)motionPen=pen;Check(pen<.025f,"box "+box+" cuts "+pen+" m into the table at "+hz+" Hz t "+t.ToString("0.00"));Check(s.Palm.Z<s.TableFront&&s.Palm.Y>s.Table,"the palm leaves the table's space at "+hz+" Hz t "+t.ToString("0.00"));}
                if(!siren&&raw>=0)
                {
                    float age=s.Age;int hand=s.Left?CapturedRig.LHand:CapturedRig.RHand;
                    if(Math.Abs(age-NativeDraftPlant.Contact)<.5f*dt+.002f)
                    {   // At the contact the arm points across the table at the chosen card's reach point.
                        var shoulder=p.WorldPosition[hand-2];var goal=s.OnTable(cards[5].X,cards[5].Y,NativeDraftStage.ReachLift);
                        float cos=Vector3.Dot(Vector3.Normalize(p.WorldPosition[hand]-shoulder),Vector3.Normalize(goal-shoulder));worstDir=Math.Max(worstDir,(float)Math.Acos(Math.Min(1,cos)));
                    }
                    if(Math.Abs(age-NativeDraftPlant.CarryEnd)<.5f*dt+.002f)worstPlant=Math.Max(worstPlant,(s.Palm-s.Plant.At).Length());
                    // After the return both hands rest on the table again.
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
        Check(worstDir<.25f,"the reach points "+worstDir+" rad off the chosen card");Check(worstPlant<.03f,"the palm misses the wrist by "+worstPlant+" m at the plant");Check(settle<.01f,"the hands do not settle back on the table ("+settle+" m)");Check(worstRate<.02f,"frame rates disagree by "+worstRate+" rad while browsing");Check(worstPick<.15f,"frame rates disagree by "+worstPick+" rad through the pick");Check(allocated==0,"the stage allocated "+allocated+" bytes");
        yield return "Draft stage at 30/60/144/240 Hz (local and remote pilot and Siren, 15 Hz guest clock): head <= "+headRate.ToString("0.0")+" rad/s, chest <= "+chestRate.ToString("0.0")+" rad/s, hands <= "+handRate.ToString("0.00")+" m/s per frame (no snaps; considering <= "+browseHand.ToString("0.00")+" m/s), one considering arm at a time (the other at most "+bothUp.ToString("0.00")+" up on a switch), no dip into the pick, rates agree within "+(worstRate*57.3f).ToString("0.0")+" deg browsing and "+(worstPick*57.3f).ToString("0")+" deg through the pick, reach across the table within "+(worstDir*57.3f).ToString("0")+" deg of the chosen card, palm on the wrist within "+(worstPlant*1000).ToString("0")+" mm, hands back on the table within "+(settle*1000).ToString("0")+" mm, pilot behind the table's edge and nothing deeper than "+(motionPen*1000).ToString("0")+" mm into it, 0 bytes per frame";
    }
}

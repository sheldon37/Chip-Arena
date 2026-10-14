using System;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using E=ChipArena.UnityConversion.NativeExpeditionHud;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition screen layout (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md), shared by the live game (a bound session's
    /// view, NativeExpeditionLiveUI.cs) and the DEV preview: the world board on the left, your chips on the right, and in the centre
    /// the host-transfer overlay, the phase's panel (building, sanctuary, victory, final area) and the caches or the loot. The dice
    /// reveal opens when a new stored record appears. Everything reads the view; controls return intents for the caller to send.
    /// </summary>
    public static class NativeExpeditionScreen
    {
        /// <summary>The dice reveal: the newest record once it appears in the view (not the ones already there when watching began).</summary>
        public sealed class Dice
        {
            public ExpeditionDice Record;public float At=-100;private int seen=-1,loop=-1;private string run;
            public void Reset(ExpeditionView v){run=v?.run;loop=v?.loop??-1;seen=v?.dice.Length??-1;Record=null;}
            public void Watch(ExpeditionView v,float now)
            {
                if(v==null)return;if(v.run!=run||v.loop!=loop||seen<0){Reset(v);return;}
                if(v.dice.Length>seen){Record=v.dice[v.dice.Length-1];At=now;}seen=v.dice.Length;
            }
            public void Show(ExpeditionDice d,float now){Record=d;At=now;}
            public bool Open=>Record!=null;
        }

        /// <summary>The panels for one view between `top` and `bottom` (canvas units). Returns the intent a control raised.</summary>
        public static ExpeditionCommand Panels(ExpeditionView v,E.Controls c,float W,float top,float bottom,NativeExpeditionWorld greybox=null,float t=0,bool reduced=false)
        {
            var command=default(ExpeditionCommand);float gap=16,side=Mathf.Clamp((W-48-2*gap)*.27f,280,380),cx=24+side+gap,cw=W-2*(24+side+gap);
            E.Board(new Rect(24,top,side,bottom-top),v,c);
            var chip=E.Chips(new Rect(W-24-side,top,side,bottom-top),v,c);if(chip.intent!=ExpeditionIntent.None)command=chip;
            if(greybox!=null){Greybox(new Rect(cx,top,cw,bottom-top),v,greybox,c,t,reduced);return command;}
            float y=top;
            if(v.transfer.stage!=ExpeditionTransferStage.None)
            {
                int steps=NativeExpeditionPresentation.Transfer(v,out _,out _).Length,cands=v.transfer.candidates.Length;
                var tr=new Rect(cx,y,cw,Mathf.Min(Mathf.Max(140,bottom-y-260),96+30*steps+(cands>0?28+34*cands:0)));E.Transfer(tr,v,c);y=tr.yMax+gap;
            }
            float want=v.phase==ExpeditionPhase.Victory?560:v.phase==ExpeditionPhase.Town?400:360,panel=Mathf.Min(want,bottom-y);
            var ctx=new Rect(cx,y,cw,panel);ExpeditionCommand pick=default;
            switch(v.phase)
            {
                case ExpeditionPhase.Instance:pick=E.Building(ctx,v,c);break;
                case ExpeditionPhase.Victory:pick=E.Victory(ctx,v,c);break;
                case ExpeditionPhase.Town:pick=E.Sanctuary(ctx,v,c);break;
                default:E.Phase(ctx,v);break;
            }
            if(pick.intent!=ExpeditionIntent.None)command=pick;
            float rest=bottom-ctx.yMax-gap;
            if(rest>140)
            {
                var low=new Rect(cx,ctx.yMax+gap,cw,rest);
                if(v.phase==ExpeditionPhase.Town&&v.chests.Length>0)
                {
                    if(v.loot.Length>0&&cw>=640){float h=(cw-gap)/2;E.Caches(new Rect(low.x,low.y,h,low.height),v,c);E.Loot(new Rect(low.x+h+gap,low.y,h,low.height),v);}
                    else E.Caches(low,v,c);
                }
                else E.Loot(low,v);
            }
            return command;
        }

        /// <summary>The dice reveal over the screen (dimmed behind). Returns true when its Close was pressed.</summary>
        public static bool DiceOverlay(Dice d,ExpeditionView v,E.Controls c,float W,float H,float now,bool reduced,bool dim=true)
        {
            if(d.Record==null)return false;float t=now-d.At,dh=Mathf.Min(H-80,160+44*d.Record.eligible.Length);var dr=new Rect(W/2-290,H/2-dh/2-30,580,dh);
            if(dim)U.Fill(new Rect(0,0,W,H),C.Void(.55f));E.Dice(dr,v,d.Record,t,c,reduced);
            bool close=U.Btn(new Rect(dr.xMax-128,dr.y+14,110,30),"Close",false,true,1,13);
            if(close||t>NativeExpeditionPresentation.Dice.Duration(d.Record)+6)d.Record=null;return close;
        }

        // ---- DEV PREVIEW greybox -----------------------------------------------------------------------------------------------
        private static readonly int[] order=new int[256];private static readonly float[] depth=new float[256];private static readonly bool[] shown=new bool[256];
        /// <summary>
        /// DEV PREVIEW ONLY: the current area's world pieces on a greybox floor, seen by a fixed camera fitted to the anchors, with the
        /// same glyphs and labels the live game projects. Only real anchors are drawn; an area without anchors shows none.
        /// </summary>
        public static void Greybox(Rect r,ExpeditionView v,NativeExpeditionWorld world,E.Controls c,float t,bool reduced)
        {U.BeginBand(r);try{
            H.Panel(r,1,C.Warn(),NativeHudCyber.Cut,H.Corners.Diagonal,true,.96f);
            U.Text(new Rect(r.x+18,r.y+12,r.width-36,14),"GREYBOX · DEV ONLY · PIECES AT THE VIEW'S ANCHORS",11,C.Warn(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            U.Text(new Rect(r.x+18,r.y+28,r.width-36,22),NativeExpeditionPresentation.Name(v.area),18,C.Ink(),TextAnchor.MiddleLeft,bold:true,tracking:.04f);
            int n=Math.Min(world.Build(v),order.Length);
            if(n==0){U.Text(new Rect(r.x+18,r.y+60,r.width-36,18),"No anchors in this area: nothing is drawn (never a guessed origin).",12,C.Muted(),TextAnchor.MiddleLeft);return;}
            float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue,floor=0;
            for(int i=0;i<n;i++){var p=world.Pieces[i];minX=Mathf.Min(minX,p.ax);maxX=Mathf.Max(maxX,p.ax);minZ=Mathf.Min(minZ,p.az);maxZ=Mathf.Max(maxZ,p.az);floor+=p.ay/n;}
            float mx=(minX+maxX)/2,mz=(minZ+maxZ)/2,span=Mathf.Max(10,Mathf.Max(maxX-minX,maxZ-minZ)+6);
            var view=new Rect(r.x+8,r.y+56,r.width-16,r.height-84);
            var cam=ExpeditionGreyboxCamera.Look(mx,floor+span*.62f,mz-span*.72f,mx,floor,mz+span*.08f,50,view.x,view.y,view.width,view.height);
            // The greybox floor: a 2 m dot grid over the anchors' bounds.
            for(float gx=Mathf.Floor((minX-4)/2)*2;gx<=maxX+4;gx+=2)for(float gz=Mathf.Floor((minZ-4)/2)*2;gz<=maxZ+4;gz+=2)
                if(cam.Project(gx,floor,gz,out float sx,out float sy,out float dz)){float s=Mathf.Clamp(cam.Scale(dz)*.05f,1,3);U.Fill(new Rect(sx-s/2,sy-s/2,s,s),C.Edge(.9f));}
            // Back to front.
            for(int i=0;i<n;i++){var p=world.Pieces[i];cam.Project(p.x,p.y,p.z,out _,out _,out depth[i]);order[i]=i;}
            for(int i=1;i<n;i++){int k=order[i],j=i-1;while(j>=0&&depth[order[j]]<depth[k]){order[j+1]=order[j];j--;}order[j+1]=k;}
            float squash=Mathf.Clamp(Mathf.Abs(cam.fy),.2f,1);
            // Nearest first claims the label areas (order is far to near).
            E.LabelsReset();
            for(int o=n-1;o>=0;o--)
            {
                var p=world.Pieces[order[o]];shown[o]=false;if(!cam.Project(p.x,p.y+NativeExpeditionWorld.GlyphHeight(p.kind),p.z,out float qx,out float qy,out float qd))continue;
                var at=new Vector2(qx,qy);if(p.labelAtAnchor&&cam.Project(p.ax,p.ay+NativeExpeditionWorld.GlyphHeight(p.kind),p.az,out float ax,out float ay,out _))at=new Vector2(ax,ay);
                shown[o]=E.LabelClaim(p,at,cam.Scale(qd),9);
            }
            for(int o=0;o<n;o++)
            {
                var p=world.Pieces[order[o]];if(!cam.Project(p.x,p.y,p.z,out float fx,out float fy,out float fd))continue;float k=cam.Scale(fd),rr=NativeExpeditionWorld.RingRadius(p.kind)*k;
                // The floor ring and a thin stalk up to the glyph, in the piece's tone.
                var ring=new Rect(fx-rr,fy-rr*squash,rr*2,rr*2*squash);U.Round(ring,U.A(E.Tone(p.tone),p.settled?.06f:.14f),Mathf.Min(ring.width,ring.height)/2,E.Tone(p.tone,p.settled?.4f:.9f),1.5f);
                if(!cam.Project(p.x,p.y+NativeExpeditionWorld.GlyphHeight(p.kind)+NativeExpeditionWorld.Bob(p,t,reduced),p.z,out float gx2,out float gy2,out _))continue;
                if(p.actionable)U.Fill(new Rect(fx-1,gy2,2,Mathf.Max(0,fy-gy2)),E.Tone(p.tone,.35f));
                var label=new Vector2(gx2,gy2);if(p.labelAtAnchor&&cam.Project(p.ax,p.ay+NativeExpeditionWorld.GlyphHeight(p.kind),p.az,out float lx,out float ly,out _))label=new Vector2(lx,ly);
                E.Piece(p,new Vector2(gx2,gy2),label,k,shown[o],true,1,9);
            }
            U.Text(new Rect(r.x+18,r.yMax-24,r.width-36,16),"Live: drawn in 3D at these anchors · labels within "+NativeExpeditionWorld.LabelDistance+" m · culled past "+NativeExpeditionWorld.CullDistance+" m · claiming goes through the commands.",10,C.Muted(),TextAnchor.MiddleLeft,mono:true);
        }finally{U.EndBand();}}
    }
}

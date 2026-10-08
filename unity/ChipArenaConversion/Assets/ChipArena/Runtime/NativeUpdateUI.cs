using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // Presentation adapter only: NativeUpdater owns checking, downloads, verification, shutdown and restart.
        public NativeUpdater Updater { get;private set; }
        private void InitializeUpdater()
        {
            if(dedicated)return;
            Updater=gameObject.AddComponent<NativeUpdater>();
            Updater.Initialize(()=>menu&&!Online&&(arena==null||match.Phase==NativePhase.Match),Save);
        }
        private const float UpdateCardWidth=340;private float updateCardAt=-1,updateCardSeen=-1;
        private static string UpdateChip(NativeUpdater u)=>NotPublished(u)?"NOT PUBLISHED":UpdateChip(u.State,u.Progress);
        private static string UpdateChip(NativeUpdateState s,float progress)=>s==NativeUpdateState.Available?"UPDATE READY":s==NativeUpdateState.Downloading?"DOWNLOADING "+Mathf.RoundToInt(progress*100)+"%":s==NativeUpdateState.Checking?"CHECKING":s==NativeUpdateState.Verifying?"VERIFYING":s==NativeUpdateState.Preparing?"PREPARING":s==NativeUpdateState.Restarting?"RESTARTING":s==NativeUpdateState.Current?"UP TO DATE":s==NativeUpdateState.Error?"ERROR":s==NativeUpdateState.Unavailable?"UNAVAILABLE":"IDLE";
        private static Color UpdateTone(NativeUpdateState s)=>s==NativeUpdateState.Available?U.Hex(0xff6b35):s==NativeUpdateState.Current?U.Hex(0x59dd83):s==NativeUpdateState.Error?U.Hex(0xff535f):s==NativeUpdateState.Unavailable||s==NativeUpdateState.Idle?U.Hex(0xa6a7ad):U.Hex(0xffb15c);
        // The update card, opened by the home screen's Update Game pill and hanging beneath it from (right, top) (the
        // source .panel look): build identities, status, progress, and the actions the service allows — Check / Retry,
        // Cancel while checking or downloading, and Update and restart only while CanInstall (outside a match and room).
        // The service closes and restarts the game itself.
        private void UpdatePanel(float right,float top)
        {
            if(Updater==null)return;var u=Updater;var state=u.State;float w=UpdateCardWidth,pad=18;
            if(Time.unscaledTime-updateCardSeen>.25f)updateCardAt=Time.unscaledTime;updateCardSeen=Time.unscaledTime;float enter=U.PanelIn(Time.unscaledTime-updateCardAt);
            bool progress=state==NativeUpdateState.Downloading||state==NativeUpdateState.Verifying||state==NativeUpdateState.Preparing||state==NativeUpdateState.Restarting;
            var status=U.Wrap(u.Status??"",13,w-2*pad,false,4);bool hint=state==NativeUpdateState.Available&&!u.CanInstall;var hintLines=hint?U.Wrap("Finish the match and leave the room before updating.",12,w-2*pad,false,2):null;
            bool check=!u.Busy&&state!=NativeUpdateState.Unavailable,install=state==NativeUpdateState.Available,cancel=u.CanCancel;bool buttons=check||install||cancel;
            float h=pad+16+12+18+(u.Available!=null&&install?18:0)+8+status.Count*13*1.45f+(progress?22:0)+(hint?6+hintLines.Count*12*1.45f:0)+(buttons?14+44:0)+pad;
            var r=new Rect(right-w,top-8*(1-enter),w,h);updateCard=r;U.Panel(r,enter);float y=r.y+pad,x=r.x+pad,iw=w-2*pad;
            // Eyebrow and the state chip.
            U.Text(new Rect(x,y,iw,16),"UNITY UPDATES",11,U.Hex(0xa6a7ad,enter),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            string chip=UpdateChip(u);Color tone=UpdateTone(state);float cw=U.Measure(chip,10,true,true,.12f).x+18;var cr=new Rect(r.xMax-pad-cw,y-2,cw,20);
            U.Pill(cr,U.A(tone,.14f*enter),U.A(tone,.55f*enter));U.Text(cr,chip,10,U.A(tone,enter),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
            if(state==NativeUpdateState.Checking){float a=Time.unscaledTime*6;for(int i=0;i<6;i++){float t=a-i*.5f;var d=new Rect(cr.x-16+5*Mathf.Cos(t),cr.center.y-1.5f+5*Mathf.Sin(t),3,3);U.Round(d,U.A(tone,enter*(1-i/6f)),2);}}
            y+=16+12;
            // Build identities.
            U.Text(new Rect(x,y,iw,18),"Installed · "+(u.Installed!=null?NativeUpdater.Short(u.Installed.version):"starter build required"),12,U.Hex(0xd7d8de,enter),TextAnchor.MiddleLeft,mono:true);y+=18;
            if(u.Available!=null&&install){U.Text(new Rect(x,y,iw,18),"Available · "+NativeUpdater.Short(u.Available.version),12,U.Hex(0xff996f,enter),TextAnchor.MiddleLeft,bold:true,mono:true);y+=18;}
            y+=8;foreach(var line in status){U.Text(new Rect(x,y,iw,13*1.45f),line,13,state==NativeUpdateState.Error?U.Hex(0xffb3ba,enter):U.Hex(0xbfc0c9,enter),TextAnchor.MiddleLeft);y+=13*1.45f;}
            // Progress: the download fills; verification and preparation shimmer until the service restarts the game.
            if(progress)
            {
                y+=10;var track=new Rect(x,y,iw,6);U.Round(track,new Color(1,1,1,.08f*enter),4);
                if(state==NativeUpdateState.Downloading){float f=Mathf.Clamp01(u.Progress);if(f>0)U.HGradient(new Rect(x,y,iw*f,6),U.Hex(0xff6b35,enter),U.Hex(0xffb15c,enter));}
                else{float band=iw*.3f,bx=x+Mathf.Repeat(Time.unscaledTime*.8f,1.3f)*iw-band;var b=Rect.MinMaxRect(Mathf.Max(x,bx),y,Mathf.Min(x+iw,bx+band),y+6);if(b.width>0){U.HGradient(new Rect(b.x,b.y,b.width/2,6),Color.clear,U.Hex(0xffb15c,enter));U.HGradient(new Rect(b.x+b.width/2,b.y,b.width/2,6),U.Hex(0xffb15c,enter),Color.clear);}}
                y+=12;
            }
            if(hint){y+=6;foreach(var line in hintLines){U.Text(new Rect(x,y,iw,12*1.45f),line,12,U.Hex(0xa6a7ad,enter),TextAnchor.MiddleLeft);y+=12*1.45f;}}
            if(!buttons)return;y+=14;
            // Actions: Update and restart (primary, gated by CanInstall), Retry after an error, Check otherwise, and Cancel.
            float gap=10,bw=cancel?(iw-gap)/2:iw;
            if(install){float iw2=check?iw*.62f:iw;if(U.Btn(new Rect(x,y,iw2,44),"Update and restart",true,u.CanInstall,enter,14))u.InstallAvailableUpdate();if(check&&U.Btn(new Rect(x+iw2+gap,y,iw-iw2-gap,44),"Check",false,true,enter,14))u.CheckForUpdates();return;}
            if(check&&U.Btn(new Rect(x,y,bw,44),state==NativeUpdateState.Error?"Retry":"Check for updates",state==NativeUpdateState.Error,true,enter,14))u.CheckForUpdates();
            if(cancel&&U.Btn(new Rect(check?x+bw+gap:x,y,check?bw:iw,44),state==NativeUpdateState.Downloading?"Cancel download":"Cancel",false,true,enter,14))u.Cancel();
        }
    }
}

using System;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // The REVIVE-O-MATIC's prompt and screen (docs/REVIVE-MACHINE.md), in the menus' kit, so a mouse and a controller both work (the kit's buttons are the controller's focus targets). The screen reads NativeReviveMachine.Board and asks
    // ReviveMachineRequest; it decides nothing. Without the rules bound it shows the list and says plainly it can't act yet.
    public sealed partial class ConversionGame
    {
        /// <summary>The rules' hook (Astra, docs/REVIVE-MACHINE.md): asked with the dead pilot's account when the viewer presses REVIVE; null when the request went off, else why not (shown on the screen). Unbound: the screen says it isn't connected.</summary>
        public Func<string,string> ReviveMachineRequest;
        private bool reviveScreenOpen;private string reviveNotice;private float reviveNoticeUntil;private readonly NativeReviveBoard reviveLiveBoard=new NativeReviveBoard();
        private static readonly Color ReviveTeal=new Color(.24f,.76f,.74f),ReviveBrass=new Color(.91f,.72f,.34f),ReviveRed=new Color(1,.3f,.26f);
        private bool ReviveMachineScreenOpen=>reviveScreenOpen;
        private bool ReviveMachinePreviewing=>presentation!=null&&(presentation.ReviveMachineGallery||presentation.ReviveMachineAuto)&&arena?.Sandbox==true;
        // The list as the view has it: every member down and not gone for good; the wallet; whether this pilot can pay here. Spits come from NativeReviveMachine.LiveSpits, which the rules fill.
        private NativeReviveBoard ReviveMachineLiveBoard()
        {
            var v=expeditionLiveView;var b=reviveLiveBoard;if(v==null)return null;
            int n=0;foreach(var m in v.members)if(m!=null&&!m.alive&&!m.extracted)n++;if(b.Dead.Length!=n)b.Dead=new NativeReviveDead[n];
            int i=0;foreach(var m in v.members)if(m!=null&&!m.alive&&!m.extracted)b.Dead[i++]=NativeReviveMachine.FromMember(m);
            b.Wallet=v.mine.wallet;b.Spits=NativeReviveMachine.LiveSpits??Array.Empty<NativeReviveSpit>();b.Blocked=presentation?.GeneratedRun?.Mutators!=null&&Array.IndexOf(presentation.GeneratedRun.Mutators,"permadeath")>=0?"PERMADEATH: THE MACHINE WON'T START THIS RUN.":null;
            NativeReviveMachine.Fill?.Invoke(b,v);return b;
        }
        // Every frame in the world: keeps the board current.
        private void ReviveMachineSync()
        {
            if(ReviveMachinePreviewing)return;if(LiveExpedition!=null&&presentation!=null&&presentation.ReviveMachineStands)NativeReviveMachine.Board=ReviveMachineLiveBoard();else if(LiveExpedition==null)NativeReviveMachine.Board=null;
        }
        private bool ReviveMachineNear()
        {
            var me=arena?.Local;if(me==null||me.Hp<=0||presentation==null||!presentation.ReviveMachineStands||menu)return false;if(match?.Phase!=NativePhase.Fight)return false;
            var use=presentation.ReviveMachineUsePoint();var at=me.Body.position;return new Vector2(at.x-use.x,at.z-use.z).sqrMagnitude<=NativeReviveMachine.UseRange*NativeReviveMachine.UseRange&&Mathf.Abs(at.y-use.y)<3;
        }
        /// <summary>Update, before play is worked out: E (or the pad's interact) at the machine opens its screen, so the press never reaches the ability; E, Escape or the pad's back close it. True: an Escape was used here.</summary>
        private bool ReviveMachineKeys()
        {
            ReviveMachineSync();
            var preview=ReviveMachinePreviewing?presentation.ReviveMachinePreview:null;
            if(preview!=null&&presentation.ReviveMachineAuto)
            {
                reviveScreenOpen=preview.WantOpen;
                if(preview.WantPress>=0&&preview.WantPress<preview.Board.Dead.Length){ReviveMachinePress(preview.Board.Dead[preview.WantPress]);preview.WantPress=-1;}
                return false;
            }
            bool near=ReviveMachineNear()||preview!=null&&arena?.Local?.Hp>0&&!menu&&(presentation.ReviveMachineUsePoint()-arena.Local.Body.position).sqrMagnitude<64;
            if(reviveScreenOpen)
            {
                if(!near){reviveScreenOpen=false;return false;}
                if(Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.E)||PadBack){reviveScreenOpen=false;return true;}
                return false;
            }
            if(near&&!BoardOpen&&!expeditionScreenOpen&&!PvePickOpen&&!ExpeditionPickPointer&&GUIUtility.keyboardControl==0&&(Input.GetKeyDown(KeyCode.E)||PadDown(PadAction.Interact)))
            {if(NativeReviveMachine.Current(NativeReviveMachine.Board,presentation.ReviveMachineClock)==null){reviveScreenOpen=true;reviveNotice=null;NativeUi.Cue(NativeUiSounds.Click);}}
            return false;
        }
        private void ReviveMachinePress(NativeReviveDead row)
        {
            string refused=ReviveMachinePreviewing?presentation.ReviveMachinePreview.Request(row.Account,presentation.ReviveMachineClock):ReviveMachineRequest!=null?ReviveMachineRequest(row.Account):NotWired;
            if(refused!=null){reviveNotice=refused;reviveNoticeUntil=Time.unscaledTime+4;return;}
            reviveScreenOpen=false;
        }
        private const string NotWired="THE RULES AREN'T CONNECTED YET. NOTHING WAS CHARGED.";
        /// <summary>GuiFrame, over the HUD: the prompt at the machine, the screen, and the comic word when it goes.</summary>
        private void ReviveMachineGui(float width,float height)
        {
            if(presentation==null||!presentation.ReviveMachineStands||menu)return;
            ReviveMachineWord(width,height);
            if(reviveScreenOpen){ReviveMachineScreen(width,height);return;}
            var me=arena?.Local;if(me==null||me.Hp<=0||Event.current.type!=EventType.Repaint)return;
            bool near=ReviveMachineNear()||ReviveMachinePreviewing&&(presentation.ReviveMachineUsePoint()-me.Body.position).sqrMagnitude<64;if(!near)return;
            var board=NativeReviveMachine.Board;int down=board?.Dead?.Length??0;bool busy=NativeReviveMachine.Current(board,presentation.ReviveMachineClock)!=null;
            float y=height*.68f,w=380;var r=new Rect(width/2-w/2,y,w,46);H.Tag(r,ReviveBrass,.95f);
            var key=new Rect(r.x+10,r.y+8,30,30);U.Round(key,ReviveBrass,5);U.Text(key,ReviveMachineKeyLabel,15,C.Void(),TextAnchor.MiddleCenter,bold:true,mono:true);
            U.Text(new Rect(r.x+50,r.y+5,r.width-58,20),"REVIVE-O-MATIC",14,C.Ink(),TextAnchor.MiddleLeft,bold:true,tracking:.08f);
            U.Text(new Rect(r.x+50,r.y+24,r.width-58,16),busy?"STAND CLEAR":down==0?"NOBODY'S DEAD":down==1?"1 DOWN · $"+(board?.Cost??NativeReviveMachine.Price):down+" DOWN · $"+(board?.Cost??NativeReviveMachine.Price),11,busy?ReviveRed:ReviveBrass,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
        }
        private string ReviveMachineKeyLabel=>padPointerActive||UnityEngine.InputSystem.Gamepad.current!=null&&ControllerPrefs.enabled&&padRecent?NativeControllerBindings.Label(ControllerPrefs.bindings[(int)PadAction.Interact],PadLabels):"E";
        private bool padRecent=>padMoveAxis.sqrMagnitude>.01f||padLookAxis.sqrMagnitude>.01f;
        // PTOOEY, over the mouth, as it goes: a comic book's word, there and gone.
        private void ReviveMachineWord(float width,float height)
        {
            float a=presentation.ReviveMachineSeq-NativeReviveMachine.Spit;if(a<0||a>=.85f||Event.current.type!=EventType.Repaint||cameraView==null)return;
            var scale=U.S;var at=cameraView.WorldToScreenPoint(presentation.ReviveMachineMouth());if(at.z<=0)return;at/=scale;float pop=a<.1f?a/.1f:1,s=(.5f+.72f*pop)*(1+.05f*Mathf.Sin(a*30)),fade=a<.62f?1:1-(a-.62f)/.23f;
            float w=430*s,hh=120*s;var r=new Rect(Mathf.Clamp(at.x,w/2,width-w/2)-w/2,Mathf.Clamp(height-at.y,hh,height-hh)-hh*.9f-a*40,w,hh);
            var dark=new Color(.1f,.03f,.02f,fade);foreach(var d in Outline)U.Text(new Rect(r.x+d.x,r.y+d.y,r.width,r.height),"PTOOEY!",82*s,dark,TextAnchor.MiddleCenter,bold:true,italic:true);
            U.Text(r,"PTOOEY!",82*s,new Color(1,.86f,.16f,fade),TextAnchor.MiddleCenter,bold:true,italic:true,glow:new Color(1,.3f,.05f,.8f*fade));
        }
        private static readonly Vector2[] Outline={new Vector2(-4,0),new Vector2(4,0),new Vector2(0,-4),new Vector2(0,5),new Vector2(-3,-3),new Vector2(3,-3),new Vector2(-3,4),new Vector2(3,4)};
        // The screen: the list, each row who, where the body is, what did it; a REVIVE button on each. Its states: nobody dead, can't pay, offline, the rules not connected, blocked outright.
        private void ReviveMachineScreen(float width,float height)
        {
            var b=NativeReviveMachine.Board;var dead=b?.Dead??Array.Empty<NativeReviveDead>();int cost=b?.Cost??NativeReviveMachine.Price,wallet=b?.Wallet??0;bool preview=b?.Preview==true,wired=preview||ReviveMachineRequest!=null;
            U.Fill(new Rect(0,0,width,height),C.Void(.72f));
            float pw=Mathf.Min(1040,width-80),rowH=118,ph=Mathf.Clamp(164+Mathf.Max(1,dead.Length)*(rowH+10)+70,330,height-50),px=width/2-pw/2,py=height/2-ph/2;var panel=new Rect(px,py,pw,ph);
            H.Panel(panel,1,ReviveBrass,14,H.Corners.Diagonal,true,.96f);
            // The header: the machine's name in its own marquee, the wallet in the corner.
            var marquee=new Rect(px+24,py+18,pw-48,84);U.Round(marquee,new Color(.03f,.03f,.04f,.95f),6,new Color(.8f,.82f,.86f,.55f),1.5f);
            float clock=Time.unscaledTime;for(int i=0;i<34;i++){float k=(i+.5f)/34,lit=((int)(clock*5)+i)%2==0?1:.25f;var bulb=new Color(1,.74f,.36f,lit);U.Fill(new Rect(marquee.x+k*marquee.width-2,marquee.y+4,4,4),bulb);U.Fill(new Rect(marquee.x+k*marquee.width-2,marquee.yMax-8,4,4),bulb);}
            U.Text(new Rect(marquee.x+24,marquee.y+12,520,46),"REVIVE-O-MATIC",36,new Color(1,.9f,.72f),TextAnchor.MiddleLeft,bold:true,tracking:.05f,glow:new Color(1,.6f,.2f,.5f));
            U.Text(new Rect(marquee.x+26,marquee.y+56,560,18),"GOOD AS NEW OR CLOSE ENOUGH  ·  $"+cost+" A HEAD  ·  NO REFUNDS",11,ReviveBrass,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            U.Text(new Rect(marquee.xMax-250,marquee.y+12,226,18),"YOUR WALLET",10,C.Muted(),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.16f);
            U.Text(new Rect(marquee.xMax-250,marquee.y+30,226,38),"$"+wallet.ToString("N0"),30,wallet<cost?ReviveRed:new Color(.55f,1,.7f),TextAnchor.MiddleRight,bold:true,mono:true);
            float y=py+114;
            if(!string.IsNullOrEmpty(b?.Blocked))
            {
                var r=new Rect(px+24,y,pw-48,34);U.Fill(r,new Color(.5f,.08f,.06f,.5f));U.Text(r,b.Blocked,13,ReviveRed,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);y+=42;
            }
            if(dead.Length==0)
            {
                var r=new Rect(px+24,y+6,pw-48,ph-(y-py)-76);U.Fill(r,C.Void(.5f));
                ReviveMachineFace(new Vector2(r.center.x,r.y+66),clock);
                U.Text(new Rect(r.x,r.y+118,r.width,40),"NOBODY'S DEAD.",30,C.Ink(),TextAnchor.MiddleCenter,bold:true,tracking:.06f);
                U.Text(new Rect(r.x,r.y+156,r.width,22),"The machine is disappointed. Come back when someone has made a mistake.",14,C.Muted(),TextAnchor.MiddleCenter);
            }
            else
            {
                bool poor=wallet<cost;
                for(int i=0;i<dead.Length&&y+rowH<py+ph-50;i++,y+=rowH+10)
                {
                    var d=dead[i];var row=new Rect(px+24,y,pw-48,rowH);bool offline=!d.Connected;U.Fill(row,C.Void(.55f));U.Fill(new Rect(row.x,row.y,4,row.height),offline?C.Dim(.8f):ReviveRed);
                    // Who.
                    U.Text(new Rect(row.x+18,row.y+10,240,30),(d.Name??"?").ToUpperInvariant(),21,C.Ink(),TextAnchor.MiddleLeft,bold:true,tracking:.03f);
                    U.Text(new Rect(row.x+18,row.y+40,240,16),offline?"OFFLINE":d.DiedAt>=0?"DOWN":"DOWN",11,offline?C.Dim():ReviveRed,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
                    if(!string.IsNullOrEmpty(d.Weapon))U.Text(new Rect(row.x+18,row.y+58,240,16),d.Weapon.ToUpperInvariant(),10,C.Muted(),TextAnchor.MiddleLeft,mono:true,tracking:.12f);
                    // Where the body lies.
                    float cx=row.x+250,cw=row.width-250-226;
                    U.Text(new Rect(cx,row.y+9,cw,14),"BODY",10,ReviveBrass,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
                    U.Text(new Rect(cx,row.y+22,cw,22),string.IsNullOrEmpty(d.AreaName)?"NOT REPORTED":d.AreaName,15,C.Ink(),TextAnchor.MiddleLeft,bold:true,tracking:.02f);
                    string place=!string.IsNullOrEmpty(d.Place)?d.Place:d.Located?"Grid "+Mathf.RoundToInt(d.Body.x)+", "+Mathf.RoundToInt(d.Body.z):"";if(place.Length>0)U.Text(new Rect(cx+Mathf.Min(cw*.5f,200),row.y+23,cw-Mathf.Min(cw*.5f,200),20),place,12,C.Muted(),TextAnchor.MiddleLeft);
                    // What did it.
                    U.Text(new Rect(cx,row.y+52,cw,14),"CAUSE OF DEATH",10,ReviveBrass,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
                    bool known=!string.IsNullOrEmpty(d.Cause);U.Text(new Rect(cx,row.y+66,cw,40),known?d.Cause+(string.IsNullOrEmpty(d.Killer)?"":" ("+d.Killer+")"):"Not reported yet",14,known?C.Ink():C.Muted(),TextAnchor.UpperLeft,wrap:true);
                    // The button.
                    var go=new Rect(row.xMax-212,row.y+20,196,46);bool able=!offline&&!poor&&wired&&string.IsNullOrEmpty(b.Blocked);
                    if(U.Btn(go,"REVIVE · $"+cost,true,able,1,16))ReviveMachinePress(d);
                    string why=offline?"THEY HAVE TO BE CONNECTED":poor?"YOU'RE $"+(cost-wallet)+" SHORT":!wired?"NOT CONNECTED YET":null;if(why!=null)U.Text(new Rect(go.x-14,go.yMax+6,go.width+28,18),why,10,ReviveRed,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);
                }
            }
            // The foot: what closes it, what's wrong.
            var foot=new Rect(px+24,py+ph-44,pw-48,30);
            string notice=reviveNotice!=null&&Time.unscaledTime<reviveNoticeUntil?reviveNotice:preview?"DEV PREVIEW · NOT GAMEPLAY · NOBODY IS CHARGED":!wired?NotWired:"PICK A PILOT. THEY COME OUT OF THE FRONT. STAND CLEAR.";
            U.Text(new Rect(foot.x,foot.y,foot.width-130,foot.height),notice,11,reviveNotice!=null&&Time.unscaledTime<reviveNoticeUntil?ReviveRed:preview||!wired?C.Warn():C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            if(U.Btn(new Rect(foot.xMax-120,foot.y,120,foot.height),"Close",false,true,1,13))reviveScreenOpen=false;
        }
        // The machine's bored face, on the empty list: two dials and a flat grin.
        private static void ReviveMachineFace(Vector2 c,float clock)
        {
            foreach(int side in new[]{-1,1}){var e=new Rect(c.x+side*34-24,c.y-36,48,48);U.Round(e,new Color(.86f,.82f,.68f),24,new Color(.8f,.82f,.86f),2);float a=(side>0?.2f:-.1f)+.06f*Mathf.Sin(clock*1.3f+side);var m=GUI.matrix;GUIUtility.RotateAroundPivot(a*Mathf.Rad2Deg+200,new Vector2(e.center.x*U.S,e.center.y*U.S));U.Fill(new Rect(e.center.x-1.5f,e.center.y-18,3,18),new Color(.7f,.14f,.1f));GUI.matrix=m;}
            var grin=new Rect(c.x-50,c.y+22,100,18);U.Round(grin,new Color(.82f,.84f,.88f),4);for(int i=1;i<6;i++)U.Fill(new Rect(grin.x+i*100f/6-1,grin.y+2,2,14),new Color(.1f,.1f,.12f));
        }
    }
    public static partial class NativeReviveMachine
    {
        /// <summary>The rules' hooks (Astra): the replicated revives in progress, and a chance to complete a live board's rows (cause, body spot, price, why it's blocked) from the view each frame.</summary>
        public static NativeReviveSpit[] LiveSpits;
        public static Action<NativeReviveBoard,ExpeditionView> Fill;
    }
    public sealed partial class NativeAudio
    {
        private readonly AudioClip[] reviveClips=new AudioClip[NativeReviveMachineSounds.Count];private int reviveWarm;private float reviveLastT=-1;
        private AudioClip ReviveClip(int i)
        {
            if(reviveClips[i]!=null)return reviveClips[i];var samples=NativeReviveMachineSounds.Samples(i);var clip=AudioClip.Create("Revive machine "+i,samples.Length,1,NativeReviveMachineSounds.SampleRate,false);clip.SetData(samples,0);return reviveClips[i]=clip;
        }
        /// <summary>Each frame the machine stands: its cues as `t` (seconds from the money) passes them, panned and faded for where it stands from the listener. A frame's work: one synthesized clip warmed at a time, so the show's first sound never stalls it.</summary>
        public void ReviveMachineFrame(float t,Vector3 at,Transform listener)
        {
            if(voices==null)return;if(reviveWarm<NativeReviveMachineSounds.Count)ReviveClip(reviveWarm++);
            float from=reviveLastT;reviveLastT=t;if(t<0||from<0&&t>.3f)return;if(t<from)from=-1;if(Muted||Effects<=0||listener==null)return;
            var d=at-listener.position;float dist=d.magnitude,fade=Mathf.Clamp01(1.15f-dist/42f);if(fade<=0)return;float pan=dist>.1f?Mathf.Clamp(Vector3.Dot(d/dist,listener.right)*.7f,-.8f,.8f):0;
            foreach(var cue in NativeReviveMachineSounds.Timeline)
            {
                if(from>=cue.At||t<cue.At)continue;int free=-1;for(int v=0;v<voices.Length;v++)if(!voices[v].isPlaying){free=v;break;}if(free<0)return;
                var voice=voices[free];keyedVoices[free]=true;voice.clip=ReviveClip(cue.Sound);voice.loop=false;voice.volume=Mathf.Clamp01(cue.Gain*fade*Effects);voice.panStereo=pan;voice.pitch=1;voice.Play();
            }
        }
        private void ClearReviveClips(){foreach(var clip in reviveClips)if(clip!=null)CapturedAnimation.Release(clip);}
    }
}

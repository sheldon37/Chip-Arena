using System;
using System.Collections.Generic;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // Blind Pick with N choices, its screen (owner 2026-11-18: "a customizable amount of choices (Default 3) of weapon/class to
    // pick (we only pick 1). This will be similar to a chip screen."). My dealt weapons are dealt like the chip tray's cards,
    // one after another from a deck under the screen, each flipping face up as the weapon's hero card (NativeWeaponCard: its
    // stats, role and description), or its compact card when many are dealt. They take the weapon grid's highlight and
    // choice: the holo highlight, a click (or 1-9, the arrows) chooses (unconfirmed, so a timeout keeps it), Lock in or Enter
    // confirms; clicks go through U.SilentButton. Look.SelectAge restarts from 0 on each choice and LockAge on the lock, as the
    // weapon grid's, for the per-weapon selection animations (NativeWeaponCard.Flourishes after the cards).
    // Presentation only. It draws the hook's view (NativeBlindChoiceView: my offers, my selection, the locked-in count, the
    // time) and sends the opening's own opening-choice messages; the rules (the deal, the checks, the timeout, the privacy)
    // are Astra's (NativeBlindChoices.RulesReady; DEV_LOG 2026-11-18, "For Astra"). Until they land only the Editor's DEV
    // preview draws it (NativeBlindChoicePreview.cs).
    public sealed partial class ConversionGame
    {
        private NativeNetMessage blindViewFrom;private NativeBlindChoiceView blindView;private string blindSession;
        private readonly List<ContentWeapon> blindWeapons=new List<ContentWeapon>();private Rect[] blindCards=Array.Empty<Rect>();private int blindCols=1;private readonly NativeBlindChoices.Deal[] blindDeals=new NativeBlindChoices.Deal[NativeBlindChoices.Max];
        private static readonly string[] BlindKeys={"1","2","3","4","5","6","7","8","9"};

        // The hook: my N-choice pick from the opening's private view (built once a view; null for Blind Pick All, which sends no
        // offers, and for any view while the rules are not in, except the Editor preview's own).
        private NativeBlindChoiceView BlindChoiceView(NativeNetMessage v)
        {
            if(v==null)return null;if(ReferenceEquals(v,blindViewFrom))return blindView;blindViewFrom=v;
            blindView=NativeBlindChoiceView.From(v.pickSession,v.pick,v.pickCards,v.chip,v.id,v.team,v.remainingSeconds);
            if(blindView!=null&&!NativeBlindChoices.RulesReady&&!BlindPreviewing(v))blindView=null;
            if(blindView!=null)blindSession=v.pickSession;return blindView;
        }
        // The room's choice count (0 = Blind Pick All). Astra: back these with the rules' room option (e.g. NativeRoomOptions /
        // preferences `blindChoices`, normalised by NativeBlindChoices.Normalize) when the rules land; until then a room has no
        // N-choice pick, so they read 0 and write nothing.
        private static int BlindChoicesOf(NativeRoomOptions o)=>0;
        private static void SetBlindChoices(NativeRoomOptions o,int choices){}
        // The lobby's weapon line and a hidden pilot's weapon name while the room's weapons are picked at the start.
        private int RoomBlindChoices=>NativeBlindChoices.RulesReady?BlindChoicesOf(RulesGuest?GuestRules:RulesNow()):0;
        private string RoomBlindName=>RoomBlindChoices>=NativeBlindChoices.Min?"Blind Pick":"Blind Pick All";
        private string RoomBlindLine=>RoomBlindChoices>=NativeBlindChoices.Min?"Blind Pick · Pick one of your dealt weapons at match start":"Blind Pick All · Choose your weapon at match start";
        // The mode the opening's headers name: Blind Pick when this session dealt choices, else the room's rule (Blind Pick All),
        // else Deathmatch (its starting chip only).
        private string OpeningMode()
        {
            if(openingView!=null&&openingView.pickSession==blindSession)return "BLIND PICK";
            bool blind=server!=null?GuestRules?.blindPick==true:prefs.blindPick;return blind?"BLIND PICK ALL":"DEATHMATCH";
        }

        private void BlindChoiceStep(NativeNetMessage v,NativeBlindChoiceView view,float W,float H,float now)
        {
            float m=H<=760?22:28,age=now-openingStepAt;bool calm=prefs.reducedEffects,repaint=Event.current.type==EventType.Repaint,locked=openingLockAt>=0;
            blindWeapons.Clear();foreach(var id in view.Offers){var x=OpeningWeapon(id);if(x!=null&&blindWeapons.Count<NativeBlindChoices.Max)blindWeapons.Add(x);}
            int n=Math.Min(blindWeapons.Count,NativeBlindChoices.Max);if(blindCards.Length!=n)blindCards=new Rect[n];
            DraftHeader("BLIND PICK  ·  "+n+" CHOICES",NativeCyberTheme.Cyan(),"Pick your weapon","Choose one of the "+n+" weapons dealt to you. Only you see them until every pilot locks in.",W,H,out float top);
            OpeningMeta(v,W,H,true,now);if(n==0)return;
            // The deal's area: under the header and the meta, over the keycaps, Lock in and the key hints.
            float keysY=H-m-24,lockY=keysY-18-52,areaTop=Mathf.Max(top+8,(H<=760?16:22)+32+12+58+10),areaBottom=lockY-14-34,areaW=W-2*m;
            var lay=NativeBlindChoices.Fit(n,areaW,Mathf.Max(60,areaBottom-areaTop));blindCols=Math.Max(1,lay.Cols);
            float blockBottom=areaTop;for(int i=0;i<n;i++){NativeBlindChoices.Place(lay,i,n,m,areaTop,areaW,areaBottom-areaTop,out float cx,out float cy);blindCards[i]=new Rect(cx,cy,lay.Width,lay.Height);blockBottom=Mathf.Max(blockBottom,cy+lay.Height);}
            // Lock in sits under the deal (its number keys between), as the chip tray's footer does, never under the key hints.
            lockY=Mathf.Min(lockY,blockBottom+12+22+22);
            // My choice: the unconfirmed click (the weapon grid's highlightedWeapon), else the host's, when it was dealt to me.
            string chosen=view.Offered(highlightedWeapon)?highlightedWeapon:view.Selected;
            if(openingSelectSeen!=chosen){if(chosen!=null)openingSelectAt=now;openingSelectSeen=chosen;}
            int under=-1;var deals=blindDeals;
            for(int i=0;i<n;i++){var r=blindCards[i];deals[i]=NativeBlindChoices.At(age,i,W/2-r.center.x,H+r.height*.35f-r.center.y,calm);if(deals[i].Face&&deals[i].Flip>=.98f&&U.Hover(r))under=i;}
            int lit=locked?-1:under>=0?under:openingFocus>=0&&openingFocus<n?openingFocus:-1;
            if(repaint&&openingHoverFocus.Step(lit,lit>=0?blindWeapons[lit].id:null,!locked))nativeAudio?.UiCue("ui.draft.hover",null,.5f);
            // The backdrop's tint follows the highlighted card, else my choice (OpeningGui reads openingHero).
            openingHero=lit>=0?blindWeapons[lit].id:chosen;
            float first=0,second=0;int top1=-1;
            for(int i=0;i<n;i++)
            {
                if(repaint){float target=NativeHoloScan.Target(lit==i,false);if(NativeHoloScan.Engages(weaponHolo[i],target))weaponHoloAt[i]=now;weaponHolo[i]=NativeHoloScan.Step(weaponHolo[i],target,Time.unscaledDeltaTime);}
                float e=weaponHolo[i];if(e>first){second=first;first=e;top1=i;}else if(e>second)second=e;
            }
            for(int i=0;i<n;i++)
            {
                var x=blindWeapons[i];var r=blindCards[i];var d=deals[i];if(d.Alpha<=.002f)continue;var acc=NativeWeaponCard.Accent(x);
                bool sel=x.id==chosen;float lv=weaponHolo[i],recede=locked?(sel?0:.7f):NativeHoloScan.Recede(lv,i==top1?second:first)*.8f,lift=d.Face?NativeHoloScan.Lift(lv)*.6f-(sel&&!calm?4:0):0;
                U.Push(r.center,new Vector2(d.Dx,d.Dy+lift),d.Scale*d.Flip,d.Degrees,d.Scale);
                if(!d.Face){if(Mathf.Abs(d.Degrees)>.01f)DealtBack(r,d.Alpha);else NativeWeaponCard.Back(r,d.Alpha,now,NativeCyberTheme.Cyan(),calm);}
                else
                {
                    float faceAt=NativeBlindChoices.DealStart+i*NativeBlindChoices.DealStagger+NativeBlindChoices.DealFly+NativeBlindChoices.DealFlip*.5f;
                    var look=new NativeWeaponCard.Look{Alpha=d.Alpha,Build=1,Hover=lv,HoverAge=now-weaponHoloAt[i],Selected=sel?1:0,SelectAge=sel?now-openingSelectAt:-10,
                        Locked=sel&&locked?1:0,LockAge=sel&&locked?now-openingLockAt:-10,Recede=recede,Time=now,Index=i,Calm=calm};
                    if(lay.Hero){NativeWeaponCard.Hero(r,x,look,NativeWeaponCard.Measure(x,content.weapons),calm?10:age-faceAt);if(recede>0&&repaint)U.Round(r,U.Hex(0x05070d,.5f*recede*d.Alpha),12);}
                    else NativeWeaponCard.Draw(r,x,look);
                    if(d.Land>0&&repaint){U.Glow(r,U.A(acc,.45f*d.Land),24);U.Round(r,new Color(1,1,1,.16f*d.Land),12);}
                }
                U.Pop();
                if(!d.Face||d.Flip<.98f)continue;
                // Over the card: YOUR CHOICE on mine; under it, its number key.
                if(sel&&repaint){const string tag="YOUR CHOICE";float tw=U.Measure(tag,9.5f,true,true,.2f).x+18;var tr=new Rect(r.center.x-tw/2,r.y+lift-11,tw,22);NativeHudChrome.Tag(tr,acc);U.Text(tr,tag,9.5f,NativeCyberTheme.Ink(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.2f);}
                if(i<BlindKeys.Length&&repaint){float kw=NativeHudChrome.KeyWidth(BlindKeys[i],22);NativeHudChrome.Key(new Rect(r.center.x-kw/2,r.yMax+12,kw,22),BlindKeys[i],locked&&!sel?.35f:1,sel?U.Mix(acc,Color.white,.4f):default);}
                if(!locked&&U.SilentButton(r)){openingFocus=i;OpeningChoose(x.id,false);}
            }
            NativeWeaponCard.Flourishes();
            BlindChoiceKeys(n,chosen,locked);
            // Lock in, under the row; a hint before a choice; the wait once locked.
            float dealt=calm?1:M.Clamp01((age-NativeBlindChoices.DealEnd(n)+.2f)/.35f),bw=Mathf.Clamp(W*.24f,300,420);var btn=new Rect(W/2-bw/2,lockY,bw,52);var pick=OpeningWeapon(chosen);
            if(locked){string wait="LOCKED IN  ·  WAITING FOR THE OTHER PILOTS";U.Text(btn,wait,12,NativeCyberTheme.Good(dealt),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.24f);}
            else if(pick==null){string hint="CHOOSE ONE OF YOUR "+n+" WEAPONS";U.Text(btn,hint,12,NativeCyberTheme.Muted(dealt),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.24f);}
            else if(OpeningLockButton(btn,pick,dealt,now)||OpeningConfirmKey())OpeningLockIn(chosen,now);
            OpeningKeys(H,"1–"+Mathf.Clamp(n,1,9),"Choose","← →","Browse","ENTER","Lock in");
        }
        // The card in flight, face down and turning: plain layers only (no clip, which IMGUI would map through the turn).
        private static void DealtBack(Rect r,float alpha)
        {
            if(Event.current.type!=EventType.Repaint)return;var tone=NativeCyberTheme.Cyan();
            U.Glow(new Rect(r.x+8,r.yMax-6,r.width-16,8),new Color(0,0,0,.5f*alpha),14);U.RoundGradient(r,U.Hex(0x101633,.96f*alpha),U.Hex(0x060a16,.98f*alpha),12);
            float size=Mathf.Min(r.width,r.height)*.42f;U.Text(new Rect(r.center.x-size/2,r.center.y-size/2,size,size),"?",size*.62f,U.A(U.Mix(tone,Color.white,.4f),alpha),TextAnchor.MiddleCenter,bold:true);
            U.Round(r,Color.clear,12,U.A(U.Mix(NativeCyberTheme.Edge(),tone,.5f),alpha),1.5f);
        }
        // The arrows move through the cards (rows by the layout's columns) and choose; 1-9 choose that card. Enter locks (above).
        private void BlindChoiceKeys(int n,string chosen,bool locked)
        {
            var e=Event.current;if(n==0||locked||e.type!=EventType.KeyDown)return;int at=openingFocus>=0&&openingFocus<n?openingFocus:Math.Max(0,blindWeapons.FindIndex(x=>x.id==chosen)),next;
            if(e.keyCode==KeyCode.RightArrow)next=(at+1)%n;else if(e.keyCode==KeyCode.LeftArrow)next=(at+n-1)%n;
            else if(e.keyCode==KeyCode.DownArrow)next=Math.Min(n-1,at+blindCols);else if(e.keyCode==KeyCode.UpArrow)next=Math.Max(0,at-blindCols);
            else if(e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha9&&e.keyCode-KeyCode.Alpha1<n)next=e.keyCode-KeyCode.Alpha1;else return;
            e.Use();openingFocus=next;if(blindWeapons[next].id!=highlightedWeapon)OpeningChoose(blindWeapons[next].id,false);
        }
    }
}

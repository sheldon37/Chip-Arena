using System;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // pve/siren-reveal.ts: for every player, the Siren's grin (1.4 s), then the fixed four-card track with the current chip
    // booting and "???" for the chips not reached yet (3.2 s), then a .4 s fade. Timed by the hold's own clock.
    public sealed partial class ConversionGame
    {
        private bool SirenRevealActive=>arena?.Pve!=null&&arena.Pve.Holding&&arena.Pve.Cinematic=="reveal"&&!menu;
        // The grin: two slit eyes and a wide toothed smile that opens across the dark.
        private static void SirenGrin(float W,float H,float a,float open)
        {
            float cx=W/2,cy=H*.46f,gw=Mathf.Min(W*.46f,720)*open,gh=Mathf.Max(4,26*open);var violet=U.Hex(0xb7a8ff);
            foreach(int side in new[]{-1,1}){var eye=new Rect(cx+side*W*.11f-60,cy-H*.17f,120,10*open+2);U.Glow(eye,U.A(violet,.55f*a),24);U.Round(eye,U.Hex(0xf6f3ff,a),6);}
            var mouth=new Rect(cx-gw/2,cy,gw,gh);U.Glow(mouth,U.A(violet,.5f*a),40);U.Round(mouth,U.Hex(0xf6f3ff,a),Mathf.Min(16,gh/2));
            int teeth=17;for(int i=1;i<teeth;i++){float x=mouth.x+mouth.width*i/teeth;U.Fill(new Rect(x-1.5f,mouth.y,3,mouth.height),U.Hex(0x050409,a));}
        }
        private void SirenReveal()
        {
            var v=arena.Pve;float W=U.W,H=U.H,age=v.HoldDuration-v.HoldLeft,smile=1.4f,end=v.HoldDuration;int chip=Mathf.Clamp(v.Reveal,1,4);var track=NativeChipCard.Text.sirenTrack;if(track==null||track.Length==0)return;
            if(age<smile)
            {   // siren-selection pve-smile: black, the grin opening and holding.
                U.Fill(new Rect(0,0,W,H),U.Hex(0x020103));float ks=age/smile,a=ks<.15f?ks/.15f:ks>.85f?(1-ks)/.15f:1;SirenGrin(W,H,a,M.Bezier(.16f,.84f,.24f,1,M.Clamp01(ks/.4f)));return;
            }
            float since=age-smile,fade=1-M.Clamp01((age-(end-.4f))/.4f);
            U.Fill(new Rect(0,0,W,H),U.Hex(0x020103,.98f*fade));U.RadialStops(new Rect(-W*.207f,H*.3f,W*1.414f,H*1.4f),U.Hex(0x2b1652,.95f*fade),new float[]{0,.58f,1},new[]{1f,0,0});SirenGrin(W,H*.62f,.35f*.55f*fade,1);
            var current=track[chip-1];bool found=chip==3;
            // Header, then the spotlight: pve-spot-in .7 s (a flattened bar that unfolds and jolts), the name with its glitch split.
            string eyebrow=(found?"FOUND HIM · ":"")+"THE SIREN CHOOSES · CHIP "+chip+" / "+track.Length;float y=H*.16f;
            U.Text(new Rect(0,y,W,16),eyebrow,12,U.Hex(0xc9bfff,fade),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.32f,glow:U.Hex(0x7d5cff,.6f*fade));y+=34;
            float k=M.Clamp01(since/.7f),u=M.Bezier(.16f,.8f,.22f,1,k),sy=k<.12f?.04f:k<.3f?Mathf.Lerp(.04f,1.05f,(k-.12f)/.18f):k<.56f?1:1,tx=k>=.3f&&k<.4f?-8:k>=.4f&&k<.48f?6:0,op=M.Clamp01(k/.12f)*fade;
            float F=Mathf.Clamp(W*.056f,34,84);U.Push(new Vector2(W/2,y+F/2),new Vector2(tx,0),1,0,sy);
            float g=Mathf.Repeat(since+.2f,1.8f)/1.8f,h=Mathf.Repeat(since+.3f,1.8f)/1.8f;var title=new Rect(0,y,W,F);U.SplitCopy(title,title,g,false,current.name,F,op,TextAnchor.MiddleCenter);U.SplitCopy(title,title,h,true,current.name,F,op,TextAnchor.MiddleCenter);
            U.Text(new Rect(0,y,W,F),current.name,F,U.Hex(0xf6f3ff,op),TextAnchor.MiddleCenter,bold:true,tracking:.01f,glow:U.Hex(0x9b87ff,.8f*op),shadow:new Color(0,0,0,op));
            float ds=Mathf.Clamp(W*.015f,15,20);var lines=U.Wrap(current.description,ds,Mathf.Min(W*.6f,62*ds*.5f));float ly=y+F+12;foreach(var line in lines){U.Text(new Rect(0,ly,W,ds*1.45f),line,ds,U.Hex(0xddd6fb,op),TextAnchor.MiddleCenter,shadow:new Color(0,0,0,op));ly+=ds*1.45f;}
            U.Pop();
            // The track: revealed chips (the current one booting with the source's power-on reveal), the rest locked.
            float cw=Mathf.Min(228,(W-48-3*16)/4),tw=cw*4+48,tx0=(W-tw)/2,ty=Mathf.Max(ly+28,H*.5f);var size=ChipSize.Tray(false,4);size.Name=19;float ch=Mathf.Max(236,track.Max(c=>NativeChipCard.Height(c,cw,size)));
            for(int i=0;i<track.Length;i++)
            {
                var r=new Rect(tx0+i*(cw+16),ty,cw,ch);
                if(i<chip)
                {
                    bool boot=i==chip-1;var reveal=boot?NativeChipCard.Entrance("siren",since):new NativeChipCard.Reveal{Alpha=1,Scale=1,ScaleX=1,ScaleY=1,Bright=1};var look=ChipLook.Idle(Time.unscaledTime);look.Alpha=reveal.Alpha*fade*(boot?1:.8f);look.Bright=reveal.Bright;
                    U.Push(r.center,new Vector2(reveal.Dx,reveal.Dy),reveal.Scale*reveal.ScaleX,0,reveal.Scale*reveal.ScaleY);if(boot){U.Glow(r,U.Hex(0x7d5cff,.6f*look.Alpha),60);U.Round(new Rect(r.x-1,r.y-1,r.width+2,r.height+2),Color.clear,10,U.Hex(0xb7a8ff,.6f*look.Alpha),1);}
                    NativeChipCard.Draw(r,track[i],0,size,look);U.Pop();continue;
                }
                float pulse=.7f+.3f*(.5f-.5f*Mathf.Cos(Time.unscaledTime*2*Mathf.PI/2.2f));U.Round(r,U.Hex(0x0d0a14,fade),9,U.Hex(0xb7a8ff,.33f*fade));
                U.Text(new Rect(r.x,r.center.y-40,r.width,54),"???",54,U.Mix(U.Hex(0x4e4470),U.Hex(0x6d5fb0),pulse-.7f)*new Color(1,1,1,fade*pulse),TextAnchor.MiddleCenter,bold:true,tracking:.08f,glow:U.Hex(0x7d5cff,.35f*fade));
                U.Text(new Rect(r.x,r.center.y+22,r.width,14),(i+1)+" / "+track.Length,11,U.Hex(0x6f6790,fade),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.2f);
            }
        }
    }
}

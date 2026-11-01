using System.Collections.Generic;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // C17 (client/pve/beacons.ts, downed.ts, hologram.ts): the beacon label and the HELP bubble are camera-facing sprites
    // in the source; here they are drawn with the kit at the projected point, sized by the view depth exactly as a sprite
    // is. The label is depth tested in the source (hidden behind walls, a raycast here); the HELP bubble is drawn over the
    // scene. Both sit under the HUD panels, as world objects do.
    public sealed partial class ConversionGame
    {
        private Texture2D helpBubble;private readonly Dictionary<int,string> beaconSubs=new Dictionary<int,string>();
        private static readonly Vector2[] HelpOutline={new Vector2(-1,0),new Vector2(1,0),new Vector2(0,-1),new Vector2(0,1),new Vector2(-.7f,-.7f),new Vector2(.7f,.7f),new Vector2(-.7f,.7f),new Vector2(.7f,-.7f)};
        private void PveBeaconHud()
        {
            if(menu||arena?.Pve==null||cameraView==null||IntroActive||ReplayActive||Event.current.type!=EventType.Repaint)return;
            float t=arena.Time,tan=Mathf.Tan(cameraView.fieldOfView*.5f*Mathf.Deg2Rad),half=cameraView.pixelHeight*.5f;Vector3 eye=cameraView.transform.position;var physics=arena.Root.scene.GetPhysicsScene();
            // Labels first; the HELP bubbles draw above them (renderOrder 6, beacons.ts:73).
            for(int pass=0;pass<2;pass++)foreach(var p in arena.Pilots)
            {
                if(!NativeBeaconView.Target(arena,p,out var row))continue;Vector3 at=NativeBeaconView.Position(p);
                if(pass==0)
                {
                    // beacons.ts:92 + hologram.ts:34-37: "F · REVIVE" over "$150", or "SELF-REVIVE" over "0:04"; 1.5 x .45 m.
                    Vector3 centre=new Vector3(at.x,at.y+NativeBeaconRules.LabelY(t),at.z),s=cameraView.WorldToScreenPoint(centre);if(s.z<=.05f)continue;
                    float k=half/(s.z*tan)/U.S,text=NativeBeaconRules.LabelText*k;if(text<3)continue;
                    Vector3 delta=centre-eye;if(physics.Raycast(eye,delta.normalized,out _,delta.magnitude-.08f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))continue;
                    float w=NativeBeaconRules.LabelWidth*k,h=NativeBeaconRules.LabelHeight*k,x=s.x/U.S-w*.5f,y=(Screen.height-s.y)/U.S-h*.5f;if(x>U.W||x+w<0||y>U.H||y+h<0)continue;
                    bool self=NativeBeaconView.Self(row);
                    // The kit's glow (eight halo copies) stacks at the glyph edge where the canvas blur (.12 h) spreads thin, so it runs at half the source alpha.
                    Color glow=U.Hex(self?NativeBeaconRules.SelfGlow:NativeBeaconRules.DownGlow,NativeBeaconRules.GlowAlpha*.5f);
                    string sub=self?BeaconSub(-1-NativeBeaconRules.ClockSeconds(row.SelfAt-t)):BeaconSub(NativePveRules.RevivePrice(row.Revived));float subSize=NativeBeaconRules.LabelSub*k;
                    U.Text(new Rect(x-w,y+h*.36f-text,w*3,text*2),self?NativeBeaconRules.SelfLabel:NativeBeaconRules.DownLabel,text,U.Hex(self?NativeBeaconRules.SelfText:NativeBeaconRules.DownText),TextAnchor.MiddleCenter,bold:true,glow:glow);
                    U.Text(new Rect(x-w,y+h*.76f-subSize,w*3,subSize*2),sub,subSize,U.Hex(self?NativeBeaconRules.SelfSub:NativeBeaconRules.DownSub),TextAnchor.MiddleCenter,bold:true,glow:glow);
                    continue;
                }
                // downed.ts:54-60 + beacons.ts:95-96: everyone but the downed player sees HELP!, constant on screen between
                // 5 and 26 m, its bottom above the label, bobbing, tilting about its bottom centre.
                if(p.Seat.id==arena.LocalId)continue;
                float width=NativeBeaconRules.HelpWidth(Vector3.Distance(eye,new Vector3(at.x,at.y+2.6f,at.z))),bob=NativeBeaconRules.HelpBob(t,NativeGoreRules.FnvDigits(p.Seat.id));
                Vector3 anchor=cameraView.WorldToScreenPoint(new Vector3(at.x,at.y+NativeBeaconRules.HelpY(width,bob),at.z));if(anchor.z<=.05f)continue;
                float scale=half/(anchor.z*tan)/U.S,bw=width*scale,bh=bw*NativeBeaconRules.HelpAspect;var pivot=new Vector2(anchor.x/U.S,(Screen.height-anchor.y)/U.S);
                if(pivot.x+bw<0||pivot.x-bw>U.W||pivot.y<0||pivot.y-bh*2>U.H||!EnsureHelpBubble())continue;
                U.Push(pivot,Vector2.zero,1,-NativeBeaconRules.HelpTilt(t,NativeGoreRules.DigitCount(p.Seat.id))*Mathf.Rad2Deg);
                var r=new Rect(pivot.x-bw*.5f,pivot.y-bh,bw,bh);U.Image(r,helpBubble,Color.white);
                // beacons.ts:57-58 "HELP!" 62 px of 256, centred at (128, 53), #e8192e with a 4 px white stroke.
                float size=bw*62/256f,cy=r.y+bh*53/128f,o=Mathf.Max(.5f,bw*2/256f);var line=new Rect(r.x-bw*.25f,cy-size,bw*1.5f,size*2);
                foreach(var d in HelpOutline)U.Text(new Rect(line.x+d.x*o,line.y+d.y*o,line.width,line.height),NativeBeaconRules.HelpText,size,Color.white,TextAnchor.MiddleCenter,bold:true);
                U.Text(line,NativeBeaconRules.HelpText,size,U.Hex(0xe8192e),TextAnchor.MiddleCenter,bold:true);U.Pop();
            }
        }
        // "$price" for key >= 0, the self-revive clock for key = -1 - seconds; cached so the HUD allocates once per value.
        private string BeaconSub(int key)
        {
            if(beaconSubs.TryGetValue(key,out string text))return text;if(beaconSubs.Count>512)beaconSubs.Clear();
            text=key>=0?"$"+key.ToString(System.Globalization.CultureInfo.InvariantCulture):NativeBeaconRules.Clock(-1-key);beaconSubs[key]=text;return text;
        }
        private bool EnsureHelpBubble()
        {
            if(helpBubble!=null)return true;
            helpBubble=new Texture2D(NativeBeaconRules.HelpWidthPx,NativeBeaconRules.HelpHeightPx,TextureFormat.RGBA32,true,false){name="HELP bubble",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new byte[NativeBeaconRules.HelpWidthPx*NativeBeaconRules.HelpHeightPx*4];NativeBeaconRules.HelpBubble(pixels,true);helpBubble.SetPixelData(pixels,0);helpBubble.Apply(true,true);return true;
        }
    }
}

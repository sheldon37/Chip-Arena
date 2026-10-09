using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // C14 in the menus: the live 3D pilot portraits (NativePortraits.cs) on the pilot card (Play, Boss Test, Create, Join and
    // Customize's LIVE PREVIEW), the room lobby's pilot cards (host, guests and bots) and the PvE results cards, as the
    // source draws a portrait-stage canvas in each (lobby-ui.ts pilotCard, main.ts:163 pilot-showcase, pve/results.ts), and
    // Customize's IDLE ANIMATION picker (main.ts:131 lobbyPoseOptions). The card animations stay as the portrait's frame: the
    // card entrance, the accent glow and the turning tick ring with its orbiting dot sit behind the pilot, and the pilot
    // emblem (PilotEmblem) stands in until the portrait is ready, where it cannot render, or where the stage is too small.
    // Drawing only: the pose is prefs.lobbyPose (saved and sent in the profile's cosmetics by Save Pilot, as before).
    public sealed partial class ConversionGame
    {
        private NativePortraitRenderer portraitRenderer;
        private static readonly float[] DotAt={0f,.96f,1f},DotAlpha={1f,1f,0f};
        // The renderer, added beside the game by the first card that asks for a portrait. Only stages on screen ask (the
        // source skips a canvas outside the window); `top` is the hat top the renderer framed the pilot with.
        private Texture Portrait(int key,in PortraitLook look,Rect stage,out float top)
        {
            top=0;if(Event.current.type!=EventType.Repaint||presentation==null||!OnScreen(stage))return null;
            if(portraitRenderer==null){portraitRenderer=gameObject.AddComponent<NativePortraitRenderer>();portraitRenderer.Initialize(presentation,cameraView);}
            portraitRenderer.Reduced=prefs.reducedEffects;return portraitRenderer.Request(key,look,stage.width,stage.height,U.S,out top);
        }
        // Whether any of a kit rect (virtual units, drawn at U.S physical pixels in the current clip) lands inside the window.
        private static bool OnScreen(Rect r)
        {
            var m=GUI.matrix;GUI.matrix=Matrix4x4.identity;
            Vector2 a=GUIUtility.GUIToScreenPoint(new Vector2(r.x*U.S,r.y*U.S)),b=GUIUtility.GUIToScreenPoint(new Vector2(r.xMax*U.S,r.yMax*U.S));GUI.matrix=m;
            return !(b.x<0||b.y<0||a.x>Screen.width||a.y>Screen.height);
        }
        private static int HexOf(Color c)=>Mathf.Clamp(Mathf.RoundToInt(c.r*255),0,255)<<16|Mathf.Clamp(Mathf.RoundToInt(c.g*255),0,255)<<8|Mathf.Clamp(Mathf.RoundToInt(c.b*255),0,255);

        // The pilot card's stage (pilot-showcase .portrait-stage) between the card's top row and its text. Returns where the
        // name starts. The portrait comes first: the card keeps its three presentation rows (headwear, kill effect, intro)
        // only while the stage stays at least PilotStageRows tall, else the stage takes their room too (PilotCard's row loop
        // then stops by itself: no row fits above r.yMax-46). A card too short even then keeps the emblem layout.
        private const float PilotStageRows=210,PilotStageMin=120;
        private float PilotStage(Rect r,Color accent,Color body,ContentWeapon weapon,float e,float t,string note)
        {
            // PilotCard below the stage: a 6 unit gap, the name, weapon, underline, class and colour rows (`text`), then the
            // 22 unit presentation rows, each drawn while it ends above r.yMax-46; the note's lines end 18 units above the foot.
            float ns=Mathf.Clamp(r.width*.075f,22,30),text=ns*1.3f+4+20+12+30+44,top=r.y+44;int lines=note!=null?U.Wrap(note,12,r.width-44,false,3).Count:0;
            float noteTop=lines>0?r.yMax-18-lines*17-6:r.yMax-46;
            // Where the colour rows may end: above all three presentation rows (62 units) and the note, or above the note alone.
            float withRows=Mathf.Min(r.yMax-46-62,noteTop-62),alone=Mathf.Min(r.yMax-46,noteTop);
            float h=withRows-text-6-top;if(h<PilotStageRows)h=alone-text-6-top;
            var stage=new Rect(r.x+1,top,r.width-2,h);
            if(stage.height<PilotStageMin){var c=new Vector2(r.center.x,r.y+Mathf.Min(r.height*.36f,180));float ring=Mathf.Min(86,r.width*.26f);PilotEmblem(c,ring,accent,body,weapon,e,t);return c.y+ring+26;}
            var look=new PortraitLook{Spec=weapon.id,Pose=prefs.lobbyPose,Hat=prefs.hat,Body=HexOf(body),Accent=HexOf(accent)};
            float emblemRing=Mathf.Min(86,r.width*.26f);var centre=new Vector2(stage.center.x,stage.y+Mathf.Min(stage.height*.42f,emblemRing+60));
            PortraitStage(stage,1,look,accent,body,NativeIcons.Spec(weapon.id),U.Hex((int)weapon.color),e,t,0,centre,emblemRing,28);
            return stage.yMax+6;
        }
        // LobbyCard's .portrait-stage (lobby and PvE results cards): the seat's portrait, else the emblem the card had.
        private void LobbyPortrait(Rect stage,Vector2 centre,float ring,in LobbyPilot p,int index,float a,float t,ContentWeapon weapon,Color style,bool result)
        {
            string icon=weapon!=null?NativeIcons.Spec(weapon.id):"shuffle";Color tone=weapon!=null?U.Hex((int)weapon.color):style;int ticks=ring<56?20:28;
            if(weapon==null||!CardLook(p,index,result,out int key,out var look)){PilotEmblem(centre,ring,p.accent,p.body,icon,tone,a,t,index*1.7f,ticks);return;}
            PortraitStage(stage,key,look,p.accent,p.body,icon,tone,a,t,index*1.7f,centre,ring,ticks);
        }
        // The portrait over its frame: the emblem's accent glow, turning tick ring and orbiting dot around the pilot's upper
        // body (the ring follows the framing), then the pilot; the sleeping pilot's z Z Z over it. The emblem until ready.
        private void PortraitStage(Rect stage,int key,in PortraitLook look,Color accent,Color body,string icon,Color tone,float e,float t,float phase,Vector2 emblemCentre,float emblemRing,int ticks)
        {
            var texture=Portrait(key,look,stage,out float top);if(texture==null){PilotEmblem(emblemCentre,emblemRing,accent,body,icon,tone,e,t,phase,ticks);return;}
            if(Event.current.type!=EventType.Repaint)return;
            int pose=NativeLobbyPoses.Effective(look.Spec,look.Pose);bool sleep=pose==NativeLobbyPoses.Sleep;var frame=NativePortraitRules.Frame(pose,stage.width/stage.height,top);
            // The ring's centre and radius in the stage: a world height and radius through the orthographic framing.
            var view=frame.Target-frame.Position;float cos=Mathf.Sqrt(view.X*view.X+view.Z*view.Z)/view.Length(),units=stage.height/(2*frame.Half);
            float anchor=sleep?.32f:pose==NativeLobbyPoses.Chair?.98f:1.12f,radius=sleep?.86f:.8f;
            var c=new Vector2(stage.center.x,stage.center.y-(anchor-frame.Target.Y)*cos*units);float ring=Mathf.Clamp(radius*units,24,stage.width*.44f);
            PortraitRing(c,ring,accent,e,t+phase,ticks);
            U.Image(stage,texture,new Color(1,1,1,e));
            if(sleep)SleepMarks(stage,e);
        }
        // The emblem's frame without its disc and icon (PilotEmblem's glow, turning ticks and orbiting dot). Each tick is drawn
        // unrotated, as a dash image at the nearest 22.5 degrees placed on the turning ring: a rotated draw inside the lobby
        // roster's scroll clip has its clip rotated with it (Unity's draw-time clip), so ticks of a card scrolled out of the
        // column would show outside it.
        private static readonly Texture2D[] tickTextures=new Texture2D[16];private static float tickScale;
        // A 3-unit-wide dash (5 or 9 units long, square ends) at 22.5-degree steps in a 12-unit square, rasterised at the
        // kit's pixel scale (made again when the scale changes).
        private static Texture2D Tick(int k)
        {
            if(tickScale!=U.S){tickScale=U.S;for(int i=0;i<tickTextures.Length;i++)if(tickTextures[i]!=null){Object.Destroy(tickTextures[i]);tickTextures[i]=null;}}
            if(tickTextures[k]!=null)return tickTextures[k];
            int n=Mathf.Clamp(Mathf.RoundToInt(12*U.S),4,64);float len=k<8?5:9,a=(k%8)*22.5f*Mathf.Deg2Rad,sin=Mathf.Sin(a),cos=Mathf.Cos(a);var px=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                // (vx, vy) in GUI units from the centre (y down; texture row 0 is the bottom); the dash runs along (sin, -cos).
                float vx=(x+.5f)/n*12-6,vy=6-(y+.5f)/n*12,along=Mathf.Abs(vx*sin-vy*cos),across=Mathf.Abs(vx*cos+vy*sin);
                float d=Mathf.Max(along-len/2,across-1.5f);px[y*n+x]=new Color(1,1,1,Mathf.Clamp01(.5f-d*U.S));
            }
            var t=new Texture2D(n,n,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};t.SetPixels(px);t.Apply(false,true);
            return tickTextures[k]=t;
        }
        private static void PortraitRing(Vector2 c,float ring,Color accent,float e,float t,int ticks)
        {
            U.Radial(new Rect(c.x-ring*1.6f,c.y-ring*1.6f,ring*3.2f,ring*3.2f),U.A(accent,.16f*e));
            for(int i=0;i<ticks;i++)
            {
                bool major=i%7==0;float deg=i/(float)ticks*360+t*12,a=deg*Mathf.Deg2Rad,h=major?9:5,rc=ring+3-h/2;int k=(major?8:0)+Mathf.RoundToInt(Mathf.Repeat(deg,180)/22.5f)%8;
                var p=new Vector2(c.x+Mathf.Sin(a)*rc,c.y-Mathf.Cos(a)*rc);U.Image(new Rect(p.x-6,p.y-6,12,12),Tick(k),U.A(accent,(major?.9f:.45f)*e));
            }
            float orbit=t*.9f;U.RadialStops(new Rect(c.x+Mathf.Cos(orbit)*ring*.92f-3,c.y+Mathf.Sin(orbit)*ring*.92f-3,6,6),U.A(accent,e),DotAt,DotAlpha);
        }
        // lobby.css .sleep-marks: z Z Z (15 px mono #ddcaec) from 32% / 30% of the stage, rising and fading on a 3.3 s loop.
        private void SleepMarks(Rect stage,float e)
        {
            var origin=new Vector2(stage.x+stage.width*.32f,stage.y+stage.height*.30f);
            for(int i=0;i<3;i++)
            {
                NativePortraitRules.SleepMark(i,Time.unscaledTime,prefs.reducedEffects,out var offset,out float scale,out float opacity);if(opacity<=0)continue;
                // Scaled about the glyph's centre by the font size (no matrix: a scaled draw inside a scroll clip scales the clip too).
                var p=origin+new Vector2(offset.X+4.5f,offset.Y+9);float size=15*scale;U.Text(new Rect(p.x-size,p.y-size,size*2,size*2),i==0?"z":"Z",size,U.Hex(0xddcaec,opacity*e),TextAnchor.MiddleCenter,mono:true);
            }
        }
        // A lobby or results card's look and portrait key. The viewer's own card is its saved look (prefs); others come from
        // the seat: the lobby's broadcast seats (LobbySeats: weapon, colours, hat) or the PvE party rows (NativeSeat). Seats
        // carry no lobby pose yet, so other pilots stand with their weapon (the source's default and every bot's pose).
        private bool CardLook(in LobbyPilot p,int index,bool result,out int key,out PortraitLook look)
        {
            look=new PortraitLook{Spec=p.weapon,Pose="weapon",Hat="none",Body=HexOf(p.body),Accent=HexOf(p.accent)};key=0;
            if(p.tag=="YOU")
            {
                look.Pose=prefs.lobbyPose;look.Hat=prefs.hat;
                if(!result){key=10000+(Hosting?1:networkId);return true;}
                var mine=arena?.Local?.Seat;key=20000+(mine!=null?mine.id:0);return true;
            }
            if(result)
            {
                var seat=ResultSeat(p,index);if(seat==null){key=29000+index;return true;}
                look.Hat=seat.hat;key=20000+seat.id;return true;
            }
            var frame=LobbySeatFor(p,index);if(frame==null){key=19000+index;return true;}
            look.Hat=frame.hat;key=10000+frame.id;return true;
        }
        // LobbyRoster's order: the broadcast seats, after the host's own card when no broadcast lists it yet.
        private NativeSeatFrame LobbySeatFor(in LobbyPilot p,int index)
        {
            bool own=false;foreach(var s in lobbySeats)own|=s!=null&&s.id==1;int n=index-(Hosting&&!dedicated&&!own?1:0),k=0;
            foreach(var s in lobbySeats){if(s==null)continue;if(k++==n){if(s.spec==p.weapon&&s.bot==p.bot&&s.name==p.name)return s;break;}}
            foreach(var s in lobbySeats)if(s!=null&&s.spec==p.weapon&&s.bot==p.bot&&s.name==p.name)return s;
            return null;
        }
        // PveResultsPanel's order: the party rows (bots only while connected) by seat id.
        private NativeSeat ResultSeat(in LobbyPilot p,int index)
        {
            var v=arena?.Pve;if(v==null)return null;
            foreach(var r in v.Rows.Values)
            {
                var s=r.Pilot?.Seat;if(s==null||s.bot&&!s.connected)continue;int rank=0;
                foreach(var o in v.Rows.Values){var os=o.Pilot?.Seat;if(os!=null&&!(os.bot&&!os.connected)&&os.id<s.id)rank++;}
                if(rank==index&&s.spec==p.weapon&&s.name==p.name)return s;
            }
            foreach(var r in v.Rows.Values){var s=r.Pilot?.Seat;if(s!=null&&s.spec==p.weapon&&s.name==p.name)return s;}
            return null;
        }

        // main.ts:131 lobbyPoseOptions under IDLE ANIMATION (lobby.css .lobby-pose-options): the four lobby poses in a
        // two-column grid (10 px gaps) of 64 px options, the 24 px icon and the 14 px label; #202029 on a #4c4653 edge,
        // #30303a / #8e7d95 on hover, the chosen one #3d2c28 on #efb587 with its icon in #efb587. A pick sets prefs.lobbyPose,
        // which Save Pilot stores and sends with the rest of the look.
        private void LobbyPosePicker()
        {
            Field("Idle animation");GUILayout.Space(4);var grid=GUILayoutUtility.GetRect(0,64*2+10,GUILayout.ExpandWidth(true));GUILayout.Space(14);
            int current=NativeLobbyPoses.Index(prefs.lobbyPose);
            U.Defer(()=>
            {
                float w=(grid.width-10)/2;bool paint=Event.current.type==EventType.Repaint;
                for(int i=0;i<NativeLobbyPoses.Keys.Length;i++)
                {
                    var r=new Rect(grid.x+(i%2)*(w+10),grid.y+(i/2)*74,w,64);bool on=i==current;float h=on?0:U.HoverAmount(7720+i,r);
                    if(paint)
                    {
                        U.Round(r,on?U.Hex(0x3d2c28):U.Mix(U.Hex(0x202029),U.Hex(0x30303a),h),9,on?U.Hex(0xefb587):U.Mix(U.Hex(0x4c4653),U.Hex(0x8e7d95),h));
                        if(on)U.Round(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),Color.clear,8,U.Hex(0xefb587,.19f));
                        NativeIcons.Draw(new Rect(r.x+12,r.center.y-12,24,24),NativeLobbyPoses.Icons[i],on?U.Hex(0xefb587):U.Hex(0xbbb0c8));
                        U.Text(new Rect(r.x+48,r.y,r.width-58,r.height),NativeLobbyPoses.Labels[i],14,on?U.Hex(0xfff1e5):U.Hex(0xd6d0dd),TextAnchor.MiddleLeft,wrap:true);
                    }
                    if(U.Button(r)&&!on){prefs.lobbyPose=NativeLobbyPoses.Keys[i];Click();}
                }
            });
        }
    }
}

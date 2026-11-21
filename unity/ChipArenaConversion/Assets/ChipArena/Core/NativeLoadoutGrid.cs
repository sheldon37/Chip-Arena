using System;

namespace ChipArena.UnityConversion
{
    // The loadout pickers as compact cyber tiles (owner 2026-10-12: "a animated cyber effect over the choices and just the icon +
    // name ... under it", cyan instead of orange): the class row, the weapon grid and its info strip, the tiles' inner layout and
    // their motion timeline (entrance stagger, holo scan wave, hex shimmer, hover pass and chromatic flicker, selection brackets,
    // lock flash and data pulse, the DEV class's glitch). Engine-free so the managed checks cover every slot width, UI scale and
    // resolution. Lengths are virtual px (NativeUiMotion.Measure: 900 tall at 16:9), times seconds. Calm is Reduced effects:
    // nothing moves or flickers; every effect rests where it ends.
    public static class NativeLoadoutGrid
    {
        public enum Row{Panel,Lobby,Locker}
        public struct Grid{public int Count,Cols,Rows;public float Width,TileW,TileH,Gap,Height;}
        public struct Cell{public float X,Y,W,H;}

        // ---- Weapons: square tiles, at most TileMax wide, as many columns as the slot holds at that size (so every class shows
        // the same tile size at a given width; rows are left-aligned). Never under TileMin while two columns are possible.
        public const float TileMax=124,TileMin=72,Gap=10,NarrowGap=8;
        public static Grid Weapons(float width,int count,bool narrow)
        {
            float gap=narrow?NarrowGap:Gap;width=Math.Max(1,width);count=Math.Max(0,count);
            int cols=Math.Max(1,(int)Math.Ceiling((width+gap)/(TileMax+gap)-1e-4));float tile=(width-(cols-1)*gap)/cols;
            while(cols>1&&tile<TileMin){cols--;tile=(width-(cols-1)*gap)/cols;}
            tile=Math.Min(tile,TileMax);int rows=(count+cols-1)/cols;
            return new Grid{Count=count,Cols=cols,Rows=rows,Width=width,TileW=tile,TileH=tile,Gap=gap,Height=rows<=0?0:rows*tile+(rows-1)*gap};
        }
        // The info strip under the weapons: the hovered weapon's role and key stats (else the equipped one's, dimmed).
        public const float StripGap=10,StripHeight=44;
        public static float WeaponsHeight(float width,int count,bool narrow)=>Weapons(width,count,narrow).Height+StripGap+StripHeight;

        // ---- Classes: one row of equal tiles (two columns when a tile would be narrower than ClassMin), ClassHeight tall.
        public const float ClassGap=8,ClassMin=64;
        public static float ClassHeight(Row row)=>row==Row.Lobby?62:row==Row.Locker?66:72;
        public static Grid Classes(float width,int count,Row row,bool narrow)
        {
            float gap=narrow?6:ClassGap,h=ClassHeight(row);width=Math.Max(1,width);count=Math.Max(0,count);int cols=Math.Max(1,count);float w=(width-(cols-1)*gap)/cols;
            while(cols>1&&w<ClassMin){cols=Math.Max(1,cols/2);w=(width-(cols-1)*gap)/cols;}
            int rows=(count+cols-1)/cols;return new Grid{Count=count,Cols=cols,Rows=rows,Width=width,TileW=w,TileH=h,Gap=gap,Height=rows<=0?0:rows*h+(rows-1)*gap};
        }
        public static Cell At(Grid g,int i){int c=g.Cols<=0?0:i%g.Cols,r=g.Cols<=0?0:i/g.Cols;return new Cell{X=c*(g.TileW+g.Gap),Y=r*(g.TileH+g.Gap),W=g.TileW,H=g.TileH};}
        public static int Col(Grid g,int i)=>g.Cols<=0?0:i%g.Cols;
        public static int RowOf(Grid g,int i)=>g.Cols<=0?0:i/g.Cols;

        // ---- Inside a weapon tile (`tile` square): the name block (NameLines lines at NameSize, LineHeight each) above NamePad,
        // and the icon (IconShare of the tile, IconMin..IconMax, shrunk to fit small tiles) centred in the space above it, a
        // little low so the hover lift has room.
        public const float NamePad=9,LineHeight=1.18f,IconShare=.46f,IconMin=30,IconMax=60,IconTop=8,IconGap=4,LiftHover=4,LiftSelected=2;
        public const int NameLines=2;
        public static float NameSize(float tile)=>tile<96?11.5f:12.5f;
        public static void WeaponFace(float tile,out float icon,out float iconCy,out float nameTop,out float nameH)
        {
            float ns=NameSize(tile);nameH=NameLines*ns*LineHeight;nameTop=tile-NamePad-nameH;float bottom=nameTop-IconGap;
            icon=Math.Min(Math.Max(tile*IconShare,IconMin),IconMax);icon=Math.Min(icon,bottom-IconTop);iconCy=(IconTop+bottom)/2+Math.Min(2,(bottom-IconTop-icon)/2);
        }
        // Inside a class tile (`h` tall): the icon and one name line (uppercase, tracked) centred together.
        public static float ClassIcon(Row row)=>row==Row.Lobby?24:row==Row.Locker?26:28;
        public static float ClassNameSize(Row row)=>row==Row.Lobby?11:11.5f;
        public const float ClassTracking=.14f,ClassNameGap=7;
        public static void ClassFace(Row row,float h,out float icon,out float iconCy,out float nameTop,out float nameH)
        {
            icon=ClassIcon(row);nameH=ClassNameSize(row)*1.2f;float total=icon+ClassNameGap+nameH,top=(h-total)/2+1;iconCy=top+icon/2;nameTop=top+icon+ClassNameGap;
        }
        // How far the icon rises: on hover and, a little, while selected (calm: still).
        public static float Lift(float hover,float selected,bool calm)=>calm?0:LiftHover*hover+LiftSelected*selected*(1-hover);

        // ---- Motion -----------------------------------------------------------------------------------------------------
        static float Clamp01(float v)=>v<0?0:v>1?1:v;
        static float Repeat(float v,float p)=>v-(float)Math.Floor(v/p)*p;
        // Entrance: each tile rises Rise px and fades in over EnterTime, Stagger after the one before it, whenever the picker
        // appears or shows another class (calm: shown at once).
        public const float EnterTime=.34f,Stagger=.045f,Rise=10;
        public static float Enter(float age,int index,bool calm)=>calm?1:NativeUiMotion.OutCubic((age-index*Stagger)/EnterTime);
        // When the equipped tile's lock plays after an entrance (its tile mostly up).
        public static float LockAfterEnter(int index,bool calm)=>calm?0:index*Stagger+EnterTime*.6f;
        // The idle holo scan: one pass down each tile every ScanPeriod, taking ScanShare of it; tiles are offset by ScanWave per
        // column and 1.6x that per row, so the pass travels across the grid as a wave. False between passes (and calm).
        public const float ScanPeriod=4.8f,ScanShare=.42f,ScanWave=.11f;
        public static bool Scan(float t,int col,int row,bool calm,out float progress)
        {
            progress=0;if(calm)return false;float c=Repeat(t-col*ScanWave-row*ScanWave*1.6f,ScanPeriod)/ScanPeriod;if(c>=ScanShare)return false;
            progress=NativeUiMotion.EaseInOut(c/ScanShare);return true;
        }
        // The hex shimmer: a soft band of brighter lattice drifting across the face, its centre -.4..1.4 of the width (calm: none).
        public const float ShimmerPeriod=7.5f;
        public static bool Shimmer(float t,int col,int row,bool calm,out float centre)
        {
            centre=0;if(calm)return false;centre=-.4f+1.8f*Repeat(t+col*.37f+row*.61f,ShimmerPeriod)/ShimmerPeriod;return true;
        }
        // Hover: one fast bright scan pass on the pointer's arrival (HoverPass long) and the chromatic edge flicker: three hard
        // steps right after it, then a short blip every BlipPeriod while held. 0..1 strength; calm: none.
        public const float HoverPass=.5f,BlipPeriod=3.3f;
        public static bool HoverScan(float age,bool calm,out float progress)
        {
            progress=0;if(calm||age<0||age>=HoverPass)return false;progress=NativeUiMotion.Bezier(.45f,0,.25f,1,age/HoverPass);return true;
        }
        public static float Flicker(float age,bool calm)
        {
            if(calm||age<0)return 0;if(age<.05f)return 1;if(age<.09f)return 0;if(age<.14f)return .7f;if(age<.19f)return 0;if(age<.24f)return .4f;
            float x=Repeat(age-.24f,BlipPeriod);return x>=BlipPeriod-.1f&&x<BlipPeriod-.06f?.6f:x>=BlipPeriod-.04f?.35f:0;
        }
        // Selection: corner brackets snap from Rest+Travel onto Rest just outside the tile (a little overshoot) over Snap, then
        // breathe by Breathe px each BreathPeriod; the lock flash fades over Lock; the data pulse ring leaves the icon every
        // PulsePeriod, growing 0..1 and fading. Calm: brackets at rest and opaque, no flash, a still faint ring.
        public const float BracketRest=2.5f,BracketTravel=7,Snap=.22f,Breathe=.8f,BreathPeriod=2.6f,Lock=.32f,PulsePeriod=2.4f,PulseDelay=.1f,CalmRing=.45f,CalmRingAlpha=.22f;
        public static float Breath(float t,bool calm)=>calm?1:1+.12f*(float)Math.Sin(t*2*Math.PI/BreathPeriod);
        public static float BracketOffset(float age,float t,bool calm)
        {
            if(calm)return BracketRest;float snap=NativeUiMotion.Bezier(.2f,.9f,.3f,1.35f,age/Snap),settle=Clamp01((age-Snap)/.3f);
            return BracketRest+BracketTravel*(1-snap)+Breathe*settle*(float)Math.Sin(t*2*Math.PI/BreathPeriod);
        }
        public static float BracketOpacity(float age,bool calm)=>calm?1:Clamp01(age/.08f);
        public static float LockFlash(float age,bool calm){if(calm||age<0||age>=Lock)return 0;float u=1-age/Lock;return u*u;}
        public static bool Pulse(float age,bool calm,out float grow,out float alpha)
        {
            if(calm){grow=CalmRing;alpha=CalmRingAlpha;return true;}grow=alpha=0;if(age<PulseDelay)return false;
            float u=Repeat(age-PulseDelay,PulsePeriod)/PulsePeriod;grow=NativeUiMotion.OutCubic(u);alpha=.55f*(float)Math.Pow(1-u,1.6);return alpha>.002f;
        }
        // The DEV class's mystery: its name's glitch beat, as the Siren title split (NativeUi.SirenSplit's 0..1 cycle; calm: -1).
        public const float GlitchPeriod=3.4f;
        public static float Glitch(float t,bool calm)=>calm?-1:Repeat(t,GlitchPeriod)/GlitchPeriod;
        // The info strip: how quickly it brightens for a hovered weapon and fades back to the equipped one (s per full swing),
        // its content's cross-fade, and the pointer notch's follow rate (1/s, exponential).
        public const float StripIn=.12f,StripOut=.25f,StripFade=.14f,NotchRate=18;
        public static float StripStep(float level,bool live,float dt){float target=live?1:0,rate=Math.Max(0,dt)/(live?StripIn:StripOut);return level<target?Math.Min(target,level+rate):Math.Max(target,level-rate);}
        public static float Follow(float from,float to,float dt)=>from<0?to:to+(from-to)*(float)Math.Exp(-NotchRate*Math.Max(0,dt));
    }
}

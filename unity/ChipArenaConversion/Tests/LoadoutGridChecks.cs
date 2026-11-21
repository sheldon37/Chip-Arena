using System;
using System.Collections.Generic;
using System.Linq;
using ChipArena.UnityConversion;
using G=ChipArena.UnityConversion.NativeLoadoutGrid;

// Engine-free checks for the loadout pickers' cyber tiles (Core/NativeLoadoutGrid, NativeLoadoutText.Role): the class row and
// weapon grid fit their slot at 1280x720, 1920x1080 and 3440x1440 at every interface scale (.75 to 1.5), hit areas never
// overlap, the tile face keeps the icon clear of the name, and Reduced effects stills every motion. Drawing is not exercised.
internal static class LoadoutGridChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Loadout tiles: "+what);}
    private const float Eps=.01f;
    private static void Fits(G.Grid g,float width,int count,string what)
    {
        Check(g.Cols>=1&&g.Rows==(count+g.Cols-1)/g.Cols,what+": rows");
        var cells=Enumerable.Range(0,count).Select(i=>G.At(g,i)).ToArray();
        foreach(var c in cells)Check(c.X>=-Eps&&c.Y>=-Eps&&c.X+c.W<=width+Eps&&c.Y+c.H<=g.Height+Eps&&c.W>0&&c.H>0,what+": a tile outside its slot");
        for(int i=0;i<cells.Length;i++)for(int j=i+1;j<cells.Length;j++)
        {
            var a=cells[i];var b=cells[j];float gx=Math.Max(b.X-(a.X+a.W),a.X-(b.X+b.W)),gy=Math.Max(b.Y-(a.Y+a.H),a.Y-(b.Y+b.H));
            Check(Math.Max(gx,gy)>=Math.Min(G.NarrowGap,6)-Eps,what+": hit areas "+i+" and "+j+" overlap or touch");
        }
        if(count>0)Check(Math.Abs(cells.Max(c=>c.Y+c.H)-g.Height)<Eps,what+": the reserved height is the grid's");
    }
    public static IEnumerable<string> Run(ConversionContent data)
    {
        var screens=new[]{(1280,720),(1920,1080),(3440,1440)};int cases=0;
        foreach(var (sw,sh) in screens)for(int k=0;k<=15;k++)
        {
            float ui=.75f+.05f*k;var l=NativeUiMotion.Measure(sw,sh,ui);bool narrow=l.Width<=580;string at=sw+"x"+sh+" @"+ui.ToString("0.00");
            // The panel screens' slot (BeginScreen at 650 wide with the pilot card when it fits, 32 px padding, the form's 4 px
            // insets, the scroll bar) and every other width a slot of this canvas can take (lobby roster, Locker view).
            float card=l.Width-56-650-26>=270?Math.Min(390,l.Width-56-650-26):0,pw=Math.Min(650,l.Width-56-(card>0?card+26:0));
            var widths=new List<float>{pw-64-8,pw-64-8-16};for(float w=160;w<=Math.Min(l.Width-56,1400);w+=7.5f)widths.Add(w);
            foreach(float w in widths)
            {
                for(int n=1;n<=8;n++)
                {
                    var g=G.Weapons(w,n,narrow);Fits(g,w,n,at+" weapons "+n+" at "+w);
                    Check(g.TileW<=G.TileMax+Eps&&(g.TileW>=G.TileMin-Eps||g.Cols==1),at+": tile size "+g.TileW+" at "+w);
                    Check(Math.Abs(g.TileW-G.Weapons(w,1,narrow).TileW)<Eps,at+": every class shows the same tile size at "+w);
                    Check(g.TileW*l.Scale>=40,at+": a tile at least 40 physical px ("+g.TileW*l.Scale+")");
                    G.WeaponFace(g.TileW,out float icon,out float cy,out float nameTop,out float nameH);float lift=G.LiftHover+G.LiftSelected;
                    Check(icon*l.Scale>=NativeIconRaster.SmallPx*.7f&&cy-icon/2-lift>=2&&cy+icon/2<=nameTop-2+Eps&&nameTop+nameH<=g.TileH-G.NamePad+Eps,at+": weapon face at tile "+g.TileW);
                    Check(Math.Abs(G.WeaponsHeight(w,n,narrow)-(g.Height+G.StripGap+G.StripHeight))<Eps&&G.StripGap>=6,at+": the info strip sits under the grid");
                    cases++;
                }
                foreach(var row in new[]{G.Row.Panel,G.Row.Lobby,G.Row.Locker})for(int n=3;n<=4;n++)
                {
                    var g=G.Classes(w,n,row,narrow);Fits(g,w,n,at+" classes "+n+" "+row+" at "+w);Check(g.TileW>=G.ClassMin-Eps||g.Cols==1,at+": class tile width");
                    if(w>=n*G.ClassMin+(n-1)*G.ClassGap)Check(g.Rows==1,at+": the class row stays one row at "+w);
                    G.ClassFace(row,g.TileH,out float icon,out float cy,out float nameTop,out float nameH);
                    Check(cy-icon/2-G.LiftHover>=2&&cy+icon/2<=nameTop+Eps&&nameTop+nameH<=g.TileH-2,at+": class face "+row);cases++;
                }
            }
            // The Play vs Bots slot: the largest class (7 weapons for a developer's Melee) takes two rows, the old cards four.
            var play=G.Weapons(pw-64-8,7,narrow);if(pw>=600)Check(play.Rows<=2,at+": seven weapons in "+play.Rows+" rows");
            G.WeaponFace(play.TileW,out float pi,out _,out _,out _);Check(pi*l.Scale>=NativeIconRaster.SmallPx+4,at+": the panel's weapon icons draw at "+pi*l.Scale+" physical px");
        }
        yield return "Loadout tiles fit their slot at 1280x720, 1920x1080 and 3440x1440 at every interface scale (.75-1.5): "+cases+" grids, no overlapping hit areas, icon clear of the name";

        Check(G.Enter(0,0,false)==0&&G.Enter(G.EnterTime,0,false)==1&&G.Enter(G.Stagger*3,3,false)==0&&G.Enter(G.Stagger*3+G.EnterTime,3,false)==1,"entrance stagger");
        float prev=-1;for(float t=0;t<1;t+=.01f){float e=G.Enter(t,2,false);Check(e>=prev-1e-5f,"entrance is monotonic");prev=e;}
        Check(G.LockAfterEnter(4,false)>G.Stagger*4&&G.LockAfterEnter(4,false)<G.Stagger*4+G.EnterTime,"the lock plays as its tile comes up");
        bool passes=false;for(float t=0;t<G.ScanPeriod;t+=.05f)passes|=G.Scan(t,0,0,false,out _);Check(passes,"the idle scan passes");
        Check(G.Scan(.5f,0,0,false,out float p0)&&G.Scan(.5f,1,0,false,out float p1)&&p1<p0,"the scan travels across the grid as a wave");
        Check(G.Flicker(.02f,false)==1&&G.Flicker(.07f,false)==0&&G.Flicker(1.5f,false)==0,"the hover flicker: hard steps, then still between blips");
        Check(G.BracketOffset(0,0,false)>G.BracketRest+G.BracketTravel*.9f&&Math.Abs(G.BracketOffset(G.Snap,0,false)-G.BracketRest)<.5f,"the brackets snap onto the tile");
        Check(G.LockFlash(.01f,false)>.9f&&G.LockFlash(G.Lock,false)==0,"the lock flash fades");
        Check(G.Pulse(G.PulseDelay+.2f,false,out float grow,out float alpha)&&grow>0&&alpha>0,"the data pulse runs");
        yield return "Tile motion: staggered entrance, scan wave, hover flicker, bracket snap, lock flash and data pulse";

        for(float t=0;t<8;t+=.1f)
        {
            Check(G.Enter(t,5,true)==1&&!G.Scan(t,0,0,true,out _)&&!G.Shimmer(t,0,0,true,out _)&&!G.HoverScan(t,true,out _)&&G.Flicker(t,true)==0,"Reduced effects: no entrance, scan, shimmer or flicker");
            Check(G.BracketOffset(t,t,true)==G.BracketRest&&G.BracketOpacity(t,true)==1&&G.LockFlash(t,true)==0&&G.Breath(t,true)==1&&G.Glitch(t,true)<0&&G.Lift(1,1,true)==0,"Reduced effects: still brackets, no flash, breath, glitch or lift");
            Check(G.Pulse(t,true,out float g,out float a)&&g==G.CalmRing&&a==G.CalmRingAlpha,"Reduced effects: a still ring");
        }
        yield return "Reduced effects stills every tile effect (static ring and brackets, no flicker)";

        foreach(var w in data.weapons)
        {
            string role=NativeLoadoutText.Role(w);
            Check(role.Length>0&&role.Length<=44&&!role.Contains("Q:")&&!role.Contains("E:"),"role line for "+w.id+": '"+role+"'");
            Check(w.devOnly==role.StartsWith("DEV",StringComparison.Ordinal),"DEV weapons say so in their role ("+w.id+")");
        }
        Check(NativeLoadoutText.Role(new ContentWeapon{id="new",description="First sentence. Second."})=="First sentence"&&NativeLoadoutText.Role(null)=="","an unknown weapon's role is its first sentence");
        yield return "Every weapon has a one-line role for the info strip (no ability names; DEV weapons marked)";
    }
}

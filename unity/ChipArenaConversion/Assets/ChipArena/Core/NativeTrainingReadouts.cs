using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What one target's readout shows, quantised to what is drawn (tenths), so a readout's texture is rebuilt only when a
    /// drawn value changes (<see cref="Same"/>): cumulative actual HP `total`, the last hit's amount and headshot flag, the
    /// rolling 5 s DPS, the source (weapon or environment and hit kind) and the capsule-to-capsule distance. Reads only the
    /// replicated <see cref="TrainingReadout"/> (host and guests alike: NativeArena.TrainingReadouts()).
    /// </summary>
    public struct NativeTrainingReadoutKey
    {
        public long Total,Last,Dps,Distance;public bool Head,Any;public string Source;
        public static long Tenths(float v)=>MathUtil.Finite(v)?(long)Math.Round(Math.Max(0,Math.Min(1e9f,v))*10):0;
        public static NativeTrainingReadoutKey Of(TrainingReadout r)
        =>r==null?default:new NativeTrainingReadoutKey{Total=Tenths(r.total),Last=Tenths(r.last),Dps=Tenths(r.dps),Distance=Tenths(r.distance),Head=r.headshot,Any=r.total>0||r.last>0,Source=r.source};
        public bool Same(in NativeTrainingReadoutKey o)=>Total==o.Total&&Last==o.Last&&Dps==o.Dps&&Distance==o.Distance&&Head==o.Head&&Any==o.Any&&string.Equals(Source,o.Source,StringComparison.Ordinal);
    }

    /// <summary>
    /// The readout's text, written into a caller's char buffer (no strings, no allocation): numbers to one decimal below 10,000
    /// ("1240.5"), whole and grouped above ("12,345"), and the source as "RIFLE · HIT" (the kind dropped when it is a plain Hit;
    /// "environment" reads HAZARD; a Tick kind loses "TICK"). Every writer returns the new length and never passes `cap`.
    /// </summary>
    public static class NativeTrainingReadoutText
    {
        /// <summary>Writes a non-negative value (tenths, as the key holds it).</summary>
        public static int Number(char[] o,int at,long tenths,int cap)
        {
            if(tenths<0)tenths=0;
            if(tenths<100000)
            {
                long whole=tenths/10;int d=(int)(tenths%10);at=Whole(o,at,whole,false,cap);if(at<cap)o[at++]='.';if(at<cap)o[at++]=(char)('0'+d);return at;
            }
            return Whole(o,at,(tenths+5)/10,true,cap);
        }
        private static int Whole(char[] o,int at,long v,bool group,int cap)
        {
            int digits=1;for(long q=v;q>=10;q/=10)digits++;int end=at+digits+(group?(digits-1)/3:0),i=end-1,k=0;
            // right to left: a digit at a time, a comma before every third
            for(long q=v;i>=at;k++){if(group&&k>0&&k%3==0){if(i<cap)o[i]=',';i--;}if(i<at)break;if(i<cap)o[i]=(char)('0'+q%10);q/=10;i--;}
            return Math.Min(end,cap);
        }
        public static int Put(char[] o,int at,string s,int cap){if(s!=null)for(int i=0;i<s.Length&&at<cap;i++)o[at++]=Upper(s[i]);return at;}
        private static char Upper(char c)=>c>='a'&&c<='z'?(char)(c-32):c;
        /// <summary>The source label: the spec part (at most `specCap` characters, "environment" as HAZARD), then " · " and the hit
        /// kind unless it is "Hit" or "hazard" (AbilityHit as ABILITY, a ...Tick without TICK), cut at `cap`.</summary>
        public static int Source(char[] o,int at,string source,int cap,int specCap=11)
        {
            if(string.IsNullOrEmpty(source))return Put(o,at,"-",cap);
            int colon=source.IndexOf(':');int specEnd=colon<0?source.Length:colon;
            if(specEnd==11&&string.CompareOrdinal(source,0,"environment",0,11)==0)at=Put(o,at,"HAZARD",cap);
            else for(int i=0;i<specEnd&&i<specCap&&at<cap;i++)o[at++]=Upper(source[i]);
            if(colon<0||colon+1>=source.Length)return at;
            int k0=colon+1,k1=source.Length;
            if(k1-k0==3&&string.CompareOrdinal(source,k0,"Hit",0,3)==0||k1-k0==6&&string.Compare(source,k0,"hazard",0,6,StringComparison.OrdinalIgnoreCase)==0)return at;
            if(k1-k0==10&&string.CompareOrdinal(source,k0,"AbilityHit",0,10)==0){at=Put(o,at," · ",cap);return Put(o,at,"ABILITY",cap);}
            if(k1-k0>4&&string.CompareOrdinal(source,k1-4,"Tick",0,4)==0)k1-=4;
            at=Put(o,at," · ",cap);for(int i=k0;i<k1&&at<cap;i++)o[at++]=Upper(source[i]);return at;
        }
    }

    /// <summary>
    /// One target's readout cell as an RGBA32 image (<see cref="Width"/> x <see cref="Height"/>, row 0 at the bottom as Unity
    /// stores it): the pad's header, the total in large lamps, the last hit with a HEAD badge on a headshot, the 5 s DPS, and
    /// the source and distance. The same image is the range or ring board's cell and the floating tag over the dummy (its
    /// translucent plate is the alpha). <see cref="Paint"/> writes only into its own buffers: allocation free.
    /// </summary>
    public sealed class NativeTrainingReadoutRaster
    {
        public const int Width=256,Height=168;
        public readonly byte[] Pixels=new byte[Width*Height*4];
        private readonly char[] line=new char[40];
        // sRGB colours of the cell (NativeCyberTheme)
        private const int Bg=NativeTrainingLook.Panel,Band=NativeTrainingLook.Indigo,Frame=NativeTrainingLook.Edge,Accent=NativeTrainingLook.Cyan,Head=NativeTrainingLook.Muted,
            Value=NativeTrainingLook.Ink,Caption=NativeTrainingLook.Dim,Rate=NativeTrainingLook.CyanSoft,Shot=NativeTrainingLook.Danger;
        /// <summary>Paints the cell for `header` (the pad's, built once per map) and `key` (the target's quantised readout).</summary>
        public void Paint(string header,string tag,in NativeTrainingReadoutKey key,int accent=Accent)
        {
            Fill(0,0,Width,Height,Bg,232);Fill(0,0,Width,26,Band,240);Fill(0,26,Width,2,accent,255);
            Fill(0,0,Width,2,Frame,255);Fill(0,Height-2,Width,2,Frame,255);Fill(0,0,2,Height,Frame,255);Fill(Width-2,0,2,Height,Frame,255);
            int n=NativeTrainingReadoutText.Put(line,0,header,line.Length);Text(10,7,2,n,Value);
            if(!string.IsNullOrEmpty(tag)){n=NativeTrainingReadoutText.Put(line,0,tag,line.Length);Text(Width-10-TextWidth(n,2),7,2,n,accent);}
            n=NativeTrainingReadoutText.Put(line,0,"TOTAL HP",line.Length);Text(12,34,2,n,Caption);
            if(!key.Any)
            {
                n=NativeTrainingReadoutText.Put(line,0,"0.0",line.Length);Text(12,52,6,n,Caption,true);
                n=NativeTrainingReadoutText.Put(line,0,"NO HITS YET",line.Length);Text(12,101,3,n,Caption);
                n=NativeTrainingReadoutText.Put(line,0,"SHOOT OR STRIKE IT",line.Length);Text(12,151,2,n,Caption);
                return;
            }
            n=NativeTrainingReadoutText.Number(line,0,key.Total,line.Length);int big=n<=6?6:n<=7?5:4;Text(12,52+(6-big)*3,big,n,Value,true);
            // the last hit and its headshot badge
            n=NativeTrainingReadoutText.Put(line,0,"LAST ",line.Length);n=NativeTrainingReadoutText.Number(line,n,key.Last,line.Length);Text(12,101,3,n,Value);
            if(key.Head){Fill(Width-82,98,70,27,Shot,255);n=NativeTrainingReadoutText.Put(line,0,"HEAD",line.Length);Text(Width-82+(70-TextWidth(n,2))/2,105,2,n,Bg);}
            // the rolling DPS
            n=NativeTrainingReadoutText.Put(line,0,"DPS ",line.Length);n=NativeTrainingReadoutText.Number(line,n,key.Dps,line.Length);Text(12,127,3,n,Rate);
            n=NativeTrainingReadoutText.Put(line,0,"5 S",line.Length);Text(Width-12-TextWidth(n,2),131,2,n,Caption);
            // the source and the distance, the distance right-aligned
            int m=NativeTrainingReadoutText.Number(line,0,key.Distance,line.Length);m=NativeTrainingReadoutText.Put(line,m," M",line.Length);int dw=TextWidth(m,2);Text(Width-12-dw,151,2,m,Head);
            int room=(Width-24-dw-10)/(2*NativeTrainingFont.Advance);n=NativeTrainingReadoutText.Source(line,0,key.Source,Math.Max(1,Math.Min(line.Length,room)));Text(12,151,2,n,Head);
        }
        public static int TextWidth(int chars,int scale)=>NativeTrainingFont.Width(chars)*scale;
        // y down from the top, as the layout reads; rows flip into Unity's bottom-up order here
        private void Fill(int x,int y,int w,int h,int hex,int alpha)
        {
            byte r=(byte)(hex>>16&255),g=(byte)(hex>>8&255),b=(byte)(hex&255),a=(byte)alpha;
            int x0=Math.Max(0,x),x1=Math.Min(Width,x+w),y0=Math.Max(0,y),y1=Math.Min(Height,y+h);
            for(int yy=y0;yy<y1;yy++){int row=(Height-1-yy)*Width;for(int xx=x0;xx<x1;xx++){int o=(row+xx)*4;Pixels[o]=r;Pixels[o+1]=g;Pixels[o+2]=b;Pixels[o+3]=a;}}
        }
        // the line buffer's first n characters at (x, y) in cells of `scale` px; lamps: each cell drawn one pixel smaller (an LED face)
        private void Text(int x,int y,int scale,int n,int hex,bool lamps=false)
        {
            for(int i=0;i<n;i++)
            {
                char c=line[i];int gx=x+i*NativeTrainingFont.Advance*scale;if(gx>=Width)break;
                for(int cy=0;cy<NativeTrainingFont.Rows;cy++)for(int cx=0;cx<NativeTrainingFont.Columns;cx++)
                    if(NativeTrainingFont.Lit(c,cx,cy)){if(lamps)Fill(gx+cx*scale,y+cy*scale,scale-1,scale-1,hex,255);else Fill(gx+cx*scale,y+cy*scale,scale,scale,hex,255);}
            }
        }
    }

    /// <summary>
    /// The floating tag over a target: shown while it has a readout and was hit in the last <see cref="TagHold"/> s, fading
    /// out over <see cref="TagFade"/> s after, for the <see cref="MaxTags"/> targets hit most recently only (so a blast across
    /// a lane does not stack tags); a short pop on a new hit; a constant share of the view's height (<see cref="TagScreen"/>,
    /// so it stays readable down the range and through a scope's narrower view) between <see cref="TagMin"/> and
    /// <see cref="TagMax"/> m (the boards carry the far targets' detail).
    /// </summary>
    public static class NativeTrainingTags
    {
        public const float TagHold=6,TagFade=2,TagScreen=.075f,TagMin=.32f,TagMax=2.4f,TagLift=1.3f,PopSeconds=.2f,Pop=.16f;
        /// <summary>A drawn actor's position is its feet (NativeArena's snapshot: the body less .865 m); pads and tags use the
        /// capsule centre, as the authored pads do.</summary>
        public const float FeetToCentre=.865f;
        /// <summary>At most this many tags at once: the targets hit most recently.</summary>
        public const int MaxTags=3;
        public static float Alpha(bool any,float age)=>!any||!MathUtil.Finite(age)?0:age<=TagHold?1:age>=TagHold+TagFade?0:1-(age-TagHold)/TagFade;
        public static float Height(float distance,float fovDegrees)=>Math.Min(TagMax,Math.Max(TagMin,2*Math.Max(0,distance)*(float)Math.Tan(Math.Max(1,Math.Min(170,fovDegrees))*Math.PI/360)*TagScreen));
        /// <summary>Keeps the `MaxTags` newest hits in `slots` (pad indices, -1 empty) as `at` arrives: call once per candidate.</summary>
        public static void Keep(int[] slots,float[] times,int pad,float at)
        {
            int worst=0;for(int i=0;i<slots.Length;i++){if(slots[i]<0){worst=i;break;}if(times[i]<times[worst])worst=i;}
            if(slots[worst]<0||at>times[worst]){slots[worst]=pad;times[worst]=at;}
        }
        public static float Scale(float age)=>age>=0&&age<PopSeconds?1+Pop*(1-age/PopSeconds):1;
        /// <summary>The board cell's flash on a new hit (0..1, decaying over half a second).</summary>
        public static float Flash(float age)=>age>=0&&age<.5f?1-age/.5f:0;
    }
}

using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One pod at one presented moment (NativePodDrop.Evaluate): where it is, how it burns, its door and lights.</summary>
    public struct PodFrame
    {
        /// <summary>The drop is under way (from the countdown's start until the door rests); after it the pod is parked.</summary>
        public bool Dropping;
        /// <summary>Presentation time minus the fight start (NativeSpawnIn.Age).</summary>
        public float Age;
        /// <summary>The pod's base over its landing spot, along the approach (m), and its lean into the fall (radians, about
        /// the horizontal axis across the approach).</summary>
        public Vector3 Offset;public float Lean;
        /// <summary>Re-entry heat on the seams (0..1), the retro burn (0..1), the cabin lamp (0..1), the LED sign's power
        /// (0..1, flickering on after the landing), the door seam's flare before it opens (0..1), the beacon (0..1).</summary>
        public float Heat,Retro,Cabin,Sign,Seam,Beacon;
        /// <summary>The door's turn about its hinge (0 shut, NativePodModels.OpenAngle resting open).</summary>
        public float Door;
        /// <summary>Seconds since the landing (negative before it).</summary>
        public float Land;
        /// <summary>The pilot shows (the door is opening); before it the pilot rides hidden inside.</summary>
        public bool Shown;
        /// <summary>The presented pilot's lift over the floor (the pod's floor while it falls): my first-person view rides it.</summary>
        public float Lift;
    }

    /// <summary>
    /// The Expedition drop pods (owner 2026-11-02: "When all players load in, we essentially drop in to the hub from a pod. (This
    /// pod is how players can choose to leave as well) using their OWN pod. (Can have an LED sign with the player name)"; the
    /// model is NativePodModels, the draw Runtime/NativePodPresentation.cs). In a generated run's hub the pods take the place of
    /// the spawn-in beam (NativeSpawnIn) over the same clock: the run's opening countdown, which already holds every pilot at its
    /// spawn, so nothing here changes a rule, a timing or the network.
    ///  - From the countdown's start each pilot's pod streaks down out of the sky along one shared approach (a burning trail,
    ///    seams white-hot), fires its retros, and slams onto the pilot's spawn at Impact (staggered a little per pilot); dust,
    ///    sparks and a camera kick. Steam vents, the LED sign flickers on with the pilot's name, the cabin lights.
    ///  - At DoorAt the seam flares and the door drops open outward into a ramp just before GO; the pilot shows in the doorway
    ///    and steps out at GO. The pilot rides hidden inside until then (my own view, in first person, rides the fall).
    ///  - Then the pod stays parked where it landed for the run: the pilot's way out (Astra: leaving by pod; Launch is its
    ///    presentation: the door closes, the retros light and it lifts away).
    /// Pure functions of (age, seed): any frame, peer or late frame shows the same moment. Engine free; nothing allocates.
    /// </summary>
    public static class NativePodDrop
    {
        // ---- the drop (seconds from the fight start; the countdown runs from -NativeSpawnIn.Countdown) ----
        public const float Appear=-NativeSpawnIn.Countdown;
        /// <summary>The first pod's landing, and each later pilot's extra delay (Order: 0, 1, 2, ...).</summary>
        public const float Impact=-1.35f,Stagger=.12f;
        /// <summary>The fall: from Altitude over the spot, Reach off along the approach, fast then braking (it lands at about a
        /// third of its starting speed); the retros burn its last RetroSeconds.</summary>
        public const float Altitude=48,Reach=20,RetroSeconds=.55f;
        /// <summary>The door: its seam flares from DoorAt - SeamSeconds, it drops open from DoorAt over DoorSeconds (gravity:
        /// slow, then fast) and bounces once at rest; the pilot shows from DoorAt + ShowAfter.</summary>
        public const float DoorAt=-.14f,DoorSeconds=.38f,SeamSeconds=.35f,ShowAfter=.1f;
        /// <summary>The drop's end: the door has rested.</summary>
        public const float End=DoorAt+DoorSeconds+.3f;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}

        /// <summary>The shared approach: the horizontal direction the pods come in from (unit, xz), from the run's seed.</summary>
        public static Vector3 Approach(int seed)
        {
            double a=.6+Hash(seed,1)*1.9;return new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));
        }
        /// <summary>When pilot `order` (0 the first) lands.</summary>
        public static float ImpactOf(int order)=>Impact+Stagger*Math.Max(0,Math.Min(7,order));
        /// <summary>
        /// Pod `order`'s frame `age` s from the fight start (NaN: parked). Before the countdown's start nothing has dropped
        /// (Dropping false, Offset far up: the draw shows no pod); after End the pod is parked, door open.
        /// </summary>
        public static PodFrame Evaluate(float age,int order,int seed)
        {
            var f=new PodFrame{Age=age,Land=-1};float impact=ImpactOf(order);
            if(float.IsNaN(age)||age>=End){Parked(ref f,float.IsNaN(age)?10:age-impact);return f;}
            if(age<Appear){f.Offset=new Vector3(0,Altitude*4,0);return f;}
            f.Dropping=true;f.Land=age-impact;
            if(age<impact)
            {
                float u=Clamp01((age-Appear)/(impact-Appear)),k=1-u;
                // the height: from Altitude, braking (h'(0) = -1.55 H/T, h'(1) = -.45 H/T); the reach shrinks as it closes
                f.Offset=Approach(seed)*(Reach*k*k)+new Vector3(0,Altitude*k*(1-.55f*u),0);
                f.Lean=.32f*k;f.Heat=Clamp01(u*2.2f)*(1-.35f*Smooth((u-.75f)/.25f));
                f.Retro=Smooth((age-(impact-RetroSeconds))/.12f);
                f.Lift=f.Offset.Y+NativePodModels.Floor;
            }
            else
            {
                float a=age-impact;
                // the slam: a squash and a damped recoil
                f.Offset=new Vector3(0,-.08f*(float)Math.Exp(-9*a)*(float)Math.Cos(22*a)*(a<.7f?1:0),0);
                f.Heat=1-Smooth(a/1.4f);f.Retro=1-Smooth(a/.12f);f.Lift=Math.Max(0,f.Offset.Y)+NativePodModels.Floor;
                f.Sign=SignOn(a);f.Cabin=Smooth((a-.15f)/.25f)*.5f;f.Beacon=((int)(a*4))%2==0?1:.2f;
            }
            f.Seam=age<DoorAt?Smooth((age-(DoorAt-SeamSeconds))/SeamSeconds):1-Smooth((age-DoorAt)/.2f);
            f.Door=DoorTurn(age-DoorAt);if(f.Door>0)f.Cabin=Math.Max(f.Cabin,.5f+.5f*Smooth(f.Door/NativePodModels.OpenAngle));
            f.Shown=age>=DoorAt+ShowAfter;if(f.Shown)f.Lift=0;
            return f;
        }
        private static void Parked(ref PodFrame f,float sinceLand)
        {
            f.Dropping=false;f.Offset=Vector3.Zero;f.Door=NativePodModels.OpenAngle;f.Shown=true;f.Land=sinceLand;f.Sign=1;f.Cabin=.7f;
            f.Beacon=((int)(Math.Max(0,sinceLand)*1.25f))%2==0?1:.25f;
        }
        // The sign flickers on a quarter second after the landing: two blinks, then steady.
        private static float SignOn(float a)=>a<.25f?0:a<.32f?1:a<.38f?.15f:a<.43f?1:a<.47f?.3f:1;
        /// <summary>The door's turn `t` s after it starts to open: gravity's ease (slow off the seal, fast to the ground), a
        /// small bounce, then rest at OpenAngle.</summary>
        public static float DoorTurn(float t)
        {
            if(!(t>0))return 0;if(t<DoorSeconds){float u=t/DoorSeconds;return NativePodModels.OpenAngle*u*u;}
            float b=t-DoorSeconds;return NativePodModels.OpenAngle-.12f*(float)Math.Exp(-10*b)*Math.Abs((float)Math.Sin(14*b));
        }

        // ---- leaving (Astra's rule triggers it; this is what it looks like) ----
        /// <summary>The door closes over this long, the retros light, the pod lifts away and is gone by LaunchGone.</summary>
        public const float LaunchClose=.5f,LaunchIgnite=.9f,LaunchGone=3.4f;
        /// <summary>A parked pod `since` s after its pilot boarded to leave: the door swings shut, the retros build, it lifts
        /// slowly then fast straight up (Dropping true while it shows; Offset far up once gone).</summary>
        public static PodFrame Launch(float since)
        {
            var f=new PodFrame{Age=since,Land=10};if(!(since>=0)){Parked(ref f,10);return f;}
            if(since>=LaunchGone){f.Offset=new Vector3(0,Altitude*4,0);return f;}
            f.Dropping=true;f.Sign=1;f.Cabin=1;f.Beacon=((int)(since*8))%2==0?1:.2f;
            f.Door=NativePodModels.OpenAngle*(1-Smooth(since/LaunchClose));
            f.Retro=Smooth((since-LaunchClose)/(LaunchIgnite-LaunchClose));
            float up=since-LaunchIgnite;if(up>0)f.Offset=new Vector3(0,up*up*(4+3*up),0);f.Heat=Clamp01(up*.6f);
            return f;
        }

        /// <summary>Camera kick for my own pod's landing (metres down, and a pitch in degrees); 0 outside it.</summary>
        public static float Kick(in PodFrame f,out float pitch)
        {
            pitch=0;if(!f.Dropping||f.Land<0||f.Land>.35f)return 0;float x=f.Land,d=x<.04f?x/.04f:(1-(x-.04f)/.31f)*(1-(x-.04f)/.31f);
            pitch=2.4f*d;return .22f*d;
        }

        // ---- the effects (closed form, through the directed entrances' emitters; world frame, y up) ----
        /// <summary>
        /// The pod's effects at `spot` (its landing point on the floor) for frame `f`: the re-entry fireball and its burning
        /// trail, the retro jets, the landing's dust ring and sparks, the steam vents, the door seam's flare and the sparks as it
        /// drops open. `facing` is the doorway's direction (unit, xz); `quality` the effects setting.
        /// </summary>
        public static void Effects(INativeEntranceFx fx,in PodFrame f,Vector3 spot,Vector3 facing,int seed,float quality,float floor)
        {
            if(fx==null||!f.Dropping)return;var white=Vector3.One;var fire=NativeEntranceFx.Hex(0xffb35a,2.6f);float t=f.Age;int s=seed*97;
            var b=spot+f.Offset;var dir=-Vector3.Normalize(new Vector3(f.Offset.X,Math.Max(1,f.Offset.Y),f.Offset.Z));
            if(f.Land<0)
            {
                // re-entry: a fireball under the heat shield and a trail along the line it came down
                if(f.Heat>.05f){fx.Glow(b+dir*.1f,white*(3*f.Heat),0,1.5f,1,0,s+1);fx.Glow(b-dir*.5f,fire*f.Heat,0,3.4f,0,0,s+2);}
                for(int i=0;i<12;i++){if(i%2==1&&Hash(s+3,i)>quality)continue;var p=b-dir*(.6f+i*.55f)+Direction(s+4,i)*.18f;fx.Streak(p,-dir*14,Vector3.Lerp(white*2.6f,fire,i/12f)*(f.Heat*(1-i/14f)),.14f-i*.008f,.5f,.05f,.8f,0,s+10+i);}
                for(int i=0;i<10;i++){if(Hash(s+5,i)>quality)continue;var p=b-dir*(1.5f+i*1.6f)+Direction(s+6,i)*(.2f+.05f*i);fx.Smoke(p,NativeEntranceFx.Hex(0x50545e),.35f*f.Heat*(1-i/11f),1.1f+.25f*i,i/10f,s+30+i,floor);}
            }
            // the retros: four short blue-white jets under the hull, the ground lit as it comes in
            if(f.Retro>.02f)
            {
                var jet=NativeEntranceFx.Hex(0x9fd8ff,3.2f)*f.Retro;
                for(int i=0;i<4;i++){double a=i*Math.PI/2+Math.PI/4;var o=b+new Vector3(.55f*(float)Math.Sin(a),.05f,.55f*(float)Math.Cos(a));var down=new Vector3(.35f*(float)Math.Sin(a),-1,.35f*(float)Math.Cos(a));
                    fx.Streak(o+down*.5f,down*9,jet,.16f,.6f,.05f,.9f,0,s+60+i);fx.Glow(o,jet*.6f,0,.6f,1,0,s+64+i);}
                if(f.Offset.Y<6)fx.Glow(spot+new Vector3(0,.05f,0),jet*(.35f*(1-f.Offset.Y/6)),0,2.6f,0,0,s+68);
            }
            // the landing: a dust ring, sparks, steam from the sides
            if(f.Land>=0)
            {
                float born=t-f.Land;
                NativeEntranceFx.GroundDust(fx,t,born,spot,26,.9f,3.2f,2.2f,NativeEntranceFx.Hex(0x878f9e),.6f,.6f,s+100,quality,floor);
                NativeEntranceFx.Sparks(fx,t,born,spot+new Vector3(0,.1f,0),new Vector3(0,1,0),1.5f,26,6f,.8f,9.8f,.8f,white*3,fire,.022f,s+140,quality,floor);
                var side=new Vector3(facing.Z,0,-facing.X);
                NativeEntranceFx.Jet(fx,t,born+.1f,born+1.8f,spot+side*.78f+new Vector3(0,.5f,0),side+new Vector3(0,.15f,0),14,1.6f,1f,.3f,NativeEntranceFx.Hex(0xc9ced8),.4f,s+180,quality,floor,.5f,.18f);
                NativeEntranceFx.Jet(fx,t,born+.1f,born+1.8f,spot-side*.78f+new Vector3(0,.5f,0),-side+new Vector3(0,.15f,0),14,1.6f,1f,.3f,NativeEntranceFx.Hex(0xc9ced8),.4f,s+200,quality,floor,.5f,.18f);
            }
            // the door: its seam flares, then sparks and a puff as it drops open
            var doorway=spot+facing*.74f+new Vector3(0,1.05f,0);
            if(f.Seam>.01f&&f.Door<.05f)for(int i=0;i<6;i++)fx.Glow(doorway+new Vector3(facing.Z,0,-facing.X)*(Signed(s+220,i)*.27f)+new Vector3(0,Signed(s+221,i)*.78f,0),white*(1.6f*f.Seam),0,.3f,1,0,s+222+i);
            float open=t-DoorAt;
            if(open>=0&&open<1.2f)
            {
                NativeEntranceFx.Sparks(fx,t,DoorAt,doorway,facing+new Vector3(0,.3f,0),.9f,22,4.2f,.6f,9.8f,1f,white*3,fire,.02f,s+240,quality,floor);
                NativeEntranceFx.Jet(fx,t,DoorAt,DoorAt+.14f,doorway,facing+new Vector3(0,.2f,0),60,1.6f,1.2f,.36f,NativeEntranceFx.Hex(0xb8bcc6),.45f,s+280,quality,floor,.3f,.45f);
            }
        }

        // ---- the pod's look: paint, trim, lights (the Locker's Pod tab; prefs.pod) ----
        /// <summary>The look's entries, in canonical order: "paint:rrggbb,trim:rrggbb,light:rrggbb" (six lowercase hex digits,
        /// only the ones set; "" the default: the pilot's body colour, gunmetal trim, the pilot's accent in the lights).</summary>
        public static readonly string[] Keys={"paint","trim","light"};
        public const int DefaultTrim=0x3a4152;
        /// <summary>The Locker's swatches for the paint and trim (the lights use the accent swatches).</summary>
        public static readonly int[] Paints={0xe5e7eb,0x2a2f3a,0xd9433b,0xf08a24,0xf2c94c,0x58c472,0x2fb5a8,0x3a86d9,0x6c5ce7,0xd65db1,0x8a6a4f,0x0f1115};
        public static string Normalize(string look)
        {
            if(string.IsNullOrEmpty(look))return "";var v=new int[Keys.Length];for(int i=0;i<v.Length;i++)v[i]=-1;
            foreach(var entry in look.Split(','))
            {
                int colon=entry.IndexOf(':');if(colon<=0)continue;int k=Array.IndexOf(Keys,entry.Substring(0,colon).Trim());if(k<0)continue;
                int rgb=Hex(entry.Substring(colon+1).Trim());if(rgb>=0)v[k]=rgb;
            }
            return Join(v);
        }
        /// <summary>The look's entry `key` (0xRRGGBB), or -1 when it has none.</summary>
        public static int Of(string look,string key)
        {
            if(string.IsNullOrEmpty(look))return -1;int result=-1;
            foreach(var entry in look.Split(',')){int colon=entry.IndexOf(':');if(colon>0&&entry.Substring(0,colon).Trim()==key){int rgb=Hex(entry.Substring(colon+1).Trim());if(rgb>=0)result=rgb;}}
            return result;
        }
        /// <summary>The look with `key` set to `rgb` (negative: back to the default), normalized.</summary>
        public static string With(string look,string key,int rgb)
        {
            int k=Array.IndexOf(Keys,key);look=Normalize(look);if(k<0)return look;var v=new int[Keys.Length];for(int i=0;i<v.Length;i++)v[i]=Of(look,Keys[i]);v[k]=rgb<0?-1:rgb&0xffffff;return Join(v);
        }
        /// <summary>The colours a pod draws in (0xRRGGBB): the look's, else the pilot's body colour (paint), gunmetal (trim) and
        /// the pilot's accent (lights). `body` and `accent` are the seat's "#rrggbb" strings.</summary>
        public static void Colors(string look,string body,string accent,out int paint,out int trim,out int light)
        {
            paint=Of(look,"paint");trim=Of(look,"trim");light=Of(look,"light");
            if(paint<0)paint=Hex((body??"").TrimStart('#'));if(paint<0)paint=0xe5e7eb;if(trim<0)trim=DefaultTrim;
            if(light<0)light=Hex((accent??"").TrimStart('#'));if(light<0)light=0x70e1d0;
        }
        private static string Join(int[] v){var sb=new System.Text.StringBuilder();for(int i=0;i<Keys.Length;i++){if(v[i]<0)continue;if(sb.Length>0)sb.Append(',');sb.Append(Keys[i]).Append(':').Append(v[i].ToString("x6"));}return sb.ToString();}
        private static int Hex(string s)
        {
            if(s==null||s.Length!=6)return -1;int v=0;foreach(char c in s){int d=c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:c>='A'&&c<='F'?c-'A'+10:-1;if(d<0)return -1;v=v*16+d;}return v;
        }

        // ---- the LED name sign ----
        /// <summary>The sign's dot grid: columns across the lit face (ten letters of 5 + 1 dots), rows (7 and a margin each side).</summary>
        public const int SignColumns=60,SignRows=9,MaxName=20;
        /// <summary>The name as the sign spells it: upper case, letters the font lacks as '?', trimmed to MaxName; "PILOT" when
        /// empty.</summary>
        public static string SignText(string name)
        {
            var sb=new System.Text.StringBuilder();foreach(char c0 in (name??"").Trim()){char c=char.ToUpperInvariant(c0);sb.Append(Glyph(c)!=null?c:'?');if(sb.Length>=MaxName)break;}
            var t=sb.ToString().Trim();return t.Length==0?"PILOT":t;
        }
        /// <summary>The sign's dot columns for `text` (a letter 5 dots and 1 gap); text wider than the sign scrolls.</summary>
        public static int TextColumns(string text)=>Math.Max(0,(text??"").Length*6-1);
        public static bool Scrolls(string text)=>TextColumns(text)>SignColumns;
        /// <summary>
        /// The sign as an LED dot texture laid along v (Runtime scrolls it along the sign: the screen shader scrolls v), RGBA
        /// bytes, bottom row first: `dot` pixels per LED, round lit dots in white and the unlit ones faint. Width SignRows x dot;
        /// height (the sign's length) the sign's columns (or the text's and a gap, when it scrolls) x dot. Column 0 is the
        /// sign's left end at v 0.
        /// </summary>
        public static byte[] SignPixels(string text,int dot,out int width,out int height)
        {
            text=text??"";bool scroll=Scrolls(text);int cols=scroll?TextColumns(text)+12:SignColumns,start=scroll?0:(SignColumns-TextColumns(text))/2;
            width=SignRows*dot;height=cols*dot;var o=new byte[width*height*4];float r=dot*.42f,c=(dot-1)*.5f;
            for(int col=0;col<cols;col++)for(int row=0;row<SignRows;row++)
            {
                bool lit=Lit(text,col-start,row-1);byte a=lit?(byte)255:(byte)24;
                for(int py=0;py<dot;py++)for(int px=0;px<dot;px++)
                {
                    float dx=px-c,dy=py-c,d=(float)Math.Sqrt(dx*dx+dy*dy);float k=Clamp01((r-d)/1.2f+.5f);if(k<=0)continue;
                    // the texture's x runs across the sign (its rows, top row at x 0), y along it (columns)
                    int x=row*dot+px,y=col*dot+py,i=(y*width+x)*4;o[i]=o[i+1]=o[i+2]=255;o[i+3]=(byte)(a*k);
                }
            }
            return o;
        }
        /// <summary>Whether text column `col` (0 the first letter's left), row `row` (0 the top of a letter) is lit.</summary>
        public static bool Lit(string text,int col,int row)
        {
            if(col<0||row<0||row>6||text==null)return false;int i=col/6,x=col%6;if(i>=text.Length||x==5)return false;var g=Glyph(text[i]);
            return g!=null&&(g[row]>>(4-x)&1)!=0;
        }
        // A 5 x 7 font: rows top to bottom, five bits each (bit 4 the left column).
        private static byte[] Glyph(char c)
        {
            if(c>='A'&&c<='Z')return Letters[c-'A'];if(c>='0'&&c<='9')return Digits[c-'0'];
            switch(c){case ' ':return Blank;case '-':return Dash;case '_':return Under;case '.':return Dot;case '!':return Bang;case '?':return Query;case '\'':return Tick;case '#':return Hash5;case '*':return Star;case '+':return Plus;case '/':return Slash;case ':':return Colon;}
            return null;
        }
        private static readonly byte[] Blank={0,0,0,0,0,0,0},Dash={0,0,0,0x1F,0,0,0},Under={0,0,0,0,0,0,0x1F},Dot={0,0,0,0,0,0x0C,0x0C},Bang={0x04,0x04,0x04,0x04,0x04,0,0x04},
            Query={0x0E,0x11,0x01,0x02,0x04,0,0x04},Tick={0x04,0x04,0x08,0,0,0,0},Hash5={0x0A,0x0A,0x1F,0x0A,0x1F,0x0A,0x0A},Star={0,0x04,0x15,0x0E,0x15,0x04,0},
            Plus={0,0x04,0x04,0x1F,0x04,0x04,0},Slash={0x01,0x01,0x02,0x04,0x08,0x10,0x10},Colon={0,0x0C,0x0C,0,0x0C,0x0C,0};
        private static readonly byte[][] Letters={
            new byte[]{0x0E,0x11,0x11,0x1F,0x11,0x11,0x11},new byte[]{0x1E,0x11,0x11,0x1E,0x11,0x11,0x1E},new byte[]{0x0E,0x11,0x10,0x10,0x10,0x11,0x0E},new byte[]{0x1E,0x11,0x11,0x11,0x11,0x11,0x1E},
            new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x1F},new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x10},new byte[]{0x0E,0x11,0x10,0x17,0x11,0x11,0x0F},new byte[]{0x11,0x11,0x11,0x1F,0x11,0x11,0x11},
            new byte[]{0x0E,0x04,0x04,0x04,0x04,0x04,0x0E},new byte[]{0x07,0x02,0x02,0x02,0x02,0x12,0x0C},new byte[]{0x11,0x12,0x14,0x18,0x14,0x12,0x11},new byte[]{0x10,0x10,0x10,0x10,0x10,0x10,0x1F},
            new byte[]{0x11,0x1B,0x15,0x15,0x11,0x11,0x11},new byte[]{0x11,0x11,0x19,0x15,0x13,0x11,0x11},new byte[]{0x0E,0x11,0x11,0x11,0x11,0x11,0x0E},new byte[]{0x1E,0x11,0x11,0x1E,0x10,0x10,0x10},
            new byte[]{0x0E,0x11,0x11,0x11,0x15,0x12,0x0D},new byte[]{0x1E,0x11,0x11,0x1E,0x14,0x12,0x11},new byte[]{0x0F,0x10,0x10,0x0E,0x01,0x01,0x1E},new byte[]{0x1F,0x04,0x04,0x04,0x04,0x04,0x04},
            new byte[]{0x11,0x11,0x11,0x11,0x11,0x11,0x0E},new byte[]{0x11,0x11,0x11,0x11,0x11,0x0A,0x04},new byte[]{0x11,0x11,0x11,0x15,0x15,0x15,0x0A},new byte[]{0x11,0x11,0x0A,0x04,0x0A,0x11,0x11},
            new byte[]{0x11,0x11,0x11,0x0A,0x04,0x04,0x04},new byte[]{0x1F,0x01,0x02,0x04,0x08,0x10,0x1F}};
        private static readonly byte[][] Digits={
            new byte[]{0x0E,0x11,0x13,0x15,0x19,0x11,0x0E},new byte[]{0x04,0x0C,0x04,0x04,0x04,0x04,0x0E},new byte[]{0x0E,0x11,0x01,0x02,0x04,0x08,0x1F},new byte[]{0x1E,0x01,0x01,0x0E,0x01,0x01,0x1E},
            new byte[]{0x02,0x06,0x0A,0x12,0x1F,0x02,0x02},new byte[]{0x1F,0x10,0x1E,0x01,0x01,0x11,0x0E},new byte[]{0x06,0x08,0x10,0x1E,0x11,0x11,0x0E},new byte[]{0x1F,0x01,0x02,0x04,0x08,0x08,0x08},
            new byte[]{0x0E,0x11,0x11,0x0E,0x11,0x11,0x0E},new byte[]{0x0E,0x11,0x11,0x0F,0x01,0x02,0x0C}};

        // ---- hashing ----
        private static float Hash(int a,int b){unchecked{uint h=(uint)a*0x9E3779B1u+(uint)b*0x85EBCA77u+0x27D4EB2Fu;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return (h>>8)*(1f/16777216f);}}
        private static float Signed(int a,int b)=>Hash(a,b)*2-1;
        private static Vector3 Direction(int a,int b){var v=new Vector3(Signed(a,b*3),Signed(a,b*3+1),Signed(a,b*3+2));return v.LengthSquared()>1e-6f?Vector3.Normalize(v):Vector3.UnitY;}
    }
}

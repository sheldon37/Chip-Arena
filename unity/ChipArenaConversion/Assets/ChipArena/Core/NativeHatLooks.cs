using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The cosmetic gate (NativeCosmeticRules.Allowed; engine-free so the managed checks run it): an id is allowed when it is
    // public, or a dev- item / the Auditor Halo with a grant for "type:id" (DEV access grants everything), and the Gibber, the
    // Sheldon Head and the Sheldon Intro need live DEV access itself (no grant can hand them out). The host normalises every joining seat with the
    // peer's verified access, so a non-DEV request for them becomes no hat / the default effect.
    public static class NativeCosmeticGate
    {
        public const string SheldonIntro="sheldon-intro";
        public static bool DevOnly(string id)=>id=="dev-gibber"||id==NativeHatLooks.SheldonHead||id==SheldonIntro;
        public static bool Restricted(string id)=>id!=null&&(id.StartsWith("dev-",StringComparison.Ordinal)||id=="auditor-halo"||id==NativeHatLooks.SheldonHead||id==SheldonIntro);
        public static bool Allowed(NativeAccess a,string type,string id,double now)=>id!=null&&(!DevOnly(id)||(a??NativeAccess.Guest).Developer)&&(!Restricted(id)||(a??NativeAccess.Guest).Allows(type+":"+id,now));
    }

    // Headwear and backdrop colours (owner 2026-10-12: "Lets make Hats color-customizable and backdrop (Instead of
    // autosetting)"). Two optional picks travel with the pilot like the backdrop key (prefs.hatColor / prefs.backColor ->
    // profile cosmetics -> host seat -> lobby and match seat frames): "" is Auto, anything else is "#rrggbb".
    //  - Auto keeps today's look exactly: a hat keeps its authored colours (the bank's "hat-<id>" model, queued as before),
    //    and the backdrop takes the accent colour.
    //  - A hat colour recolours the hat's main material and keeps its details (a cap's brim, a top hat's band, a wizard's
    //    star): the runtime builds one tintable twin of each hat model ("hat-<id>+tint") whose main vertices carry the body
    //    tag (emission.w 1) with their brightness relative to the main colour, and queues it with the colour as the body tint.
    //  - The Sheldon Head's glow (face, ring and trim) always takes the body tint: the hat colour, or cyan on Auto.
    //  - A backdrop colour replaces the accent colour as the backdrop's tint.
    // Presentation only: the authority never reads these except to normalise them.
    public static class NativeHatLooks
    {
        public const string Auto="";
        /// <summary>A seat frame's longest colour ("#rrggbb"; NativeGameFrame.Valid rejects longer ones).</summary>
        public const int MaxLength=7;
        public const string SheldonHead="sheldon-head";
        /// <summary>The bank's tintable twin of a hat model, and the Sheldon Head's shell without the animated face and ring.</summary>
        public const string TintSuffix="+tint",LiveSuffix="+live";
        /// <summary>The Sheldon Head's glow on Auto: the cyber cyan (NativeCyberTheme.CyanHex).</summary>
        public const int SheldonGlow=0x22e6ff;

        // ---- Normalisation -------------------------------------------------------------------------------------------
        /// <summary>"" (Auto) for null, empty or anything but #rrggbb; else the colour in lower case.</summary>
        public static string Normalize(string value)
        {
            if(value==null||value.Length!=7||value[0]!='#')return Auto;
            for(int i=1;i<7;i++){char c=value[i];if(!(c>='0'&&c<='9'||c>='a'&&c<='f'||c>='A'&&c<='F'))return Auto;}
            bool lower=true;for(int i=1;i<7;i++)if(value[i]>='A'&&value[i]<='F')lower=false;
            return lower?value:value.ToLowerInvariant();
        }
        public static bool IsAuto(string value)=>Normalize(value).Length==0;
        /// <summary>The colour as 0xRRGGBB, or -1 on Auto.</summary>
        public static int Rgb(string value)
        {
            if(value==null||value.Length!=7||value[0]!='#')return -1;int rgb=0;
            for(int i=1;i<7;i++){char c=value[i];int d=c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:c>='A'&&c<='F'?c-'A'+10:-1;if(d<0)return -1;rgb=rgb<<4|d;}
            return rgb;
        }
        public static string Hex(int rgb)=>"#"+(rgb&0xffffff).ToString("x6");

        // ---- Palettes (the Locker's swatches; the cyber cyan first) ---------------------------------------------------
        public static readonly string[] HatSwatches={"#22e6ff","#ff3dcf","#8b5cff","#4dffa6","#ffc24d","#ff4d6d","#ff6b35","#3b82f6","#f4f4f5","#9ca3af","#2a2f3d","#0b0d14"};
        public static readonly string[] BackSwatches={"#22e6ff","#7ff3ff","#ff3dcf","#8b5cff","#4dffa6","#ffc24d","#ff4d6d","#ff6b35","#3b82f6","#f9a8d4","#f4f4f5","#6b7280"};

        // ---- The custom control: hue and shade (one slider from black through the full colour to a pastel) -------------
        public const float FullShade=.5f,PastelSaturation=.85f;
        public static int Custom(float hue,float shade)
        {
            hue-=(float)Math.Floor(hue);shade=Clamp01(shade);
            float s=shade<=FullShade?1:1-(shade-FullShade)/(1-FullShade)*PastelSaturation,v=shade<=FullShade?shade/FullShade:1;
            return FromHsv(hue,s,v);
        }
        /// <summary>The control's position for a colour (the inverse of Custom where it can be; greys sit on hue 0).</summary>
        public static void Decompose(int rgb,out float hue,out float shade)
        {
            ToHsv(rgb,out hue,out float s,out float v);
            shade=v<.999f?v*FullShade:FullShade+(1-s)/PastelSaturation*(1-FullShade);shade=Clamp01(shade);
        }
        public static int FromHsv(float h,float s,float v)
        {
            h=(h-(float)Math.Floor(h))*6;int i=(int)Math.Floor(h)%6;float f=h-(float)Math.Floor(h),p=v*(1-s),q=v*(1-s*f),t=v*(1-s*(1-f));
            float r,g,b;switch(i){case 0:r=v;g=t;b=p;break;case 1:r=q;g=v;b=p;break;case 2:r=p;g=v;b=t;break;case 3:r=p;g=q;b=v;break;case 4:r=t;g=p;b=v;break;default:r=v;g=p;b=q;break;}
            return Byte(r)<<16|Byte(g)<<8|Byte(b);
        }
        public static void ToHsv(int rgb,out float h,out float s,out float v)
        {
            float r=(rgb>>16&255)/255f,g=(rgb>>8&255)/255f,b=(rgb&255)/255f,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;
            v=max;s=max<=0?0:d/max;h=0;
            if(d>1e-6f){h=max==r?(g-b)/d:max==g?2+(b-r)/d:4+(r-g)/d;h/=6;if(h<0)h+=1;}
        }
        private static int Byte(float c)=>(int)Math.Round(Clamp01(c)*255,MidpointRounding.AwayFromZero);

        // ---- Hat recolouring -------------------------------------------------------------------------------------------
        // Each hat's main material(s) as authored (client/cosmetics.ts dressRig, sRGB): the first is the reference the picked
        // colour lands on exactly; a darker or lighter part of the same material keeps its brightness relative to it (a
        // beanie's cuff, a wizard's brim). Everything else (brims, bands, stars, jewels, the pom, the stem) keeps its colour.
        // The Auditor halo's bank model (its fallback without the halo shader) recolours its red; the drawer re-hues.
        private static readonly Dictionary<string,int[]> MainColours=new Dictionary<string,int[]>
        {
            {"cap",new[]{0xff6b35}},{"beanie",new[]{0xd277ff,0xb760e8}},{"top",new[]{0x191a20,0x15151a}},{"wizard",new[]{0x904bea,0x6334a6}},
            {"crown",new[]{0xffd269,0xffbf3e}},{"cone",new[]{0xff6b35,0xff662e}},{"antenna",new[]{0xff4949}},{"halo",new[]{0xffd971}},
            {"cowboy",new[]{0xbc8352,0x8e5938}},{"auditor-halo",new[]{0xff1735,0xff1835}},
        };
        /// <summary>Whether a picked colour changes this hat (none and unknown hats ignore it).</summary>
        public static bool Colorable(string hat)=>hat!=null&&(hat==SheldonHead||MainColours.ContainsKey(hat));
        public static IEnumerable<string> TintableHats=>MainColours.Keys;
        public static int[] Mains(string hat)=>hat!=null&&MainColours.TryGetValue(hat,out var m)?m:Array.Empty<int>();
        /// <summary>The bank model to queue for a hat in a colour: the authored model on Auto (exactly as before), else the tintable twin.</summary>
        public static string Model(string hat,string color)
        {
            if(string.IsNullOrEmpty(hat))return null;
            if(hat==SheldonHead||!MainColours.ContainsKey(hat)||IsAuto(color))return "hat-"+hat;
            return "hat-"+hat+TintSuffix;
        }
        /// <summary>The body tint (0xRRGGBB) the hat is queued with, or -1 for none (Auto on a plain hat: the call is unchanged).</summary>
        public static int Tint(string hat,string color)
        {
            if(hat==SheldonHead){int c=Rgb(color);return c<0?SheldonGlow:c;}
            if(hat==null||!MainColours.ContainsKey(hat))return -1;return Rgb(color);
        }
        /// <summary>The backdrop's tint: the picked backdrop colour, else the accent (Auto: today's behaviour).</summary>
        public static int Back(string backColor,int accent){int c=Rgb(backColor);return c<0?accent:c;}

        /// <summary>
        /// A bank vertex of `hat` (linear colour): whether it belongs to the main material, and its brightness relative to the
        /// reference (the tintable twin's grey level). Matching is within a hair of the exported value (6 decimals).
        /// </summary>
        public static bool IsMain(string hat,float r,float g,float b,out float level)
        {
            level=1;var mains=Mains(hat);if(mains.Length==0)return false;
            foreach(int hex in mains)
            {
                var c=NativePortraitRules.Linear(hex);
                if(Math.Abs(c.X-r)<2e-3f&&Math.Abs(c.Y-g)<2e-3f&&Math.Abs(c.Z-b)<2e-3f){level=Luma(r,g,b)/Math.Max(1e-6f,Luma(NativePortraitRules.Linear(mains[0])));return true;}
            }
            return false;
        }
        /// <summary>The emission a main vertex keeps in the twin: its brightness relative to the reference (0 when it had none).</summary>
        public static float EmissionLevel(string hat,float r,float g,float b)
        {var mains=Mains(hat);if(mains.Length==0)return 0;float l=Luma(r,g,b);return l<=0?0:l/Math.Max(1e-6f,Luma(NativePortraitRules.Linear(mains[0])));}
        public static float Luma(float r,float g,float b)=>.2126f*r+.7152f*g+.0722f*b;
        public static float Luma(System.Numerics.Vector3 c)=>Luma(c.X,c.Y,c.Z);

        /// <summary>
        /// The Auditor halo's palette re-hued to a picked colour (its drawer's material colours, sRGB): each keeps its own
        /// saturation and brightness and takes the colour's hue, so the halo stays the same halo in another colour. Auto: as authored.
        /// </summary>
        public static int Rehue(int authored,string color)
        {
            int c=Rgb(color);if(c<0)return authored;ToHsv(c,out float hue,out float cs,out _);ToHsv(authored,out _,out float s,out float v);
            return FromHsv(hue,Math.Min(s,Math.Max(cs,.35f)),v);
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }
}

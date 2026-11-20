using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The dev intro's name card, the "architect" effect (Core NativeIntroArchitect.Card; docs/INTRO-DEV-ARCHITECT.md "The name").
    /// The sky's forged name is the hero; the card is its small caption under the architect (Core sets its size). The live name is
    /// built a letter at a time in step with the letters forged in the sky: each letter's slot first runs through code (characters
    /// cycling, a cursor under it), then locks to the letter in a white flash that cools to gold, its colour split and a slice of
    /// it thrown sideways, both settling to nothing; a light sweeps the finished name and two ticks close round it. Reduced
    /// effects: no cycling and no fast flashes (each letter fades in from a soft blur, the split and the slice stay small). Drawn in
    /// place of the plain name; allocation-free.
    /// </summary>
    public sealed class NativeArchitectCard:INativeIntroCardEffect
    {
        private static readonly string[] Code={"0","1","<",">","/","\\","[","]","#","=","+","*","%","&","$","{","}","7","4","X"};
        private static readonly Color Ink=new Color(1f,.96f,.86f),Hot=new Color(1,1,1),Gold=new Color(1f,.8f,.42f),Cyan=new Color(.55f,.95f,1f),Rose=new Color(1f,.36f,.52f),Shadow=new Color(0,0,0,.75f);
        /// <summary>When letter `i`'s slot starts to decode and when it locks (card age, s): the sky's pens' pace (letter i is written
        /// from NativeIntroArchitect.LetterAt(i) for LetterTime).</summary>
        public static float Start(int i)=>NativeIntroArchitect.NameStart-NativeIntroArchitect.CardAt+NativeIntroArchitect.LetterStep*i;
        public static float Lock(int i)=>Start(i)+NativeIntroArchitect.LetterTime;
        private static float Hash(int a,int b){uint h=(uint)a*0x9E3779B1u^(uint)b*0x85EBCA77u;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;return (h&0xffff)/65536f;}
        public bool Draw(in NativeIntroCardFrame f,bool before)
        {
            float a=f.Card.EffectA,distort=Mathf.Clamp01(f.Card.EffectB),alpha=f.Alpha;int n=f.Letters.Length;if(n==0)return false;
            bool reduced=f.Reduced;var box=f.Box;float size=f.Size;
            if(before)
            {
                for(int i=0;i<n;i++)
                {
                    string letter=f.Letters[i];if(letter==" ")continue;var r=new Rect(box.x+f.X[i],box.y,f.Widths[i]+4,box.height);float s=Start(i),l=Lock(i);
                    if(a<s)continue;
                    if(a<l)
                    {
                        // decoding: the slot runs through code (held still in Reduced effects), a cursor under it
                        float u=(a-s)/(l-s);string code=Code[(int)(Hash(i,reduced?0:(int)(a*24))*Code.Length)];
                        U.Text(r,code,size*.86f,U.A(Cyan,(.35f+.45f*u)*alpha),TextAnchor.MiddleLeft,bold:true,mono:true,glow:U.A(Cyan,.3f*alpha));
                        U.Fill(new Rect(r.x,box.yMax-size*.12f,Mathf.Max(3,f.Widths[i]*.9f),Mathf.Max(2,size*.05f)),U.A(Cyan,.8f*alpha));
                        continue;
                    }
                    float age=a-l,fresh=Mathf.Exp(-age*(reduced?4:6))*Mathf.Lerp(.35f,1,distort);
                    // the colour cools from white heat to the name's gold-white; the lock's swell settles
                    var ink=Color.Lerp(Hot,Ink,Mathf.Clamp01(age*2.2f));float scale=reduced?1:1+.16f*Mathf.Exp(-age*12);float o=reduced?Mathf.Clamp01(age/.25f):1;
                    float split=size*.075f*fresh*(reduced?.35f:1);
                    if(split>.4f){var ra=r;ra.x+=split;var rb=r;rb.x-=split;U.Text(ra,letter,size,U.A(Rose,.55f*fresh*alpha*o),TextAnchor.MiddleLeft,bold:true);U.Text(rb,letter,size,U.A(Cyan,.6f*fresh*alpha*o),TextAnchor.MiddleLeft,bold:true);}
                    U.Push(r.center,Vector2.zero,scale);
                    U.Text(r,letter,size,U.A(ink,alpha*o),TextAnchor.MiddleLeft,bold:true,glow:U.A(Color.Lerp(Hot,Gold,Mathf.Clamp01(age*1.5f)),(.55f+.45f*fresh)*alpha*o),shadow:U.A(Shadow,alpha*o),blur:reduced?6*(1-o):0);
                    U.Pop();
                    // a slice of the letter thrown sideways, settling
                    float slip=size*.14f*fresh*(Hash(i,7)>.5f?1:-1)*(reduced?.3f:1);
                    if(Mathf.Abs(slip)>.6f)
                    {
                        float y0=box.y+box.height*(.3f+.3f*Hash(i,3)),hgt=box.height*(.12f+.1f*Hash(i,5));U.BeginBand(new Rect(r.x-size,y0,r.width+size*2,hgt));
                        var rs=r;rs.x+=slip;U.Text(rs,letter,size,U.A(Hot,.9f*alpha*fresh),TextAnchor.MiddleLeft,bold:true);U.EndBand();
                    }
                }
                return true;
            }
            // after the name: a scan line riding the letters as they decode; the sweep of light over the finished name; the ticks
            float last=Lock(n-1);
            if(a<last+.12f){int at=Mathf.Clamp((int)((a-Start(0))/NativeIntroArchitect.LetterStep),0,n-1);float x=box.x+Mathf.Clamp(f.X[at]+f.Widths[at],0,box.width);U.Fill(new Rect(x,box.y+box.height*.12f,Mathf.Max(2,size*.04f),box.height*.76f),U.A(Cyan,.85f*alpha));}
            float sweep=Mathf.Clamp01((a-(last+.55f))/.7f);
            if(sweep>0&&sweep<1){float sx=box.x-40+(box.width+80)*sweep;U.HGradient(new Rect(sx-70,box.y,70,box.height),new Color(1,1,1,0),U.A(new Color(1,.95f,.8f),.32f*alpha*Mathf.Sin(sweep*Mathf.PI)));}
            float ticks=Mathf.Clamp01((a-last)/.35f)*alpha;
            if(ticks>0)
            {
                float h=box.height*.7f,w=Mathf.Max(2,size*.035f),gap=size*.32f,y=box.center.y-h*.5f;var c=U.A(Gold,.75f*ticks);
                U.Fill(new Rect(box.x-gap-w,y,w,h),c);U.Fill(new Rect(box.x-gap-w,y,size*.18f,w),c);U.Fill(new Rect(box.x-gap-w,y+h-w,size*.18f,w),c);
                U.Fill(new Rect(box.xMax+gap,y,w,h),c);U.Fill(new Rect(box.xMax+gap+w-size*.18f,y,size*.18f,w),c);U.Fill(new Rect(box.xMax+gap+w-size*.18f,y+h-w,size*.18f,w),c);
            }
            return false;
        }
    }
}

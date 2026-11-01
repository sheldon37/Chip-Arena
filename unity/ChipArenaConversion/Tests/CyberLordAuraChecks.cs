using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using ChipArena.UnityConversion;

// The DEV Cyber Lord's two skins (owner 2026-10-13: "Change Cyber lord weapon to instead just show an aura around the hands
// (Like a glitchy / cyber aura)", "(We can use this current model as a prototype)"): Standard, the glitch aura round the hands
// (Core/NativeCyberLordAura.cs), and the Prototype Series, today's hardlight gauntlets (Core/NativeCyberLordLook.cs Pieces).
//  1. the skin map carries the "cyberlord" pick; a missing, unknown or malformed value reads as Standard;
//  2. the Prototype Series draws exactly the shared head's pieces (pinned digest);
//  3. Standard draws no gauntlet, fist shell or emitter piece, keeps the seams, crest and shimmer, and its aura hugs the hands;
//  4. the aura's parts and signals (ignition, flare, mana, guard, ranged emitters on the casts' anchor);
//  5. determinism, tiers (reduced never strobes) and no allocation;
//  6. the Runtime wiring (source text; Unity cannot run here).
// Pixels are checked by the offline renders, not here.
internal static class CyberLordAuraChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Cyber Lord aura: "+what);count++;}
    /// <summary>NativeCyberLordLook.Pieces over ProtoFrames on the shared head ae3bbbb (before the skins), each value rounded to
    /// 1e-5, computed by the scratch digest harness from the original code.</summary>
    private const string PrototypeDigest="cadec07d97a6d3f4863a7538695603aaeede9c530156acb15603b6a4a5fd76f9";private const int PrototypePieces=15341;
    private static readonly int L=CapturedRig.LHand,R=CapturedRig.RHand,LE=CapturedRig.LElbow,RE=CapturedRig.RElbow;
    private static bool HandBone(int b)=>b==L||b==R||b==LE||b==RE;
    private static NativeCyberLordLook.Flare FlareAt(float t){float strike=(float)Math.Floor(t*2)/2;return NativeCyberLordLook.FlareAt(t,strike,(int)(t*2)%3,strike+.1f);}

    // The digest's frames (the scratch harness runs exactly these): 240 frames over 6 s, formed at 1 s, a combo flare every
    // .5 s, first person, the dash's dither, Reduced and the low tier on their own beats, ranged mode from 3 s.
    private static string ProtoDigest(bool viaSkin,out int total)
    {
        var into=new CyberPiece[NativeCyberLordLook.Capacity];var ms=new MemoryStream();var w=new BinaryWriter(ms);total=0;
        for(int f=0;f<240;f++)
        {
            float t=f/40f;var flare=FlareAt(t);bool first=f%5==0,low=f%4==0;float solid=f%7==0?.4f:1,q=f%3==0?.3f:1;
            var mode=new NativeCyberLordLook.Mode{Ranged=Math.Clamp((t-3)/.5f,0,1),Charge=f%6==0?.7f:0,Glitch=t>=3&&t<3.3f?(float)Math.Sin(Math.PI*(t-3)/.3f):0};
            int n=viaSkin?NativeCyberLordLook.PiecesIn(NativeWeaponSkins.PrototypeIndex,f*13+1,t,1,flare,first,solid,q,low,into,mode,f%9==0?1:0):NativeCyberLordLook.Pieces(t,1,flare,first,solid,q,low,into,mode);
            total+=n;w.Write(n);
            for(int i=0;i<n;i++)
            {
                var p=into[i];w.Write(p.Bone);w.Write((int)p.Shape);
                foreach(float v in new[]{p.Position.X,p.Position.Y,p.Position.Z,p.Scale.X,p.Scale.Y,p.Scale.Z,p.Rotation.X,p.Rotation.Y,p.Rotation.Z,p.Rotation.W,p.Color.X,p.Color.Y,p.Color.Z})w.Write((long)Math.Round(v*1e5));
                if(p.Rise!=0)throw new Exception("Cyber Lord aura: a Prototype piece rises");
            }
        }
        w.Flush();return Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
    }
    private static int Aura(float t,CyberPiece[] into,int seed=1,float formAt=-10,NativeCyberLordLook.Flare flare=default,bool first=false,float solid=1,float q=1,bool low=false,NativeCyberLordLook.Mode mode=default,float guard=0)
        =>NativeCyberLordLook.PiecesIn(NativeWeaponSkins.StandardIndex,seed,t,formAt,flare,first,solid,q,low,into,mode,guard);
    private static float Lum(Vector3 c)=>c.X*.2126f+c.Y*.7152f+c.Z*.0722f;
    private static float HandLight(CyberPiece[] p,int n,int hand,int elbow){float s=0;for(int i=0;i<n;i++)if(p[i].Kind==CyberPiece.Aura&&(p[i].Bone==hand||p[i].Bone==elbow))s+=Lum(p[i].Color)*p[i].Scale.X;return s;}
    /// <summary>An RGB-split pair at i: a magenta and a cyan copy of one shard either side of it across the hand.</summary>
    private static bool SplitPair(CyberPiece[] p,int i,int n)=>i+1<n&&p[i].Kind==CyberPiece.Aura&&p[i+1].Kind==CyberPiece.Aura&&p[i].Rise==0&&p[i+1].Rise==0&&p[i].Bone==p[i+1].Bone&&p[i].Scale==p[i+1].Scale
        &&p[i].Position.Y==p[i+1].Position.Y&&p[i].Position.Z==p[i+1].Position.Z&&Math.Abs(p[i].Position.X-p[i+1].Position.X)>.006f&&p[i].Color.X>p[i].Color.Y&&p[i+1].Color.X<p[i+1].Color.Y;
    private static int Kinds(CyberPiece[] p,int n,byte kind){int c=0;for(int i=0;i<n;i++)if(p[i].Kind==kind)c++;return c;}

    // A recording effects sink for the haze.
    private sealed class Sprites:ICyberFxSink
    {
        public int Count,View,World,Magenta;public double Hash,Light;
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed)=>throw new Exception("Cyber Lord aura: the haze draws sprites only");
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)=>throw new Exception("Cyber Lord aura: the haze draws sprites only");
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor)=>throw new Exception("Cyber Lord aura: the haze draws sprites only");
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile)
        {
            Count++;if(view)View++;else World++;if(color.X>color.Y)Magenta++;Light+=Lum(color)*size;Hash=Hash*1.0000003+at.X*.37+at.Y*.61+at.Z*.89+color.X+color.Y*1.3+size*7+profile;
            if(!(float.IsFinite(at.X)&&float.IsFinite(at.Y)&&float.IsFinite(at.Z)&&float.IsFinite(color.X)&&size>0&&size<.6f))throw new Exception("Cyber Lord aura: an unsane haze sprite");
        }
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard)=>throw new Exception("Cyber Lord aura: the haze draws sprites only");
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color)=>throw new Exception("Cyber Lord aura: the haze draws sprites only");
        public void Voxel(bool view,WeaponShape shape,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color)=>throw new Exception("Cyber Lord aura: the haze draws sprites only");
        public void Reset(){Count=View=World=Magenta=0;Hash=Light=0;}
    }
    // A recording entrance effects sink (the Hardlight Boot's Effects).
    private sealed class EntranceFx:INativeEntranceFx
    {
        public int Glows,Other;public double Hash;
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Glows++;Hash=Hash*1.0000003+at.X+at.Y*.7+color.Y*1.3+size*3+seed*.001;}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Other++;Hash=Hash*1.0000003+head.X+velocity.Y+width;}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Other++;Hash=Hash*1.0000003+at.Z+opacity;}
    }
    private static readonly NativeCyberLordAura.HandPoints Hands=new NativeCyberLordAura.HandPoints{LFist=new Vector3(-.2f,1.2f,.4f),LArm=new Vector3(-.22f,1.05f,.25f),LEmitter=new Vector3(-.2f,1.2f,.53f),
        RFist=new Vector3(.2f,1.25f,.45f),RArm=new Vector3(.22f,1.1f,.3f),REmitter=new Vector3(.2f,1.25f,.58f),Right=Vector3.UnitX,Scale=1};

    public static IEnumerable<string> Run(string root)
    {
        // ------------------------------------------------------------------------------------------------------------------
        // 1. The skin map
        // ------------------------------------------------------------------------------------------------------------------
        count=0;int c0=count;
        const string id=CyberLordSpec.Id;
        Check(NativeWeaponSkins.Skinned(id)&&!NativeWeaponSkins.Modelled(id)&&NativeWeaponSkins.Weapons[^1]==id&&Array.IndexOf(NativeWeaponSkins.Entries,id)==NativeWeaponSkins.Entries.Length-3,"the Cyber Lord is the last skinned entry before the body (and the face gear), and has no weapon model");
        Check(NativeWeaponSkins.Weapons.Count(w=>!NativeWeaponSkins.Modelled(w))==1&&NativeWeaponSkins.Weapons.Where(w=>w!=id).All(NativeWeaponSkins.Modelled),"every other skinned weapon keeps its models");
        Check(NativeWeaponSkins.With("",id,NativeWeaponSkins.Prototype)=="cyberlord:prototype"&&NativeCyberLordAura.Skin("cyberlord:prototype")==NativeWeaponSkins.PrototypeIndex&&NativeWeaponSkins.Of("cyberlord:prototype",id)==NativeWeaponSkins.Prototype,"\"cyberlord:prototype\" round-trips");
        Check(NativeWeaponSkins.With("cyberlord:prototype",id,NativeWeaponSkins.Standard)==""&&NativeWeaponSkins.Normalize(" cyberlord : prototype ")=="cyberlord:prototype"&&NativeWeaponSkins.Normalize("cyberlord:standard")=="","normalized; Standard is the default and never written");
        Check(NativeWeaponSkins.Normalize("body:sheldon,cyberlord:prototype,bow:prototype,sword:prototype")=="sword:prototype,bow:prototype,cyberlord:prototype,body:sheldon","canonical order: the weapons, the Cyber Lord, the body");
        foreach(var bad in new[]{null,"","cyberlord","cyberlord:","cyberlord:gold","cyberlord:prototypes","cyberlordx:prototype","xcyberlord:prototype",":prototype,,","sword:prototype,body:sheldon","pistol:prototype,rifle:prototype,machinegun:prototype,fire:prototype,water:prototype,lightning:prototype,darkness:prototype,sword:prototype,daggers:prototype,shield:prototype,greatsword:prototype,emperor:prototype,ansatsuken:prototype,leet:prototype,bow:prototype,body:prototype"})
            Check(NativeCyberLordAura.Skin(bad)==NativeWeaponSkins.StandardIndex,"a missing or malformed pick reads as the aura ("+(bad??"null")+")");
        Check(NativeWeaponSkins.Normalize("cyberlord:gold,cyberlord:")==""&&NativeWeaponSkins.Normalize("cyberlord:prototype,cyberlord:standard")=="","unknown skins drop, the last pick wins");
        // An older client parses only its own entries: the new one never moves another weapon's or the body's pick.
        Check(NativeWeaponSkins.Index("cyberlord:prototype,sword:prototype","sword")==NativeWeaponSkins.PrototypeIndex&&NativeWeaponSkins.Index("cyberlord:prototype","sword")==NativeWeaponSkins.StandardIndex&&NativeBodySkins.Of("cyberlord:prototype,body:sheldon")==NativeBodySkins.SheldonIndex,"the entry leaves the other picks alone");
        Check(NativeBodySkins.Gate("cyberlord:prototype,body:sheldon",false)=="cyberlord:prototype","the body's DEV gate keeps the Cyber Lord's pick");
        var longest=string.Join(",",NativeWeaponSkins.Entries.Select(e=>e+":"+(e==NativeWeaponSkins.FaceEntry?"visor":"prototype")));Check(NativeWeaponSkins.Normalize(longest)==longest&&longest.Length==309&&longest.Length<=NativeWeaponSkins.MaxLength,"the longest map is 309 characters, inside the frame limit "+NativeWeaponSkins.MaxLength);
        Check(NativeWeaponSkins.Model("weapon-cyberlord",NativeWeaponSkins.StandardIndex)=="weapon-cyberlord"&&NativeWeaponSkins.StandardModel(id,0)==null&&NativeWeaponSkins.Held(id,"",0)=="weapon-cyberlord","no model id is invented for the look-only entry");
        yield return $"Cyber Lord skin map: {count-c0} checks (\"cyberlord\" after the weapons and before the body, round-trips \"cyberlord:prototype\", a missing / unknown / malformed / older map reads as the aura, longest map {longest.Length} of {NativeWeaponSkins.MaxLength})";

        // ------------------------------------------------------------------------------------------------------------------
        // 2. The Prototype Series is today's look, piece for piece
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        var d0=ProtoDigest(false,out int n0);var d1=ProtoDigest(true,out int n1);
        Check(n0==PrototypePieces&&d0==PrototypeDigest,"Pieces draws the shared head's look ("+n0+" pieces, "+d0+")");
        Check(n1==PrototypePieces&&d1==PrototypeDigest,"PiecesIn on the Prototype Series draws it too, whatever the seed or guard ("+n1+" pieces, "+d1+")");
        yield return $"Cyber Lord Prototype Series: {count-c0} checks ({n0} pieces over 240 frames = ae3bbbb, digest {d0.Substring(0,16)})";

        // ------------------------------------------------------------------------------------------------------------------
        // 3. Standard: no gauntlets, the body pieces kept, the aura hugging the hands
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        var a=new CyberPiece[NativeCyberLordLook.Capacity];var p=new CyberPiece[NativeCyberLordLook.Capacity];int frames=0,auraTotal=0,splitPairs=0,strands=0,bands=0;float maxRise=0;
        var emitterAt=NativeCyberLordLook.EmitterAt;
        for(int f=0;f<600;f++)
        {
            float t=2+f/60f;var flare=FlareAt(t);bool first=f%5==0,low=f%4==1;float q=f%3==0?.3f:1;
            var mode=new NativeCyberLordLook.Mode{Ranged=f>=300?Math.Clamp((f-300)/30f,0,1):0,Charge=f%6==0?.7f:0,Glitch=f>=300&&f<318?.8f:0};float guard=f%7<3?1:0;
            int na=Aura(t,a,f%3+1,1,flare,first,1,q,low,mode,guard),np=NativeCyberLordLook.Pieces(t,1,flare,first,1,q,low,p,mode);frames++;
            // The Prototype's gauntlet cages, fist shells and emitters: none of them, nor anything in their place, in the Standard.
            for(int i=0;i<na;i++)
            {
                var x=a[i];Check(x.Kind==CyberPiece.Body||x.Kind==CyberPiece.Aura||x.Kind==CyberPiece.AuraEmitter,"Standard draws no gauntlet, fist shell or emitter piece");
                if(x.Kind==CyberPiece.Body)continue;
                for(int k=0;k<np;k++)if(p[k].Kind!=CyberPiece.Body&&p[k].Bone==x.Bone&&(p[k].Position-x.Position).Length()<1e-4f&&(p[k].Scale-x.Scale).Length()<1e-4f)throw new Exception("Cyber Lord aura: a Prototype piece in the Standard skin at "+x.Position);
            }
            // The seams, crest and shimmer: exactly the Prototype's body pieces, in order.
            int bi=0;for(int k=0;k<np;k++){if(p[k].Kind!=CyberPiece.Body)continue;while(bi<na&&a[bi].Kind!=CyberPiece.Body)bi++;Check(bi<na&&a[bi].Bone==p[k].Bone&&a[bi].Position==p[k].Position&&a[bi].Scale==p[k].Scale&&a[bi].Color==p[k].Color,"the Standard keeps the Prototype's seams, crest and shimmer");bi++;}
            // The aura hugs the hand and the lower forearm: small pieces close to them, the bands thin, the strands short.
            for(int i=0;i<na;i++)
            {
                var x=a[i];if(x.Kind==CyberPiece.Body)continue;auraTotal++;
                Check(HandBone(x.Bone),"aura pieces ride the hands and forearms only");Check(!first||NativeCyberLordLook.ViewBone(x.Bone),"first person keeps the view model's bones");
                Check(float.IsFinite(x.Position.X+x.Position.Y+x.Position.Z+x.Color.X+x.Color.Y+x.Color.Z)&&x.Scale.X>0&&x.Scale.Y>0&&x.Scale.Z>0&&Math.Abs(x.Rotation.Length()-1)<1e-3f&&x.Shape==WeaponShape.Cube,"aura piece sane");
                float big=Math.Max(x.Scale.X,Math.Max(x.Scale.Y,x.Scale.Z)),mid=x.Scale.X+x.Scale.Y+x.Scale.Z-big-Math.Min(x.Scale.X,Math.Min(x.Scale.Y,x.Scale.Z));
                Check(mid<=.04f&&big<=.2f,"no aura piece is a panel or a box: under 4 cm across in two directions ("+x.Scale+")");
                if(x.Bone==L||x.Bone==R)Check((x.Position-new Vector3(0,-.052f,.024f)).Length()<=.3f||(x.Position-emitterAt).Length()<.21f,"hand pieces stay round the fist, the hit's burst within 21 cm of the emitter ("+x.Position+")");
                else Check(x.Position.Y<=-.1f&&x.Position.Y>=-.27f&&new Vector2(x.Position.X,x.Position.Z).Length()<=.22f,"forearm pieces stay on the lower forearm ("+x.Position+")");
                Check(x.Rise>=0&&x.Rise<=.4f,"strands rise a little ("+x.Rise+")");maxRise=Math.Max(maxRise,x.Rise);if(x.Rise>0)strands++;
                if(big>.1f&&x.Scale.Y<.01f)bands++;
            }
            for(int i=0;i+1<na;i++)if(SplitPair(a,i,na))splitPairs++;
        }
        Check(auraTotal>frames*20&&splitPairs>frames/2&&strands>frames&&bands>frames/2&&maxRise>.1f,"the aura has shards, RGB-split pairs, scanlines and rising strands ("+auraTotal+" pieces, "+splitPairs+" split pairs, "+bands+" bands, "+strands+" strand pixels over "+frames+" frames)");
        Check(NativeCyberLordLook.Capacity>=26+NativeCyberLordAura.Max&&NativeCyberLordLook.Capacity>=120,"the look's buffer holds either skin ("+NativeCyberLordLook.Capacity+")");
        yield return $"Cyber Lord Standard skin: {count-c0} checks (no gauntlet, fist shell or emitter piece over {frames} frames, the Prototype's seams, crest and shimmer kept, every aura piece small and on the hands or lower forearms; {auraTotal} pieces, {splitPairs} RGB-split pairs, {bands} scanline bands, strands up to {maxRise*100:0} cm)";

        // ------------------------------------------------------------------------------------------------------------------
        // 4. The parts and the signals
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        {
            // Both hands, the ignition (nothing before the form time, building over FormSeconds), the glitch jumps' schedule.
            int before=Aura(4.999f,a,1,5),mid=0,full=0;for(int f=0;f<20;f++){mid+=Aura(5+NativeCyberLordLook.FormSeconds*.35f+f*.001f,a,1,5);full+=Aura(5+NativeCyberLordLook.FormSeconds+.5f+f*.05f,a,1,5);}
            Check(before==0&&mid<full,"the aura ignites from the form time ("+before+" before, "+mid/20+" building, "+full/20+" lit)");
            int n=Aura(7,a);bool lh=false,rh=false,le=false,re=false;for(int i=0;i<n;i++){lh|=a[i].Bone==L;rh|=a[i].Bone==R;le|=a[i].Bone==LE;re|=a[i].Bone==RE;}Check(lh&&rh&&le&&re,"both hands and both lower forearms");
            int jumps=0,jumpFrames=0;
            for(int f=0;f<60*30;f++){float t=f/60f;int hs=NativeCyberLordAura.HandSeed(3,0);float d=NativeCyberLordAura.Jump(hs,t,out _);if(d!=0){jumpFrames++;if(f==0||NativeCyberLordAura.Jump(hs,t-1/60f,out _)==0)jumps++;Check(Math.Abs(d)>=.012f&&Math.Abs(d)<=.032f,"a jump of 1.2 to 3.2 cm");}}
            Check(jumps>=8&&jumps<=25&&jumpFrames<=jumps*6,"glitch jumps now and then, a step or two each ("+jumps+" in 30 s, "+jumpFrames+" frames)");
            // The striking hand's flare: brighter and a burst of shards; the mana lights both hands; the guard packs it denser.
            float lt=0,rt=0,lb=0,rb=0;int bursts=0,quiet=0,guarded=0,open=0,mana=0;
            for(int f=0;f<120;f++)
            {
                float t=8+f/60f;var jab=new NativeCyberLordLook.Flare{Left=.8f,HitLeft=.8f};n=Aura(t,a,2,-10,jab);lt+=HandLight(a,n,L,LE);rt+=HandLight(a,n,R,RE);
                for(int i=0;i<n;i++)if(a[i].Bone==L&&a[i].Rotation!=Quaternion.Identity&&a[i].Kind==CyberPiece.Aura)bursts++;
                n=Aura(t,a,2);lb+=HandLight(a,n,L,LE);rb+=HandLight(a,n,R,RE);open+=n;for(int i=0;i<n;i++)if(a[i].Rotation!=Quaternion.Identity&&a[i].Kind==CyberPiece.Aura)quiet++;
                guarded+=Aura(t,a,2,-10,default,false,1,1,false,default,1);mana+=Aura(t,a,2,-10,new NativeCyberLordLook.Flare{Charge=1});
            }
            Check(lt>lb*2&&lt>rt*1.6f&&Math.Abs(rt-rb)<rb*.35f,"the jab flares the striking hand only ("+(lt/lb).ToString("0.0")+"x; the other "+(rt/rb).ToString("0.00")+"x)");
            Check(bursts>=120*6&&quiet==0,"a burst of shards off the knuckles with the flare, none without");
            {int clench=0;for(int f=0;f<60;f++){n=Aura(8+f/60f,a,2,-10,CyberLordIdlePose.Clench(new CyberIdleGlitch{LClench=1,RClench=1},default));for(int i=0;i<n;i++)if(a[i].Rotation!=Quaternion.Identity&&a[i].Kind==CyberPiece.Aura)clench++;}Check(clench==0,"the idle's clench brightens the aura but bursts nothing");}
            Check(guarded>open*1.25f&&mana>open,"a guard packs the shimmer denser ("+guarded+" vs "+open+" pieces), the mana adds to it");
            Check(NativeCyberLordAura.Guard(1,1,2)==0&&NativeCyberLordAura.Guard(1.5f,1,2)==1&&NativeCyberLordAura.Guard(2.06f,1,2)>0&&NativeCyberLordAura.Guard(2.2f,1,2)==0&&NativeCyberLordAura.Guard(5,-100,0)==0,"the guard's weight eases in and out of the replicated block");
            // Dithered with the Data Dash's body.
            int solidN=0,ditherN=0;float solidL=0,ditherL=0;for(int f=0;f<60;f++){float t=9+f/60f;solidN+=n=Aura(t,a,4);solidL+=HandLight(a,n,R,RE);ditherN+=n=Aura(t,a,4,-10,default,false,.3f);ditherL+=HandLight(a,n,R,RE);}
            Check(ditherL<solidL*.45f&&Aura(9,a,4,-10,default,false,0)==Aura(9,p,4,-10,default,false,0)&&Aura(9,a,4,-10,default,false,0)<=Kinds(a,Aura(9,a,4,-10,default,false,0),CyberPiece.Body)+0,"the dash's dither dims the aura and a vanished body shows none");
        }
        {
            // Ranged mode: the shards condense on the emitter ahead of each palm (the look's EmitterAt, where the casts start), a
            // core and a turning hex glyph on each hand (the right the main emitter with prongs, the left the support coil).
            Check(NativeCyberLordLook.EmitterAt==new Vector3(0,-.06f,.155f)&&NativeCyberLordFx.EmitterAhead==.09f,"the emitter anchors are unchanged (the look's EmitterAt, the effects' EmitterAhead)");
            var ranged=new NativeCyberLordLook.Mode{Ranged=1};int n=Aura(6,a,1,-10,default,false,1,1,false,ranged);
            Vector3 lcore=default,rcore=default;int lglyph=0,rglyph=0,strand=0;
            for(int i=0;i<n;i++){var x=a[i];if(x.Rise>0)strand++;if(x.Kind!=CyberPiece.AuraEmitter)continue;if(x.Scale.X==x.Scale.Y&&x.Scale.Z<x.Scale.X){if(x.Bone==L)lcore=x.Position;else rcore=x.Position;}else if(x.Bone==L)lglyph++;else if(x.Bone==R)rglyph++;}
            Check(lcore==emitterAt&&rcore==emitterAt,"each palm's emitter point sits on the look's EmitterAt");
            Check(rglyph==9&&lglyph==12,"the right hand's main emitter (hex and three prongs) and the left's support coil (two hexes) ("+rglyph+", "+lglyph+")");
            Check(strand==0,"the strands die down in ranged mode");
            float Spread(NativeCyberLordLook.Mode m){float s=0;int k=0;for(int f=0;f<30;f++){int nn=Aura(6+f/30f,a,1,-10,default,false,1,1,false,m);for(int i=0;i<nn;i++)if(a[i].Bone==R&&a[i].Kind==CyberPiece.Aura&&a[i].Rise==0){s+=(a[i].Position-emitterAt).Length();k++;}}return s/Math.Max(1,k);}
            float melee=Spread(default),gun=Spread(ranged);Check(gun<melee*.6f,"the right hand's shards condense on the emitter ("+(melee*100).ToString("0.0")+" to "+(gun*100).ToString("0.0")+" cm)");
            n=Aura(6,a,1,-10,default,false,1,1,false,ranged);var g0=a.Take(n).First(x=>x.Kind==CyberPiece.AuraEmitter&&x.Bone==R&&x.Scale.X>.04f).Position;n=Aura(6.2f,a,1,-10,default,false,1,1,false,ranged);var g1=a.Take(n).First(x=>x.Kind==CyberPiece.AuraEmitter&&x.Bone==R&&x.Scale.X>.04f).Position;
            Check((g0-g1).Length()>.01f&&Math.Abs((g0-emitterAt).Length()-(g1-emitterAt).Length())<1e-3f,"the glyph turns about the emitter");
            int none=Aura(6,a,1,-10,default,false,1,1,false,new NativeCyberLordLook.Mode{Ranged=0});Check(Kinds(a,none,CyberPiece.AuraEmitter)==0,"no emitter in melee");
        }
        {
            // The entrance: the Prototype Series' Hardlight Boot draws exactly what it drew; the Standard's adds the aura's haze from
            // the gauntlets' FormAt beat, swelling with the punches' flares.
            var proto=new NativeCyberLordIntroScript();proto.Prepare();var aura=new NativeCyberLordIntroScript{Aura=true};aura.Prepare();var shared=(NativeCyberLordIntroScript)NativeEntranceDirector.Script(CyberLordSpec.Entrance);shared.Prepare();
            Check(!shared.Aura&&!proto.Aura,"the entrance's default is the Prototype's (the runtime sets the pick)");
            int extraBefore=0,extraAfter=0;
            for(float t=0;t<NativeCyberLordIntroScript.PalmHit+1;t+=1/30f)
            {
                foreach(float q in new[]{1f,.3f})
                {
                    var a0=new EntranceFx();var a1=new EntranceFx();var a2=new EntranceFx();proto.Effects(t,a0,q);shared.Effects(t,a1,q);aura.Effects(t,a2,q);
                    Check(a0.Hash==a1.Hash&&a0.Glows==a1.Glows&&a0.Other==a1.Other,"the Prototype's entrance effects are unchanged");
                    Check(a2.Other==a0.Other,"the aura adds glows only");if(t<NativeCyberLordIntroScript.FormAt)extraBefore+=a2.Glows-a0.Glows;else extraAfter+=a2.Glows-a0.Glows;
                }
            }
            Check(extraBefore==0&&extraAfter>0,"the haze ignites at the FormAt beat ("+extraAfter+" glows after, none before)");
            var j0=new EntranceFx();aura.Effects(NativeCyberLordIntroScript.JabHit+.02f,j0,1);var r0=new EntranceFx();proto.Effects(NativeCyberLordIntroScript.JabHit+.02f,r0,1);Check(j0.Glows-r0.Glows==6,"two hands, each a glow and its split with full effects");
            var low=new EntranceFx();aura.Effects(NativeCyberLordIntroScript.JabHit+.02f,low,.3f);var lowP=new EntranceFx();proto.Effects(NativeCyberLordIntroScript.JabHit+.02f,lowP,.3f);Check(low.Glows-lowP.Glows==2,"reduced effects: one steady glow a hand");
        }
        yield return $"Cyber Lord aura signals: {count-c0} checks (ignites from the form time, both hands and forearms, glitch jumps a step or two now and then, the striking hand's flare and burst, the mana, a denser guard, the dash's dither, ranged emitters on the casts' anchor with the main emitter right and the support coil left)";

        // ------------------------------------------------------------------------------------------------------------------
        // 5. Determinism, tiers and allocation
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;string tierNote="";
        {
            // A pure function of the pilot and the clock: the same frame twice, in any order; another pilot differs.
            var times=Enumerable.Range(0,90).Select(i=>3+i*.037f).ToArray();var shuffled=times.OrderBy(t=>Math.Sin(t*977)).ToArray();var seen=new Dictionary<float,string>();
            string Key(CyberPiece[] x,int n){var sb=new System.Text.StringBuilder();for(int i=0;i<n;i++)sb.Append(x[i].Bone).Append(x[i].Position).Append(x[i].Scale).Append(x[i].Color).Append(x[i].Rise).Append(';');return sb.ToString();}
            foreach(float t in times)seen[t]=Key(a,Aura(t,a,5,1,FlareAt(t),false,1,1,false,default,.5f));
            foreach(float t in shuffled)Check(Key(a,Aura(t,a,5,1,FlareAt(t),false,1,1,false,default,.5f))==seen[t],"the same frame in any order");
            int differ=0;foreach(float t in times)if(Key(a,Aura(t,a,6,1,FlareAt(t),false,1,1,false,default,.5f))!=seen[t])differ++;Check(differ>times.Length*.9f,"another pilot's aura differs ("+differ+"/"+times.Length+")");
            var sp=new Sprites();var sq=new Sprites();int same=0;foreach(float t in shuffled){sp.Reset();sq.Reset();NativeCyberLordAura.Halo(sp,false,Hands,5,t,1,FlareAt(t),1,1,false,default,0);NativeCyberLordAura.Halo(sq,false,Hands,5,t,1,FlareAt(t),1,1,false,default,0);if(sp.Hash==sq.Hash&&sp.Count==sq.Count)same++;}
            Check(same==times.Length,"the haze is a pure function too");
        }
        {
            // Tiers: Reduced and the low graphics tier thin the aura, first person keeps a lighter one; Reduced never strobes,
            // splits or jumps (its light moves smoothly frame to frame), the full aura flickers.
            long full=0,reduced=0,low=0,fp=0;float worstReduced=0,worstFull=0,prevR=-1,prevF=-1;int reducedSplits=0,lowSplits=0;var sp=new Sprites();long hazeFull=0,hazeReduced=0,hazeLow=0;int hazeMagentaReduced=0;double hazePrev=-1,hazeJump=0;
            for(int f=0;f<600;f++)
            {
                float t=4+f/60f;int nf=Aura(t,a,7),nr=Aura(t,p,7,-10,default,false,1,.3f);full+=nf;reduced+=nr;low+=Aura(t,a,7,-10,default,false,1,1,true);fp+=Aura(t,a,7,-10,default,true);
                float lr=0;for(int i=0;i<nr;i++)if(p[i].Kind==CyberPiece.Aura){lr+=Lum(p[i].Color)*p[i].Scale.X;if(SplitPair(p,i,nr))reducedSplits++;}
                nf=Aura(t,a,7);float lf=0;for(int i=0;i<nf;i++)if(a[i].Kind==CyberPiece.Aura)lf+=Lum(a[i].Color)*a[i].Scale.X;
                if(prevR>0){worstReduced=Math.Max(worstReduced,Math.Abs(lr-prevR)/prevR);worstFull=Math.Max(worstFull,Math.Abs(lf-prevF)/prevF);}prevR=lr;prevF=lf;
                int nl=Aura(t,a,7,-10,default,false,1,1,true);for(int i=0;i+1<nl;i++)if(SplitPair(a,i,nl))lowSplits++;
                sp.Reset();NativeCyberLordAura.Halo(sp,false,Hands,7,t,-10,default,1,1,false,default,0);hazeFull+=sp.Count;
                sp.Reset();NativeCyberLordAura.Halo(sp,false,Hands,7,t,-10,default,1,.3f,false,default,0);hazeReduced+=sp.Count;hazeMagentaReduced+=sp.Magenta;if(hazePrev>=0)hazeJump=Math.Max(hazeJump,Math.Abs(sp.Light-hazePrev)/hazePrev);hazePrev=sp.Light;
                sp.Reset();NativeCyberLordAura.Halo(sp,false,Hands,7,t,-10,default,1,1,true,default,0);hazeLow+=sp.Count;
            }
            Check(reduced<full*.75f&&low<full*.45f&&fp<full*.75f,"Reduced ("+100*reduced/full+"%), the low tier ("+100*low/full+"%) and first person ("+100*fp/full+"%) thin the aura");
            Check(worstReduced<.12f&&worstFull>.2f,"Reduced never strobes (its light moves at most "+(worstReduced*100).ToString("0")+"% a frame; full flickers up to "+(worstFull*100).ToString("0")+"%)");
            Check(reducedSplits==0&&lowSplits==0,"no RGB split with Reduced or on the low tier");
            tierNote="pieces: Reduced "+100*reduced/full+"%, low "+100*low/full+"%, first person "+100*fp/full+"% of full; Reduced's light moves at most "+(worstReduced*100).ToString("0")+"% a frame, full's up to "+(worstFull*100).ToString("0")+"%; haze sprites "+hazeFull/600+" / "+hazeReduced/600+" / "+hazeLow/600;
            Check(hazeReduced<hazeFull&&hazeLow<hazeReduced&&hazeMagentaReduced==0&&hazeJump<.05,"the haze: Reduced steady without the split ("+(hazeJump*100).ToString("0.0")+"% a frame), the low tier the fist glows only ("+hazeFull/600+" / "+hazeReduced/600+" / "+hazeLow/600+" sprites)");
            sp.Reset();NativeCyberLordAura.Halo(sp,true,Hands,7,5,-10,default,1,1,false,default,0);Check(sp.View==sp.Count&&sp.Count>0,"first person's haze is on the view layer");
            sp.Reset();NativeCyberLordAura.Halo(sp,false,Hands,7,4.99f,5,default,1,1,false,default,0);int pre=sp.Count;sp.Reset();NativeCyberLordAura.Halo(sp,false,Hands,7,5,-10,default,0,1,false,default,0);Check(pre==0&&sp.Count==0,"no haze before the form time or on a vanished body");
            sp.Reset();NativeCyberLordAura.Halo(sp,false,Hands,7,5,-10,default,1,.3f,false,default,0);double still=sp.Light;sp.Reset();NativeCyberLordAura.Halo(sp,false,Hands,7,5,-10,new NativeCyberLordLook.Flare{Right=1},1,.3f,false,default,0);Check(sp.Light>still*1.3,"the flare brightens the haze");
        }
        {
            var nul=new Sprites();var buf=new CyberPiece[NativeCyberLordLook.Capacity];var view=new CyberLordView();var actor=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.Combo(actor,0,10);CyberLordFeed.Read(actor,view);
            void Frame(float t)
            {
                var flare=FlareAt(t);var mode=new NativeCyberLordLook.Mode{Ranged=(t%2)/2,Charge=t%1,Glitch=(t*3)%1};
                NativeCyberLordLook.PiecesIn(NativeWeaponSkins.StandardIndex,1,t,9,flare,false,1,1,false,buf,mode,NativeCyberLordAura.Guard(t,10,11));
                NativeCyberLordLook.PiecesIn(NativeWeaponSkins.StandardIndex,2,t,9,flare,true,.5f,.3f,true,buf,mode,1);
                NativeCyberLordLook.PiecesIn(NativeWeaponSkins.PrototypeIndex,1,t,9,flare,false,1,1,false,buf,mode,0);
                for(int i=0;i<8;i++)CyberLordIdlePose.ScanGain(CyberLordIdlePose.Glitch(1,t,1,false),buf[i],i,false);
                nul.Reset();NativeCyberLordAura.Halo(nul,false,Hands,1,t,9,flare,1,1,false,mode,1);NativeCyberLordAura.Halo(nul,true,Hands,1,t,9,flare,1,.3f,true,mode,0);
                NativeCyberLordAura.Skin("sword:prototype,cyberlord:prototype");NativeCyberLordLook.ModeOf(view,t);
            }
            for(float t=8;t<13;t+=1/60f)Frame(t);
            long before=GC.GetAllocatedBytesForCurrentThread();for(float t=8;t<13;t+=1/60f)Frame(t);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"a 60 Hz playthrough of both skins, the haze and the skin read allocates "+bytes+" bytes");
        }
        yield return $"Cyber Lord aura tiers and cost: {count-c0} checks (deterministic per pilot and clock in any order; {tierNote}; Reduced never splits or jumps; the haze steady when Reduced and on the view layer in first person; 0 bytes per frame)";

        // ------------------------------------------------------------------------------------------------------------------
        // 6. The Runtime wiring (source text)
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        string Rt(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        var pres=Rt("NativeCyberLordPresentation.cs");var portraits=Rt("NativePortraits.cs");var build=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs"));
        Check(pres.Contains("private int CyberSkin(CapturedAnimation.Player rig,int id)=>NativeCyberLordAura.Skin(rig!=null&&rig==introRig?introSkins:SeatWeaponSkins(id));"),"a rig's skin is its pilot's seat pick (the entrance its intro pilot's)");
        Check(pres.Contains("int n=NativeCyberLordLook.PiecesIn(CyberSkin(rig,a.Id),a.Id,time,formAt,flare,first,solid,")&&pres.Contains("float guard=rig!=introRig?NativeCyberLordAura.Guard(time,a.BlockAt,a.BlockUntil):0;"),"the arena, first person and the entrance draw the look in the pilot's skin with its guard");
        Check(pres.Contains("intro.Aura=CyberSkin(rig,a.Id)!=NativeWeaponSkins.PrototypeIndex;"),"the entrance ignites the aura for a Standard pilot, the gauntlets for the Prototype");
        Check(pres.Contains("NativeCyberLordLook.PiecesIn(CyberSkin(null,g.Actor),g.Actor*3+g.Index,at,-100,flare,false,g.Opacity,.3f,true,cyberPieces,default);"),"the fork-bomb clones wear the caster's skin");
        Check(pres.Contains("if(p.Rise!=0)m.m13+=p.Rise*((Vector3)bone.GetColumn(1)).magnitude;")&&portraits.Contains("if(pc.Rise!=0)m.m13+=pc.Rise;"),"the strands rise in the world (the arena, the view model, the portraits)");
        Check(pres.Contains("CyberAuraHaze(s,cyberClock,q,viewOk);")&&pres.Contains("CyberSkin(null,a.Id)==NativeWeaponSkins.PrototypeIndex)continue;")&&pres.Contains("NativeCyberLordAura.Halo(cyberSink,me,at,a.Id,time,formAt,"),"the haze for the Standard skin only, the first-person pilot's on the view layer");
        Check(portraits.Contains("int skin=s.Look.Skin==NativeWeaponSkins.Prototype?NativeWeaponSkins.PrototypeIndex:NativeWeaponSkins.StandardIndex;")&&portraits.Contains("NativeCyberLordLook.PiecesIn(skin,s.Key,time,-100,"),"the lobby, Locker and card portraits (and the Sheldon Idle) draw the portrait's skin");
        Check(portraits.Contains("if(skin==NativeWeaponSkins.StandardIndex)CyberAuraGlow(time,turn);")&&portraits.Contains("Draw(haloQuad,Matrix4x4.TRS(at,turn,Vector3.one*.36f),additive);"),"the portraits add the aura's haze on the radial sprite");
        Check(Rt("NativeLockerUI.cs").Contains("if(AllowedWeapon(w)&&NativeWeaponSkins.Skinned(w.id))lockerSkins.Add(w.id);")&&Rt("NativeLockerUI.cs").Contains("Skin=LockerSkinShown(w.id)"),"the Locker lists the Cyber Lord's two skins behind its DEV gate and tries them on the stage");
        Check(build.Contains("if(!NativeWeaponSkins.Modelled(spec))continue;")&&build.Contains("NativeCyberLordLook.PiecesIn(skin,1,1,0,default,first,1,1,false,pieces,default);"),"the build gate skips the look-only entry and checks both skins' pieces");
        Check(!File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeCyberLordAura.cs")).Contains("UnityEngine"),"the aura is Core (no Unity)");
        {
            // The Editor build gate's frame (CheckCyberLordModel: seed 1 at 1 s, formed at 0) has pieces on both hands, each skin and view.
            var g=new CyberPiece[NativeCyberLordLook.Capacity];
            foreach(int skin in new[]{NativeWeaponSkins.StandardIndex,NativeWeaponSkins.PrototypeIndex})foreach(bool first in new[]{false,true})
            {int n=NativeCyberLordLook.PiecesIn(skin,1,1,0,default,first,1,1,false,g,default);bool l=false,r=false;for(int i=0;i<n;i++){l|=g[i].Bone==L;r|=g[i].Bone==R;}Check(n>0&&l&&r,"the build gate's frame has both hands (skin "+skin+", first person "+first+")");}
        }
        yield return $"Cyber Lord aura wiring: {count-c0} checks (the seat's pick in the arena, first person, killcam, entrance, clones and portraits; rising strands; the haze; the Locker; the build gate)";
    }
}

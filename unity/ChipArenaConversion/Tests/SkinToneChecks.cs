using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;
// The pilot's skin tone and the Locker's MASKS section (owner 2026-10-28: "Give skin color customization, along with a 'Color
// selector circle' for more color options. Remove Body Skin Options 'Prototype' and 'Sheldon Clothes' and just add 'Masks'
// section for selecting masks"): the look map's skin entry (Core NativeLook), the colour wheel and skin rules (Core
// NativeColorWheel) and their shader mirror, the model's in-place retint, and the Locker's sections and try-ons.
internal static class SkinToneChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Skin tone: "+what);count++;}
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Reads(string[] maps){int sink=0;for(int i=0;i<2000;i++){var m=maps[i%maps.Length];sink+=NativeLook.SkinOf(m)&1;sink+=NativeLook.SameOutfit(m,maps[(i+1)%maps.Length])?1:0;}return sink;}
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long ReadBytes(string[] maps){Reads(maps);long before=GC.GetAllocatedBytesForCurrentThread();int sink=Reads(maps);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(sink>=0,"reads ran");return bytes;}
    public static void Run(string root)
    {
        Map();Wheel();Skin();Sources(root);
        Console.WriteLine("PASS skin tone: "+count+" checks (the look map's skin entry, gate and outfit identity, the colour wheel, the skin test and its shader mirror, the model's in-place retint, the Locker's MASKS and SKIN TONE sections and try-ons)");
    }

    // ---- the look map's skin entry ----
    private static void Map()
    {
        string outfit=NativeLook.With(NativeLook.With("",NativeLook.Upper,NativeLook.Ids[NativeLook.SlotIndex(NativeLook.Upper)][2]),NativeLook.Hair,NativeLook.Ids[NativeLook.SlotIndex(NativeLook.Hair)][1]);
        string toned=NativeLook.WithSkin(outfit,0xC48A62);
        Check(toned==outfit+",skin:c48a62"&&NativeLook.SkinOf(toned)==0xc48a62&&NativeLook.SkinOf(outfit)==-1&&NativeLook.SkinOf("")==-1&&NativeLook.SkinOf(null)==-1,"the tone is the map's last entry, six lowercase hex digits");
        Check(NativeLook.WithSkin("",0x5c3523)=="skin:5c3523"&&NativeLook.WithSkin(toned,-1)==outfit&&NativeLook.WithSkin(toned,0x1ffffff)==outfit+",skin:ffffff","a tone alone, removed by a negative one, and masked to 24 bits");
        Check(NativeLook.Normalize(toned)==toned&&NativeLook.Normalize("skin:C48A62,"+outfit)==toned&&NativeLook.Normalize(outfit+",skin:abcdef,skin:c48a62")==toned,"normalize keeps it (any case, any position, the last valid one)");
        foreach(var bad in new[]{"skin:#c48a62","skin:c48a6","skin:c48a622","skin:zzzzzz","skin:","skin: "})
            Check(NativeLook.Normalize(outfit+","+bad)==outfit&&NativeLook.SkinOf(NativeLook.Normalize(outfit+","+bad))==-1,"an invalid tone is dropped: "+bad);
        Check(NativeLook.With(toned,NativeLook.Feet,NativeLook.Ids[NativeLook.SlotIndex(NativeLook.Feet)][2]).EndsWith(",skin:c48a62",StringComparison.Ordinal),"wearing a piece keeps the tone");
        // The DEV gate drops Sheldon's pieces, never the tone (the tone is not DEV gated).
        int upper=NativeLook.SlotIndex(NativeLook.Upper);string dev=null;foreach(var id in NativeLook.Ids[upper])if(NativeLook.DevOnly(id))dev=id;
        Check(dev!=null,"a DEV top exists to gate");
        string devToned=NativeLook.WithSkin(NativeLook.With("",NativeLook.Upper,dev),0x432619);
        Check(NativeLook.Gate(devToned,false)=="skin:432619"&&NativeLook.Gate(devToned,true)==devToned&&NativeLook.Gate("skin:432619",false)=="skin:432619","the gate keeps the tone when it drops a DEV piece");
        // Outfit identity: the same pieces in another tone.
        Check(NativeLook.SameOutfit(outfit,toned)&&NativeLook.SameOutfit(toned,NativeLook.WithSkin(outfit,0x111111))&&NativeLook.SameOutfit("","skin:aabbcc")&&NativeLook.SameOutfit(null,"")
            &&!NativeLook.SameOutfit(outfit,"")&&!NativeLook.SameOutfit(toned,NativeLook.With(toned,NativeLook.Hair,NativeLook.Ids[NativeLook.SlotIndex(NativeLook.Hair)][0])),"a tone change is the same outfit; a piece change is not");
        // The longest map stays under the contract's bound.
        var longest=new int[NativeLook.Slots.Length];string map="";for(int s=0;s<NativeLook.Slots.Length;s++){var ids=NativeLook.Ids[s];int best=0;for(int k=1;k<ids.Length;k++)if(ids[k].Length>ids[best].Length)best=k;map=NativeLook.With(map,NativeLook.Slots[s],ids[best]);}
        map=NativeLook.WithSkin(map,0xffffff);Check(map.Length<=NativeLook.MaxLength&&NativeLook.Normalize(map)==map,"the longest toned map is canonical and bounded ("+map.Length+")");
        // An account map without a tone keeps this PC's (until the Worker stores it); one with a tone keeps its own.
        Check(NativeLook.KeepSkin(outfit,"skin:5c3523")==outfit+",skin:5c3523"&&NativeLook.KeepSkin(toned,"skin:5c3523")==toned&&NativeLook.KeepSkin(outfit,"")==outfit&&NativeLook.KeepSkin(null,null)==""&&NativeLook.KeepSkin("","skin:5c3523")=="skin:5c3523","an account map without a tone keeps the local one");
        // The presets: ten, named, distinct, light to deep, and each reads as skin.
        var tones=NativeLook.SkinTones;Check(tones.Length==10&&NativeLook.SkinToneLabels.Length==tones.Length&&tones.Distinct().Count()==tones.Length&&NativeLook.SkinToneLabels.All(l=>!string.IsNullOrWhiteSpace(l)),"ten named distinct presets");
        for(int i=1;i<tones.Length;i++)Check(NativeColorWheel.Luma(tones[i])<NativeColorWheel.Luma(tones[i-1]),"the presets run light to deep: "+NativeLook.SkinToneLabels[i]);
        foreach(var t in tones)Check(NativeColorWheel.SkinShare(t)>=.99f,"a preset reads as skin: "+t.ToString("x6"));
        Check(NativeLook.SkinHex(0xc48a62)=="#c48a62"&&NativeLook.SkinHex(-1)=="","the Locker's hex label");
        // The reads a frame makes (the body's retint test) allocate nothing.
        long bytes=ReadBytes(new[]{outfit,toned,"",devToned,map});Check(bytes==0,"SkinOf and SameOutfit allocate nothing ("+bytes+" bytes over 2000 reads)");
    }

    // ---- the colour wheel ----
    private static void Wheel()
    {
        for(int h=0;h<24;h++)for(int s=1;s<=4;s++)
        {
            float hue=h/24f,sat=s/4f;NativeColorWheel.Point(hue,sat,out float x,out float y);NativeColorWheel.FromPoint(x,y,out float hb,out float sb);
            float dh=Math.Abs(hb-hue);dh=Math.Min(dh,1-dh);Check(dh<1e-4f&&Math.Abs(sb-sat)<1e-4f&&Math.Abs(Math.Sqrt(x*x+y*y)-sat)<1e-4,"a disc point and its hue and saturation agree ("+hue+", "+sat+")");
        }
        NativeColorWheel.Point(0,1,out float rx,out float ry);NativeColorWheel.Point(.25f,1,out float ux,out float uy);
        Check(Math.Abs(rx-1)<1e-5f&&Math.Abs(ry)<1e-5f&&Math.Abs(ux)<1e-5f&&Math.Abs(uy-1)<1e-5f,"hue 0 at the right, a quarter turn counter-clockwise at the top");
        NativeColorWheel.FromPoint(3,0,out float ph,out float ps);NativeColorWheel.FromPoint(0,0,out float ch,out float cs);
        Check(ps==1&&ph==0&&cs==0&&ch==0,"past the rim is the rim; the centre is white");
        Check(NativeColorWheel.Rgb(0,1,1)==0xff0000&&NativeColorWheel.Rgb(1/3f,1,1)==0x00ff00&&NativeColorWheel.Rgb(.3f,0,1)==0xffffff&&NativeColorWheel.Rgb(.6f,1,0)==0&&NativeColorWheel.Rgb(0,2,-1)==0,"the wheel's colours (clamped)");
        foreach(var c in NativeLook.SkinTones.Concat(new[]{0x70e1d0,0x3b82f6,0xffc24d}))
        {
            NativeColorWheel.Hsv(c,out float h,out float s,out float v);int back=NativeColorWheel.Rgb(h,s,v);
            Check(Math.Abs((back>>16&255)-(c>>16&255))<=1&&Math.Abs((back>>8&255)-(c>>8&255))<=1&&Math.Abs((back&255)-(c&255))<=1,"a colour survives the wheel: "+c.ToString("x6"));
        }
    }

    // ---- the skin test and the tone rule (the shader's mirror) ----
    private static void Skin()
    {
        Check(NativeColorWheel.SkinShare(NativeColorWheel.ModelSkin)>=.99f,"the model's own skin reads as skin");
        // Not skin: whites, greys, black, pure hues, the cyber palette, the default cloth, cool colours.
        foreach(var c in new[]{0xffffff,0xf4f4f5,0xe5e7eb,0x9ca3af,0x2c303a,0x0b0c10,0x000000,0xff0000,0x00ff00,0x0000ff,0x22e6ff,0x70e1d0,0x8b5cff,0x3b82f6,0x313a5a,0x4dffa6})
            Check(NativeColorWheel.SkinShare(c)<.05f,"does not read as skin: "+c.ToString("x6"));
        // A palette's base skin: its brightest skin colour.
        Check(NativeColorWheel.BaseSkin(new[]{0x2c303a,0xd8a07a,0xffffff,0xefd0bd,-1,0x70e1d0})==0xefd0bd&&NativeColorWheel.BaseSkin(new[]{0x2c303a,0xffffff,-1})==-1&&NativeColorWheel.BaseSkin(null)==-1,"the base skin is the brightest skin colour, or none");
        // The tone: the base becomes the tone; a darker shade keeps its ratio; non-skin colours stay.
        foreach(var tone in NativeLook.SkinTones)
        {
            int got=NativeColorWheel.Toned(NativeColorWheel.ModelSkin,tone,NativeColorWheel.ModelSkin);
            Check(Math.Abs((got>>16&255)-(tone>>16&255))<=1&&Math.Abs((got>>8&255)-(tone>>8&255))<=1&&Math.Abs((got&255)-(tone&255))<=1,"the base skin takes the tone exactly: "+tone.ToString("x6"));
            int shade=0xc4a090,toned=NativeColorWheel.Toned(shade,tone,NativeColorWheel.ModelSkin);
            Check(NativeColorWheel.Luma(toned)<NativeColorWheel.Luma(got)+1e-3f,"a shade stays darker than the skin under "+tone.ToString("x6"));
            foreach(var keep in new[]{0xffffff,0x2c303a,0x70e1d0,0x0b0c10})Check(NativeColorWheel.Toned(keep,tone,NativeColorWheel.ModelSkin)==keep,"a non-skin colour keeps its colour: "+keep.ToString("x6"));
        }
        Check(NativeColorWheel.Toned(0xd8a07a,-1,NativeColorWheel.ModelSkin)==0xd8a07a&&NativeColorWheel.Toned(0xd8a07a,0x5c3523,-1)==0xd8a07a,"no tone or no base: unchanged");
    }

    // ---- the shader mirror, the model's retint and the Locker ----
    private static void Sources(string root)
    {
        string A(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
        string fx=A("Resources/NativeVisualBodyFx.cginc"),toon=A("Resources/NativeVisualBodyToon.shader"),wheel=A("Core/NativeColorWheel.cs");
        string share=fx.Substring(fx.IndexOf("float SkinShare(float3 c)",StringComparison.Ordinal));share=share.Substring(0,share.IndexOf("float3 SkinToned(",StringComparison.Ordinal));
        Check(share.Contains("saturate((c.r-c.g)*60.0+0.5)*saturate((c.g-c.b)*60.0+0.5)")&&share.Contains("smoothstep(0.05,0.12,sat)")&&share.Contains("(1.0-smoothstep(0.80,0.92,sat))")&&share.Contains("smoothstep(0.03,0.08,hi)")
            &&wheel.Contains("Clamp01((r-g)*60+.5f)*Clamp01((g-b)*60+.5f)")&&wheel.Contains("Smooth(.05f,.12f,sat)*(1-Smooth(.80f,.92f,sat))*Smooth(.03f,.08f,hi)"),"the shader's skin test is the Core's, term for term");
        Check(share.Contains("#ifndef UNITY_COLORSPACE_GAMMA")&&share.Contains("c=sqrt(max(c,float3(0.0,0.0,0.0)));"),"the shader tests linear colours in (about) sRGB, as the Core does");
        Check(fx.Contains("if(_SkinTone.w<0.5)return c;")&&fx.Contains("return lerp(c,saturate(c*(_SkinTone.rgb/max(_SkinRef.rgb,float3(0.0003,0.0003,0.0003)))),SkinShare(c));"),"no tone draws as before; a tone scales a skin colour by tone over base");
        Check(Regex.Matches(toon,@"SpawnTinted\(SkinToned\(_UseCustomTexture>\.5\?VisualColours\(map\.rgb,i\.color\.rgb\):map\.rgb\)").Count==2&&Regex.IsMatch(toon,@"_SkinTone\(""[^""]*"",Vector\)=\(0,0,0,0\)"),"the surface and the outline take the tone; off by default");
        string body=A("Runtime/NativeVisualBodyPresentation.cs"),portrait=A("Runtime/NativeVisualPortraitPresentation.cs");
        string dress=body.Substring(body.IndexOf("private static void VisualDress(",StringComparison.Ordinal));dress=dress.Substring(0,dress.IndexOf("lookNamed.Clear();\n",StringComparison.Ordinal));
        Check(dress.Contains("int tone=NativeLook.SkinOf(look);")&&dress.Contains("if(tone>=0){")&&dress.Contains("foreach(var r in lookNamed.Values)VisualSkinToneOne(r,tone);}"),"a body takes its tone when it is dressed (an untoned body is left alone)");
        Check(Count(body,"VisualSkinToneOne(")==3&&Count(body+portrait,"VisualRetone(")==4&&body.Contains("(body.Hood!=hood||!VisualRetone(body,VisualLookOf(id)))")&&portrait.Contains("!VisualRetone(s.Model,s.Outfit))")&&body.Contains("if(viewBody?.Root!=null&&!VisualRetone(viewBody,LocalLook??\"\"))"),"the tone is written when dressed and on a tone change only (never per frame); the arena body, the first-person body and the portrait retint in place");
        string one=body.Substring(body.IndexOf("private static void VisualSkinToneOne(",StringComparison.Ordinal));one=one.Substring(0,one.IndexOf("r.SetPropertyBlock(toneBlock);",StringComparison.Ordinal));
        Check(one.Contains("r.GetPropertyBlock(toneBlock);")&&one.Contains("if(linear){t=t.linear;b=b.linear;}")&&one.Contains("toneBlock??=new MaterialPropertyBlock();")&&!Regex.IsMatch(one,@"\bnew\s+(?!Vector4\(|MaterialPropertyBlock\(\);)"),"the tone keeps the renderer's other block values, matches the colour space and allocates nothing");
        Check(body.Contains("return skinParts.Contains(n)||n.StartsWith(\"Head.\",StringComparison.Ordinal)||n.StartsWith(\"Body.\",StringComparison.Ordinal);")&&!body.Contains("\"Hair\"")," only the body parts and the head and body meshes are skin parts");
        // The Locker: on the model MASKS and SKIN TONE replace BODY SKIN and BODY COLOUR; Classic keeps those.
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        string ui=R("NativeLockerUI.cs"),skins=R("NativeLockerSkinsUI.cs"),wheelUi=R("NativeColorWheelUI.cs"),outfit=R("NativeLockerOutfitUI.cs"),portraitUi=R("NativePortraitUI.cs");
        string layout=Between(ui,"case NativeLocker.Appearance:","case NativeLocker.KillEffects:"),deferred=Between(ui.Substring(ui.IndexOf("private void LockerDeferred()",StringComparison.Ordinal)),"case NativeLocker.Appearance:","case NativeLocker.KillEffects:");
        Check(layout.Contains("if(LkOnModel){LkHead();LkReserve(LkMasksH());LkHead();LkReserve(LkToneH());}")&&layout.Contains("else{LkHead();LkReserve(LkBodyRowH());LkHead();LkSwatches(ref prefs.bodyColor,\"#e5e7eb\");}"),"the layout reserves MASKS and SKIN TONE on the model, BODY SKIN and BODY COLOUR in Classic");
        int masks=deferred.IndexOf("\"MASKS\"",StringComparison.Ordinal),tone=deferred.IndexOf("\"SKIN TONE\"",StringComparison.Ordinal),accent=deferred.IndexOf("\"ACCENT COLOUR\"",StringComparison.Ordinal),outfitAt=deferred.IndexOf("\"OUTFIT\"",StringComparison.Ordinal),callsign=deferred.IndexOf("\"CALLSIGN\"",StringComparison.Ordinal);
        Check(callsign>=0&&callsign<masks&&masks<tone&&tone<accent&&accent<outfitAt&&deferred.Contains("LkMasks(lockerSlots[k++]);")&&deferred.Contains("LkSkinTone(lockerSlots[k++]);"),"CALLSIGN, MASKS, SKIN TONE, ACCENT COLOUR, then OUTFIT");
        Check(ui.Contains("if(!LkOnModel)lockerSkins.Add(NativeBodySkins.Entry);")&&!ui.Contains("lockerSkins.Add(NativeWeaponSkins.FaceEntry)")&&!skins.Contains("LkFaceCard")&&ui.Contains("model==lockerListsModel"),"on the model the Skins tab lists only weapons (rebuilt when the model is switched)");
        string masksBody=Body(skins,"private void LkMasks(");
        Check(masksBody.Contains("string key=NativeWeaponSkins.FaceKeys[k];")&&masksBody.Contains("if(click&&!on)SetFaceGear(key);")&&masksBody.Contains("int worn=FaceGearEquipped(),")
            &&Regex.Matches(Regex.Match(skins,@"LkMaskLabels=\{([^}]*)\}").Groups[1].Value,"\"[^\"]+\"").Count==NativeWeaponSkins.FaceKeys.Length&&Regex.Matches(Regex.Match(skins,@"LkMaskTexts=\{([^}]*)\}").Groups[1].Value,"\"[^\"]+\"").Count==NativeWeaponSkins.FaceKeys.Length,
            "MASKS offers each face gear (No mask among them), through the face setter");
        Check(masksBody.Contains("if(hot&&repaint)lockerFaceTry=key;")&&portraitUi.Contains("lockerFaceTry!=null&&menuScreen==MenuScreen.Customize&&lockerTab==NativeLocker.Appearance?lockerFaceTry:null")
            &&ui.Contains("lockerBodyTry=null;lockerFaceTry=null;lockerToneTry=-2;}"),"hovering a mask tries it on the stage (cleared every repaint)");
        // The SKIN TONE row: one writer, try-ons through LookShown, no per-frame allocation in the draw.
        Check(Count(wheelUi+skins+ui+outfit,"NativeLook.WithSkin(prefs.look")==1&&wheelUi.Contains("private void SetSkinTone(int rgb,bool fromWheel=false){prefs.look=NativeLook.WithSkin(prefs.look,rgb);"),"SetSkinTone is the only writer of the tone");
        Check(outfit.Contains("if(lockerToneTry==-2||menuScreen!=MenuScreen.Customize||lockerTab!=NativeLocker.Appearance)return lookShown;")&&outfit.Contains("lookTried=NativeLook.WithSkin(lookShown,lockerToneTry);")&&outfit.Contains("lookTriedTone!=lockerToneTry"),"a hovered tone is tried on the stage, its map made once per hover");
        string draw=Body(wheelUi,"private void LkSkinTone(");
        Check(!Regex.IsMatch(draw,@"\bnew\s+(?!Rect\(|Color\(|Vector2\()")&&!draw.Contains("+\"")&&!draw.Contains("$\"")&&!draw.Contains("ToString("),"the SKIN TONE row allocates nothing per frame");
        Check(wheelUi.Contains("if(lkWheel!=null)return lkWheel;")&&wheelUi.Contains("if(lkTrackHue!=lkToneHue||lkTrackSat!=lkToneSat)"),"the disc is made once and the brightness track only when the hue or saturation moves");
        Check(A("Runtime/NativeProfileUI.cs").Contains("if(c.look!=null)prefs.look=NativeLook.KeepSkin(NativeLook.Gate(c.look,"),"an account load keeps this PC's tone when the account has none");
        Check(skins.Contains("skin=U.Hex(LkToneShown())"),"the model thumbnails wear the picked tone");
    }
    private static int Count(string s,string what){int n=0,i=0;while((i=s.IndexOf(what,i,StringComparison.Ordinal))>=0){n++;i+=what.Length;}return n;}
    private static string Between(string s,string a,string b){int i=s.IndexOf(a,StringComparison.Ordinal),j=i<0?-1:s.IndexOf(b,i+a.Length,StringComparison.Ordinal);return i<0||j<0?"":s.Substring(i,j-i);}
    private static string Body(string s,string head)
    {
        int i=s.IndexOf(head,StringComparison.Ordinal);if(i<0)return "";int open=s.IndexOf('{',i),depth=0;
        for(int k=open;k<s.Length;k++){if(s[k]=='{')depth++;else if(s[k]=='}'&&--depth==0)return s.Substring(i,k-i+1);}
        return "";
    }
}

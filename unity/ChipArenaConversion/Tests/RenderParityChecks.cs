using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Editor parity (2026-11-21, owner: the shipped build reads much darker than the clips he approved). Every look was tuned in the Editor's
// Play mode, which ran in an Untitled scene: its default Directional Light (FFF4D6, 1.0, 50/-30/0, soft shadows) keyed the world and,
// with no baked lighting data, the Editor reflected Unity's Default-Skybox-Cubemap. The player loads Prototype.unity (no light) and
// reflected UnityBlackCube. Runtime/NativeLighting now carries both (Create: ParityKey, DefaultReflection). Pinned here so neither can
// silently go away:
//  - the key is Unity's default Directional Light exactly (colour, intensity, rotation, soft shadows, default biases), made in Create
//    after the moon and the rim, once, and skipped only when a scene brings its own directional light; nothing else touches it;
//  - Prototype.unity carries no light, so the player always gets the rig's key and never two;
//  - the reflection is Custom with Resources/NativeDefaultReflection.mat's _Tex, the built-in Default-Skybox-Cubemap (fileID 20200 in
//    unity_builtin_extra), and only the v2 intros' IntroOwn/IntroRelease (which give it back) change the reflection mode otherwise.
internal static class RenderParityChecks
{
    private static int count;
    private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Render parity: "+what);}

    public static void Run(string root)
    {
        count=0;
        string runtime=Path.Combine(root,"Assets/ChipArena/Runtime");
        string lighting=File.ReadAllText(Path.Combine(runtime,"NativeLighting.cs"));
        // The key: Unity's default new-scene Directional Light, as the Editor's Untitled scene had it.
        Check(lighting.Contains("public const string ParityKeyName=\"Key light (Editor parity)\";"),"the key keeps its plain name");
        Check(lighting.Contains("ParityKeyColor=new Color(1f,244f/255f,214f/255f);public const float ParityKeyIntensity=1f;"),"the key is FFF4D6 at intensity 1");
        Check(lighting.Contains("ParityKeyEuler=new Vector3(50f,-30f,0f);"),"the key turns 50/-30/0 (Unity's default sun)");
        Check(lighting.Contains("parityKey=go.AddComponent<Light>();parityKey.type=LightType.Directional;parityKey.color=ParityKeyColor;parityKey.intensity=ParityKeyIntensity;parityKey.shadows=LightShadows.Soft;"),
            "the key is a directional light with soft shadows and Unity's default biases (the quality level sets its shadows)");
        Check(lighting.Contains("go.transform.SetParent(parent,false);go.transform.rotation=Quaternion.Euler(ParityKeyEuler);"),"the key's world rotation is the default sun's");
        var create=Regex.Match(lighting,@"public static void Create\(Transform parent\)\s*\{(.*?)\n        \}",RegexOptions.Singleline);
        Check(create.Success,"NativeLighting.Create found");
        string body=create.Groups[1].Value;
        Check(body.TrimStart().StartsWith("bool sceneKey=SceneKeyLight();",StringComparison.Ordinal),"Create looks for a scene's own directional light before it makes its rig");
        int moonAt=body.IndexOf("RenderSettings.sun=moon=Directional(\"Moon key\"",StringComparison.Ordinal),keyAt=body.IndexOf("if(!sceneKey)ParityKey(parent);",StringComparison.Ordinal),reflAt=body.IndexOf("DefaultReflection();",StringComparison.Ordinal);
        Check(moonAt>0&&keyAt>moonAt&&reflAt>keyAt,"Create makes the moon, the rim, then the key (unless the scene has one), then the reflection; the moon stays RenderSettings.sun");
        Check(lighting.Contains("if(parityKey!=null)return;"),"a second Create never adds a second key");
        Check(lighting.Contains("if(l.type==LightType.Directional&&l.isActiveAndEnabled&&l!=moon&&l!=rim&&l!=parityKey)return true;"),"only a directional light of the scene's own counts as its key");
        Check(Regex.Matches(lighting,@"\bparityKey\b").Count==8,"nothing else in NativeLighting touches the key ("+Regex.Matches(lighting,@"\bparityKey\b").Count+" uses)");
        foreach(var f in Directory.GetFiles(runtime,"*.cs").Where(f=>!f.EndsWith("NativeLighting.cs",StringComparison.Ordinal)))
        {
            string s=File.ReadAllText(f);
            Check(!s.Contains("ParityKey")&&!s.Contains("Key light (Editor parity)"),Path.GetFileName(f)+" leaves the key alone");
            Check(!s.Contains("defaultReflectionMode=")&&!s.Contains("customReflectionTexture="),Path.GetFileName(f)+" leaves the default reflection alone");
        }
        // The reflection: the built-in Default-Skybox-Cubemap, through a material in Resources.
        Check(lighting.Contains("public const string DefaultReflectionResource=\"NativeDefaultReflection\";"),"the reflection's carrier is Resources/NativeDefaultReflection");
        Check(lighting.Contains("var cube=Resources.Load<Material>(DefaultReflectionResource)?.GetTexture(\"_Tex\");")
            &&lighting.Contains("RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=cube;"),"the default reflection is Custom with the carrier's cubemap");
        Check(Regex.Matches(lighting,@"RenderSettings\.defaultReflectionMode=").Count==3,"only DefaultReflection, IntroOwn and IntroRelease set the reflection mode");
        Check(lighting.Contains("RenderSettings.defaultReflectionMode=keptReflectMode;RenderSettings.customReflectionTexture=keptReflect;"),"a v2 intro gives the reflection back");
        string mat=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeDefaultReflection.mat"));
        Check(mat.Contains("m_Shader: {fileID: 103, guid: 0000000000000000f000000000000000, type: 0}"),"the carrier is a Skybox/Cubemap material");
        Check(Regex.IsMatch(mat,@"- _Tex:\s*\n\s*m_Texture: \{fileID: 20200, guid: 0000000000000000f000000000000000, type: 0\}"),"its _Tex is the built-in Default-Skybox-Cubemap");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeDefaultReflection.mat.meta")),"the carrier has its meta");
        // The player's scene: no light of its own, so the rig's key is the only one.
        string scene=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Scenes/Prototype.unity"));
        Check(!scene.Contains("--- !u!108 ")&&!scene.Contains("Light:"),"Prototype.unity carries no light");
        Console.WriteLine("PASS render parity: "+count+" checks: the rig's Editor-parity key (Unity's default sun: FFF4D6, 1.0, 50/-30/0, soft shadows) once, no light in the player's scene, the default reflection Default-Skybox-Cubemap");
    }
}

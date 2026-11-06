using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NVector = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;

namespace ChipArena.UnityConversion.Editor
{
    // Real Unity/GPU diagnostic for DEV_LOG's white/green rooftop props. Offline GLSL
    // renders cannot exercise Unity's vertex upload or the Windows shader compiler.
    public static class NativeRooftopRenderChecks
    {
        [MenuItem("Chip Arena/Check rooftop shader pixels")]
        public static void Run()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
                throw new InvalidOperationException("Rooftop pixel checks require a graphics device; omit -nographics.");
            if(Application.isPlaying)throw new InvalidOperationException("Leave Play mode before rooftop pixel checks.");
            Debug.Log("ROOFTOP_GPU: "+SystemInfo.graphicsDeviceType+" / "+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceVersion);
            var previous=RenderTexture.active;bool async=ShaderUtil.allowAsyncCompilation;
            var go=new GameObject("Rooftop pixel check"){hideFlags=HideFlags.HideAndDontSave};
            var target=new RenderTexture(32,32,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var pixels=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);
            var buffer=new CommandBuffer{name="Rooftop pixel check"};
            Mesh mesh=null;Material material=null;
            try
            {
                ShaderUtil.allowAsyncCompilation=false;
                var view=go.AddComponent<Camera>();view.enabled=false;view.cullingMask=0;
                view.transform.SetPositionAndRotation(new Vector3(0,0,2),Quaternion.Euler(0,180,0));
                view.orthographic=true;view.orthographicSize=1;view.nearClipPlane=.1f;view.farClipPlane=10;
                view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.black;
                view.allowHDR=true;view.allowMSAA=false;view.targetTexture=target;
                view.AddCommandBuffer(CameraEvent.BeforeForwardOpaque,buffer);
                // Exercise the same mesh upload as the home screen, including float4 UV channels.
                var upload=typeof(NativeRooftopScene).GetMethod("WorldMesh",BindingFlags.Static|BindingFlags.NonPublic);
                if(upload==null)throw new Exception("Production rooftop mesh uploader was not found.");
                var failures=new List<string>();
                foreach(string name in new[]{"NativeRooftopAttributeProbe","NativeRooftop","NativeRooftopProp"})
                {
                    Shader shader=name=="NativeRooftopAttributeProbe"
                        ?AssetDatabase.LoadAssetAtPath<Shader>("Assets/ChipArena/Editor/NativeRooftopAttributeProbe.shader")
                        :Resources.Load<Shader>(name);
                    if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Rooftop shader unavailable: "+name);
                    material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                    // Isolate upload and material shading from live scene globals and bloom.
                    material.SetVector("_ArenaToon",Vector4.zero);
                    material.SetVector("_RoofSignPos",new Vector4(0,10,10,1));
                    material.SetVector("_RoofSignColor",Vector4.zero);
                    material.SetVector("_RoofCyanPos",new Vector4(0,10,10,1));
                    var lights=new Vector4[8];for(int k=0;k<8;k++)lights[k]=new Vector4(0,10,10,1);
                    material.SetVectorArray("_RoofLightPos",lights);material.SetVectorArray("_RoofLightCol",new Vector4[8]);
                    int count=name=="NativeRooftop"?2:7;
                    for(int kind=0;kind<count;kind++)
                    foreach(var normal in new[]{NVector.UnitX,-NVector.UnitX,NVector.UnitY,-NVector.UnitY,NVector.UnitZ,-NVector.UnitZ})
                    {
                        int mode=name=="NativeRooftop"?kind+1:NativeRooftopCity.Prop;
                        var attr=new NVector4(.05f,.055f,.07f,kind);
                        var right=NVector.Normalize(NVector.Cross(Math.Abs(normal.Y)>.5f?NVector.UnitZ:NVector.UnitY,normal));
                        var up=NVector.Cross(normal,right);
                        var direction=new Vector3(-normal.X,normal.Y,normal.Z);
                        view.transform.SetPositionAndRotation(direction*2,Quaternion.LookRotation(-direction,new Vector3(-up.X,up.Y,up.Z)));
                        var q=new RooftopQuad{A=-right-up,B=right-up,C=right+up,D=-right+up,Normal=normal,Uv=new NVector4(-1,-1,1,1),Mode=mode,Attr=attr};
                        mesh=(Mesh)upload.Invoke(null,new object[]{"Rooftop probe",new List<RooftopQuad>{q},0,int.MaxValue});
                        buffer.Clear();buffer.DrawMesh(mesh,Matrix4x4.identity,material,0,0);
                        view.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();
                        var c=pixels.GetPixel(16,16);
                        Debug.Log($"ROOFTOP_PIXEL: shader={name} mode={mode} material={kind} normal={normal} rgba=({c.r:R},{c.g:R},{c.b:R},{c.a:R})");
                        bool finite=Finite(c.r)&&Finite(c.g)&&Finite(c.b)&&Finite(c.a);
                        bool ok=name=="NativeRooftopAttributeProbe"
                            ?finite&&Mathf.Abs(c.r-attr.X)<.002f&&Mathf.Abs(c.g-attr.Y)<.002f&&Mathf.Abs(c.b-attr.Z)<.002f&&Mathf.Abs(c.a-kind)<.002f
                            :finite&&c.r>=0&&c.g>=0&&c.b>=0&&Mathf.Max(c.r,Mathf.Max(c.g,c.b))<.5f&&c.a>.99f;
                        if(!ok)failures.Add(name+" material "+kind+" normal "+normal);
                        UnityEngine.Object.DestroyImmediate(mesh);mesh=null;
                    }
                    UnityEngine.Object.DestroyImmediate(material);material=null;
                }
                if(failures.Count!=0)throw new Exception("Rooftop GPU pixel failures: "+string.Join(", ",failures));
                Debug.Log("PASS rooftop GPU pixels: production float4 attribute upload, legacy lit/neon and all seven prop materials. Full-menu/player acceptance remains separate.");
            }
            finally
            {
                RenderTexture.active=previous;ShaderUtil.allowAsyncCompilation=async;
                if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);if(material!=null)UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(go);buffer.Release();target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // The held guard's shield (owner 2026-11-09: "change the block effect"; right click "a held block for melee"): while a pilot's
    // replicated guard is up (Actor.Guarding, since GuardAt; Runtime NativeGuard.cs) a curved panel of light stands in front of it on its
    // facing (Resources/NativeGuardShield.shader), in its accent: grown out from its middle as it goes up, a ring and a flash where each
    // blocked blow strikes it (the "hold" Block events, read with the others in NativeBlockFxPresentation.cs, which also throw their
    // sparks), cracked and falling apart when the guard breaks ("guard-break"), faded out when it drops. The local first-person pilot's own
    // is fainter (it stands before the eye). Presentation only.
    public sealed partial class ArenaPresentation
    {
        // The panel: a patch of a sphere about the chest (pilot frame: +z its facing, y up from the feet), its angles (degrees) and radius.
        public const float ShieldYaw=58,ShieldDown=52,ShieldUp=60,ShieldRadius=.86f;
        public static readonly Vector3 ShieldCentre=new Vector3(0,1.05f,.04f);
        private Mesh guardShieldMesh;private Material guardShieldMaterial;private bool guardShieldFailed;private MaterialPropertyBlock guardShieldBlock;
        private readonly Vector4[] guardHits=new Vector4[4];private readonly Dictionary<int,float> guardLastUp=new Dictionary<int,float>();
        private static readonly int ShieldColorId=Shader.PropertyToID("_ShieldColor"),ShieldAgeId=Shader.PropertyToID("_ShieldAge"),ShieldFadeId=Shader.PropertyToID("_ShieldFade"),
            ShieldBreakId=Shader.PropertyToID("_Break"),ShieldStrengthId=Shader.PropertyToID("_Strength"),ShieldHitsId=Shader.PropertyToID("_Hits");
        /// <summary>The panel's point at (u, v) (0..1 across, 0..1 up) in the pilot's frame, and the (u, v) of a point in that frame.</summary>
        public static Vector3 ShieldPoint(float u,float v)
        {
            float yaw=(u*2-1)*ShieldYaw*Mathf.Deg2Rad,pitch=(-ShieldDown+v*(ShieldDown+ShieldUp))*Mathf.Deg2Rad;
            return ShieldCentre+ShieldRadius*new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch));
        }
        public static Vector2 ShieldUv(Vector3 local)
        {
            var d=local-ShieldCentre;if(d.sqrMagnitude<1e-8f)return new Vector2(.5f,.5f);d.Normalize();
            float yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,pitch=Mathf.Asin(Mathf.Clamp(d.y,-1,1))*Mathf.Rad2Deg;
            return new Vector2(Mathf.Clamp01((yaw/ShieldYaw+1)*.5f),Mathf.Clamp01((pitch+ShieldDown)/(ShieldDown+ShieldUp)));
        }
        private Mesh GuardShieldMesh()
        {
            if(guardShieldMesh!=null)return guardShieldMesh;const int U=30,V=20;var vs=new Vector3[(U+1)*(V+1)];var ns=new Vector3[vs.Length];var uvs=new Vector2[vs.Length];var tris=new int[U*V*6];
            for(int j=0;j<=V;j++)for(int i=0;i<=U;i++){int k=j*(U+1)+i;float u=i/(float)U,v=j/(float)V;vs[k]=ShieldPoint(u,v);ns[k]=(vs[k]-ShieldCentre).normalized;uvs[k]=new Vector2(u,v);}
            int t=0;for(int j=0;j<V;j++)for(int i=0;i<U;i++){int a=j*(U+1)+i,b=a+1,c=a+U+1,d=c+1;tris[t++]=a;tris[t++]=c;tris[t++]=b;tris[t++]=b;tris[t++]=c;tris[t++]=d;}
            guardShieldMesh=new Mesh{name="Guard shield",hideFlags=HideFlags.HideAndDontSave,vertices=vs,normals=ns,uv=uvs,triangles=tris};
            guardShieldMesh.bounds=new Bounds(ShieldCentre,Vector3.one*2.2f);return guardShieldMesh;
        }
        private Material GuardShieldMaterial()
        {
            if(guardShieldMaterial!=null||guardShieldFailed)return guardShieldMaterial;var shader=Resources.Load<Shader>("NativeGuardShield");
            if(shader==null||!shader.isSupported){guardShieldFailed=true;return null;}
            return guardShieldMaterial=new Material(shader){name="Guard shield",hideFlags=HideFlags.HideAndDontSave};
        }
        /// <summary>RenderBlockFx, each frame: every guarding pilot's shield (and a breaking one's last moment).</summary>
        private void RenderGuardShields(Snapshot s,float time)
        {
            if(s?.Actors==null)return;var mat=GuardShieldMaterial();if(mat==null)return;var mesh=GuardShieldMesh();guardShieldBlock??=new MaterialPropertyBlock();
            foreach(var a in s.Actors)
            {
                if(a==null)continue;bool up=a.Alive&&a.Guarding&&time>=a.GuardAt;if(up)guardLastUp[a.Id]=time;
                // Its last break and its blows (the newest four), from the Block events.
                float broke=-1;int hits=0;for(int k=0;k<guardHits.Length;k++)guardHits[k]=new Vector4(0,0,-1,0);
                float fade=up?1:guardLastUp.TryGetValue(a.Id,out float last)?Mathf.Clamp01(1-(time-last)/.15f):0;
                var frame=ShieldFrame(a);var inverse=frame.inverse;
                for(int i=blockRecords.Count-1;i>=0;i--)
                {
                    var r=blockRecords[i];if(r.Blocker!=a.Id||r.Detail==null)continue;float age=time-r.At;if(age<0)continue;
                    if(r.Detail=="guard-break"&&age<.45f&&broke<0)broke=age;
                    else if(r.Detail=="hold"&&age<.9f&&hits<guardHits.Length){var uv=ShieldUv(inverse.MultiplyPoint3x4(r.Point));guardHits[hits++]=new Vector4(uv.x,uv.y,age,1);}
                }
                if(fade<=0&&broke<0)continue;if(broke>=0)fade=1;
                bool own=FirstPerson&&a.Id==LocalId;var accent=AccentTint(a.AccentColor);var colour=new Color(accent.r*.5f+.15f,accent.g*.5f+.42f,accent.b*.5f+.55f)*1.05f;
                guardShieldBlock.Clear();guardShieldBlock.SetColor(ShieldColorId,colour.linear);guardShieldBlock.SetFloat(ShieldAgeId,time-a.GuardAt);guardShieldBlock.SetFloat(ShieldFadeId,fade);
                guardShieldBlock.SetFloat(ShieldBreakId,broke);guardShieldBlock.SetFloat(ShieldStrengthId,own?.35f:1);guardShieldBlock.SetVectorArray(ShieldHitsId,guardHits);
                Graphics.DrawMesh(mesh,frame,mat,0,null,0,guardShieldBlock,ShadowCastingMode.Off,false);
            }
        }
        // The shield's frame: the pilot's drawn feet and the way its posed body faces (else the replicated feet and yaw).
        private Matrix4x4 ShieldFrame(Actor a)
        {
            Vector3 at=visualPositions.TryGetValue(a.Id,out var drawn)?drawn:Position(a);float yaw=a.Yaw;
            if(rigs.TryGetValue(a.Id,out var rig)&&rig?.Root!=null){var f=rig.Root.transform.forward;f.y=0;if(f.sqrMagnitude>1e-4f)yaw=Mathf.Atan2(f.x,f.z)*Mathf.Rad2Deg;}
            return Matrix4x4.TRS(at,Quaternion.Euler(0,yaw,0),Vector3.one);
        }
    }
}

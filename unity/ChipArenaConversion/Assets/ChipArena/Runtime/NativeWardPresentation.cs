using System;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeWardMotion {public string commit;public NativeWardPart[] parts;}
    [Serializable] public sealed class NativeWardPart {public string id,kind;public int entry,side;public float phase;public float[] matrix;}
    public sealed partial class ArenaPresentation
    {
        private NativeWardMotion wardMotion;private Material pitMaterial;private MaterialPropertyBlock pitProperties;
        private void WardPresentation(NativeArena arena)
        {
            if(wardMotion==null){wardMotion=JsonUtility.FromJson<NativeWardMotion>(Resources.Load<TextAsset>("WardMotion").text);if(wardMotion.commit!=arena.Content.sourceCommit)throw new InvalidOperationException("Ward motion source differs");}
            double song=arena.BossSongTime;var pos=arena.BossTrack.Position(arena.BossTrack.Looped(Math.Max(0,song)));float beats=(pos.bar-1)*4+pos.beat-1+(float)pos.phase,t=arena.Time;float tick=Mathf.Floor(beats)+Mathf.Clamp01(Mathf.Repeat(beats,1)*6),bar=beats/4;float intensity=song<0?.25f:arena.BossTrack.Section(arena.BossTrack.Looped(song))=="climax"?1:.6f;
            foreach(var p in wardMotion.parts)
            {
                if(p.matrix==null||p.matrix.Length!=16)continue;var m=new Matrix4x4();for(int i=0;i<16;i++)m[i]=p.matrix[i];Matrix4x4 change=Matrix4x4.identity;float opening=p.entry>=0?NativeVentRules.Open(arena.Entry(p.entry),t,arena.Pve==null):0;
                switch(p.kind)
                {
                    case "vent":change=Matrix4x4.Rotate(Quaternion.Euler(0,0,1.95f*opening*Mathf.Rad2Deg));break;
                    case "door":change=Matrix4x4.Rotate(Quaternion.Euler(0,p.side*1.75f*opening*Mathf.Rad2Deg,0));break;
                    case "cell":change=Matrix4x4.Scale(new Vector3(1,arena.Pve==null?.45f/3.45f:Mathf.Lerp(1,.45f/3.45f,opening),1));break;
                    case "fan":change=Matrix4x4.Rotate(Quaternion.Euler(0,t*24,0));break;
                    case "minute":case "hour":change=Matrix4x4.Rotate(Quaternion.Euler(0,0,tick*360/(p.kind=="minute"?16:192)));break;
                    case "chandelier":change=Matrix4x4.Rotate(Quaternion.Euler(intensity*1.4f*Mathf.Sin(2*Mathf.PI*(bar+p.phase)*2+1),0,intensity*2.8f*Mathf.Sin(2*Mathf.PI*(bar+p.phase))));break;
                    case "doors":change=Matrix4x4.Translate(Vector3.forward*(-intensity*.09f*Mathf.Exp(-(float)pos.phase*9)));break;
                }
                sourceModels.Queue(p.id,m*change);
            }
            if(pitMaterial==null)pitMaterial=new Material(Resources.Load<Shader>("NativeWardPit"));
            for(int e=0;e<8;e++)
            {var entry=arena.Entry(e);float open=NativeVentRules.Open(entry,t,arena.Pve==null),warning=arena.Pve==null&&entry!=null?NativeVentRules.Warning(entry,t):0;Vector3 at=NativeArena.StageVents[e];if(open>.001f){pitProperties.Clear();pitProperties.SetFloat("_Open",open);Graphics.DrawMesh(cube,Matrix4x4.TRS(at+Vector3.up*.012f,Quaternion.identity,new Vector3(2.1f,.005f,2.1f)),pitMaterial,0,null,0,pitProperties,UnityEngine.Rendering.ShadowCastingMode.Off,false);}if(warning>0)Ring(at+Vector3.up*.03f,1.12f,7,t*25);}
        }
    }
}

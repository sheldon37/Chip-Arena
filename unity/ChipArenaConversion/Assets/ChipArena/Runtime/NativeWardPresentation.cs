using System;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeWardMotion {public string commit;public NativeWardPart[] parts;}
    [Serializable] public sealed class NativeWardPart {public string id,kind;public int entry,side;public float phase;public float[] matrix;}
    public sealed partial class ArenaPresentation
    {
        private NativeWardMotion wardMotion;private Material pitMaterial;private MaterialPropertyBlock pitProperties;
        // siren/stage.ts MOODS: tubes, strobe, erratic, beacons, spin, grins, eyes, circle, red, night, organ, crowd, sway, thump, fan.
        private const int MoodBeacons=3,MoodSpin=4,MoodSway=12,MoodThump=13,MoodFan=14;
        private static readonly System.Collections.Generic.Dictionary<string,float[]> WardMoods=new System.Collections.Generic.Dictionary<string,float[]>
        {
            {"build",new[]{.45f,0,.8f,.15f,.5f,.1f,0,.25f,0,.5f,.2f,.1f,.25f,0,.3f}},{"groove",new[]{.9f,0,.1f,.45f,.9f,.35f,0,.45f,.1f,.5f,.6f,.5f,.5f,.15f,.7f}},
            {"lift",new[]{.95f,.25f,.1f,1,3.2f,.8f,0,.75f,.35f,.45f,.9f,.85f,.8f,.6f,1.4f}},{"breather",new[]{.18f,0,1,0,.3f,.2f,1,.3f,0,.38f,.12f,0,.15f,0,.2f}},
            {"climax",new[]{1,1,0,1,5.5f,1,1,1,.6f,.42f,1,1,1,1,2.4f}},{"outro",new[]{.5f,0,.5f,.2f,.6f,.3f,.4f,.3f,.1f,.5f,.35f,.3f,.4f,.1f,.5f}},
        };
        private readonly float[] wardMood=(float[])WardMoods["build"].Clone();private float wardLast,wardFan,wardBeacon;private readonly NativeEntryView[] wardEntries=new NativeEntryView[8];
        private void WardPresentation(NativeArena arena)
        {
            if(wardMotion==null){wardMotion=JsonUtility.FromJson<NativeWardMotion>(Resources.Load<TextAsset>("WardMotion").text);if(wardMotion.commit!=arena.Content.sourceCommit)throw new InvalidOperationException("Ward motion source differs");}
            // Port of WardZeroVisuals.update's song clock: the section's mood eases in over about a bar, and the fan and beacons
            // integrate their speeds so a section change never jumps them. Before the song (PvP) the hall rests in the build mood.
            float t=arena.Time,now=PresentationClock(arena),dt=Mathf.Clamp(now-wardLast,0,.05f);wardLast=now;
            double song=arena.BossSongTime,looped=song>=0?arena.BossTrack.Looped(song):0;var pos=song>=0?arena.BossTrack.Position(looped):(0,1,(double)Mathf.Repeat(now,1));int index=song>=0?arena.BossTrack.Beat(looped):-1;
            if(!WardMoods.TryGetValue(song>=0?arena.BossTrack.Section(looped):"build",out var target))target=WardMoods["build"];float k=1-Mathf.Exp(-dt*2.2f);for(int i=0;i<wardMood.Length;i++)wardMood[i]+=(target[i]-wardMood[i])*k;
            float phase=(float)pos.Item3,beatPulse=(1-phase)*(1-phase)*(1-phase),down=pos.Item2==1?beatPulse:0,kick=pos.Item2%2==1?beatPulse:0;
            float beats=index>=0?index+phase:now*2,bar=(pos.Item2-1+phase)/4,tick=Mathf.Floor(beats)+Mathf.Clamp01((beats%1)*6),sway=wardMood[MoodSway];
            wardFan+=dt*wardMood[MoodFan]*2.2f;wardBeacon+=dt*wardMood[MoodSpin];float beam=.16f*wardMood[MoodBeacons]*(.6f+.4f*down);
            foreach(var p in wardMotion.parts)
            {
                if(p.matrix==null||p.matrix.Length!=16)continue;var m=new Matrix4x4();for(int i=0;i<16;i++)m[i]=p.matrix[i];Matrix4x4 change=Matrix4x4.identity;var entry=p.entry>=0?arena.Entry(p.entry):null;float opening=NativeVentRules.Open(entry,t,arena.Pve==null);
                switch(p.kind)
                {
                    // stage.ts updateEntries: the lid swings VENT_SWING·open and rattles while a Ward Zero vent warns (NativeStageVentFx).
                    case "vent":change=Matrix4x4.Rotate(Quaternion.Euler(0,0,(float)NativeStageVentFx.LidAngle(opening,arena.Pve==null&&entry!=null?NativeVentRules.Warning(entry,t):0,t)*Mathf.Rad2Deg));break;
                    case "door":change=Matrix4x4.Rotate(Quaternion.Euler(0,p.side*1.75f*opening*Mathf.Rad2Deg,0));break;
                    case "cell":change=Matrix4x4.Scale(new Vector3(1,arena.Pve==null?.45f/3.45f:Mathf.Lerp(1,.45f/3.45f,opening),1));break;
                    case "fan":change=Matrix4x4.Rotate(Quaternion.Euler(0,wardFan%(2*Mathf.PI)*Mathf.Rad2Deg,0));break;
                    case "beam":change=Matrix4x4.Rotate(Quaternion.Euler(0,(wardBeacon+p.phase)%(2*Mathf.PI)*Mathf.Rad2Deg,0));if(beam>.002f)sourceModels.Queue(p.id,m*change,opacity:beam);continue;
                    case "minute":case "hour":change=Matrix4x4.Rotate(Quaternion.Euler(0,0,tick*360/(p.kind=="minute"?16:192)));break;
                    case "chandelier":change=Matrix4x4.Rotate(Quaternion.Euler(sway*.025f*Mathf.Sin(2*Mathf.PI*(bar+p.phase)*2+1)*Mathf.Rad2Deg,0,sway*.05f*Mathf.Sin(2*Mathf.PI*(bar+p.phase))*Mathf.Rad2Deg));break;
                    case "doors":change=Matrix4x4.Translate(Vector3.forward*(-wardMood[MoodThump]*.09f*kick));break;
                }
                sourceModels.Queue(p.id,m*change);
            }
            if(pitMaterial==null)pitMaterial=new Material(Resources.Load<Shader>("NativeWardPit"));
            for(int e=0;e<8;e++)
            {var entry=arena.Entry(e);wardEntries[e]=entry;float open=NativeVentRules.Open(entry,t,arena.Pve==null);Vector3 at=NativeArena.StageVents[e];if(open>.001f){pitProperties.Clear();pitProperties.SetFloat("_Open",open);Graphics.DrawMesh(cube,Matrix4x4.TRS(at+Vector3.up*.012f,Quaternion.identity,new Vector3(2.1f,.005f,2.1f)),pitMaterial,0,null,0,pitProperties,UnityEngine.Rendering.ShadowCastingMode.Off,false);}}
            // The vents' rim, wisps, eruptions and smoke (the warning is the rim filling and black wisps, not a ring of cubes).
            StageVentEffects(arena,wardEntries);
        }
    }
}

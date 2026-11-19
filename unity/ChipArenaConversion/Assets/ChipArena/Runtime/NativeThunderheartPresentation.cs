using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // Public bridge for a private prefab. Its component can consume these channels for VLB
    // intensities or a single weather profile. The prefab is instantiated only in the drawn view.
    public abstract class ThunderheartAtmosphere : MonoBehaviour
    {
        public abstract bool OwnsBeams {get;}
        public abstract bool OwnsWeather {get;}
        public abstract void Pose(float bass,float body,float air,float crown,float quality);
    }

    internal sealed class ThunderheartArt : IDisposable
    {
        internal sealed class Piece
        {
            public string Name;
            public Vector3 Pivot;
            public Mesh Mesh;
            public int Petal=-1,Side;
            public NativePropBody Prop;
            public bool TracksProp;
        }
        public readonly List<Piece> Pieces=new List<Piece>();
        public Material Surface;
        public NativeMusicEnvelope Score;
        public ThunderheartArt()
        {
            try
            {
                var art=Resources.Load<TextAsset>("ThunderheartArt");
                var shader=Resources.Load<Shader>("ThunderheartSurface");
                if(!art||!shader)throw new InvalidDataException("Missing Carillon art or shader");
                var score=Resources.Load<TextAsset>("ThunderheartScore");
                if(score)Score=new NativeMusicEnvelope(score.bytes);
                Surface=new Material(shader);
                using(var reader=new BinaryReader(new MemoryStream(art.bytes,false)))
                {
                    if(reader.ReadUInt32()!=0x31524143)throw new InvalidDataException("Invalid Carillon art");
                    int count=reader.ReadUInt16();
                    for(int k=0;k<count;k++)
                    {
                        string name=System.Text.Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadByte()));
                        var p=new Piece{Name=name,Pivot=Point(reader),Side=name.EndsWith("-1",StringComparison.Ordinal)?-1:1};Pieces.Add(p);
                        if(name.StartsWith("petal",StringComparison.Ordinal))p.Petal=int.Parse(name.Substring(5));
                        p.TracksProp=name.EndsWith("-stop",StringComparison.Ordinal)||name.StartsWith("votive",StringComparison.Ordinal)||name.StartsWith("drum",StringComparison.Ordinal);
                        int nv=reader.ReadInt32(),nf=reader.ReadInt32();
                        var vertices=new Vector3[nv];var colors=new Color32[nv];var indices=new int[nf*3];
                        for(int i=0;i<nv;i++){vertices[i]=Point(reader);colors[i]=new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),reader.ReadByte());}
                        for(int i=0;i<indices.Length;i++)indices[i]=reader.ReadInt32();
                        p.Mesh=new Mesh{name=name,indexFormat=nv>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                        p.Mesh.vertices=vertices;p.Mesh.colors32=colors;p.Mesh.triangles=indices;p.Mesh.RecalculateNormals();p.Mesh.UploadMeshData(true);
                    }
                }
            }
            catch{Dispose();throw;}
        }
        private static Vector3 Point(BinaryReader r)=>new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        public void Dispose(){foreach(var p in Pieces)if(p.Mesh)CapturedAnimation.Release(p.Mesh);Pieces.Clear();if(Surface)CapturedAnimation.Release(Surface);}
    }

    public sealed partial class ArenaPresentation
    {
        public float MusicReaction=1;
        private ThunderheartArt carillon;
        private ThunderheartAtmosphere carillonAtmosphere;
        private GameObject carillonPrivateRoot;
        private NativeArena carillonView;
        private int carillonGeneration=-1;
        private bool carillonFailed;
        private float carillonWind=1;
        private MaterialPropertyBlock carillonProperties;
        private static readonly int ScoreId=Shader.PropertyToID("_Music"),SignalId=Shader.PropertyToID("_Signal");
        private bool CarillonWeatherOwned=>carillonAtmosphere&&carillonAtmosphere.OwnsWeather;
        private bool PrepareCarillon(NativeArena arena)
        {
            if(arena.MapId!=NativeThunderheart.Id||carillonFailed)return false;
            if(carillon==null)try{carillon=new ThunderheartArt();carillonProperties=new MaterialPropertyBlock();}catch(Exception e){carillonFailed=true;Debug.LogException(e);return false;}
            return true;
        }
        private void ClearCarillon()
        {
            if(carillonPrivateRoot){carillonPrivateRoot.SetActive(false);CapturedAnimation.Release(carillonPrivateRoot);}
            carillonPrivateRoot=null;carillonAtmosphere=null;carillonView=null;carillonGeneration=-1;
        }
        private void DrawCarillon(NativeArena arena)
        {
            if(arena.MapId!=NativeThunderheart.Id){if(carillonView!=null)ClearCarillon();return;}
            if(!PrepareCarillon(arena))return;
            if(!ReferenceEquals(arena,carillonView)||carillonGeneration!=arena.Generation)
            {
                ClearCarillon();carillonView=arena;carillonGeneration=arena.Generation;
                var addon=Resources.Load<GameObject>("CarillonPrivateAtmosphere");
                if(addon){carillonPrivateRoot=Instantiate(addon,transform);carillonAtmosphere=carillonPrivateRoot.GetComponent<ThunderheartAtmosphere>();}
                foreach(var p in carillon.Pieces)if(p.TracksProp)p.Prop=null;
            }
            var match=arena.Match;float now=FieldClock(arena);
            float time=(float)NativeThunderheart.Clock(match.Phase,match.FightAt,now);
            var score=carillon.Score?.At(time)??default;
            int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            var hs=arena.Map.hazards;
            var north=NativeWardZeroHazards.At(hs[0],match.Phase,round,match.FightAt,now);
            var south=NativeWardZeroHazards.At(hs[1],match.Phase,round,match.FightAt,now);
            float warning=NorthOrSouth(north,south),crown=Mathf.Max(Crown(north),Crown(south));
            float bass=score.Bass*MusicReaction,body=score.Body*MusicReaction,air=score.Air*MusicReaction;
            carillonWind=.78f+body*.42f+crown*.28f;
            if(carillonAtmosphere)carillonAtmosphere.Pose(bass,body,air,crown,LowGraphics?0:EffectsQuality);
            var beams=carillonAtmosphere&&carillonAtmosphere.OwnsBeams?null:AdvancedBeams;
            for(int i=0;i<4;i++)
            {
                double a=i*Math.PI/2+.54;
                beams?.Add(3*Math.Cos(a),12,3*Math.Sin(a),(6+3*crown)*Math.Cos(a),.3,(6+3*crown)*Math.Sin(a),.19,1.08,.46+.22*bass,.45+.18*body,.31+.2*air,.5,i);
            }
            carillonProperties.SetVector(ScoreId,new Vector4(bass,body,air+.22f*score.Attack*MusicReaction,time*1.6f));
            foreach(var p in carillon.Pieces)
            {
                var pos=p.Pivot;var rot=Quaternion.identity;var scale=Vector3.one;float signal=0;
                if(p.TracksProp)
                {
                    // Replica prop objects can be recreated by a keyframe. Rebind by stable authored ID.
                    if(!p.Prop||p.Prop.Health<=0){p.Prop=null;for(int i=0;i<arena.Props.Count;i++)if(arena.Props[i].Definition.id==p.Name){p.Prop=arena.Props[i];break;}}
                    if(!p.Prop||p.Prop.Health<=0)continue;pos=p.Prop.transform.position;rot=p.Prop.transform.rotation;
                    signal=.8f*(1-Mathf.Clamp01(p.Prop.Health/p.Prop.Definition.hp));
                }
                else if(p.Petal>=0)
                {
                    float a=p.Petal*Mathf.PI/4;
                    rot=Quaternion.AngleAxis(8+60*crown+3*body,new Vector3(Mathf.Sin(a),0,-Mathf.Cos(a)));
                    signal=warning*.9f;
                }
                else if(p.Name=="heart")rot=Quaternion.Euler(2*Mathf.Sin(time*.73f)*(bass+crown),0,3*Mathf.Sin(time*.73f+.4f)*(bass+crown));
                else if(p.Name=="orrery")rot=Quaternion.Euler(9+body*4,time*2.1f+7*body,0);
                else if(p.Name=="gate1"||p.Name=="gate-1")
                {
                    var h=hs[p.Side>0?0:1];pos.y=arena.CarillonGateBottom(h,now);scale.y=(5.35f-pos.y)/4.8f;signal=warning;
                }
                else if(p.Name=="well1"||p.Name=="well-1"||p.Name=="bellows")
                {
                    var h=hs[p.Name=="bellows"?4:p.Side<0?2:3];
                    var s=NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(match.Phase,match.FightAt,now));
                    bool fed=p.Name=="bellows"||arena.CarillonWellFed(h);
                    signal=fed?.28f+(float)s.Warn*.8f:0;
                    if(p.Name!="bellows")rot=Quaternion.Euler(0,fed?time*26:0,0);
                    if(fed&&s.Phase==NativeFactoryHazards.Bursting)beams?.Add(pos.x,pos.y+.1,pos.z,pos.x+(p.Name=="bellows"?3:0),pos.y+4.5,pos.z,.65,1.15,.19,.67,.60);
                }
                else if(p.Name=="grate")
                {
                    var g=NativeCyberCityHazards.Grid(hs[5],round,match.FightAt,now);
                    signal=match.Phase==NativePhase.Fight?(float)g.Level:0;
                }
                else if(p.Name=="sentinel1"||p.Name=="sentinel-1")
                {
                    float near=CarillonProximity(arena,pos);
                    rot=Quaternion.Euler(0,0,near*22*Mathf.Sin(now*3));signal=near*.55f;
                }
                carillonProperties.SetFloat(SignalId,signal);
                Graphics.DrawMesh(p.Mesh,Matrix4x4.TRS(pos,rot,scale),carillon.Surface,0,null,0,carillonProperties,LowGraphics?ShadowCastingMode.Off:ShadowCastingMode.On,true);
            }
        }
        internal static float CarillonProximity(NativeArena arena,Vector3 pos)
        {
            float d=36;
            foreach(var p in arena.Pilots)if(p.Hp>0&&p.Seat.connected)d=Mathf.Min(d,(p.Body.position-pos).sqrMagnitude);
            return 1-Mathf.Sqrt(d)/6;
        }
        private static float Crown(NativeHazardState s)=>s.Phase==NativeHazardPhase.Warn?.35f*s.Level:s.Phase==NativeHazardPhase.Danger?.35f+.65f*s.Level:NativeWardZeroHazards.Closure(s);
        private static float NorthOrSouth(NativeHazardState a,NativeHazardState b)=>Mathf.Max(a.Level,b.Level);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityPrototype
{
    public sealed class ArenaPresentation : MonoBehaviour
    {
        private Mesh cube;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Material> compatibilityMaterials = new List<Material>();
        private readonly List<Matrix4x4>[] batches = { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
        private readonly Matrix4x4[] drawBuffer = new Matrix4x4[1023];
        private readonly Dictionary<int, Vector3> visualPositions = new Dictionary<int, Vector3>();
        private readonly List<GameObject> debris = new List<GameObject>();
        private readonly List<GameObject> coverObjects = new List<GameObject>();
        private ParticleSystem particles;
        private Material particleMaterial;
        private int lastEvent;
        private Transform stage;
        public bool DebrisEnabled = true;
        public bool ForceCompatibilityRendering;
        public bool UsesInstancing => !ForceCompatibilityRendering && SystemInfo.supportsInstancing;
        public string RenderingMode => UsesInstancing ? "GPU instanced" : "Compatibility";
        public int DebrisCount { get { debris.RemoveAll(o => o == null); return debris.Count; } }
        public int ParticleCount { get { return particles != null ? particles.particleCount : 0; } }
        public int DrawnParts { get; private set; }
        public static Vector3 Vec(V3 v) { return new Vector3(v.x, v.y, v.z); }

        public void Initialize()
        {
            GameObject sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = sample.GetComponent<MeshFilter>().sharedMesh; Destroy(sample);
            Color[] colors = { new Color(.17f,.85f,.83f), new Color(.45f,.58f,1), new Color(.92f,.30f,.38f), new Color(.08f,.11f,.17f), new Color(.87f,.95f,1) };
            Shader shader = Resources.Load<Shader>("PrototypeLit");
            if (shader == null) throw new System.InvalidOperationException("PrototypeLit shader is missing");
            foreach (Color color in colors)
            {
                materials.Add(new Material(shader) { color = color, enableInstancing = true });
                compatibilityMaterials.Add(new Material(shader) { color = color, enableInstancing = false });
            }
            Debug.Log($"Unity prototype renderer: {RenderingMode}; {SystemInfo.graphicsDeviceType}; {SystemInfo.graphicsDeviceName}; shaderSupported={shader.isSupported}");
            stage = new GameObject("Arena geometry").transform; stage.SetParent(transform);
            Block("Floor", new Vector3(0, -.35f, 0), new Vector3(50, .7f, 50), materials[3], true);
            for (int i = -24; i <= 24; i += 4)
            {
                Block("Grid X", new Vector3(i,.012f,0), new Vector3(.025f,.014f,48), materials[0], false);
                Block("Grid Z", new Vector3(0,.012f,i), new Vector3(48,.014f,.025f), materials[0], false);
            }
            for (int i = 0; i < 4; i++)
            {
                bool alongX = i < 2; float sign = i % 2 == 0 ? -1 : 1;
                Vector3 pos = alongX ? new Vector3(0,.9f,sign*24.5f) : new Vector3(sign*24.5f,.9f,0);
                Block("Boundary",pos,alongX?new Vector3(50,1.8f,1):new Vector3(1,1.8f,50),materials[3],true);
                pos.y=1.82f; Block("Boundary light",pos,alongX?new Vector3(50,.06f,1.04f):new Vector3(1.04f,.06f,50),materials[0],false);
            }
            var fx = new GameObject("Impact particles"); fx.transform.SetParent(transform); particles = fx.AddComponent<ParticleSystem>(); particles.Stop();
            var main = particles.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20000; main.startLifetime = .55f; main.startSpeed = 0; main.startSize = .12f; main.gravityModifier = .25f;
            var emission = particles.emission; emission.enabled = false; var shape = particles.shape; shape.enabled = false;
            particleMaterial = new Material(Resources.Load<Shader>("PrototypeParticles"));
            var pr = particles.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = particleMaterial;
            var size = particles.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
        }
        private GameObject Block(string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.SetParent(stage); o.transform.position=position; o.transform.localScale=scale;
            o.GetComponent<Renderer>().sharedMaterial=material; if(!collider) Destroy(o.GetComponent<Collider>()); return o;
        }
        public void ResetPresentation()
        {
            lastEvent=0; visualPositions.Clear(); particles.Clear();
            foreach(var o in debris) if(o!=null) Destroy(o); debris.Clear();
            foreach(var o in coverObjects) if(o!=null) Destroy(o); coverObjects.Clear();
        }
        public Vector3 Position(Actor actor) { Vector3 p; return visualPositions.TryGetValue(actor.Id,out p)?p:Vec(actor.Position); }
        public void Render(Snapshot s, float renderTime, bool smooth)
        {
            if(s==null) return;
            foreach(var list in batches) list.Clear(); DrawnParts=0;
            foreach(Actor a in s.Actors)
            {
                Vector3 old; Vector3 target=Vec(a.Position);
                if(!visualPositions.TryGetValue(a.Id,out old)||(old-target).sqrMagnitude>64) old=target;
                Vector3 p=smooth?Vector3.Lerp(old,target,1-Mathf.Exp(-18*Time.unscaledDeltaTime)):target;
                visualPositions[a.Id]=p;
                if(!a.Alive) continue;
                int color=a.Bot?2:a.Id-1;
                if(renderTime<a.FlashUntil) color=4;
                Pose(a,p,renderTime,color);
            }
            foreach(Shot p in s.Shots) Add(4, Matrix4x4.TRS(Vec(p.Position),Quaternion.identity,Vector3.one*.18f));
            for(int k=0;k<batches.Length;k++)
            {
                var list=batches[k];
                for(int start=0;start<list.Count;start+=1023)
                {
                    int count=Mathf.Min(1023,list.Count-start); list.CopyTo(start,drawBuffer,0,count);
                    bool drawn=false;
                    if(UsesInstancing)
                    {
                        try
                        {
                            Graphics.DrawMeshInstanced(cube,0,materials[k],drawBuffer,count,null,ShadowCastingMode.Off,false);
                            drawn=true;
                        }
                        catch(System.InvalidOperationException e) { UseCompatibility(e.Message); }
                        catch(System.ArgumentException e) { UseCompatibility(e.Message); }
                    }
                    if(!drawn)
                        for(int j=0;j<count;j++) Graphics.DrawMesh(cube,drawBuffer[j],compatibilityMaterials[k],0);
                }
            }
            while(coverObjects.Count<s.Covers.Length) coverObjects.Add(null);
            for(int i=0;i<s.Covers.Length;i++)
            {
                Cover c=s.Covers[i]; var o=coverObjects[i];
                if(c.Hp<=0) { if(o!=null) Destroy(o); coverObjects[i]=null; continue; }
                if(o==null) coverObjects[i]=Block("Breakable cover "+c.Id,Vec(c.Position)+Vector3.up*c.Height*.5f,new Vector3(c.HalfX*2,c.Height,c.HalfZ*2),materials[3],true);
            }
            foreach(ArenaEvent e in s.Events)
            {
                if(e.Id<=lastEvent) continue; lastEvent=e.Id;
                if(e.Kind==EventKind.Shot||e.Kind==EventKind.Dodge) continue;
                Burst(Vec(e.Position), e.Kind==EventKind.Block?new Color(.3f,.9f,1):e.Kind==EventKind.Death?new Color(1,.2f,.35f):new Color(1,.7f,.3f),e.Kind==EventKind.Death?24:12);
                if(DebrisEnabled&&(e.Kind==EventKind.Break||e.Kind==EventKind.Death)) Debris(Vec(e.Position)+Vector3.up*.3f,e.Kind==EventKind.Death?materials[2]:materials[3]);
            }
        }
        private void UseCompatibility(string reason)
        {
            ForceCompatibilityRendering=true;
            Debug.LogWarning("Instanced drawing failed; switched characters and projectiles to compatibility rendering: "+reason);
        }
        private void Pose(Actor a,Vector3 position,float time,int color)
        {
            bool roll=a.Rolling(time), attack=a.Attacking(time), guard=time<a.BlockUntil;
            float speed=new Vector2(a.Velocity.x,a.Velocity.z).magnitude;
            float gait=Mathf.Sin(time*(a.Sprinting?17:12)+a.Id*.4f)*Mathf.Clamp01(speed/Rules.Speed);
            float phase=Mathf.Clamp01((time-a.AttackAt)/Rules.SwordDuration);
            float swing=attack?Mathf.Sin(phase*Mathf.PI):0;
            float rollAngle=roll?(time-a.RollAt)/Rules.RollDuration*360:0;
            Quaternion facing=Quaternion.Euler(0,a.Yaw,0);
            if(roll) facing=Quaternion.LookRotation(Vec(a.RollDirection));
            Matrix4x4 root=Matrix4x4.TRS(position+Vector3.up*(roll?.7f:0),facing*Quaternion.Euler(rollAngle,attack?(a.Combo==1?-1:1)*swing*35:0,0),Vector3.one);
            Part(root,new Vector3(0,1.04f,0),new Vector3(.57f,.66f,.34f),Quaternion.Euler(a.Sprinting?14:3,0,-gait*3),color);
            Part(root,new Vector3(0,.66f,0),new Vector3(.46f,.22f,.32f),Quaternion.identity,3);
            Part(root,new Vector3(0,1.58f,0),new Vector3(.43f,.42f,.39f),Quaternion.Euler(-4,0,0),color);
            Part(root,new Vector3(0,1.62f,.205f),new Vector3(.32f,.09f,.02f),Quaternion.identity,4);
            for(int side=-1;side<=1;side+=2)
            {
                float legAngle=roll?-75:gait*side*32;
                Matrix4x4 hip=root*Matrix4x4.TRS(new Vector3(side*.16f,.66f,0),Quaternion.Euler(legAngle,0,0),Vector3.one);
                Part(hip,new Vector3(0,-.17f,0),new Vector3(.22f,.35f,.25f),Quaternion.identity,color);
                Matrix4x4 knee=hip*Matrix4x4.TRS(new Vector3(0,-.33f,0),Quaternion.Euler(Mathf.Max(0,-legAngle)*.8f,0,0),Vector3.one);
                Part(knee,new Vector3(0,-.14f,0),new Vector3(.20f,.30f,.23f),Quaternion.identity,3);
                Part(knee,new Vector3(0,-.29f,.08f),new Vector3(.25f,.14f,.40f),Quaternion.identity,color);
                float armAngle=roll?-90:guard?-75:attack?(side==1?-135+swing*170:-25-swing*45):-gait*side*28;
                Matrix4x4 shoulder=root*Matrix4x4.TRS(new Vector3(side*.4f,1.32f,0),Quaternion.Euler(armAngle,0,side*(attack?15:6)),Vector3.one);
                Part(shoulder,new Vector3(0,-.18f,0),new Vector3(.22f,.36f,.24f),Quaternion.identity,color);
                Matrix4x4 elbow=shoulder*Matrix4x4.TRS(new Vector3(0,-.34f,0),Quaternion.Euler(guard?-75:attack?-35:-15,0,0),Vector3.one);
                Part(elbow,new Vector3(0,-.16f,0),new Vector3(.19f,.32f,.21f),Quaternion.identity,3);
                if(side==1)
                {
                    Part(elbow,new Vector3(0,-.41f,.1f),new Vector3(.35f,.08f,.14f),Quaternion.identity,color);
                    Part(elbow,new Vector3(0,-.98f,.1f),new Vector3(.10f,1.10f,.08f),Quaternion.identity,4);
                }
            }
            if(guard) Part(root,new Vector3(0,1,.58f),new Vector3(.9f,.9f,.055f),Quaternion.identity,0);
        }
        private void Part(Matrix4x4 parent,Vector3 p,Vector3 scale,Quaternion rotation,int color) { Add(color,parent*Matrix4x4.TRS(p,rotation,scale)); }
        private void Add(int color,Matrix4x4 matrix) { batches[color].Add(matrix); DrawnParts++; }
        private void Burst(Vector3 position,Color color,int count)
        {
            particles.Play();
            for(int i=0;i<count;i++)
            {
                var e=new ParticleSystem.EmitParams { position=position, velocity=Random.onUnitSphere*Random.Range(2,6),startColor=color,startSize=Random.Range(.05f,.17f),startLifetime=Random.Range(.3f,.8f) };
                particles.Emit(e,1);
            }
        }
        private void Debris(Vector3 position,Material material)
        {
            debris.RemoveAll(o=>o==null);
            for(int i=0;i<8&&debris.Count<512;i++)
            {
                var o=Block("Temporary PhysX debris",position+Random.insideUnitSphere*.35f,Vector3.one*Random.Range(.18f,.38f),material,true);
                var body=o.AddComponent<Rigidbody>(); body.mass=.5f; body.AddForce((Random.onUnitSphere+Vector3.up)*5,ForceMode.Impulse); body.AddTorque(Random.onUnitSphere*8,ForceMode.Impulse);
                debris.Add(o); Destroy(o,6);
            }
        }
        private void OnDestroy()
        {
            foreach(var m in materials) Destroy(m);
            foreach(var m in compatibilityMaterials) Destroy(m);
            if(particleMaterial!=null) Destroy(particleMaterial);
        }
    }
}

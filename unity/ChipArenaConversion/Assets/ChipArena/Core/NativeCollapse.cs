using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    public sealed class CollapseBox
    {public Vector3 Center,Half;public Quaternion Rotation=Quaternion.Identity;public bool Ankle;}
    public sealed class CollapsePart
    {public string Name,Parent;public Vector3 Position,Anchor;public Quaternion Rotation=Quaternion.Identity;public bool Free;public CollapseBox[] Boxes=Array.Empty<CollapseBox>();public bool HasAnkle;public Vector3 AnklePivot;public Quaternion AnkleFrame=Quaternion.Identity;}
    public sealed class CollapseProp
    {public Vector3 Position,Size;public Quaternion Rotation=Quaternion.Identity;}
    public sealed class CollapsePose
    {public Vector3 Position;public Quaternion Rotation=Quaternion.Identity,Ankle=Quaternion.Identity;}
    // Port of body-collapse.ts. All mathematics stays in the original right-handed rig frame.
    // The Unity adapter reflects the result once, and releases bodies with these pose derivatives.
    public sealed class NativeCollapse
    {
        private sealed class Node
        {
            public CollapsePart Source;public Node Parent;public readonly List<Node> Children=new List<Node>();
            public Quaternion Rel0=Quaternion.Identity,RelK=Quaternion.Identity,Foot0=Quaternion.Identity;public string Kind;
        }
        public readonly float Release,KneesAt,ReleaseAt;public float FallAngle=>fall;
        private float fall,ground,low0,joltPitch;private readonly List<Node> nodes=new List<Node>();private Node root;private Node[] knees;
        private Quaternion facing,pelvisK,footK;private Vector3 lateral,pelvisKPos,pivot,slideVelocity,joltSpine;
        private readonly Dictionary<string,CollapsePose>[] poses={new Dictionary<string,CollapsePose>(),new Dictionary<string,CollapsePose>(),new Dictionary<string,CollapsePose>()};
        private static float Clamp(float x,float a=0,float b=1)=>Math.Max(a,Math.Min(b,x));
        private static float Smooth(float x){x=Clamp(x);return x*x*(3-2*x);}
        private static Quaternion Rx(float a)=>Quaternion.CreateFromAxisAngle(Vector3.UnitX,a);
        private static Vector3 Rotate(Vector3 v,Quaternion q)=>Vector3.Transform(v,q);
        private static string Kind(string s)=>s=="pelvis"||s=="spine"||s=="head"?s:s.EndsWith("-shoulder")?"arm":s.EndsWith("-elbow")?"elbow":s.EndsWith("-hip")?"hip":s.EndsWith("-knee")?"knee":"other";
        public static bool Supports(IEnumerable<CollapsePart> parts){var have=new HashSet<string>(parts.Where(p=>!p.Free).Select(p=>p.Name));return new[]{"pelvis","spine","left-hip","left-knee","right-hip","right-knee"}.All(have.Contains);}
        public NativeCollapse(CollapsePart[] parts,float facingRadians,Vector3 velocity,float ground,Vector3 shot,float severity=1,CollapseProp[] props=null,float kneesAt=.32f,float releaseAt=.82f,float step=1f/60)
        {
            if(!Supports(parts))throw new ArgumentException("Collapse requires an attached pelvis, trunk and both legs.");
            KneesAt=Math.Max(.05f,kneesAt);ReleaseAt=Math.Max(KneesAt+.05f,releaseAt);Release=(float)Math.Ceiling(ReleaseAt/step-1e-6)*step;this.ground=ground;
            var byName=new Dictionary<string,Node>();foreach(var p in parts){var n=new Node{Source=p,Kind=Kind(p.Name)};if(p.HasAnkle)n.Foot0=p.Rotation*p.AnkleFrame;nodes.Add(n);byName.Add(p.Name,n);}
            foreach(var n in nodes)if(!n.Source.Free&&n.Source.Parent!=null&&byName.TryGetValue(n.Source.Parent,out var parent)){n.Parent=parent;parent.Children.Add(n);n.Rel0=Quaternion.Inverse(parent.Source.Rotation)*n.Source.Rotation;}
            root=byName["pelvis"];knees=nodes.Where(n=>n.Kind=="knee"&&!n.Source.Free&&n.Parent!=null).ToArray();
            facing=Quaternion.CreateFromAxisAngle(Vector3.UnitY,facingRadians);lateral=Rotate(Vector3.UnitX,facing);pelvisK=facing*Rx(.1f);footK=facing*Rx(.12f);
            foreach(var n in nodes){int side=n.Source.Name.StartsWith("left-")?1:n.Source.Name.StartsWith("right-")?-1:0;
                n.RelK=n.Kind=="hip"?Quaternion.Inverse(pelvisK)*facing:n.Kind=="knee"?Rx(1.2f):n.Kind=="spine"?Rx(.08f):n.Kind=="head"?Rx(.15f):n.Kind=="arm"?CapturedRig.Euler(-.12f,0,side*.06f):n.Kind=="elbow"?Rx(-.35f):n.Rel0;}
            if(shot.LengthSquared()>1e-9f)shot=Vector3.Normalize(shot);joltPitch=.38f*(.6f+.4f*Clamp(severity))*Clamp(shot.Z*1.2f,-1,1);joltSpine=Vector3.Cross(Vector3.UnitY,shot);slideVelocity=new Vector3(velocity.X,0,velocity.Z);
            pelvisKPos=root.Source.Position;var start=Solve(0,poses[0],false);low0=knees.Min(n=>Lowest(n,start[n.Source.Name]));Vector3? before=Feet(start);
            var kneel=Solve(KneesAt+1e-6f,poses[0],false);Vector3? after=Feet(kneel);if(before.HasValue&&after.HasValue){Vector3 forward=Rotate(Vector3.UnitZ,facing);pelvisKPos+=forward*Clamp(Vector3.Dot(before.Value-after.Value,forward),-.1f,.35f);}
            kneel=Solve(KneesAt+1e-6f,poses[0],false);float low=knees.Min(n=>Lowest(n,kneel[n.Source.Name]));if(MathUtil.Finite(low))pelvisKPos.Y+=ground+.006f-low;
            kneel=Solve(KneesAt+1e-6f,poses[0],false);foreach(var n in knees)pivot+=kneel[n.Source.Name].Position;pivot/=Math.Max(1,knees.Length);fall=ClearAngle(kneel,props??Array.Empty<CollapseProp>());
            bool Clear(float angle){fall=angle;for(int i=0;i<=16;i++){var p=Solve(KneesAt+(Release-KneesAt)*i/16,poses[1],true);foreach(var n in nodes)if(!n.Source.Free&&n.Kind!="knee"&&n.Kind!="hip"&&p.TryGetValue(n.Source.Name,out var pose)&&Lowest(n,pose)<ground+.025f)return false;}return true;}
            float most=fall;if(!Clear(most)){float lo=0,hi=most;for(int i=0;i<8;i++){float mid=(lo+hi)/2;if(Clear(mid))lo=mid;else hi=mid;}fall=lo;}else fall=most;
        }
        private Vector3? Feet(Dictionary<string,CollapsePose> pose){Vector3 sum=Vector3.Zero;int count=0;foreach(var k in knees)if(k.Source.HasAnkle){var p=pose[k.Source.Name];sum+=Rotate(k.Source.AnklePivot,p.Rotation)+p.Position;count++;}return count>0?sum/count:(Vector3?)null;}
        private static float Lowest(Node n,CollapsePose p)
        {
            float low=float.PositiveInfinity;foreach(var b in n.Source.Boxes)for(int i=0;i<8;i++){Vector3 corner=new Vector3((i&1)!=0?b.Half.X:-b.Half.X,(i&2)!=0?b.Half.Y:-b.Half.Y,(i&4)!=0?b.Half.Z:-b.Half.Z);corner=Rotate(corner,b.Rotation)+b.Center;if(b.Ankle&&n.Source.HasAnkle)corner=Rotate(corner-n.Source.AnklePivot,p.Ankle)+n.Source.AnklePivot;low=Math.Min(low,(Rotate(corner,p.Rotation)+p.Position).Y);}return low;
        }
        public IReadOnlyDictionary<string,CollapsePose> Pose(float age)=>Solve(Clamp(age,0,Release),poses[2],true);
        public void Each(float age,Action<string,CollapsePose,Vector3,Vector3> visit)
        {
            const float h=1f/480;float t=Clamp(age,0,Release),a=Math.Max(0,t-h),b=t+h;
            var A=Solve(a,poses[0],true);var B=Solve(b,poses[1],true);var P=Solve(t,poses[2],true);
            foreach(var pair in P){var pa=A[pair.Key];var pb=B[pair.Key];Vector3 lin=(pb.Position-pa.Position)/(b-a);Quaternion q=Quaternion.Normalize(pb.Rotation*Quaternion.Inverse(pa.Rotation));if(q.W<0)q=new Quaternion(-q.X,-q.Y,-q.Z,-q.W);float s=(float)Math.Sqrt(q.X*q.X+q.Y*q.Y+q.Z*q.Z),angle=2*(float)Math.Atan2(s,q.W);Vector3 ang=s<1e-9f?Vector3.Zero:new Vector3(q.X,q.Y,q.Z)*(angle/(s*(b-a)));visit(pair.Key,pair.Value,lin,ang);}
        }
        private static CollapsePose Entry(Dictionary<string,CollapsePose> map,string key){if(!map.TryGetValue(key,out var p)){p=new CollapsePose();map.Add(key,p);}return p;}
        private Dictionary<string,CollapsePose> Solve(float t,Dictionary<string,CollapsePose> output,bool withFall)
        {
            float legs=Smooth((t-.03f)/(KneesAt-.03f)),drop=Smooth((float)Math.Pow(Clamp((t-.03f)/(KneesAt-.03f)),.8)),arms=Smooth(t/.22f),trunk=Smooth(t/KneesAt),f=withFall?Math.Max(0,(t-KneesAt)/(ReleaseAt-KneesAt)):0,phi=fall*f*f,fs=Smooth(f),pulse=t<.05f?(float)Math.Sin(Math.PI/2*t/.05f):(float)Math.Exp(-(t-.05f)/.11f);
            var rp=Entry(output,root.Source.Name);rp.Rotation=Quaternion.CreateFromAxisAngle(lateral,phi)*Quaternion.Slerp(root.Source.Rotation,pelvisK,legs);rp.Position=Vector3.Lerp(root.Source.Position,pelvisKPos,drop);
            bool headFree=!nodes.Any(n=>n.Kind=="head"&&!n.Source.Free);
            void Visit(Node n){foreach(var c in n.Children){var p=output[n.Source.Name];var e=Entry(output,c.Source.Name);float w=c.Kind=="arm"||c.Kind=="elbow"?arms:c.Kind=="hip"||c.Kind=="knee"?legs:trunk;Quaternion rel=Quaternion.Slerp(c.Rel0,c.RelK,w);
                if(c.Kind=="hip")rel*=Rx(-.15f*phi);else if(c.Kind=="knee")rel*=Rx(-.85f*phi);else if(c.Kind=="spine"){rel*=Rx(.12f*fs);if(joltSpine.LengthSquared()>1e-9f)rel=Quaternion.CreateFromAxisAngle(Vector3.Normalize(joltSpine),(headFree?.12f:.05f)*pulse)*rel;}else if(c.Kind=="head")rel*=Rx(.35f*fs+joltPitch*pulse);else if(c.Kind=="arm")rel=Rx(.15f*phi)*rel;
                e.Rotation=p.Rotation*rel;e.Position=Rotate(c.Source.Anchor,p.Rotation)+p.Position;if(c.Source.HasAnkle)e.Ankle=Quaternion.Inverse(e.Rotation)*Quaternion.Slerp(c.Foot0,footK,legs)*Quaternion.Inverse(c.Source.AnkleFrame);Visit(c);}}
            Visit(root);
            if(phi>0){Vector3 mid=Vector3.Zero;foreach(var k in knees)mid+=output[k.Source.Name].Position;mid=pivot-mid/Math.Max(1,knees.Length);foreach(var p in output.Values)p.Position+=mid;}
            if(withFall&&t<KneesAt+1e-6f){float limit=low0+(ground+.006f-low0)*legs,low=knees.Min(k=>Lowest(k,output[k.Source.Name]));float lift=limit-low;if(lift>0)foreach(var p in output.Values)p.Position+=Vector3.UnitY*lift;}
            if(withFall){Vector3 slide=slideVelocity*((1-(float)Math.Exp(-5*t))/5);foreach(var p in output.Values)p.Position+=slide;}return output;
        }
        private float ClearAngle(Dictionary<string,CollapsePose> kneel,CollapseProp[] props)
        {
            if(props.Length==0)return 1.45f;var probes=new List<Vector3>();foreach(var n in nodes.Where(n=>!n.Source.Free&&(n.Kind=="spine"||n.Kind=="head"))){if(!kneel.TryGetValue(n.Source.Name,out var p))continue;foreach(var b in n.Source.Boxes){probes.Add(Rotate(b.Center,p.Rotation)+p.Position);probes.Add(Rotate(b.Center+Rotate(new Vector3(0,b.Half.Y,b.Half.Z),b.Rotation),p.Rotation)+p.Position);}}
            bool Inside(Vector3 v){foreach(var p in props){var local=Rotate(v-p.Position,Quaternion.Inverse(p.Rotation));if(Math.Abs(local.X)<p.Size.X/2+.08f&&Math.Abs(local.Y)<p.Size.Y/2+.08f&&Math.Abs(local.Z)<p.Size.Z/2+.08f)return true;}return false;}
            if(probes.Any(Inside))return 0;for(float a=.05f;a<=1.45f+1e-6f;a+=.05f){var q=Quaternion.CreateFromAxisAngle(lateral,a);if(probes.Any(x=>Inside(Rotate(x-pivot,q)+pivot)))return Math.Max(0,a-.1f);}return 1.45f;
        }
    }
}

using System;
namespace ChipArena.UnityConversion
{
    // Stable serialized IDs: append actions/buttons, never reorder. Independent of device drivers and glyphs.
    public enum PadAction { Attack, Aim, Ability, Jump, Roll, Reload, Interact, Block, Sprint, Lock, Slot1, Slot2, Slot3, Slot4, Slot5, Chips, Board, Camera, Pause, Count }
    public enum PadButton { South, East, West, North, LeftShoulder, RightShoulder, LeftTrigger, RightTrigger, LeftStick, RightStick, Up, Down, Left, Right, Start, Count }
    [Serializable] public sealed class NativeControllerPreferences
    {
        public int schema=1;
        public bool enabled=true, invertY, swapSticks;
        public string labels="Auto";
        public float sensitivity=180, deadzone=.18f;
        public int[] bindings;
        public void Normalize()
        {
            if(schema!=1){schema=1;enabled=true;invertY=swapSticks=false;labels="Auto";sensitivity=180;deadzone=.18f;bindings=null;}
            if(labels!="Auto"&&labels!="Xbox"&&labels!="PlayStation"&&labels!="Steam")labels="Auto";
            sensitivity=Finite(sensitivity)?Math.Max(45,Math.Min(360,sensitivity)):180;
            deadzone=Finite(deadzone)?Math.Max(.05f,Math.Min(.45f,deadzone)):.18f;
            if(bindings==null||bindings.Length!=(int)PadAction.Count){bindings=NativeControllerBindings.Defaults();return;}
            var defaults=NativeControllerBindings.Defaults();
            for(int i=0;i<bindings.Length;i++)if(bindings[i]<0||bindings[i]>=(int)PadButton.Count*2){bindings=defaults;return;}
            for(int i=0;i<bindings.Length;i++)for(int j=0;j<i;j++)if(bindings[i]==bindings[j]){bindings=defaults;return;}
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    }
    public static class NativeControllerBindings
    {
        public static readonly string[] Actions={"Attack / fire","Aim / Q / mode","Ability E","Jump","Dodge","Reload","Interact","Block / parry","Sprint (hold)","Lock / cycle target","Ability 1 / Hadoken","Ability 2 / uppercut","Ability 3","Ability 4","Ability 5","Chips","Expedition board","Camera view","Pause"};
        public static int Code(PadButton b,bool modified=false)=>(int)b+(modified?(int)PadButton.Count:0);
        public static int[] Defaults()=>new[]{Code(PadButton.RightTrigger),Code(PadButton.LeftTrigger),Code(PadButton.RightShoulder),Code(PadButton.South),Code(PadButton.East),Code(PadButton.West),Code(PadButton.North),Code(PadButton.LeftShoulder),Code(PadButton.LeftStick),Code(PadButton.RightStick),Code(PadButton.Up),Code(PadButton.Right),Code(PadButton.Down),Code(PadButton.Left),Code(PadButton.Up,true),Code(PadButton.North,true),Code(PadButton.West,true),Code(PadButton.RightStick,true),Code(PadButton.Start)};
        // Swap an existing assignment so neither action silently disappears.
        public static int Bind(NativeControllerPreferences p,PadAction action,int code)
        {
            p.Normalize();if((int)action<0||action>=PadAction.Count||code<0||code>=(int)PadButton.Count*2)throw new ArgumentOutOfRangeException();
            int at=(int)action,old=p.bindings[at],other=Array.IndexOf(p.bindings,code);
            if(other>=0)p.bindings[other]=old;p.bindings[at]=code;return other==at?-1:other;
        }
        public static string Label(int code,string layout)
        {
            string[] names=layout=="PlayStation"?PlayStation:Xbox;
            return (code>=(int)PadButton.Count?(layout=="PlayStation"?"Create/Share + ":layout=="Steam"?"Back + ":"View + "):"")+names[code%(int)PadButton.Count];
        }
        static readonly string[] Xbox={"A","B","X","Y","LB","RB","LT","RT","L stick click","R stick click","D-pad up","D-pad down","D-pad left","D-pad right","Menu"};
        static readonly string[] PlayStation={"Cross","Circle","Square","Triangle","L1","R1","L2","R2","L3","R3","D-pad up","D-pad down","D-pad left","D-pad right","Options"};
        public static float AxisScale(float magnitude,float deadzone)=>magnitude<=deadzone?0:Math.Min(1,(magnitude-deadzone)/(1-deadzone))/magnitude;
    }
    // Latch a chord at the physical press. Releasing the modifier cannot turn a held chord into a fresh attack.
    public sealed class NativeControllerState
    {
        public readonly bool[] Held=new bool[(int)PadAction.Count],Down=new bool[(int)PadAction.Count],Up=new bool[(int)PadAction.Count];
        private readonly int[] pressedCodes=new int[(int)PadButton.Count];
        private uint previous;private bool neutral=true;
        public void Step(int[] bindings,uint buttons,bool modifier,bool blocked)
        {
            if(blocked)neutral=true;
            bool suppress=blocked||neutral;
            if(neutral&&!blocked&&buttons==0&&!modifier){neutral=false;previous=0;}
            for(int b=0;b<pressedCodes.Length;b++)
            {
                uint bit=1u<<b;int code=b+(modifier?(int)PadButton.Count:0);
                if(suppress||(buttons&bit)==0)pressedCodes[b]=-1;
                else if((previous&bit)==0)pressedCodes[b]=code;
                else if(pressedCodes[b]!=code)pressedCodes[b]=-1;
            }
            for(int a=0;a<Held.Length;a++)
            {
                bool held=!suppress&&pressedCodes[bindings[a]%(int)PadButton.Count]==bindings[a];
                Down[a]=held&&!Held[a];Up[a]=!held&&Held[a];Held[a]=held;
            }
            previous=buttons;
        }
    }

}

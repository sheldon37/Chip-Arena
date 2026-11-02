using System;
namespace ChipArena.UnityConversion
{
    // Device-independent directional focus and repeat. Rectangles are in screen pixels.
    public sealed class NativeMenuNavigation
    {
        public struct Item { public float X,Y,W,H,ClipY,ClipH;public bool Slider,Clipped;public float CX=>X+W/2;public float CY=>Y+H/2; }
        private readonly Item[] items=new Item[1024];
        public int Count {get;private set;} public int Focus {get;private set;}=-1;
        private int held;private float repeatAt;
        public void Reset(){Count=0;Focus=-1;held=0;repeatAt=0;}
        public void BeginFrame(){Count=0;}
        public int Add(Item item){if(Count==items.Length||item.W<=0||item.H<=0)return -1;items[Count]=item;return Count++;}
        public bool Current(out Item item){if(Focus<0||Focus>=Count){item=default;return false;}item=items[Focus];return true;}
        public void EnsureFocus(){if(Count==0){Focus=-1;return;}if(Focus>=0&&Focus<Count)return;Focus=0;for(int i=0;i<Count;i++)if(!items[i].Clipped||items[i].CY>=items[i].ClipY&&items[i].CY<=items[i].ClipY+items[i].ClipH){Focus=i;break;}}
        public int Pulse(float x,float y,float now)
        {
            int direction=Math.Max(Math.Abs(x),Math.Abs(y))<.55f?0:Math.Abs(x)>Math.Abs(y)?(x>0?1:2):(y>0?3:4);
            if(direction==0){held=0;return 0;}
            if(direction!=held){held=direction;repeatAt=now+.35f;return direction;}
            if(now<repeatAt)return 0;repeatAt=now+.12f;return direction;
        }
        // 1 right, 2 left, 3 down, 4 up. Returns a slider adjustment instead of leaving its row.
        public int Move(int direction)
        {
            bool had=Current(out var origin);EnsureFocus();if(!had||!Current(out origin))return 0;
            if(origin.Slider&&(direction==1||direction==2))return direction==1?1:-1;
            int best=-1;float score=float.MaxValue;bool horizontal=direction<=2;float sign=direction==1||direction==3?1:-1;
            for(int i=0;i<Count;i++)
            {
                if(i==Focus)continue;var n=items[i];float forward=((horizontal?n.CX-origin.CX:n.CY-origin.CY))*sign;
                float cross=Math.Abs(horizontal?n.CY-origin.CY:n.CX-origin.CX);if(forward<=1)continue;
                float s=forward+cross*4;if(s<score){score=s;best=i;}
            }
            if(best>=0)Focus=best;return 0;
        }
        public float ScrollNeeded()
        {
            if(!Current(out var i)||!i.Clipped)return 0;
            if(i.Y<i.ClipY)return i.Y-i.ClipY-6;
            if(i.Y+i.H>i.ClipY+i.ClipH)return i.Y+i.H-i.ClipY-i.ClipH+6;return 0;
        }
    }
}

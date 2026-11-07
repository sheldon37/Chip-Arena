using System;
namespace ChipArena.UnityConversion
{
    // The controller's menu focus feedback (Claude, 2026-11-02; owner: "Can we create a Hover sound with a glow for better
    // menu navigation?"): when the hover tick sounds and how the focus glow moves (Runtime NativeMenuFocusFx draws it).
    //  - The tick is an edge: the focus moved to another control, or focus is shown again after the mouse took over. A held
    //    focus, a list scrolling under it, or the same control frame after frame never ticks again.
    //  - The outline pops in from PopGrow px outside the control over Pop seconds, then the glow breathes with Period.
    //  - Reduced effects: no pop and no breathing (the glow and the tick stay).
    public sealed class NativeMenuFocusMotion
    {
        public const double Pop=.16,Period=1.6;
        public const float PopGrow=10;
        private int focus=-1;private bool shown;private double movedAt=double.NegativeInfinity;
        public double MovedAt=>movedAt;
        /// <summary>One drawn frame with focus on control `index` at `now` s: true when it should sound the hover tick.</summary>
        public bool Step(int index,double now)
        {
            bool edge=!shown||index!=focus;shown=true;focus=index;if(edge)movedAt=now;return edge;
        }
        /// <summary>No focus shown this frame (the mouse took over, or no control): the next focus is a new edge.</summary>
        public void Hide(){shown=false;focus=-1;}
        /// <summary>The pop left `since` s after a move, 1 at the move easing to 0 at Pop (cubic ease-out), 0 when reduced.</summary>
        public static float PopAt(double since,bool reduced)
        {
            if(reduced||!(since>=0)||since>=Pop)return 0;double t=1-since/Pop;return (float)(t*t*t);
        }
        /// <summary>The resting glow's breath, 0 to 1 over Period s; 0 when reduced.</summary>
        public static float Breath(double now,bool reduced)=>reduced||double.IsNaN(now)?0:(float)(.5+.5*Math.Sin(now*2*Math.PI/Period));
    }
}

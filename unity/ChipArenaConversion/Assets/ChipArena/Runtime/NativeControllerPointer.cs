using UnityEngine;
namespace ChipArena.UnityConversion
{
    // IMGUI receives genuine Layout/Repaint passes. Controller activation is an extra local hit test,
    // never an OS mouse event or a replacement Repaint (which would blank the menu during scrolling).
    public static class NativeControllerPointer
    {
        public static bool Active;
        private static Vector2 press,point;
        private static bool pressed;
        private static int released=-1,consumed=-1;
        private static Rect? clip;
        public static void Update(Vector2 at,bool down,bool up)
        {
            point=at;if(down){press=at;pressed=true;}
            if(up){if(pressed)released=Time.frameCount;pressed=false;}
        }
        public static void Cancel(){pressed=false;released=-1;Active=false;clip=null;}
        static Rect ScreenRect(Rect rect)
        {
            var a=GUIUtility.GUIToScreenPoint(rect.min);var b=GUIUtility.GUIToScreenPoint(rect.max);
            // GUIToScreenPoint includes the Editor game-view window origin; subtract that origin.
            var origin=GUIUtility.GUIToScreenPoint(Event.current.mousePosition)-point;
            return Rect.MinMaxRect(a.x-origin.x,a.y-origin.y,b.x-origin.x,b.y-origin.y);
        }
        public static void SetClip(Rect rect){if(Active)clip=ScreenRect(rect);}
        public static void ClearClip(){clip=null;}
        public static bool Click(Rect rect)
        {
            if(!Active||!GUI.enabled||Event.current.type!=EventType.Repaint||released!=Time.frameCount||consumed==Time.frameCount)return false;
            if(clip.HasValue&&!clip.Value.Contains(point))return false;
            var screen=ScreenRect(rect);if(!screen.Contains(point)||!screen.Contains(press))return false;
            consumed=Time.frameCount;GUIUtility.keyboardControl=0;return true;
        }
        public static bool Button(string text,GUIStyle style,params GUILayoutOption[] options)
        {bool hit=GUILayout.Button(text,style,options);return hit||Click(GUILayoutUtility.GetLastRect());}
        public static bool Toggle(bool value,string text,params GUILayoutOption[] options)=>Toggle(value,text,GUI.skin.toggle,options);
        public static bool Toggle(bool value,string text,GUIStyle style,params GUILayoutOption[] options)
        {bool result=GUILayout.Toggle(value,text,style,options);return Click(GUILayoutUtility.GetLastRect())?!result:result;}
        public static float Slider(float value,float min,float max)
        {
            float result=GUILayout.HorizontalSlider(value,min,max);var rect=GUILayoutUtility.GetLastRect();
            if(Active&&GUI.enabled&&Event.current.type==EventType.Repaint&&(pressed||released==Time.frameCount)&&(clip==null||clip.Value.Contains(point)))
            {
                var screen=ScreenRect(rect);if(screen.Contains(press)&&screen.width>0){result=Mathf.Lerp(min,max,Mathf.Clamp01((point.x-screen.x)/screen.width));consumed=Time.frameCount;}
            }
            return result;
        }
    }
}

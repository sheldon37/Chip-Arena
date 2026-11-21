using UnityEngine;
using Q=ChipArena.UnityConversion.NativeQuotaMarks;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        /// <summary>
        /// Noclip (THE HUM's relic, docs/BOSS-CHIPS.md): whoever the local pilot's roll went through is shown to them through walls while the rule's
        /// mark lasts (3 s): the quota marker's kite in the HUM's fluorescent white over their head, drawn over the scene, buzzing like a tube
        /// (steady with Reduced effects) and fading out with the mark. Repaint only; reads the presented marks, decides nothing.
        /// </summary>
        private void RelicRevealHud(float width,float height)
        {
            if(Event.current.type!=EventType.Repaint||arena?.Minions==null||cameraView==null)return;int me=arena.LocalId;if(me<=0)return;
            float now=arena.Time;bool reduced=prefs.reducedEffects;var tone=NativeArena.ColorOf(NativeBossChips.Sets[NativeBossChips.SetOf("boss-hum")].Label);
            var list=arena.Minions;
            for(int i=0;i<list.Count;i++){var m=list[i];if(m?.Body==null||m.Hp<=0)continue;float left=Left(m.TechniqueMarks,me,now);if(left>0)Reveal(m.Body.position+Vector3.up*(m.Collider!=null?m.Collider.bounds.extents.y+.35f:1.4f),left,tone,reduced,now,m.Id,width,height);}
            foreach(var p in arena.Pilots){if(p?.Body==null||p.Hp<=0||p.Seat.id==me)continue;float left=Left(p.TechniqueMarks,me,now);if(left>0)Reveal(p.Body.position+Vector3.up*1.35f,left,tone,reduced,now,p.Seat.id,width,height);}
        }
        private static float Left(NativeTechniqueMark[] marks,int me,float now)
        {if(marks==null)return 0;float left=0;foreach(var k in marks)if(k.Kind==NativeBossChips.Noclip&&k.By==me&&now<k.Until)left=Mathf.Max(left,k.Until-now);return left;}
        private void Reveal(Vector3 head,float left,Color tone,bool reduced,float now,int id,float width,float height)
        {
            var vp=cameraView.WorldToViewportPoint(head);if(vp.z<=0)return;
            float buzz=reduced?1:.82f+.18f*Mathf.Sin(now*47+id),stutter=!reduced&&Mathf.Repeat(now*1.7f+id*.37f,1)<.06f?.5f:1;float a=Mathf.Clamp01(left/.5f)*buzz*stutter;
            float dist=Vector3.Distance(cameraView.transform.position,head),size=Mathf.Lerp(30,18,Mathf.Clamp01((dist-6)/30));var at=new Vector2(vp.x*width,(1-vp.y)*height);
            float b=Q.ShapeBox;var box=new Rect(at.x-b*.5f,at.y-b*.5f,b,b);
            U.Push(at,Vector2.zero,size/b);
            if(!reduced&&!presentation.LowGraphics)U.Glow(new Rect(box.x+b*.25f,box.y+b*.25f,b*.5f,b*.5f),new Color(tone.r,tone.g,tone.b,.35f*a),b*.35f);
            NativeIcons.DrawMarkup(new Rect(box.x+1,box.y+1.5f,b,b),"quota-mark-body",Q.MarkerBody,24,new Color(0,0,0,.5f*a));
            NativeIcons.DrawMarkup(box,"quota-mark-body",Q.MarkerBody,24,new Color(tone.r,tone.g,tone.b,.28f*a));
            NativeIcons.DrawMarkup(box,"quota-mark-edge",Q.MarkerEdge,24,new Color(tone.r,tone.g,tone.b,a));
            NativeIcons.DrawMarkup(box,"quota-mark-core",Q.MarkerCore,24,new Color(1,1,1,a));
            U.Pop();
            U.Text(new Rect(at.x-40,at.y-size*.5f-17,80,14),Q.Metres(dist),10,new Color(tone.r,tone.g,tone.b,a),TextAnchor.MiddleCenter,bold:true,mono:true,shadow:new Color(0,0,0,.85f*a));
        }
    }
}

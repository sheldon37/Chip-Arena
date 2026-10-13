using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    internal sealed partial class NativeCorpse
    {
        private readonly NativeArena arena;
        internal readonly bool RetainUntilRoundEnd;
        private readonly Dictionary<NativePropBody,BoxCollider> obstacles=new Dictionary<NativePropBody,BoxCollider>();
        private readonly HashSet<NativePropBody> seenObstacles=new HashSet<NativePropBody>();
        private readonly List<NativePropBody> staleObstacles=new List<NativePropBody>();
        private float worldAt=float.NegativeInfinity,settleAt=NativeCorpseRules.SettleSeconds;
        // Copy actual collider bounds, including Brickfall's StaticBlocks (slab, stairs and rubble). A large slab's
        // centre can be far from the body. Never truncate the list before its support surface has been considered.
        // Copies live only in the cosmetic scene; corpses never obstruct players, bullets or other bodies.
        private bool RefreshWorld(float age)
        {
            // A bisected body's second half lives in its first half's scene, whose copies serve both.
            if(Host!=null)return false;
            if(age<worldAt+NativeCorpseRules.WorldRefreshSeconds)return false;worldAt=age;
            bool changed=false;seenObstacles.Clear();float radius2=NativeCorpseRules.WorldRadius*NativeCorpseRules.WorldRadius;
            foreach(var prop in arena.AllBlocks)
            {
                if(prop==null)continue;var original=prop.GetComponent<BoxCollider>();
                // Cut treads retain their authored boxes for this separate cosmetic world; gameplay uses the smooth ramp.
                if(original==null||(!original.enabled&&!NativeCyberCityStairs.IsTread(arena.MapId,prop.Definition?.id)&&!NativeMapRamps.IsVisual(arena.Map,prop.Definition?.id))||original.isTrigger||!original.gameObject.activeInHierarchy)continue;
                var bounds=original.bounds;bool near=prop.Indestructible||bounds.SqrDistance(Death.position)<=radius2;
                if(!near)foreach(var part in Parts.Values)if(bounds.SqrDistance(part.position)<=radius2){near=true;break;}
                if(!near&&weapon!=null)near=bounds.SqrDistance(weapon.position)<=radius2;
                if(!near)continue;seenObstacles.Add(prop);
                bool added=!obstacles.TryGetValue(prop,out var copy);
                if(added){var go=new GameObject("Body obstacle "+prop.name);go.transform.SetParent(root.transform);copy=go.AddComponent<BoxCollider>();copy.sharedMaterial=surface;obstacles.Add(prop,copy);}
                var source=original.transform;var target=copy.transform;
                if(added||target.position!=source.position||target.rotation!=source.rotation||target.localScale!=source.lossyScale||copy.center!=original.center||copy.size!=original.size)
                {
                    target.SetPositionAndRotation(source.position,source.rotation);target.localScale=source.lossyScale;
                    copy.center=original.center;copy.size=original.size;changed=true;
                }
            }
            staleObstacles.Clear();foreach(var pair in obstacles)if(!seenObstacles.Contains(pair.Key))staleObstacles.Add(pair.Key);
            foreach(var prop in staleObstacles){var copy=obstacles[prop];copy.enabled=false;CapturedAnimation.Release(copy.gameObject);obstacles.Remove(prop);changed=true;}
            if(changed)Physics.SyncTransforms();return changed;
        }
        private void WakeRetained(float age)
        {
            // First sight of an old death still has a bounded ten-second catch-up.
            if(!stepped)return;Twin?.Thaw(age);
            if(frozen)
            {
                foreach(var pair in Parts)if(pair.Key!=CrushPart)pair.Value.isKinematic=false;
                if(weapon!=null)weapon.isKinematic=false;frozen=false;clock=age;
            }
            settleAt=age+NativeCorpseRules.SettleSeconds;
        }
    }
}

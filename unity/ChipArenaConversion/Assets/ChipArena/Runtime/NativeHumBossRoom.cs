using System;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Lobby's room as THE HUM's language (docs/EXPEDITION-BOSS-BACKROOMS.md sections 3 and 7): the level's own look with its lamps, its partitions and its surfaces under the boss. The layout is the level's, carved (Core NativeHumArena); the room's fixtures and partitions
    // leave the static meshes and draw one by one from Core HumBossStage: each lamp at its own level (dying, flaring, burst), each partition in its place. The air (ambient, haze, the ceiling's glow) follows the lamps; the eight practical slots go to the boss's own lights first, then the brightest lamps
    // near the eye. The tears and bulges are Core NativeHumSurface's dynamic meshes, drawn in the level's own materials over the surface. With no boss none of this runs and Level 0 is exactly as it is. Presentation only: collision is untouched (the preview plays nothing).
    public sealed partial class ArenaPresentation
    {
        private BackroomsLayout humLayoutFrom,humLayoutCarved;private HumRoom humRoomLive;
        private Mesh humPartitionPaper,humPartitionTrim;private MaterialPropertyBlock humLampBlock;
        private readonly Mesh[] humPatch=new Mesh[3];private Mesh humVoid;private readonly int[] humPatchVerts={-1,-1,-1},humPatchIndex={-1,-1,-1};private int humVoidVerts=-1,humVoidIndex=-1;
        private static readonly VertexAttributeDescriptor[] HumPatchLayout=
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2)
        };
        private const MeshUpdateFlags HumQuiet=MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontResetBoneBounds;
        private static readonly Bounds HumEverywhere=new Bounds(Vector3.zero,Vector3.one*4000);

        /// <summary>BackroomsWorld: the layout to draw. With no boss, the level's own, untouched. With the DEV preview on, a carved copy with the room's fixtures left out (the presenter draws them).</summary>
        private BackroomsLayout HumBossLayout(BackroomsLayout layout,Vector3 origin)
        {
            if(!NativeHumBossPreview.On)return layout;
            if(!ReferenceEquals(humLayoutFrom,layout)||humLayoutCarved==null){humLayoutFrom=layout;humLayoutCarved=NativeHumArena.PresentationCopy(layout,NativeHumBossPreview.RoomCellX,NativeHumBossPreview.RoomCellZ,new NVector(origin.x,origin.y,origin.z),out _);}
            return humLayoutCarved;
        }
        private void HumRoomEnsure()
        {
            if(humPartitionPaper!=null)return;humLampBlock=new MaterialPropertyBlock();
            var b=new BackroomsMesh();b.Partition(NativeHumArena.PartitionCells*BackroomsLayout.Cell);
            humPartitionPaper=BackroomsMeshOf(b.Parts[(int)BackroomsMesh.Surface.Wallpaper],"THE HUM partition paper");humPartitionTrim=BackroomsMeshOf(b.Parts[(int)BackroomsMesh.Surface.Trim],"THE HUM partition trim");
            for(int i=0;i<humPatch.Length;i++){humPatch[i]=new Mesh{name="THE HUM surface "+i,hideFlags=HideFlags.HideAndDontSave};humPatch[i].MarkDynamic();}
            humVoid=new Mesh{name="THE HUM void",hideFlags=HideFlags.HideAndDontSave};humVoid.MarkDynamic();
        }
        // HumBossScenery: the lamps, the partitions, the surfaces, and the light in the air.
        private void HumRoomDraw(HumRoom room,Camera eye)
        {
            if(!BackroomsMaterials()||backroomsFixture==null)return;HumRoomEnsure();
            var st=humStage;var b=humLampBlock;b.Clear();b.SetVector(BrRestId,Vector4.zero);b.SetVector(BrLookId,new Vector4(0,1,0,0));b.SetVector(BrBlockColorId,new Vector4(1,1,1,1));
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            // NOCLIP's wrong light: the lamps go a cold green-white.
            var tint=Color.Lerp(BackroomsTint[(int)BackroomsMesh.Surface.Light],new Color(.66f,1f,.94f),st.Wrong);if(linear)tint=tint.linear;
            for(int i=0;i<NativeHumArena.FixtureCount;i++)
            {
                var f=room.FixtureAt[i];float lv=st.Lamp[i];bool off=lv<.02f;
                var mat=backroomsMats[off?(int)BackroomsMesh.Surface.DeadLight:(int)BackroomsMesh.Surface.Light];
                if(!off){b.SetColor(BrColorId,tint);b.SetFloat(BrEmitId,BackroomsGlow[(int)BackroomsMesh.Surface.Light]*lv);}
                else{var dead=new Color(.3f,.3f,.28f);if(st.Burst[i])dead=new Color(.04f,.035f,.03f);b.SetColor(BrColorId,linear?dead.linear:dead);b.SetFloat(BrEmitId,0);}
                Graphics.DrawMesh(backroomsFixture,Matrix4x4.Translate(new Vector3(f.X,f.Y,f.Z)),mat,0,null,0,b,ShadowCastingMode.Off,false);HumBossDraws++;
            }
            // The partitions in their places.
            for(int s=0;s<NativeHumArena.Partitions;s++)
            {
                room.Partition(s,st.Place[s],out var c,out bool alongX);var m=Matrix4x4.TRS(new Vector3(c.X,c.Y,c.Z),alongX?Quaternion.identity:Quaternion.Euler(0,90,0),Vector3.one);
                Graphics.DrawMesh(humPartitionPaper,m,backroomsMats[(int)BackroomsMesh.Surface.Wallpaper],0,null,0,backroomsProps,ShadowCastingMode.Off,true);
                Graphics.DrawMesh(humPartitionTrim,m,backroomsMats[(int)BackroomsMesh.Surface.Trim],0,null,0,backroomsProps,ShadowCastingMode.Off,true);HumBossDraws+=2;
            }
            // The tears and bulges on the walls, floor and ceiling, and the holes.
            var sf=humBossFx.Surface;
            for(int i=0;i<humPatch.Length;i++)
            {
                var pm=sf.Paper[i];if(pm.IndexCount==0)continue;HumPatchUpload(humPatch[i],pm,ref humPatchVerts[i],ref humPatchIndex[i]);
                int surf=i==NativeHumSurface.WallS?(int)BackroomsMesh.Surface.Wallpaper:i==NativeHumSurface.FloorS?(int)BackroomsMesh.Surface.Carpet:(int)BackroomsMesh.Surface.Ceiling;
                Graphics.DrawMesh(humPatch[i],Matrix4x4.identity,backroomsMats[surf],0,null,0,backroomsProps,ShadowCastingMode.Off,true);HumBossDraws++;
            }
            if(sf.Void.IndexCount>0){var mats=ZombieGearMaterials();if(mats!=null){HumPatchUpload(humVoid,sf.Void,ref humVoidVerts,ref humVoidIndex);Graphics.DrawMesh(humVoid,Matrix4x4.identity,mats[0],0,null,0,null,ShadowCastingMode.Off,false);HumBossDraws++;}}
            // The light: the boss's own first, then the near lamps; the air follows the lamps.
            if(eye!=null)
            {
                var e=eye.transform.position;var fx=humBossFx;var lamp=new NVector(BackroomsLampColor.x,BackroomsLampColor.y,BackroomsLampColor.z);
                st.PickLights(room,new NVector(e.x,e.y,e.z),NativeMapLooks.MaxLights,fx.LightPos,fx.LightRgb,fx.LightCount,lamp,BackroomsLampRange,humLightPos,humLightRgb);NativeLighting.PickedLights(humLightPos,humLightRgb);
                if(e.x>room.MinX-2&&e.x<room.MaxX+2&&e.z>room.MinZ-2&&e.z<room.MaxZ+2)
                {
                    float air=Mathf.Clamp01(st.Air),k=Mathf.Lerp(.05f,1,Mathf.Pow(air,1.15f));if(st.Air>1)k*=Mathf.Min(1.5f,1+(st.Air-1)*.5f);
                    var amb=Color.Lerp(BackroomsAmbient,new Color(.34f,.5f,.46f),st.Wrong*.7f);var haze=Color.Lerp(BackroomsHaze,new Color(.3f,.46f,.42f),st.Wrong*.7f);
                    RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=amb*(.9f*k);RenderSettings.ambientEquatorColor=amb*k;RenderSettings.ambientGroundColor=amb*(1.5f*k);
                    RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=haze*k;RenderSettings.fogStartDistance=6;RenderSettings.fogEndDistance=Mathf.Lerp(40,70,air);NativePackStage.Glow(Mathf.Lerp(.015f,1,air));
                }
            }
        }
        private static void HumPatchUpload(Mesh mesh,HumPatchMesh src,ref int vertexCapacity,ref int indexCapacity)
        {
            if(vertexCapacity!=src.Vertices.Length){mesh.SetVertexBufferParams(src.Vertices.Length,HumPatchLayout);vertexCapacity=src.Vertices.Length;}
            if(indexCapacity!=src.Indices.Length){mesh.SetIndexBufferParams(src.Indices.Length,IndexFormat.UInt32);indexCapacity=src.Indices.Length;mesh.subMeshCount=1;}
            mesh.SetVertexBufferData(src.Vertices,0,0,src.VertexCount,0,HumQuiet);mesh.SetIndexBufferData(src.Indices,0,0,src.IndexCount,HumQuiet);
            mesh.SetSubMesh(0,new SubMeshDescriptor(0,src.IndexCount){bounds=HumEverywhere,firstVertex=0,vertexCount=src.VertexCount},HumQuiet);mesh.bounds=HumEverywhere;
        }
        private void HumRoomHide(){}
        private void HumRoomDispose()
        {
            if(humPartitionPaper!=null)CapturedAnimation.Release(humPartitionPaper);if(humPartitionTrim!=null)CapturedAnimation.Release(humPartitionTrim);humPartitionPaper=humPartitionTrim=null;
            for(int i=0;i<humPatch.Length;i++){if(humPatch[i]!=null)CapturedAnimation.Release(humPatch[i]);humPatch[i]=null;humPatchVerts[i]=-1;humPatchIndex[i]=-1;}if(humVoid!=null)CapturedAnimation.Release(humVoid);humVoid=null;humVoidVerts=-1;humVoidIndex=-1;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The city's lamps on the GPU (Core NativeCityLamps; Resources/NativeCityLamps.cginc; docs/expedition/MERIDIAN-DRESS.md "The light").
    /// The drawers of a city map (the street dress, the street life, the bleed, the camp) each add their lamps as a block, write each
    /// lamp's level (0 out .. 1 full) every frame, and remove the block as they close. After them every frame (execution order 1100) this
    /// builds the table when the blocks or the lit map changed (the grid's cells; its only allocation), packs this frame's levels into the
    /// light texture, and turns the grid on while the lamps' map is the one lit and no stage or v2 intro owns the light; off otherwise,
    /// so every other map, a stage and an intro draw as before. Without float textures the grid stays off and the slots light as before.
    /// Allocation free per frame. Presentation only.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public sealed class NativeCityLampLight : MonoBehaviour
    {
        /// <summary>A drawer's lamps on a map; it writes <see cref="Level"/> (one per lamp, 1 full) each frame.</summary>
        public sealed class Block{public string Map;public NativeCityLamps.Lamp[] Lamps;public float[] Level;internal int Base=-1;}
        private static readonly List<Block> blocks=new List<Block>();private static NativeCityLampLight runner;private static bool dirty;private static string tableMap;
        private static NativeCityLamps table;private static Texture2D cells,data,light;private static float[] lightPix,levels;
        private static readonly int GridId=Shader.PropertyToID("_CityLampGrid"),SizeId=Shader.PropertyToID("_CityLampGridSize"),CellsId=Shader.PropertyToID("_CityLampCells"),
            DataId=Shader.PropertyToID("_CityLampData"),LightId=Shader.PropertyToID("_CityLampLight");
        /// <summary>The table the grid draws (null: none), and whether the grid lit the last frame.</summary>
        public static NativeCityLamps Table=>table;public static bool On{get;private set;}

        /// <summary>Adds a drawer's lamps on `map` (their levels start full); null for none.</summary>
        public static Block Add(string map,NativeCityLamps.Lamp[] lamps)
        {
            if(lamps==null||lamps.Length==0||string.IsNullOrEmpty(map))return null;
            var b=new Block{Map=map,Lamps=lamps,Level=new float[lamps.Length]};for(int i=0;i<b.Level.Length;i++)b.Level[i]=1;
            blocks.Add(b);dirty=true;
            if(runner==null){var go=new GameObject("City lamp light"){hideFlags=HideFlags.HideInHierarchy};runner=go.AddComponent<NativeCityLampLight>();}
            return b;
        }
        /// <summary>Takes a drawer's lamps away (the grid is built again without them).</summary>
        public static void Remove(Block b){if(b!=null&&blocks.Remove(b))dirty=true;}

        private void LateUpdate()
        {
            var map=NativeLighting.CurrentMap?.id;
            if(dirty||map!=tableMap)Rebuild(map);
            bool on=table!=null&&light!=null&&map!=null&&map==tableMap&&!NativeLighting.IntroOwned&&!NativeLighting.StageOwned;
            if(on)
            {
                for(int i=0;i<blocks.Count;i++){var b=blocks[i];if(b.Base>=0)Array.Copy(b.Level,0,levels,b.Base,b.Level.Length);}
                table.PackLight(levels,lightPix);light.SetPixelData(lightPix,0);light.Apply(false,false);
                Shader.SetGlobalVector(GridId,new Vector4(table.MinX,table.MinZ,NativeCityLamps.Cell,1));
            }
            else Shader.SetGlobalVector(GridId,Vector4.zero);
            On=on;
            if(blocks.Count==0){Release();Destroy(gameObject);runner=null;}
        }
        private static void Rebuild(string map)
        {
            dirty=false;tableMap=map;Release();
            var list=new List<NativeCityLamps.Lamp>();
            foreach(var b in blocks){b.Base=-1;if(b.Map!=map||list.Count+b.Lamps.Length>NativeCityLamps.Capacity)continue;b.Base=list.Count;list.AddRange(b.Lamps);}
            if(list.Count==0||!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))return;
            table=NativeCityLamps.Build(list);int n=table.Lamps.Length;levels=new float[n];lightPix=new float[n*4];
            var cellPix=new float[table.Nx*NativeCityLamps.CellTexels*table.Nz*4];table.PackCells(cellPix);var dataPix=new float[n*8];table.PackData(dataPix);
            cells=Texture(table.Nx*NativeCityLamps.CellTexels,table.Nz,"City lamp cells");cells.SetPixelData(cellPix,0);cells.Apply(false,true);
            data=Texture(2,n,"City lamp data");data.SetPixelData(dataPix,0);data.Apply(false,true);
            light=Texture(1,n,"City lamp light");table.PackLight(null,lightPix);light.SetPixelData(lightPix,0);light.Apply(false,false);
            Shader.SetGlobalTexture(CellsId,cells);Shader.SetGlobalTexture(DataId,data);Shader.SetGlobalTexture(LightId,light);
            Shader.SetGlobalVector(SizeId,new Vector4(table.Nx,table.Nz,table.Nx*NativeCityLamps.CellTexels,n));
            if(table.Dropped>0)Debug.LogWarning("City lamps: "+table.Dropped+" lamp-cells over the "+NativeCityLamps.PerCell+" a cell holds (their light is lost there)");
            Debug.Log("City lamps on "+map+": "+n+" lamps, "+table.Nx+" x "+table.Nz+" cells, the fullest wants "+table.Fullest);
        }
        private static Texture2D Texture(int w,int h,string name)=>new Texture2D(w,h,TextureFormat.RGBAFloat,false,true){name=name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,anisoLevel=0};
        private static void Release()
        {
            Shader.SetGlobalVector(GridId,Vector4.zero);On=false;
            if(cells!=null)Destroy(cells);if(data!=null)Destroy(data);if(light!=null)Destroy(light);cells=data=light=null;table=null;
        }
        private void OnDestroy(){if(runner==this){Release();runner=null;dirty=true;}}
    }
}

import * as T from 'three';

/** Cool night lighting leaves cover readable against the glowing combat effects. */
export function arenaLighting(scene:T.Scene,compatibility=false){
 scene.background=new T.Color(0x060810);scene.fog=new T.Fog(0x060810,30,105);
 const ambient=new T.AmbientLight(0x9aaccd,.13);ambient.name='night-ambient';scene.add(ambient);
 const sky=new T.HemisphereLight(0x92b4f0,0x171320,compatibility?.25:.48);sky.name='night-sky';scene.add(sky);
 const moon=new T.DirectionalLight(0xb2c6ff,compatibility?.52:.9);moon.name='night-moon';moon.position.set(-8,20,-12);moon.castShadow=true;
 moon.shadow.mapSize.set(2048,2048);moon.shadow.camera.left=-36;moon.shadow.camera.right=36;moon.shadow.camera.top=36;moon.shadow.camera.bottom=-36;moon.shadow.bias=-.0007;scene.add(moon);
 const rim=new T.DirectionalLight(0x58aab4,compatibility?.12:.24);rim.name='night-rim';rim.position.set(14,7,12);scene.add(rim);
}

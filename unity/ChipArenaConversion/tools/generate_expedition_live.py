#!/usr/bin/env python3
"""Compose the approved three-instance live layout; historical draft fixtures stay reproducible."""
import copy,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def load(name):return json.loads((ROOT/'Tests'/name).read_text())
def manifest():
    live=load('expedition-layout-4-join-draft.json')
    station=load('expedition-central-station-activation.json')
    foundry=load('expedition-ironworks-layout-activation.json')
    hub=live['areas'][0];iron=foundry['areas'][-1]
    records=[(station['layout']['area'],station['layout']['town']['anchor'],station['layout']['town']['door'],station['proposals']['machines'],1),
             (iron,next(a for a in foundry['areas'][0]['anchors'] if a.get('building')==iron['id']),next(d for d in foundry['areas'][0]['doors'] if d['anchor'].endswith(iron['id'])),iron['ironworks']['machines'],2)]
    for area,anchor,door,machines,turn in records:
        area=copy.deepcopy(area);door=copy.deepcopy(door)
        def rotate(p):
            p=copy.deepcopy(p)
            for _ in range(turn):p['x'],p['z']=p['z'],-p['x']
            if 'yaw' in p:p['yaw']=(p['yaw']+90*turn)%360
            return p
        door['join']=rotate(hub['doors'][0]['join']);door['approach']={k:door['join'][k] for k in ('x','y','z')}
        door['returns']=([dict(x=x,y=0,z=z,yaw=0) for z in (-72,-74) for x in (-3,-1,1,3)] if turn==1 else [dict(x=x,y=0,z=z,yaw=90) for x in (-79,-77) for z in (4,6,8,10)])
        hub['anchors'].append(anchor);hub['doors'].append(door)
        area['machines']=[]
        for i,p in enumerate(machines):
            yaw=math.radians(p['yaw']);dx,dz=math.sin(yaw),math.cos(yaw)
            point=lambda distance:dict(x=round(p['x']+dx*distance,5),y=p['y'],z=round(p['z']+dz*distance,5))
            area['machines'].append(dict(m=i,id=p['id'],kind=p['kind'],x=p['x'],y=p['y'],z=p['z'],yaw=yaw,front=p.get('front',point(.7)),dispense=point(1.3),chipDrop=point(2.2)))
        live['areas'].append(area);live['instances'].append(area['id'])
    for route in hub['hub']['reserved']:
        route=copy.deepcopy(route);route['to']=route.pop('building');hub['hub']['routes'].append(route)
    hub['hub']['reserved']=[]
    live['revision']='exp-layout-5'
    return live
if __name__=='__main__':
    out=ROOT/'Tests/expedition-live-layout.json';out.write_text(json.dumps(manifest(),separators=(',',':'))+'\n');print(out)

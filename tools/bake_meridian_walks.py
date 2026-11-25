"""Bake the approved JSON into the Core assembly used by Unity and CoreTests."""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SPECS = ROOT / "docs/expedition/meridian-plans"
CORE = ROOT / "unity/ChipArenaConversion/Assets/ChipArena/Core"
PLANS = [("crown-hill", 647), ("navigation", 784), ("arcades", 1058),
         ("stride", 1332), ("yards", 1606)]


def num(value):
    return f"{value:.7g}f"


def vec(point):
    if isinstance(point, dict):
        point = [point[k] for k in ("x", "y", "z")]
    return "new V3(" + ", ".join(map(num, point)) + ")"


def stair_landings(route):
    segments = route["segments"]
    for i in range(1, len(segments)-1):
        before, flat, after = segments[i-1:i+2]
        if (flat["kind"] == "flat" and before["kind"] == after["kind"] == "stair"
                and before["heading"] == flat["heading"] == after["heading"]
                and before["width"] == flat["width"] == after["width"]):
            yield i, flat, *route["points"][i:i+2]


def stair_ground(spec):
    # The one-metre raster rounds half-metre street edges. Restore the exact
    # footprint where a public stair or its intermediate landing crosses a cell.
    patches = [(r["x0"], r["z0"], r["x1"], r["z1"], r["ylo"])
               for r in spec["ramps"] if r["kind"] == "stair" and r.get("route")]
    for route in spec["routes"]:
        for _, seg, a, b in stair_landings(route):
            half = seg["width"]/2
            wx, wz = (half, 0) if a[0] == b[0] else (0, half)
            patches.append((min(a[0],b[0])-wx, min(a[2],b[2])-wz,
                            max(a[0],b[0])+wx, max(a[2],b[2])+wz, a[1]))
    for block in spec["ground"]:
        pieces = [(block["x0"], block["z0"], block["x1"], block["z1"], block["top"])]
        for left, near, right, far, level in patches:
            if all(float(v).is_integer() for v in (left, near, right, far)):
                continue
            updated = []
            for x, z, X, Z, y in pieces:
                l, n, r, f = max(x,left), max(z,near), min(X,right), min(Z,far)
                if l >= r or n >= f or y == level:
                    updated.append((x,z,X,Z,y))
                    continue
                updated.extend(piece for piece in (
                    (x,z,X,n,y), (x,f,X,Z,y), (x,n,l,f,y), (r,n,X,f,y),
                    (l,n,r,f,level)) if piece[0] < piece[2] and piece[1] < piece[3])
            pieces = updated
        pieces.sort(key=lambda p: (p[4] == block["top"], (p[2]-p[0])*(p[3]-p[1])), reverse=True)
        for i, (x,z,X,Z,y) in enumerate(pieces):
            yield dict(block, x0=x,z0=z,x1=X,z1=Z,top=y,
                       id=block["id"] if i == 0 else f"{block['id']}-stair-edge-{i}")


def bake(spec, seed):
    lines = [f"        private MapBounds StreetPlan{seed}()", "        {"]
    def emit(statement):
        lines.append("            " + statement)

    def box(key, x0, z0, x1, z1, bottom, top):
        args = [(x0+x1)/2, (z0+z1)/2, x1-x0, z1-z0, bottom, top]
        emit(f'Box({json.dumps(key)}, {", ".join(map(num, args))});')

    doors = {d["hall"]: d for d in spec["doors"]}
    ground = list(stair_ground(spec))
    for b in ground + spec["blocks"]:
        key = b["id"]
        x, z, X, Z, y, Y = [b[k] for k in ("x0", "z0", "x1", "z1", "bottom", "top")]
        if seed == 1058 and key == "n-lid-1":
            # The Galleria stair passes through its lid on the way to the Leads.
            # Keep the deck connected around a 1.5 m wide staircase opening.
            box(key, x,z,X,90,y,Y)
            box(key+"-north",x,96.5,X,Z,y,Y)
            box(key+"-west",x,90,4.25,96.5,y,Y)
            box(key+"-east",5.75,90,X,96.5,y,Y)
        elif key in doors:
            d = doors[key]
            # Leave an actual passage below the roof at the exact mouth.
            h, w = d["height"], d["width"]/2
            if d["facing"] in ("N", "S"):
                box(key+"-west", x,z,d["x"]-w,Z,y,y+h)
                box(key+"-east", d["x"]+w,z,X,Z,y,y+h)
            else:
                box(key+"-south", x,z,X,d["z"]-w,y,y+h)
                box(key+"-north", x,d["z"]+w,X,Z,y,y+h)
            box(key,x,z,X,Z,y+h,Y)
        else:
            box(key,x,z,X,Z,y,Y)
        if b.get("hatch"):
            emit(f'RoofHatches.Add({vec([(x+X)/2, Y, (z+Z)/2])});')
        if b["role"] == "cordon":
            along_x = X-x > Z-z
            for n in range(10, int(max(X-x,Z-z)), 60):
                px = x+n if along_x else max(x,X-4) if key.endswith("w") else X-4
                pz = z+n if not along_x else z if key.endswith("s") else Z-4
                box(f"{key}-tower-{n}",px,pz,px+4,pz+4,y,Y+9)

    for r in spec["ramps"]:
        stair = str(r["kind"] == "stair").lower()
        emit(f'Ramp("{r["id"]}", {vec(r["low"])}, {vec(r["high"])}, {num(r["width"])}, {stair});')

        if r["kind"] == "stair" and r.get("route"):
            emit(f'StreetHandrails("{r["id"]}", {vec(r["low"])}, {vec(r["high"])}, {num(r["width"])});')
    for route in spec["routes"]:
        for i, seg, a, b in stair_landings(route):
            emit(f'StreetHandrails("{route["id"]}-landing-{i}", {vec(a)}, {vec(b)}, {num(seg["width"])});')

    # Guard only continuous exposed pavement. At a crossing keep the other road open.
    for r in spec["routes"]:
        for i,seg in enumerate(r["segments"]):
            if seg["kind"] != "flat":
                continue
            a,b = r["points"][i:i+2]
            dx,dz = b[0]-a[0], b[2]-a[2]
            length = abs(dx)+abs(dz)
            ux,uz = dx/length,dz/length
            for sign in (-1,1):
                nx,nz = -uz*sign,ux*sign
                run = None
                for m in range(math.ceil(length)+1):
                    station = min(m+.5,length)
                    x = a[0]+station*ux+nx*seg["width"]/2
                    z = a[2]+station*uz+nz*seg["width"]/2
                    def ground_height(distance):
                        return max((g["top"] for g in ground
                            if g["x0"] <= x+distance*nx <= g["x1"]
                            and g["z0"] <= z+distance*nz <= g["z1"]), default=-100)
                    exposed = (seg["width"]/2 < station < length-seg["width"]/2
                               and abs(ground_height(-.5)-a[1]) < .01
                               and ground_height(.5) < a[1]-2)
                    if exposed and run is None:
                        run = m
                    if not exposed and run is not None:
                        x0,z0 = a[0]+run*ux+nx*seg["width"]/2, a[2]+run*uz+nz*seg["width"]/2
                        x1,z1 = x0+(m-run)*ux,z0+(m-run)*uz
                        box(f"street-rail-{r['id']}-{i}-{sign}-{run}",min(x0,x1)-abs(nx)*.15,
                            min(z0,z1)-abs(nz)*.15,max(x0,x1)+abs(nx)*.15,
                            max(z0,z1)+abs(nz)*.15,a[1],a[1]+1.1)
                        run = None

    for link in spec["links"]:
        a,b,w = link["a"],link["b"],link["width"]
        axis = "x" if a["x"] != b["x"] else "z"
        normal = "z" if axis == "x" else "x"
        # Find the roof face beside the top landing, including the neutral halls.
        roofs = [r for r in spec["blocks"] if r["top"] == b["y"]
                 and r[axis+"0"] <= b[axis] <= r[axis+"1"]]
        face, outward = min(((r[normal+k], sign) for r in roofs for k,sign in (("0",-1),("1",1))),
                            key=lambda pair: abs(pair[0]-b[normal]))
        length = abs(b[axis]-a[axis])
        flights = max(1, math.ceil((b["y"]-a["y"])/(length*.65)))
        flights += (flights+1)%2  # finish at the specified end on the building side
        if seed == 784 and link["id"] == "fire-escape-0":
            # At the Navigation portal the return lane is the upper terrace itself.
            # Climb out of the canal, walk across the terrace, then turn onto the roof.
            emit('Ramp("fire-escape-0-0", new V3(301,-14,19.5f), new V3(316.5f,0,19.5f), 3, true);')
            box("fire-escape-0-0-landing",316.5,18,318.5,23,-.2,0)
            emit('Ramp("fire-escape-0-1", new V3(310,0,22), new V3(315,2,22), 3, true);')
            box("fire-escape-0-1-landing",315,face,316.5,23.5,1.8,2)
            continue
        if seed == 1058 and link["id"] == "fire-escape-18":
            flights = 3
        for i in range(flights):
            low = a.copy(); high = b.copy()
            low["y"] = a["y"]+(b["y"]-a["y"])*i/flights
            high["y"] = a["y"]+(b["y"]-a["y"])*(i+1)/flights
            if i%2: low[axis],high[axis] = b[axis],a[axis]
            low[normal] = high[normal] = a[normal]+outward*w*(i%2)
            if seed == 1332 and link["id"] in ("fire-escape-1","fire-escape-2"):
                # Gain the adjacent terrace height before turning onto the outside flight.
                levels = [0,8,12,16]
                low["y"],high["y"] = a["y"]+levels[i],a["y"]+levels[i+1]
            if seed == 1058 and link["id"] == "fire-escape-18":
                # Return east of the neighbouring wing in this narrow market alley.
                stations,levels = [17,30,23,25], [0,10,14.5,16]
                low[axis],high[axis] = stations[i:i+2]
                low["y"],high["y"] = levels[i:i+2]
            key = link["id"]+"-"+str(i)
            emit(f'Ramp("{key}", {vec(low)}, {vec(high)}, {num(w)}, true);')
            end=high[axis]; direction=1 if end>low[axis] else -1
            u,U=sorted([end,end+direction*w])
            lateral=[a[normal]-w/2,a[normal]+w/2]
            if i+1<flights: lateral += [v+outward*w for v in lateral]
            else: lateral.append(face)
            v,V=min(lateral),max(lateral)
            box(key+"-landing",*( (u,v,U,V) if axis=="x" else (v,u,V,U)),high["y"]-.2,high["y"])

    # Avoid corner posts, the inside face of overlapping grades, and unsupported
    # shoulders at three terrace lips. Only the horde centerline moves.
    horde_corners = {
        (647, "e"): {3: [[138,-3.5,0]], 4: [[138,7,63],[140,7,63]]},
        (1058, "e"): {1: [[42,0,-2],[48,0,-2],[48,0,3]]},
        (1058, "s"): {1: [[0,0,-43]], 2: [[-15,0,-43],[-15,0,-45]]},
        (1332, "n"): {3: [[0,7,119],[0,7,121]], 4: [[98,7,121],[100,7,119]]},
        (1606, "e"): {1: [[28,0,2],[35,0,2],[35,0,0]]},
    }
    for r in spec["routes"]:
        dest = spec["court"]["gateway"] if r["id"]=="n" else doors["hall-"+r["to"][-1]]
        name = spec["court"]["name"] if r["id"]=="n" else dest["name"]
        emit("Routes.Add(new Route {")
        emit(f'    Name = {json.dumps(name)}, Destination = {vec(dest)},')
        emit("    Points = new[] { " + ", ".join(map(vec,r["points"])) + " },")
        corners = horde_corners.get((seed,r["id"]))
        if corners:
            path = [pt for i, point in enumerate(r["points"]) for pt in corners.get(i,[point])]
            emit("    HordePoints = new[] { " + ", ".join(map(vec,path)) + " },")
        for field,key,fmt in (("Widths","width",num),("Kinds","kind",json.dumps),("Covers","cover",json.dumps)):
            emit(f"    {field} = new[] {{ " + ", ".join(fmt(s[key]) for s in r["segments"]) + " },")
        emit("});")
    emit("return new MapBounds { " + ", ".join(f"{k} = {num(v)}" for k,v in spec["bounds"].items()) + " };")
    lines.append("        }")
    return lines


def main():
    lines = ["// Generated from docs/expedition/meridian-plans by tools/bake_meridian_walks.py.",
              "namespace ChipArena.UnityConversion", "{",
             "    public sealed partial class NativeLayeredCityWalk", "    {",
             "        private MapBounds BuildStreetPlan(uint seed)", "        {", "            switch (seed)", "            {"]
    for slug, seed in PLANS:
        lines.append(f"                case {seed}: return StreetPlan{seed}();")
    lines += ['                default: throw new System.ArgumentOutOfRangeException(nameof(seed));',
              "            }","        }"]
    for slug,seed in PLANS:
        lines += [""] + bake(json.loads((SPECS/(slug+".json")).read_text()),seed)
    (CORE/"NativeMeridianStreetPlans.cs").write_text("\n".join(lines+["    }","}",""]))


if __name__ == "__main__":
    main()

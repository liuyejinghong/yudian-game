"""Original Stimson-inspired sandstone study. Blender 4.5; no external assets.

blender --background --factory-startup --python-exit-code 1 --python build.py
blender --background mars-outcrop-r1.blend --python build.py -- --reexport
The NASA photographs listed in manifest.json inform shape only; never sampled.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import shutil
import sys

import bpy
import bmesh
from mathutils import Vector, Euler, noise

HERE = Path(__file__).resolve().parent
NAME = "mars-outcrop-r1"
SEED = 21044
RNG = random.Random(SEED)
noise.seed_set(SEED)
TEXTURE_SIZE = 2048
ATLAS_NEXT = 0
ISLAND_TRI_COUNTS = {}


def n(x, y, z=0):
    return noise.noise_vector(Vector((x, y, z)), noise_basis='PERLIN_ORIGINAL').x


def sstep(a, b, x):
    t = max(0., min(1., (x-a)/(b-a)))
    return t*t*(3-2*t)


def ramp(a,b,x):
    return max(0.,min(1.,(x-a)/(b-a)))


def tooth(a,b,c,x):
    return max(0.,min((x-a)/(b-a),(c-x)/(c-b)))


def back_up(path):
    if path.exists():
        target = HERE / "iterations" / "previous" / path.relative_to(HERE)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)


def make_chunk(name, outline, top, seed, small=False, fracture_edges=()):
    """Sample irregular angular cliff faces, with real bed ledges and torn rims.

    The outline is an authored fracture polygon, not a radial rock primitive.
    Bedding is clipped by each fracture wall and by the weathered top surface.
    """
    global ATLAS_NEXT
    island_base=ATLAS_NEXT;ATLAS_NEXT+=len(outline)+2
    verts, faces, uvs, exposure_values, island_values = [], [], [], [], []
    cx = sum(p[0] for p in outline)/len(outline)
    cy = sum(p[1] for p in outline)/len(outline)
    basal=name.startswith('00_')
    base = -.14 if not small else -.055
    lip_edges={1:(0,1,2),3:(0,1,2,3),7:(4,5,6,7)}.get(seed,())
    # Height, thickness, projection, clipped horizontal extent, and dip. Each
    # exposed patch belongs to its parent bed; these are not separate objects.
    lips={
      1:[(.33,.040,.070,-2.30,-1.28,.10),(.58,.055,.090,-2.15,-.88,.12),
         (.66,.028,.055,-1.90,-1.10,.12),(1.05,.045,.085,-2.23,-.87,-.16),
         (1.17,.065,.070,-1.72,-.78,-.16),(1.45,.050,.055,-1.90,-.75,.07)],
      3:[(.26,.047,.065,-.17,1.63,.15),(.47,.035,.090,.15,1.72,.15),
         (.54,.024,.050,.45,1.45,.15),(.78,.058,.080,-.30,1.28,-.19),
         (1.01,.036,.060,-.38,.83,-.19)],
      7:[(.36,.045,.065,.08,1.89,-.10),(.52,.030,.055,.42,1.58,-.10),
         (.80,.065,.085,-.28,1.40,.16),(.98,.043,.065,-.24,1.25,.16),
         (1.23,.039,.055,.28,1.65,-.06)]}.get(seed,[])
    cuts={1:(cx+.06,.28,.77,1.13,.50,(1,2)),
          3:(.89,.32,.54,.93,-.40,(1,2))}
    cut=cuts.get(seed)
    outline_samples = []
    for edge, p in enumerate(outline):
        q = outline[(edge+1) % len(outline)]
        length = math.dist(p,q)
        density=.070 if edge in lip_edges else .12 if not small else .10
        segments=max(2,round(length/density))
        fractions={i/segments for i in range(segments)}
        if edge in lip_edges and abs(q[0]-p[0])>.001:
            marks=[]
            for i,(_,_,_,left,right,_) in enumerate(lips):
                nick=left+.37+i*.11
                marks.extend((left,left+.10,right-.10,right,nick-.075,nick,nick+.075))
            if cut:marks.extend((cut[0]-cut[1],cut[0],cut[0]+cut[1]))
            fractions.update((x-p[0])/(q[0]-p[0]) for x in marks if 0<(x-p[0])/(q[0]-p[0])<1)
        last=-1
        for f in sorted(fractions):
            if f-last<.001:continue
            last=f
            outline_samples.append((p[0]*(1-f)+q[0]*f,
                                    p[1]*(1-f)+q[1]*f, edge, f))
    nv = len(outline_samples)

    def top_z(x,y):
        h, dx, dy = top
        weather = (.026 if small else .115)*n(x*1.65+seed,y*1.65,2.5)
        weather += (.010 if small else .033)*n(x*7.2,y*7.2,seed)
        if basal:weather+=.10*n(x*2.1,y*2.1,seed)+.027*n(x*8,y*8,seed)
        # A worn upper bed terminates across the cap, instead of a flat lid.
        direction=seed*.61
        cut=(x-cx)*math.cos(direction)+(y-cy)*math.sin(direction)
        step = 0 if small else .23*sstep(-.06,.07,cut)
        notch = 0 if small else .18*math.exp(-((x-cx-.35)**2+(y-cy+.2)**2)/.19)
        return h+dx*(x-cx)+dy*(y-cy)+weather-step-notch

    def edge_point(x,y,edge,f,z):
        out = Vector((x-cx,y-cy,0)).normalized()
        if small:
            erosion = (.065 if basal else .013)*n(x*8+seed,y*8,z*12)
            slope = -.025*sstep(base,top[0],z)
        else:
            joint=float(edge in fracture_edges)
            previous=float((edge-1)%len(outline) in fracture_edges)
            following=float((edge+1)%len(outline) in fracture_edges)
            if f<.2:joint=(joint+previous)*.5*(1-sstep(0,.2,f))+joint*sstep(0,.2,f)
            if f>.8:joint=joint*(1-sstep(.8,1,f))+(joint+following)*.5*sstep(.8,1,f)
            # Sharp bed breaks: planar recession, with narrow angular undercuts.
            q = z + .066*x-.032*y
            recess = (-.045*tooth(.34,.375,.40,q)-.032*tooth(.92,.95,.98,q)
                      -.044*tooth(1.42,1.45,1.48,q))
            exposure = .58+.42*math.sin(x*.76+y*.96+seed)**2
            recess *= 1-.82*joint
            exposure *= 1-.75*joint
            erosion = recess*exposure
            erosion += .010*n(x*5+seed,y*5,z*6)+.015*n(x*1.8,y*1.8,z*2)
            # Broad bedding benches retreat by 15–25 cm at different heights;
            # unexposed joint walls remain close, rather than opening pillars.
            slope = (-.16*ramp(.40,.43,q)-.21*ramp(.98,1.025,q)
                     -.17*ramp(1.48,1.515,q))*(.92+.08*n(x*2.3,y*.7,seed))
            slope=slope*(1-joint)-.014*sstep(.15,2.2,z)*joint
            if edge in lip_edges:
                for i,(a,t,projection,left,right,dip) in enumerate(lips):
                    h=z+dip*(x-cx)+.04*(y-cy)
                    profile=max(0,min((h-a)/(.16*t),1,(a+t-h)/(.34*t)))
                    span=max(0,min(1,(x-left)/.10,(right-x)/.10))
                    nick=left+.37+i*.11
                    broken=1-.85*max(0,1-abs(x-nick)/.075)
                    erosion+=projection*profile*span*broken
            if cut and edge in cut[5]:
                xc,width,lower,upper,dip,_=cut
                dz=dip*(x-xc)
                wedge=max(0,min(1,(x-xc+width)/.13,(xc+width-x)/.14,
                                (z-lower-dz)/.09,(upper+dz-z)/.10))
                erosion-=.22*wedge
        # Preserve joint planes: corners have no independent random rounding.
        fade = .30+.70*math.sin(math.pi*f)**.4
        v = Vector((x,y,z))+out*(erosion*fade+slope)
        if not small:
            v.x+=.15*max(z,0);v.y+=.055*max(z,0)
        v.z += (.004 if small else .002)*n(x*8,y*8,z*4)*math.sin(math.pi*max(0,min(1,(z-base)/(top[0]-base))))
        return v

    columns=[]
    for x,y,edge,f in outline_samples:
        cap=top_z(x,y)
        if small:levels=[base+(cap-base)*j/5 for j in range(6)]
        else:
            levels=[base,cap]+[base+j*.095 for j in range(1,30)]
            levels += [h-.066*x+.032*y for h in (.34,.375,.40,.43,.92,.95,.98,1.025,1.42,1.45,1.48,1.515)]
            if edge in lip_edges:
                for a,t,_,_,_,dip in lips:
                    levels += [h-dip*(x-cx)-.04*(y-cy) for h in (a,a+.16*t,a+.66*t,a+t)]
            if cut and edge in cut[5]:
                xc,_,lower,upper,dip,_=cut;dz=dip*(x-xc)
                levels += [lower+dz,lower+dz+.09,upper+dz-.10,upper+dz]
            levels=sorted(v for v in levels if base<=v<=cap)
        column=[];previous=-100
        for z in levels:
            if z-previous<.0001:continue
            previous=z;column.append(len(verts));verts.append(tuple(edge_point(x,y,edge,f,z)))
        columns.append(column)
    # Perimeter UV is separated into natural face islands at polygon corners.
    lengths=[math.dist(outline[e],outline[(e+1)%len(outline)]) for e in range(len(outline))]
    for i,(x,y,edge,f) in enumerate(outline_samples):
        k=(i+1)%nv
        nextf=outline_samples[k][3] if outline_samples[k][2]==edge else 1.
        a,b=columns[i],columns[k];aside=set(a);ia=ib=0
        # Join unequal columns at the actual bed/lip heights, so centimetre
        # edges are sampled explicitly instead of hoping a dense grid hits them.
        while ia<len(a)-1 or ib<len(b)-1:
            za=verts[a[ia+1]][2] if ia+1<len(a) else math.inf
            zb=verts[b[ib+1]][2] if ib+1<len(b) else math.inf
            if abs(za-zb)<.00001:
                face=(a[ia],b[ib],b[ib+1],a[ia+1]);ia+=1;ib+=1
            elif za<zb:face=(a[ia],b[ib],a[ia+1]);ia+=1
            else:face=(a[ia],b[ib],b[ib+1]);ib+=1
            faces.append(face)
            exposure_values.append(.08 if edge in fracture_edges else 1.)
            island_values.append(island_base+edge)
            uvx=edge*8
            uvs.append([(uvx+(f if v in aside else nextf)*lengths[edge],verts[v][2]) for v in face])
    # The upper cap has several irregular concentric interpolation rows, all
    # triangulated. These are construction topology, never layered rock rings.
    prev=[c[-1] for c in columns]
    radial_rows=13 if not small else 3
    center_z=top_z(cx,cy)
    center_x=cx+(.15*max(center_z,0) if not small else 0)
    center_y=cy+(.055*max(center_z,0) if not small else 0)
    for row in range(1,radial_rows):
        fac=1-row/radial_rows
        cur=[]
        for i,(x,y,edge,f) in enumerate(outline_samples):
            rim=Vector(verts[columns[i][-1]])
            px=center_x+(rim.x-center_x)*fac
            py=center_y+(rim.y-center_y)*fac
            # Evaluate the cap in the same pre-erosion coordinates as its rim;
            # reevaluating the displaced rim creates artificial thin fins.
            height=top_z(cx+(x-cx)*fac,cy+(y-cy)*fac)
            cur.append(len(verts));verts.append((px,py,height))
        for i in range(nv):
            k=(i+1)%nv
            quad=(prev[i],prev[k],cur[k],cur[i])
            faces.append(quad)
            exposure_values.append(0.)
            island_values.append(island_base+len(outline))
            uvs.append([(verts[v][0]+100,verts[v][1]+100) for v in quad])
        prev=cur
    center=len(verts);verts.append((center_x,center_y,center_z))
    for i in range(nv):
        tri=(prev[i],prev[(i+1)%nv],center)
        faces.append(tri);exposure_values.append(0.);island_values.append(island_base+len(outline))
        uvs.append([(verts[v][0]+100,verts[v][1]+100) for v in tri])
    bottom=tuple(c[0] for c in reversed(columns))
    faces.append(bottom);exposure_values.append(0.);island_values.append(island_base+len(outline)+1)
    uvs.append([(verts[v][0]*.12+200,verts[v][1]*.12+200) for v in bottom])
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces);mesh.update()
    exposure=mesh.attributes.new('BeddingRelief','FLOAT','FACE')
    for item,value in zip(exposure.data,exposure_values):item.value=value
    atlas=mesh.attributes.new('AtlasIsland','INT','FACE')
    for item,value in zip(atlas.data,island_values):item.value=value
    uv=mesh.uv_layers.new(name="SandstoneAtlas")
    for poly, coords in zip(mesh.polygons,uvs):
        for li,co in zip(poly.loop_indices,coords):uv.data[li].uv=co
        poly.use_smooth=not (exposure_values[poly.index]>.5 and abs(poly.normal.z)>.25)
    ob=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(ob)
    group=ob.vertex_groups.new(name=name)
    group.add(list(range(len(verts))),1,'REPLACE')
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    for edge in bm.edges:
        if len(edge.link_faces)==2 and edge.calc_face_angle()>math.radians(32):edge.smooth=False
    bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
    bm.to_mesh(mesh);bm.free();mesh.update()
    for item in mesh.attributes['AtlasIsland'].data:
        ISLAND_TRI_COUNTS[item.value]=ISLAND_TRI_COUNTS.get(item.value,0)+1
    return ob


def pack_authored_islands(mesh):
    """Pack known non-overlapping face parameterizations without native UV tools.

    ponytail: deterministic shelf packing wastes some atlas area; replace only if
    this single asset needs more texel density. No dependency or generic packer.
    """
    uv=mesh.uv_layers.active.data;ids=mesh.attributes['AtlasIsland'].data
    islands={}
    for face in mesh.polygons:
        islands.setdefault(ids[face.index].value,[]).extend(face.loop_indices)
    assert {key:len(loops)//3 for key,loops in islands.items()}==ISLAND_TRI_COUNTS,'UV island IDs changed during join'
    rectangles=[]
    for key,loops in islands.items():
        low=Vector((min(uv[i].uv.x for i in loops),min(uv[i].uv.y for i in loops)))
        high=Vector((max(uv[i].uv.x for i in loops),max(uv[i].uv.y for i in loops)))
        size=high-low
        rectangles.append((key,loops,low,size))
    rectangles.sort(key=lambda item:item[3].y,reverse=True)
    padding=.055
    width=max(max(r[3].x+2*padding for r in rectangles),math.sqrt(sum((r[3].x+2*padding)*(r[3].y+2*padding) for r in rectangles))*1.04)
    x=y=row_height=0.;placements=[]
    for key,loops,low,size in rectangles:
        w=size.x+2*padding;h=size.y+2*padding
        if x+w>width:x=0.;y+=row_height;row_height=0.
        placements.append((loops,low,Vector((x+padding,y+padding)),size))
        x+=w;row_height=max(row_height,h)
    extent=max(width,y+row_height)
    assert padding/extent*TEXTURE_SIZE>=8,'Single-side atlas padding below bake margin'
    for loops,low,origin,size in placements:
        for i in loops:uv[i].uv=(uv[i].uv-low+origin)/extent
    # Keep baked edge pixels away from neighbouring UV islands.
    for i,(_,_,a,sa) in enumerate(placements):
        for _,_,b,sb in placements[i+1:]:
            assert a.x+sa.x<=b.x or b.x+sb.x<=a.x or a.y+sa.y<=b.y or b.y+sb.y<=a.y
    assert all(0<=v<=1 for item in uv for v in item.uv)
    for face in mesh.polygons:
        a,b,c=[uv[i].uv for i in face.loop_indices]
        assert abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))>1e-12,'Degenerate UV triangle'
    print('ATLAS',len(placements),'islands;',round(TEXTURE_SIZE/extent,1),'px/m; gap',round(2*padding/extent*TEXTURE_SIZE,1),'px',flush=True)


def build_geometry():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    objects=[]
    objects.append(make_chunk("01_West_high_bed",[(-2.62,-.61),(-2.31,-1.19),(-1.65,-1.32),(-.64,-1.18),(-.43,-.63),(-.62,.13),(-.69,1.12),(-1.59,1.23),(-2.53,.64)],(2.02,.17,-.09),1,fracture_edges=(3,4,5)))
    objects.append(make_chunk("02_Foreground_crossbeds",[(-.56,-1.22),(.34,-1.39),(1.31,-1.17),(2.17,-.64),(1.90,.12),(1.10,.43),(.23,.23),(-.58,-.18)],(1.40,-.23,.12),3,fracture_edges=(5,6,7)))
    objects.append(make_chunk("03_Rear_joint_block",[(-.54,.28),(.15,.31),(1.10,.49),(1.95,.29),(2.31,.92),(1.80,1.39),(.85,1.48),(.05,1.23),(-.59,1.18)],(1.76,-.12,-.16),7,fracture_edges=(0,1,2,8)))
    objects.append(make_chunk("00_Continuous_lower_bed",[(-2.76,-.79),(-2.38,-1.37),(-1.63,-1.44),(-.75,-1.28),(.08,-1.47),(1.21,-1.31),(2.29,-.83),(2.43,.21),(2.37,1.12),(1.66,1.53),(.38,1.53),(-.84,1.34),(-2.20,1.10),(-2.63,.48)],(.23,-.01,.02),22,True))
    objects.append(make_chunk("04_Fallen_leaning_plate",[(-2.53,-1.08),(-2.12,-1.46),(-1.38,-1.73),(-1.02,-1.42),(-1.34,-1.21),(-1.86,-1.03)],(.43,-.10,.31),12,True))
    objects.append(make_chunk("05_Detached_bed_toe",[(1.63,-.97),(1.85,-1.41),(2.46,-1.37),(2.67,-.97),(2.26,-.79)],(.32,-.05,.20),16,True))
    # Fourteen unequal chips: small local clusters below broken faces, not a ring.
    chips=[(-2.68,-1.39,.44,.23,.10,.4),(-2.10,-1.71,.29,.19,.065,1.3),
           (-1.23,-1.89,.34,.21,.09,-.3),(-.83,-1.56,.18,.12,.055,.3),
           (-.48,-1.82,.47,.25,.10,.5),(.10,-1.61,.25,.13,.065,.8),
           (.53,-1.83,.35,.16,.07,1.1),(1.08,-1.56,.20,.17,.055,-.2),
           (1.51,-1.88,.36,.25,.09,.6),(2.47,-1.65,.28,.16,.07,1.4),
           (2.84,-.61,.40,.23,.11,-.5),(2.67,.77,.27,.19,.09,1.8),
           (.70,1.77,.36,.21,.085,.6),(-1.81,1.55,.30,.17,.08,-.1)]
    for i,(x,y,w,d,h,a) in enumerate(chips):
        polygon=[]
        for j in range(5+(i%3)):
            theta=math.tau*j/(5+(i%3))
            radius=RNG.uniform(.76,1.08)
            xx=math.cos(theta)*w*radius;yy=math.sin(theta)*d*radius
            polygon.append((x+xx*math.cos(a)-yy*math.sin(a),y+xx*math.sin(a)+yy*math.cos(a)))
        objects.append(make_chunk(f"Chip_{i+1:02d}",polygon,(h,RNG.uniform(-.09,.09),RNG.uniform(-.12,.12)),31+i,True))
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    obj=bpy.context.object;obj.name="Stimson_Sandstone_Outcrop"
    # Mesh coordinate centre is the horizontal asset root; ground is exactly Z=0.
    xs=[v.co.x for v in obj.data.vertices];ys=[v.co.y for v in obj.data.vertices]
    dx=(min(xs)+max(xs))/2;dy=(min(ys)+max(ys))/2
    for v in obj.data.vertices:v.co.x-=dx;v.co.y-=dy
    obj["semantic_role"]="decorative natural sandstone; no resource or collision semantics"
    obj["component_count"]=20
    obj["burial_allowance_m"]=.14
    obj["reference_method"]="PIA21044 / PIA21042 observed shapes; original mesh and procedural PBR"
    pack_authored_islands(obj.data)
    assert all(-.0001<=v<=1.0001 for item in obj.data.uv_layers.active.data for v in item.uv),'Atlas packing outside unit tile'
    return obj


def source_material(obj):
    mat=bpy.data.materials.new("Original_Sandstone_Procedural_Bake_Source")
    mat.use_nodes=True;mat.use_fake_user=True
    nd=mat.node_tree.nodes;nd.clear();ln=mat.node_tree.links
    def node(kind,name):
        result=nd.new(kind);result.label=name;return result
    def mathnode(op,a,b=None):
        p=node('ShaderNodeMath',op);p.operation=op
        for idx,val in enumerate((a,b)):
            if val is None:continue
            if isinstance(val,(int,float)):p.inputs[idx].default_value=val
            else:ln.new(val,p.inputs[idx])
        return p.outputs[0]
    def mix(a,b,f):
        p=node('ShaderNodeMixRGB','Mix');p.blend_type='MIX'
        for idx,val in ((0,f),(1,a),(2,b)):
            if isinstance(val,(float,int)):p.inputs[idx].default_value=val
            elif isinstance(val,tuple):p.inputs[idx].default_value=val
            else:ln.new(val,p.inputs[idx])
        return p.outputs[0]
    def texnoise(pos,scale,detail,roughness=.65):
        p=node('ShaderNodeTexNoise',f'Original noise {scale}/m')
        ln.new(pos,p.inputs['Vector']);p.inputs['Scale'].default_value=scale
        p.inputs['Detail'].default_value=detail;p.inputs['Roughness'].default_value=roughness
        return p.outputs['Fac']
    geom=node('ShaderNodeNewGeometry','Global geological coordinates')
    pos=geom.outputs['Position']
    xyz=node('ShaderNodeSeparateXYZ','Coordinates');ln.new(pos,xyz.inputs[0])
    norm=node('ShaderNodeSeparateXYZ','Surface orientation');ln.new(geom.outputs['Normal'],norm.inputs[0])
    x,y,z=(xyz.outputs[a] for a in 'XYZ')
    coarse=texnoise(pos,1.3,3)
    medium=texnoise(pos,14,3)
    grain=texnoise(pos,175,2)
    fine=texnoise(pos,410,1)
    bed=mathnode('ADD',z,mathnode('ADD',mathnode('MULTIPLY',x,.066),mathnode('MULTIPLY',y,-.032)))
    low=mathnode('LESS_THAN',bed,.70);high=mathnode('GREATER_THAN',bed,1.36)
    dip_lo=mathnode('ADD',mathnode('MULTIPLY',x,.19),mathnode('MULTIPLY',y,.07))
    dip_mid=mathnode('ADD',mathnode('MULTIPLY',x,-.12),mathnode('MULTIPLY',y,.11))
    dip_hi=mathnode('ADD',mathnode('MULTIPLY',x,.10),mathnode('MULTIPLY',y,-.05))
    dip=mix(mix(dip_mid,dip_lo,low),dip_hi,high)
    q=mathnode('ADD',mathnode('ADD',z,dip),mathnode('MULTIPLY',coarse,.025))
    q=mathnode('ADD',q,mathnode('MULTIPLY',mathnode('SINE',mathnode('ADD',mathnode('MULTIPLY',q,37),coarse)),.014))
    phase=mathnode('ADD',mathnode('MULTIPLY',q,93),mathnode('MULTIPLY',medium,.60))
    wave=mathnode('SINE',phase)
    finewave=mathnode('SINE',mathnode('ADD',mathnode('MULTIPLY',q,247),mathnode('MULTIPLY',medium,.9)))
    lines=mathnode('ADD',mathnode('MULTIPLY',wave,.72),mathnode('MULTIPLY',finewave,.28))
    exposure=mathnode('ADD',.22,mathnode('MULTIPLY',mathnode('ABSOLUTE',norm.outputs['Y']),.78))
    side=mathnode('SUBTRACT',1,mathnode('POWER',mathnode('ABSOLUTE',norm.outputs['Z']),4))
    relief=node('ShaderNodeAttribute','Exposed bedding faces only');relief.attribute_name='BeddingRelief'
    lamina=mathnode('MULTIPLY',lines,mathnode('MULTIPLY',side,mathnode('MULTIPLY',exposure,relief.outputs['Fac'])))
    # Low-chroma warm stone; alternating beds are relief, not paint stripes.
    tone=mathnode('ADD',mathnode('MULTIPLY',mathnode('SUBTRACT',coarse,.5),.105),mathnode('MULTIPLY',mathnode('SUBTRACT',medium,.5),.047))
    tone=mathnode('ADD',tone,mathnode('MULTIPLY',lamina,.010))
    tone=mathnode('ADD',tone,mathnode('MULTIPLY',mathnode('SUBTRACT',grain,.5),.025))
    color=node('ShaderNodeMixRGB','Subtle warm-grey mineral variation');color.blend_type='ADD'
    color.inputs[0].default_value=1.;color.inputs[1].default_value=(.345,.299,.242,1)
    ln.new(tone,color.inputs[2])
    topdust=mathnode('MULTIPLY',mathnode('MAXIMUM',norm.outputs['Z'],0),mathnode('ADD',.17,mathnode('MULTIPLY',coarse,.25)))
    footdust=mathnode('MULTIPLY',mathnode('LESS_THAN',z,.13),.18)
    dust=mathnode('MAXIMUM',topdust,footdust)
    base=mix(color.outputs[0],(.394,.326,.257,1),dust)
    rough=mathnode('ADD',.84,mathnode('MULTIPLY',grain,.12))
    bumpgrain=node('ShaderNodeBump','Sand-sized grain');ln.new(mathnode('ADD',grain,mathnode('MULTIPLY',fine,.4)),bumpgrain.inputs['Height'])
    bumpgrain.inputs['Strength'].default_value=.60;bumpgrain.inputs['Distance'].default_value=.005
    bumpbed=node('ShaderNodeBump','Truncated millimetre-scale cross laminae')
    ln.new(lamina,bumpbed.inputs['Height']);ln.new(bumpgrain.outputs[0],bumpbed.inputs['Normal'])
    bumpbed.inputs['Strength'].default_value=.32;bumpbed.inputs['Distance'].default_value=.003
    bsdf=node('ShaderNodeBsdfPrincipled','Sandstone');ln.new(base,bsdf.inputs['Base Color'])
    ln.new(rough,bsdf.inputs['Roughness']);ln.new(bumpbed.outputs[0],bsdf.inputs['Normal'])
    bsdf.inputs['Metallic'].default_value=0
    out=node('ShaderNodeOutputMaterial','Output');ln.new(bsdf.outputs[0],out.inputs[0])
    obj.data.materials.clear();obj.data.materials.append(mat)
    return mat,base,rough,bsdf,out


def bake(obj):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=16
    scene.render.bake.margin=8;scene.render.bake.use_selected_to_active=False
    scene.render.bake.use_clear=True
    mat,base,rough,bsdf,out=source_material(obj)
    nd=mat.node_tree.nodes;ln=mat.node_tree.links
    target=nd.new('ShaderNodeTexImage');target.label="Bake target"
    emission=nd.new('ShaderNodeEmission')
    textures={}
    (HERE/'textures').mkdir(exist_ok=True)
    for kind,socket in [('basecolor',base),('roughness',rough),('normal',None)]:
        im=bpy.data.images.new(f"{NAME}_{kind}",TEXTURE_SIZE,TEXTURE_SIZE,alpha=False)
        im.colorspace_settings.name='sRGB' if kind=='basecolor' else 'Non-Color'
        target.image=im
        for no in nd:no.select=False
        target.select=True;nd.active=target
        if kind=='normal':
            ln.new(bsdf.outputs[0],out.inputs[0]);bake_type='NORMAL'
        else:
            ln.new(socket,emission.inputs['Color']);ln.new(emission.outputs[0],out.inputs[0]);bake_type='EMIT'
        print("BAKE",kind,flush=True)
        bpy.ops.object.bake(type=bake_type)
        im.filepath_raw=str(HERE/'textures'/f'{kind}.png');im.file_format='PNG'
        back_up(Path(im.filepath_raw));im.save();im.pack();im.filepath='//textures/'+kind+'.png';textures[kind]=im
    ln.new(bsdf.outputs[0],out.inputs[0]);nd.remove(target);nd.remove(emission)
    pbr=bpy.data.materials.new("Stimson_Original_Baked_PBR");pbr.use_nodes=True
    nd=pbr.node_tree.nodes;ln=pbr.node_tree.links;bsdf=nd.get('Principled BSDF')
    bsdf.inputs['Metallic'].default_value=0
    for idx,(kind,im) in enumerate(textures.items()):
        node=nd.new('ShaderNodeTexImage');node.image=im;node.label=kind;node.location=(-650,250-idx*260)
        if kind=='normal':
            normal=nd.new('ShaderNodeNormalMap');normal.uv_map='SandstoneAtlas';normal.location=(-290,-250)
            ln.new(node.outputs['Color'],normal.inputs['Color']);ln.new(normal.outputs[0],bsdf.inputs['Normal'])
        else:ln.new(node.outputs['Color'],bsdf.inputs['Base Color' if kind=='basecolor' else 'Roughness'])
    obj.data.materials.clear();obj.data.materials.append(pbr)


def export_asset():
    obj=bpy.data.objects['Stimson_Sandstone_Outcrop']
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    path=HERE/f'{NAME}.glb';back_up(path)
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
        export_yup=True,export_apply=True,export_extras=True,export_texcoords=True,
        export_normals=True,export_tangents=True,export_materials='EXPORT',export_cameras=False,
        export_lights=False,export_image_format='AUTO')
    return path


def manifest(obj):
    points=[obj.matrix_world@v.co for v in obj.data.vertices]
    lo=[min(p[i] for p in points) for i in range(3)];hi=[max(p[i] for p in points) for i in range(3)]
    tris=sum(len(p.vertices)-2 for p in obj.data.polygons)
    files=[HERE/f'{NAME}.blend',HERE/f'{NAME}.glb',*(HERE/'textures').glob('*.png')]
    payload={"asset":NAME,"status":"lookdev candidate; owner visual acceptance pending",
       "authoring":"Blender 4.5.14 / original authored polygons and procedural texture bake; saved blend is the exact reexport source",
       "seed":SEED,"units":"metres", "blender_axis":"+Z up", "gltf_axis":"+Y up; Blender (x,y,z) -> glTF (x,z,-y)",
       "root_origin":"XY AABB centre, Z=ground; identity object transforms", "burial_allowance_m":.14,
       "bounds_blender":{"min":lo,"max":hi,"size":[hi[i]-lo[i] for i in range(3)]},
       "triangles":tris,"vertices":len(obj.data.vertices),"loose_components":20,
       "composition":"1 continuous basal bench, 3 obliquely jointed retreating upper beds, 2 broken plates, 14 unequal angular toe fragments",
       "materials":[{"name":"Stimson_Original_Baked_PBR","metallic":0,"textures":['basecolor.png','normal.png','roughness.png'],"resolution":[TEXTURE_SIZE,TEXTURE_SIZE],"normal":"tangent +Y (OpenGL/glTF)"}],
       "originality":"All mesh coordinates and PBR pixels authored/generated here. No photo pixel is used in runtime geometry or textures.",
       "references":[{"id":"PIA21044","url":"https://www.jpl.nasa.gov/images/pia21044-farewell-to-murray-buttes-image-4/","use":"Observed cross-bedding, vertical joints, undercut rims. Processing unspecified; not calibrated colour."},
          {"id":"PIA21042","url":"https://science.nasa.gov/resource/farewell-to-murray-buttes-image-2/","use":"Observed stepped outcrop and angular talus; processing unspecified."},
          {"id":"PIA16800","url":"https://science.nasa.gov/resource/raw-natural-and-white-balanced-views-of-martian-terrain/","use":"Colour-processing distinction only; chosen muted palette is art direction."}],
       "scope":"Decorative single outcrop study; no collisions, mining/resource semantics, LOD, game-state or Main integration.",
       "reexport_contract":{"strict":"JSON and all non-tangent buffer views including geometry, normals, UV, indices and PNG bytes are identical",
          "tangent_xyz_max_delta":.00011,"tangent_w":"exactly unchanged, +1 or -1",
          "reason":"Blender glTF exporter rounds tangents to four decimals; few components cross a rounding boundary between processes",
          "report":"verification/check-report.json; exact_binary is reported separately, never inferred"},
       "limits":["Not a measured reconstruction of a Martian site.","Texture atlas limits sub-centimetre detail; no millimetre geometry.","Godot normal/near/reverse/low and terrain contact await independent root verification and owner visual acceptance."],
       "files":{str(p.relative_to(HERE)):{"bytes":p.stat().st_size,"sha256":hashlib.sha256(p.read_bytes()).hexdigest()} for p in files}}
    path=HERE/'manifest.json';back_up(path);path.write_text(json.dumps(payload,indent=2)+"\n")
    return payload


def render_previews(prefix=''):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=48
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
    scene.view_settings.exposure=0
    world=bpy.data.worlds.new('Preview_only_neutral_daylight');world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(.56,.59,.62,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=.45;scene.world=world
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015))
    ground=bpy.context.object;ground.name='Preview_only_ground_not_saved_or_exported'
    mat=bpy.data.materials.new('Preview_only_dust');mat.diffuse_color=(.295,.244,.185,1);mat.use_nodes=True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.295,.244,.185,1)
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.95;ground.data.materials.append(mat)
    bpy.ops.object.light_add(type='SUN',location=(0,0,8));sun=bpy.context.object
    sun.rotation_euler=Euler((math.radians(29),math.radians(-26),math.radians(-32)),'XYZ')
    sun.data.energy=2.6;sun.data.angle=math.radians(.7)
    bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.lens=48
    (HERE/'renders').mkdir(exist_ok=True)
    for name,position,target in [('front',(7.7,-10.3,5.7),(0,0,.85)),('reverse',(-7.2,9.5,4.7),(0,0,.85)),('detail',(3.6,-5.2,2.35),(.1,-.5,1.03)),('rear-detail',(-3.7,5.,2.8),(0,.65,1.0))]:
        cam.location=position;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
        output=HERE/'renders'/f'{prefix}{name}.png';back_up(output);scene.render.filepath=str(output)
        print('RENDER',name,flush=True);bpy.ops.render.render(write_still=True)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--reexport',action='store_true');parser.add_argument('--render-only',action='store_true');parser.add_argument('--clay',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if args.render_only:render_previews();return
    if args.reexport:
        path=export_asset();manifest(bpy.data.objects['Stimson_Sandstone_Outcrop'])
        print('REEXPORTED',path,flush=True);return
    if args.clay:
        obj=build_geometry()
        mat=bpy.data.materials.new('Clay_geometry_review');mat.use_nodes=True
        mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.34,.32,.29,1)
        mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.90
        obj.data.materials.append(mat)
        print('CLAY_TRIANGLES',sum(len(p.vertices)-2 for p in obj.data.polygons),flush=True)
        render_previews('clay-');return
    obj=build_geometry();bake(obj)
    scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    # Store only the editable asset; preview camera, ground and lamps are added later.
    scene.world=None
    blend=HERE/f'{NAME}.blend';back_up(blend)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    # Export the persisted mesh state. Reloading also rebuilds tangent caches.
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    obj=bpy.data.objects['Stimson_Sandstone_Outcrop']
    export_asset();stats=manifest(obj)
    print('INTEGRATION_READY',json.dumps({'path':str(HERE/f'{NAME}.glb'),'triangles':stats['triangles'],'bounds':stats['bounds_blender']}),flush=True)
    render_previews()


if __name__=='__main__':main()

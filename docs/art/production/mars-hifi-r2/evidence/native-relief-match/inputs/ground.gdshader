shader_type spatial;
render_mode specular_schlick_ggx;

uniform vec4 sand : source_color = vec4(0.36,0.27,0.21,1.0);
uniform vec4 fine_sand : source_color = vec4(0.43,0.34,0.27,1.0);
uniform vec4 dark_grain : source_color = vec4(0.23,0.21,0.18,1.0);
uniform float relief_scale = 1.0;
varying vec3 place;

float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
    vec2 i=floor(p); vec2 f=fract(p); vec2 u=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1.0,0.0)),u.x),mix(hash(i+vec2(0.0,1.0)),hash(i+vec2(1.0,1.0)),u.x),u.y);
}
void vertex() { place=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
    vec2 p=place.xz;
    float macro=noise(p*.055)*.6+noise(p*.18)*.4;
    float pocket=smoothstep(.66,.84,noise(p*.13+vec2(7.0,1.0)));
    float meso=noise(p*2.0)*.65+noise(p*5.2)*.35;
    float pixel=max(length(dFdx(p)),length(dFdy(p)));
    float grain=(noise(p*82.0)-.5)*(1.0-smoothstep(.012,.055,pixel));
    float ripple=sin(p.y*29.0+p.x*4.0+noise(p*.8)*5.0)*pocket*.0008;
    vec3 base=mix(sand.rgb,fine_sand.rgb,pocket*.46+macro*.15);
    base=mix(base,dark_grain.rgb,smoothstep(.59,.87,meso)*.34*(1.0-pocket));
    ALBEDO=base*(.92+meso*.13+grain*.11);
    ROUGHNESS=.94; METALLIC=0.0;
    float height=((meso-.5)*.009*(1.0-pocket)+ripple+grain*.0006)*relief_scale;
    vec3 dx=dFdx(VERTEX); vec3 dy=dFdy(VERTEX);
    vec3 r1=cross(dy,NORMAL); vec3 r2=cross(NORMAL,dx);
    float det=dot(dx,r1);
    vec3 grad=sign(det)*(dFdx(height)*r1+dFdy(height)*r2);
    NORMAL=normalize(abs(det)*NORMAL-grad);
}

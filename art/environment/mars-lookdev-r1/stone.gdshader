shader_type spatial;
uniform vec4 base : source_color = vec4(.56,.49,.40,1.0);
varying vec3 place;
float hash(vec3 p) { return fract(sin(dot(p,vec3(127.1,311.7,74.7)))*43758.5453); }
float noise(vec3 p) {
    vec3 i=floor(p); vec3 f=fract(p); vec3 u=f*f*(3.0-2.0*f);
    return mix(mix(mix(hash(i),hash(i+vec3(1,0,0)),u.x),mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),u.x),u.y),mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),u.x),mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),u.x),u.y),u.z);
}
void vertex() { place=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
    float n=noise(place*6.0)*.7+noise(place*21.0)*.3;
    ALBEDO=base.rgb*(.90+n*.20); ROUGHNESS=.90; METALLIC=0.0;
    float height=(n-.5)*.006;
    vec3 dx=dFdx(VERTEX); vec3 dy=dFdy(VERTEX);
    vec3 r1=cross(dy,NORMAL); vec3 r2=cross(NORMAL,dx); float det=dot(dx,r1);
    NORMAL=normalize(abs(det)*NORMAL-sign(det)*(dFdx(height)*r1+dFdy(height)*r2));
}

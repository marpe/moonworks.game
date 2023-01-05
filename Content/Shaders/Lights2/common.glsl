

struct Light {
    float lightIntensity;
    vec3 lightColor;
    float volumetricIntensity;
    float angle;
    float coneAngle;   
};

vec4 CalculateLight(Light light, vec2 texCoord) {
    vec2 offset = texCoord - vec2(0.5, 0.5);
    return vec4(offset.xy, 0, 1);
}

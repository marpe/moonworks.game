

struct Light {
    vec3 lightColor;
    float lightIntensity;
    float volumetricIntensity;
    float angle;
    float coneAngle;   
};

vec3 CalculateLight(Light light, vec2 texCoord) {
    vec2 center = vec2(0.5, 0.5);
    float radius = 0.5;
    vec2 worldPos = texCoord;
    
    vec2 offset = worldPos - center;
    float distance = length(offset);
    float relativeDistance = distance / radius;
    float radialFalloff = clamp(1 - relativeDistance, 0, 1);
    radialFalloff *= radialFalloff;

    float angleRad = radians(light.angle);
    float deltaAngle = dot(vec2(cos(angleRad), sin(angleRad)), normalize(offset));
    deltaAngle = clamp(deltaAngle, 0, 1);

    float maxAngle = radians(light.coneAngle) * 0.5;
    
    float angularFalloff = max(int(light.coneAngle == 360), smoothstep(maxAngle, 0, acos(deltaAngle)));
    
    float finalIntensity = radialFalloff * angularFalloff * light.lightIntensity;
    
    return light.lightColor * finalIntensity;
}

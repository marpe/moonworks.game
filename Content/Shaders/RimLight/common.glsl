#define MAX_NUM_TOTAL_LIGHTS 128

struct Light
{
    float lightIntensity;
    float lightRadius;
    vec2 lightPos;
    vec3 lightColor;
    float volumetricIntensity;
    float rimIntensity;
    float angle;
    float coneAngle;
};

vec3 CalculateLight(vec3 baseColor, Light light, vec2 worldPos) {
    vec2 offset = light.lightPos - worldPos;
    float distanceToLight = length(offset);
    if (distanceToLight > light.lightRadius) {
        return vec3(0);
    }

    float radialFalloff = clamp(distanceToLight / light.lightRadius, 0, 1);
    radialFalloff = pow(1.0 - radialFalloff, 2.0);

    float angleRad = radians(light.angle);
    float deltaAngle = acos(dot(normalize(offset), vec2(cos(angleRad), sin(angleRad))));
    float maxAngle = radians(light.coneAngle) * 0.5;

    if (deltaAngle > maxAngle) {
        return vec3(0);
    }

    float angularFalloff = max(smoothstep(maxAngle, 0, deltaAngle), int(light.coneAngle == 360));

    float finalIntensity = clamp(light.lightIntensity * radialFalloff * angularFalloff, 0, 1);
    vec3 lightColor = light.lightColor * finalIntensity;
    vec3 shadedColor = baseColor.rgb * lightColor;
    shadedColor += lightColor * light.volumetricIntensity;

    return shadedColor;
}

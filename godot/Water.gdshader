shader_type spatial;
render_mode cull_disabled;

uniform int mode_count = 0;
uniform vec4 modes[24]; // Godot world direction X/Z, amplitude, wavenumber
uniform vec2 phases[24]; // frequency, initial phase
uniform float simulation_time = 0.0;
varying vec3 water_world;
varying vec2 water_slope;

void vertex() {
    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float height = 0.0;
    vec2 slope = vec2(0.0);
    for (int i = 0; i < mode_count; i++) {
        float phase = modes[i].w * dot(modes[i].xy, world.xz)
            - phases[i].x * simulation_time + phases[i].y;
        height += modes[i].z * cos(phase);
        slope += modes[i].xy * modes[i].z * modes[i].w * sin(phase);
    }
    VERTEX.y = height;
    NORMAL = normalize(vec3(slope.x, 1.0, slope.y));
    water_world = vec3(world.x, height, world.z);
    water_slope = slope;
}

void fragment() {
    // Spatial glints reveal movement without inventing a second visual wave field.
    float camera_distance = distance(CAMERA_POSITION_WORLD.xz, water_world.xz);
    float detail = 1.0 - smoothstep(25.0, 90.0, camera_distance);
    float wave_light = clamp(0.5 + detail * (water_world.y * 4.0 + dot(water_slope, vec2(2.5, 1.5))), 0.0, 1.0);
    vec3 water_color = mix(vec3(0.025, 0.16, 0.22), vec3(0.16, 0.49, 0.55), wave_light);
    ALBEDO = water_color;
    EMISSION = water_color * 0.22;
    ROUGHNESS = 0.32;
    SPECULAR = 0.65;
    float haze = smoothstep(100.0, 700.0, camera_distance);
    ALBEDO = mix(ALBEDO, vec3(0.43, 0.61, 0.68), haze);
}

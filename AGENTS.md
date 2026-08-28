# AGENTS.md — Oasis (contexto de trabajo opencode)

Guía operativa para agentes que trabajan en este proyecto Unity de Oasis.
Complementa a `CLAUDE.md` (colaboración, agentes Claude). Todo lo de abajo es
conocimiento verificado en la sesión de trabajo, no inventado.

## Stack y rutas

- **Motor**: Unity 6000.4.7f1, URP, C#.
- **Proyecto Unity**: `Oasis/Oasis` (carpeta con `Assets/`).
  - Editor scripts: `Oasis/Oasis/Assets/Editor/*.cs` (todos `[MenuItem("Oasis/...")]`).
  - Texturas CC0 (ambientCG): `Assets/Textures/Web/<nombre>/` con `*_Color.jpg`,
    `*_NormalDX.jpg`, `*_Metalness.jpg`.
  - Materiales: `Assets/Materials/` (Oasis* , escena) y `Assets/Materials/Generated/`
    (Mat_* , Mat_TF_00..13, Tex_*).
- **Escena**: `Assets/Scenes/SampleScene.unity`.

## Arrancar / reiniciar el editor (IMPORTANTE)

- **Ruta correcta del binario** (la ruta `/Applications/Unity/...` NO existe):
  `$HOME/Unity/Hub/Editor/6000.4.7f1/Unity.app/Contents/MacOS/Unity`
- Tras cambiar un `.cs`, Unity recompila solo (domain reload). Esperar unos segundos
  antes de llamar al MCP.
- Para reinicio forzado:
  1. `pkill -9 -f Unity.app/Contents/MacOS/Unity
  2. relanzar con `-projectPath /Users/Alberto/UnityProjects/Oasis/Oasis`
  3. esperar a que el MCP escuche en el puerto 8090.

## Control remoto del editor (MCP)

- El plugin MCP Unity escucha en `ws://localhost:8090/McpUnity`.
- Cliente: `python3 <ruta>/unity-tool.py <method> '<json params>'`
  (ruta: se copia a `/var/folders/.../T/opencode/unity-tool.py` en cada sesión;
  si no existe, recrearla con el patrón websocket de 8090).
- Métodos usados:
  - `execute_menu_item {"menuPath":"Oasis/<Menú>"}` — ejecutar generadores.
  - `get_gameobject {"idOrName":"<nombre>"}` — posición/componentes de un objeto.
    (ojo: algunos nodos reportan transform.position; la clave alta es `gameObject.components[Transform].properties.position`).
  - `get_material_info {"materialPath":"Assets/Materials/<x>.mat"}` — propiedades del material.
  - `save_scene {}`
- NO está disponible un método de captura de pantalla. Capturar ventana de Unity con
  `screencapture -x -l <windowID> out.png` (windowID vía `CGWindowListCopyWindowInfo`
  o AppleScript). El modelo actual no puede leer imágenes; el humano verifica.

## Secuencia de generación (orden correcto)

1. `Oasis/Generate Base Terrain`
2. `Oasis/Build City Core`
3. `Oasis/Build Outer Zones`
4. `Oasis/Texture City & Detail`
5. `Oasis/Clean Up Clutter`
6. `Oasis/Lighting/Final Touch` (sol, niebla, skybox HDRI, post-FX)
7. `save_scene`

`Oasis/Clean Up Clutter` también aplica las texturas web; se recomienda volver a
ejecutarlo tras texturizar (idempotente). `Oasis/Add Solidity Detail` NO se usa
(añade clutter que luego se limpia). Menús de cámara: `Oasis/Camera/Ver Ciudad` (u Otros).

### Pase visual final (`Oasis/Lighting/Final Touch`, OasisLighting.cs)

- Crea `Mat_Wood` (WoodFloor001) y `Mat_Roof` (RoofingTiles013A); reasigna
  objetos con prefijo `CauceDeck_`/`Dock_`/`Boat_`/`Mill_` a madera (10) y
  nombres con sufijo `_Roof` a teja (20).
- Tintes de material; luz direccional `Sun` (color 1,0.86,0.70, intensidad 1.3, rot 28,-32,0) y
  elimina otras direccionales; niebla lineal (0.62,0.55,0.58; 260→1150).
- Skybox `Assets/Materials/OasisSky.mat` (Skybox/Panoramic) con
  `Assets/Textures/Web/EveningSkyHDRI010A/EveningSkyHDRI010A_1K_HDR.exr`.
- Post-FX en `Assets/Materials/Generated/OasisFXProfile.asset` (perfil propio del
  Global Volume de la escena): ColorAdjustments (contraste 16, sat 14, filtro 1,0.97,0.93),
  Tonemapping ACES, Bloom (1.4, 0.75), Vignette (0.22). OJO: `VolumeProfile.Add<T>`
  lanza si existe → usar TryGet/Add; persiste con `AddObjectToAsset` + SaveAssets.
- Verificación: tras editar `.cs`, ESPERAR domain reload real (el MCP pausa la
  importación: si el log dice `domain reloads=0` la compilación vieja sigue
  activa; si no recompila, reiniciar Unity con `pkill -9`).

## Fixes aplicados (sesión 2026-08-28) — NO REPETIR SIN NECESIDAD

### "La plataforma flotaba" -> Terreno
- **Bug** en `OasisTerrainGenerator.cs` `Edges()`: `Mathf.SmoothStep(0.30, 0.48, v)`
  devolvía 0.30–0.48 en TODO el mapa → terreno entero elevado ~27 m y valle inexistente.
  Reescribir `MountainWall` como anillo: `d = max(|nx-0.5|,|nz-0.5|)`,
  `wall = clamp(InverseLerp(MountainStart, 0.9, d*2))^2`, gap de El Paso en el NORTE
  (`nz < 0.5` en el wedge central) con `PasoHeightScale`.
- Constantes actuales del terreno:
  - `CityLevel = 0.14` → 0.14*80 = 11.2 m, exactamente el `BaseY` de la ciudad (sin escalón).
  - `CityHalf = 0.11`, `ValleyNoiseAmp = 0.015`, `MountainStart = 0.72` (estribaciones lejos),
    `RiverMeanX = 0.415` (río x≈-42, rozando el lado oeste de la ciudad, casi central),
    `RiverHalfWidth = 0.025`, `RiverDepth = 0.035`.
- `BaseY`/`baseY` = **11.2** en `OasisCityGenerator.cs`, `OasisCityTextures.cs`,
  `OasisZoneBuilder.cs` (zona central) y fountain/lamps de la cinemática.

### "No se veían las texturas" -> Materiales
- `OasisMetal.mat`: `_BaseColor` era (0.12,0.15,0.19) → puesta gris claro (0.82,0.85,0.88);
  `_MetallicGlossMap` apuntaba a `_Color.jpg` (bug) → corregida a `_Metalness.jpg`;
  `_Metallic 0.5`, `_Smoothness 0.6`, tiling 4x.
- `OasisGlass.mat`: ahora con mapa `MetalPlates006` y alpha 0.88 (era 0.45, translúcido lavado).
- `OasisRoad`/`OasisPlaza`: `_BaseColor` blanco respectivo (asfalto/adoquín al natural).
- Torres `Mat_TF_00..13`: emisión de las máscaras reducida a 16% (antes 100%) para que
  el concreto CC0 se lea; `_Smoothness 0.5`.
- Regla clave URP: albedo visible = `_BaseMap.rgb × _BaseColor.rgb`; un base oscuro apaga la textura.

## Mapa de referencia (diseño)

- `/Users/Alberto/Downloads/mapa-interior-oasis.html` — SVG del valle (800×760);
  río N-S, El Corazón central, distritos numerados: 1 El Corazón, 2 La Cámara de Jano
  (subterránea, bajo El Corazón), 3 Los Archivos (ala este), 4 El Mercado (orilla del río),
  5 Los Campos del Valle (sur), 6 El Cauce (a lo largo del río), 7 La Forja (mitad norte),
  8 La Cúspide (norte, laderas del Járnviðr, incluye Los Cimientos), 9 El Barrio Bajo (oeste),
  10 El Paso (norte, único acceso). El río corre N-S rozando el lado oeste de la ciudad
  (x≈-42, el SVG lo dibuja en el centro de forma esquemática).
- Coordenadas actuales (Unity, centro = El Corazón en (0, ~11.2, 0)) alineadas al SVG (~0.5 m/px):
  - El Mercado en (-12, 58) sur del Corazón, junto al río; El Barrio Bajo (pad) en (-95, 10) oeste-centro.
  - Forja z≈-110, Cúspide NE (terrazas 88..114, z -148..-172; Los Cimientos z≈-132..-148),
    Campos z≈+130..175, ElPaso z≈-244 (gap norte).
  - Cauce: paseos en (-26,-55), (-30,-25), (-33,+20) entre ciudad y río.
  - Río x≈-42 (meandros -30..-55, sección sur x≈-35); molinos x≈-53 z=8 y -16,
    embarcadero x≈-40 z=-48; Cámara de Jano (JANO_*) en (-24,34) al pie SW del Corazón.
  - Los Archivos (ARCH_*) en (31,-16) NE del Corazón.
- Convención: vista por defecto desde el sur-este; norte = -z (El Paso).

## Agentes y skills opencode (creados ~2026-08-28)

- Agentes: `.opencode/agent/*.md` (49, convertidos de `.claude/agents/`; `mode: subagent`,
  invocables con Task `subagent_type`). Relevantes: `unity-specialist`, `gameplay-programmer`,
  `engine-programmer`, `unity-shader-specialist`, `technical-artist`, `art-director`,
  `level-designer`, `world-builder`, `technical-director`.
- Skills: `.opencode/skills/*/SKILL.md` (73, convertidos de `.claude/skills/`).
- OJO: los agentes/skills del proyecto solo cargan si opencode se lanza desde
  `/Users/Alberto/UnityProjects/Oasis` (los globales están en `~/.config/opencode/`).
- Tras crear/editar agentes, skills o `opencode.json`: **reiniciar opencode**.

## Reglas de trabajo

- Protocolo de colaboración (de CLAUDE.md): preguntar antes de escribir archivos;
  mostrar borrador; aprobación explícita; sin commits sin permiso.
- Lenguaje de conversación con el usuario: español.
- Respuestas concisas. No añadir código/comentarios no pedidos.
# Estado del proyecto

> Lo mantiene Claude Code. Actualizar después de cada paso que compile.
> Última actualización: 2026-10-15 02:55 (v0.5: puntos, combo, 9 zonas con fade, HUD ampliado y fin de partida con récord).

## Hecho

### v0.1 (committed)

- Arte placeholder, PersonajeData, CharacterSelector, PlayerController base, BackgroundScroller, CameraFollow, SceneSetup, BuildScript v0.1 → `Builds/v0.1/SkateInacap.exe`.

### v0.2 (committed)

- PlayerController reescrito (velocidad por truco, flip en aire tras Ollie, 360 con tilt Z, caída si truco no termina).
- Arte_nuevo procesado (flood fill desde 4 bordes + halo cleanup + bbox del blob más grande).
- SceneSetup con PPU/pivot por carpeta, Cámara ortographicSize=altoFondo/2, alturaSuelo en vereda.
- Build `Builds/v0.2/SkateInacap.exe`.

### v0.2b–v0.2e

- Fix spriteMode=Single forzado (meta files de arte viejo rompían LoadAsset).
- Fix dos skates visibles → Board hijo oculto por default; solo visible en Kickflip/360.
- Fix Daniel/Felipe invertidos.
- Fix bolsas blancas interiores (espacio entre piernas, axilas).
- Fix brazo recortado en aire_sin_tabla (split por componentes conectados en vez de 50/50).
- Fix skate cruzando pies (pivot top-center, localScale 0.5, localPos.y=0).

### v0.3 (nuevo)

- [x] **Escala aire_sin_tabla normalizada** (v0.3b, iteración): la referencia debía ser `aire_con_tabla` (misma postura de salto) y no `parado × 1.1` (que es cuerpo recto + tabla). Daniel aire_con_tabla=549px → aire_sin_tabla=550px. Felipe 551→552px. Felipe ya no se ve gigante al saltar con K/L.
- [x] **Fondo seamless (3 copias + flipX alterno)**: `BackgroundScroller` ahora acepta array de N fondos y renderers; al reciclar el más atrasado al frente invierte flipX respecto al vecino. `SceneSetup` crea FondoA/B/C en x=-W,0,+W con flipX=false,true,false. El borde derecho de uno calza exacto con el izquierdo de la siguiente copia espejada.
- [x] **Sombra elíptica bajo el jugador**: `process_arte_nuevo.py` genera `Assets/Art/Effects/shadow.png` (256x72, alpha radial). `SceneSetup` crea GameObject hijo "Sombra" con PPU=200. `PlayerController.ActualizarSombra()` la mantiene pegada al suelo y la achica/aclara con la altura (`alturaMaxSombra=3`).
- [x] **Balanceo idle + inclinación al moverse**: hijo "Skater" separado del playerGO raíz para no afectar Board/Sombra. Parado: `scale.Y` oscila ±2% con período 1.5s. Moviéndose: tilt Z 3° según dirección (flipX). `ResetSkaterTransform()` cancela tilt/balanceo al iniciar truco y en caída.
- [x] **HUD legible**: Label personaje 44pt bold + Outline(2px) + Shadow(3px). Label timer 56pt bold + Outline+Shadow. Posiciones subidas (30px margen).
- [x] **v0.3c fix zapatilla cortada + transparencia**: el split por columnas iguales cortaba la zapatilla izquierda del `impulsando` (bbox del dibujo x=405-894 invadía la columna de `parado` 0-444). Nuevo `process_personaje_sheet` extrae los 4 componentes conectados grandes y los asigna por centro X. También el heurístico de "bolsas blancas interiores" ahora solo elimina las VERTICALES (bbox h > w*1.3 → espacio entre piernas), preservando zapatillas blancas horizontales.
- [x] **Build `Builds/v0.3/SkateInacap.exe`** (Succeeded, 117 MB).
- [x] **v0.3d fix hueco blanco interior brazo-cuerpo y torso agachado**: la heurística anterior sólo eliminaba huecos verticales (h > w*1.3), lo que dejaba visible el hueco diagonal brazo-cuerpo en `impulsando` (size=1661 px, h/w=1.23) y el hueco interior del torso en `agachado` (size=3844 px, h/w=1.10) de Felipe. Se agrega regla `size >= 1000 → eliminar`. Las zapatillas (<500 px) y detalles del polo quedan intactos. Build `Builds/v0.3d/SkateInacap.exe`.
- [x] **v0.3f repintado del torso blanco en aire_con_tabla**: el artista dibujó la pose `aire_con_tabla` de Felipe con la mitad delantera del polo pintada de blanco/beige (RGB~240,224,166), mientras que las demás poses tienen el polo uniforme verde. El fix anterior (`size >= 1000`) no detectaba este caso porque los pixeles no eran blanco puro (RGB>=230) sino tonos claros desaturados. Nueva función `_rellenar_blanco_torso` muestrea el color del polo desde `parado` (Felipe RGB(30,118,37), Daniel RGB(151,36,33)) y repinta cualquier píxel opaco **claro (luminance ≥ 150) y desaturado (sat ≤ 0.30)** dentro de la zona del torso (25-60% vertical, 15-85% horizontal del body bbox). Felipe: 4584 px repintados; Daniel: 407 px. Amarillos saturados, piel y zapatillas preservados. Build `Builds/v0.3f/SkateInacap.exe`.

### v0.4 (nuevo)

- [x] **Nuevas hojas de personajes `Personajes_v2`**: el usuario detectó que `aire_con_tabla` tenía otro estilo distinto al resto de poses (polo con V invertida y líneas blancas vs. polo verde con rayo amarillo diagonal del resto). Pidió hojas nuevas en Canva con el estilo del aire_sin_tabla aplicado a las 5 poses. `Arte_nuevo/Personajes_v2/{felipe,daniel}_poses.jpg` traen las 5 poses en una fila horizontal con estilo uniforme: Felipe polo verde con rayo amarillo diagonal + bucket hat verde + Jordan negras; Daniel musculosa roja + pantalón gris + Jordan rojas.
- [x] **`_tools/process_personajes_v2.py`**: pipeline dedicado que lee ambas hojas, split por 5 componentes conectados grandes (≥20000 px), asigna por centro X, normaliza escala con `aire_con_tabla` (pose 4) como referencia usando `max(bbox_w, bbox_h)`, uniformiza canvas con pie al centro (reusa `uniformar_y_guardar` de v0.3e). Omite `_rellenar_blanco_torso` porque el estilo nuevo ya es uniforme. Heurística `_pose5_tiene_tabla` (pixeles amarillo-madera en el tercio inferior > 1500 px) para decidir si la pose 5 reemplaza a `aire_sin_tabla` o se preserva la anterior. Ambas hojas pasaron: pose 5 sin tabla visible → se reemplaza. Previews `Sprites_procesados/preview_v2_{Felipe,Daniel}.png` lado a lado 5×1.
- [x] **Board offset calibrado a `aire_con_tabla`** (`_tools/medir_tabla_offset.py`): medí la posición de la tabla dibujada en Felipe/Daniel `aire_con_tabla.png` filtrando pixeles amarillo-madera en el tercio inferior del bbox del personaje (así no capturo el rayo amarillo del polo). Felipe tabla top-center = (-0.20, +0.40) u, Daniel = (+0.15, +0.365) u. Promedio X ≈ 0, Y ≈ +0.38. Scale sugerido 0.428 (Felipe) y 0.370 (Daniel), promedio 0.40. `SceneSetup.cs` → `boardGO.localPosition = (0, 0.38, 0.1)`, `localScale = (0.4, 0.4, 1)`. Así al disparar Kickflip/360 (que oculta el skater pose aire_con_tabla y muestra Board+pose aire_sin_tabla) la tabla animada aparece prácticamente donde estaba la tabla pintada, sin snap.
- [x] **Build `Builds/v0.4/SkateInacap.exe`** (Succeeded, 108 MB).
- [x] **v0.3e consistencia visual entre poses**: (1) `uniformar_y_guardar` ahora centra cada sprite por el **pie real** (promedio X del 5% inferior del bbox), no por el bbox completo — antes `impulsando` y `aire_sin_tabla` tenían el pie 78-99 px desplazado del centro del canvas (brazos extendidos movían el bbox center), haciendo que el personaje saltara lateralmente al cambiar de pose. Ahora foot_dx = +1 px en las 5 poses. (2) Canvas con padding simétrico desde el pie → el pie queda en el centro geométrico del canvas (`canvas_w/2`), compatible con pivot default (0.5, 0) de Unity sin configurar pivot custom. (3) `aire_sin_tabla` ahora se escala por `max(bbox_w, bbox_h)` referenciado a `aire_con_tabla` (salto normal con espacio) — antes solo igualaba altura, dejando aire_sin_tabla con ancho 583/660 px vs aire_con_tabla 413/450 px, lo que lo hacía ver visualmente más grande. Build `Builds/v0.3e/SkateInacap.exe`.

### v0.5 (nuevo)

- [x] **ZonaData ScriptableObject** (`Assets/Scripts/ZonaData.cs`): `zona`, `subzona`, `umbral`, `fondo`, `colorCielo`. 9 assets en `Assets/Resources/Zonas/` con umbrales 0 / 1000 / 2500 / 4500 / 7000 / 10000 / 14000 / 19000 / 25000 (Puente Alto Pasaje → Feria → Plaza, Ñuñoa Avenida → Estadio Nacional → Villa Olímpica, Cordillera Cajón del Maipo → Embalse El Yeso → Farellones). `SetupArt` los crea idempotentemente desde `SceneSetup.cs`.
- [x] **ScoreManager** (`Assets/Scripts/ScoreManager.cs`): singleton de escena. Ollie=100, Kickflip=300, 360Flip=500. Combo: si el próximo truco llega en <2s el multiplicador sube hasta x5; si no, decae a x1. `NotificarCaida()` corta el combo (invocado desde `PlayerController.Caerse`). Eventos públicos: `OnPuntajeCambio`, `OnTrucoAnotado`, `OnMultiplicadorCambio`, `OnComboCortado`.
- [x] **StageManager** (`Assets/Scripts/StageManager.cs`): escucha `ScoreManager.OnPuntajeCambio` y, al cruzar umbral, hace fade a negro, swap de sprite en los 3 fondos, cambia `colorCielo` de la cámara, fade de vuelta y muestra cartel 2s con "Zona - Subzona". El inicio de partida ya muestra la zona de arranque.
- [x] **PlayerController** hooked: `CompletarTruco` llama `ScoreManager.Instance.RegistrarTruco(trucoActual)` antes de resetear estado; `Caerse` llama `NotificarCaida()`.
- [x] **GameManager ampliado**: HUD con `labelPuntaje` (arriba-derecha), `labelMultiplicador` (debajo), `toastTruco` ("KICKFLIP +300 x2") con fade in/out 1.65s. Al terminar el timer: `PanelFin` full-screen con puntaje, zona alcanzada, record (PlayerPrefs `SkateInacap_Record`) y botones "JUGAR DE NUEVO" / "MENU".
- [x] **SceneSetup actualizado**: fondo inicial = `puente_alto_pasaje.png` (en vez del placeholder), crea `ScoreManager` + `StageManager` + fade overlay + cartel + panel fin, cablea 9 ZonaData ordenados por umbral en StageManager.
- [x] **Build `Builds/v0.5/SkateInacap.exe`** (Succeeded, 164 MB).

## Qué pruebas en el .exe (v0.5)

Doble clic a `C:\Users\danie\Desktop\JuegoInacap\Builds\v0.5\SkateInacap.exe`:

1. **HUD base**: arriba-izq "Skater: Felipe/Daniel", arriba-centro timer 02:00, arriba-der puntaje 0 + multiplicador oculto.
2. **Puntos**: Space (Ollie)=+100, K (Kickflip)=+300, L (360)=+500. Encadenar Space→K en <2s suma x2 (="OLLIE +100" luego "KICKFLIP +600 x2"). Caerse (truco incompleto) resetea multiplicador.
3. **Cartel inicio**: aparece "Puente Alto - Pasaje" a los 0.3s.
4. **Transición de zona**: acumula 1000 pts → fade a negro, cambia fondo a "Feria", vuelve, cartel 2s. Igual a los 2500 (Plaza), 4500 (Ñuñoa Avenida), etc.
5. **Fin de partida**: deja correr hasta 00:00 (o fuerza con muchos trucos rápidos para ir subiendo zonas). Debe aparecer panel oscuro con puntaje, zona alcanzada, "Record: X" (o "NUEVO RECORD: X" si es la primera vez / superó), y los 2 botones. Reintentar reinicia escena; Menú vuelve al menú.

## Qué pruebas en el .exe (v0.4)

Doble clic a `C:\Users\danie\Desktop\JuegoInacap\Builds\v0.4\SkateInacap.exe`:

0. **Consistencia visual**: Felipe y Daniel se ven como la MISMA persona en las 5 poses (parado, impulsando, agachado, Ollie=Space, Kickflip/360=K/L). Polo verde con rayo amarillo diagonal (Felipe) o musculosa roja (Daniel) sin cambios de estilo entre poses.
1. **Ollie→Kickflip sin snap**: salta con Space, en el aire presiona K. La tabla animada debería aparecer aproximadamente donde estaba la tabla dibujada en la pose Ollie (no saltar lateralmente ni cambiar de tamaño abruptamente). Si notas offset grande, ajustar `boardGO.localPosition/Scale` en `SceneSetup.cs`.

## Qué pruebas en el .exe (v0.3 — referencia)

Doble clic a `C:\Users\danie\Desktop\JuegoInacap\Builds\v0.3\SkateInacap.exe`:

1. **Fondo**: avanza largo rato con flecha derecha. No deberías ver corte vertical ni franja azul vacía: 3 copias con flipX alterno hacen el borde seamless.
2. **Escala**: Felipe/Daniel parados y en aire (K, L) deberían tener aproximadamente el mismo tamaño corporal. En aire puede verse ~5% más alto por extensión, pero NO gigante.
3. **Sombra**: elipse oscura bajo los pies cuando estás en el suelo. Al saltar (Space/K/L) se achica y aclara con la altura.
4. **Idle vivo**: quieto, el personaje "respira" (sube/baja leve ~1.5 s). Al moverte, se inclina ~3° hacia la dirección de avance.
5. **HUD**: "Skater: Daniel/Felipe" arriba-izquierda y timer arriba-centro deben leerse claros sobre el cielo (letras blancas con contorno negro + sombra).
6. **Trucos**: Space=Ollie, K=Kickflip, L=360Flip; combo aéreo Space→K/L sigue funcionando.

## Qué sigue (pendiente, en orden)

De `PARA_CLAUDE_CODE.md`, próximas secciones:

1. **Perros**: obstáculos horizontales que aparecen en zona avanzada, saltar con Ollie los esquiva, colisionar = caída.
2. **Modo demo** / **audio** (música loop + SFX trucos).
3. Pulido menú, créditos, build v1.0 final.

Pendiente concreto para afinar v0.5:
- Validar que las transiciones de zona ocurran a los umbrales esperados (jugar 2-3 min seguidos encadenando trucos).
- Si el personaje "aparece" raro cuando el fondo cambia (porque los PNGs de zonas no tienen exactamente la misma altura de vereda), ajustar `alturaSuelo` por zona (hoy es fijo en SceneSetup).

## Problemas abiertos

- Validación manual de `Builds/v0.4/SkateInacap.exe` pendiente (lo acabo de generar).
- Si Ollie→Kickflip tiene snap visible, afinar `boardGO.localPosition/Scale` por personaje (requeriría guardarlos en `PersonajeData` y leerlos desde `PlayerController`).

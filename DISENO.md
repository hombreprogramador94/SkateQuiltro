# Diseño — Skate INACAP

Juego 2D de skate con stickmen por las calles de Santiago. Vista lateral, scroll
horizontal, trucos por teclado, perros persiguiéndote.

> Este documento es la fuente de verdad del alcance. Las decisiones de arte y
> gameplay van acá; el código se adapta a esto, no al revés.

---

## 1. Arte

### 1.1 Estilo
Cómic / mural, generado en Canva. Línea gruesa, color plano, sin degradados
pesados. Los archivos finales se descargan a `Arte_nuevo/` (no sube al repo
hasta que haya un set mínimo utilizable).

### 1.2 Personajes

**Felipe y Daniel tienen sprites PROPIOS** — ya no son el mismo stickman con
distinto color. Cada uno elige su set completo al seleccionarse.

Para cada personaje:
- 4 poses **en tabla**:
  - `idle` — parado
  - `push` — impulsando con una pierna
  - `crouch` — agachado (preparando truco)
  - `air` — en el aire con la tabla
- 1 pose **sin tabla**:
  - `air_notrick` — en el aire mientras la tabla hace un truco

Convención de archivo:
```
Arte_nuevo/Characters/<Felipe|Daniel>/<idle|push|crouch|air|air_notrick>.png
```

### 1.3 Tabla

La tabla se dibuja aparte en 3 vistas:
```
Arte_nuevo/Board/<side|bottom|top>.png
```

- `side` — vista normal (default)
- `bottom` — tabla girada mostrando lijas (mitad de un kickflip)
- `top` — vista desde arriba (mitad de un 360 flip)

**Kickflip y 360 flip se animan por código** sobre estos 3 sprites:
- **Kickflip**: `side → bottom → side` + rotación 360° en Z, 0.6 s.
- **360 Flip**: `side → top → side` + rotación 360° en Z + rotación 360° en Y
  (se simula escalando X de 1 → 0 → 1), 0.9 s.
- **Ollie**: la tabla queda en `side` todo el salto, sin rotación.

Durante el truco, el personaje usa la pose `air_notrick` (sin tabla pegada).

### 1.4 Zonas y fondos

**9 fondos en 3 zonas** (3 por zona):

| Zona | Fondos |
|---|---|
| **Puente Alto** | pasaje, feria, plaza |
| **Ñuñoa** | avenida, estadio, villa olímpica |
| **Cordillera** | cajón del maipo, el yeso, farellones |

Convención:
```
Arte_nuevo/Backgrounds/<puente_alto|nunoa|cordillera>/<nombre>.png
```

### 1.5 Obstáculos y NPCs

**Genéricos (todas las zonas):** cono, bache, baranda, rampa, barrera, charco.

**Jauría de perros:** 3 perros con 4 frames de carrera cada uno.
Convención:
```
Arte_nuevo/Dogs/dog<1|2|3>_run<1-4>.png
```

**NPCs específicos por zona:** se definen por zona en el ScriptableObject
(§2.2). Al principio pueden ser los mismos genéricos; cada zona agrega los
suyos cuando el arte esté.

---

## 2. Gameplay

### 2.1 Trucos y puntuación

| Tecla | Truco | Puntos base |
|---|---|---|
| Espacio | Ollie | 100 |
| K | Kickflip | 300 |
| L | 360 Flip | 500 |
| ← → | Mover | — |

**Multiplicador de combo:** encadenar un truco antes de tocar suelo lo suma al
combo. El puntaje del truco se multiplica por `(1 + combo * 0.5)`. El combo se
muestra en el HUD y se resetea al aterrizar sin truco encadenado o al chocar.

### 2.2 Zonas por puntaje

El fondo (y los obstáculos permitidos) cambian por puntaje acumulado:

| Puntaje acumulado | Fondo que se activa |
|---|---|
| 0 | Puente Alto · pasaje |
| 1 000 | Puente Alto · feria |
| 2 500 | Puente Alto · plaza |
| 4 500 | Ñuñoa · avenida |
| 7 000 | Ñuñoa · estadio |
| 10 000 | Ñuñoa · villa olímpica |
| 14 000 | Cordillera · cajón del maipo |
| 19 000 | Cordillera · el yeso |
| 25 000 | Cordillera · farellones |

**Implementación:** cada zona es un `ScriptableObject` con campos:
- `nombre` (string)
- `umbralPuntaje` (int)
- `fondo` (Sprite)
- `obstaculos` (lista de prefabs permitidos)

Las zonas viven en `Assets/Zonas/*.asset` y el `GameManager` tiene una lista
ordenada. Agregar una zona nueva = crear un asset más, sin tocar código.

### 2.3 Persecución (jauría)

La jauría de 3 perros corre detrás del jugador. El HUD muestra una **barra de
distancia** con la cercanía más próxima de los perros.

- **Choque con obstáculo** → perros se acercan X unidades.
- **Truco completado** → perros se alejan Y unidades (proporcional al puntaje
  del truco).
- **Base**: los perros se acercan lentamente con el tiempo.
- **Si te alcanzan** → game over con cartel `"¡Te pillaron los quiltros!"`.

Valores iniciales (ajustables en el Inspector):
- Acercamiento base: 0.3 u/s
- Choque con cono/bache: +5 u
- Choque con barrera/rampa fallida: +10 u
- Ollie completado: −2 u
- Kickflip completado: −6 u
- 360 Flip completado: −10 u

### 2.4 Timer

Partida de **2 min** (120 s). Al llegar a 0 → pantalla de resultados con
puntaje final + "has sobrevivido".

### 2.5 Modo Demo

Botón en el menú principal: **"MODO DEMO"**.

- Timer **pausado** (no corre).
- Perros **nunca alcanzan** (barra fija al mínimo).
- Teclas:
  - **N** → siguiente zona
  - **B** → zona anterior
- Texto `"DEMO"` fijo en la esquina superior derecha del HUD.
- Puntaje sí se acumula (para probar trucos y combos sin presión).

### 2.6 Audio

- 1 pista de música de fondo (~2 min, loopeable).
- SFX: ollie, aterrizaje, choque, truco limpio, ladrido.
- Convención: `Assets/Audio/Music/*.ogg`, `Assets/Audio/Sfx/*.wav`.

---

## 3. Build

- **Target:** Windows standalone x64 (.exe).
- **Resolución:** 1920×1080 nativa, ventana con opción pantalla completa.
- **Nombre del .exe:** `SkateInacap.exe`.
- **Carpeta de salida:** `Build/Windows/`.

---

## 4. Escenas (estado actual)

1. **`Menu.unity`** — placeholders listos. Botones:
   - INICIAR
   - SELECCION PERSONAJE
   - SALIR
   - *(pendiente añadir: MODO DEMO)*
2. **`SeleccionPersonaje.unity`** — placeholders listos. Botones Felipe,
   Daniel, Jugar, Volver. Label de selección.
3. **`Juego.unity`** — placeholders listos (quad gris + HUD con nombre y
   timer). Reemplazar cuando esté el arte: fondo, player con animaciones de
   pose, tabla, perros, obstáculos.

---

## 5. Cambios pendientes al código

Cuando se arranque el gameplay, estos ajustes son los primeros:

- [ ] **`GameData.cs`** — eliminar `ColorPersonaje`; en su lugar, exponer el
  **set de sprites** del personaje seleccionado (o `ScriptableObject` por
  personaje con sus 5 sprites).
- [ ] **`GameManager.cs`** — agregar sistema de puntaje con combo + lista
  de `ZonaData` (SO) + chequeo de umbrales + control de persecución (barra
  de distancia).
- [ ] **`MenuManager.cs`** — agregar botón "MODO DEMO" que setee un flag
  estático `GameData.ModoDemo = true` antes de cargar la escena.
- [ ] **`CharacterSelector.cs`** — mostrar preview del personaje en vez de
  solo un label.
- [ ] Nuevo **`ZonaData.cs`** — `ScriptableObject` con nombre, umbral, fondo,
  obstáculos.
- [ ] Nuevo **`PlayerController.cs`** — movimiento + trucos + animación de
  tabla (3 vistas + rotación).
- [ ] Nuevo **`DogChaser.cs`** — persecución, cercanía, game over.
- [ ] Nuevo **`ScoreManager.cs`** — puntos, combo, cambio de zona.

---

## 6. Flujo de trabajo para el arte

1. Daniel exporta cada asset de Canva como PNG, fondo transparente cuando
   corresponda.
2. Los PNG caen en `Arte_nuevo/` siguiendo las convenciones de § 1.
3. Cuando haya un set mínimo (1 personaje completo + 1 fondo + tabla + 1
   obstáculo + 1 perro), se mueven a `Assets/Art/` y se arma el player.
4. Para probar sin arte completo: cualquier pose faltante usa el placeholder
   gris actual.

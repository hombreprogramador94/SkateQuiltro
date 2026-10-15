# Skate Quiltro

**¡Que no te pillen!** — Endless runner 2D de skate ambientado en Santiago de Chile.

INACAP · Puente Alto · 2026

---

## El juego

Vas en skate por las calles de Santiago con una jauría de quiltros pisándote los
talones. Tienes **60 segundos** para aguantar sin que te alcancen, esquivando
obstáculos y sumando puntos con trucos. Si sobrevives, zafaste. Si te pillan, te
pillaron.

El escenario avanza por **9 zonas** repartidas en tres regiones, que se
desbloquean según el puntaje:

| Región | Zonas |
|---|---|
| **Puente Alto** | Pasaje · Feria · Plaza |
| **Ñuñoa** | Avenida · Estadio Nacional · Villa Olímpica |
| **Cordillera** | Cajón del Maipo · Embalse El Yeso · Farellones |

Cada zona trae sus propios obstáculos: carritos de completos y quiltros en Puente
Alto, ciclistas y mesas de café en Ñuñoa, huasos a caballo y cóndores en la
cordillera.

## Controles

| Tecla | Acción |
|---|---|
| **← →** | Moverse |
| **Espacio** | Ollie (salto) |
| **K** | Kickflip |
| **L** | 360 Flip |
| **Espacio** *en el aire* | Doble salto |
| **Esc** | Volver al menú |

### Trucos encadenados

Un truco completado en el aire habilita el siguiente. La cadena más larga son
**dos trucos por salto**:

- `L` → `Espacio` → `L` — 360 Flip, doble salto, 360 Flip
- `K` → `Espacio` → `L` — Kickflip, doble salto, 360 Flip

El segundo truco se impulsa solo lo justo para alcanzar a completarse: si no,
aterrizarías a medias y eso cuenta como caída.

## Puntaje

| Truco | Puntos |
|---|---|
| Ollie | 100 |
| Kickflip | 300 |
| 360 Flip | 500 |

Encadenar trucos con menos de **2 segundos** entre uno y otro sube el
multiplicador hasta **×5**. Caerse corta el combo, pero no resta puntos.

El récord se guarda en `PlayerPrefs` y se muestra en el menú y al terminar.

## Cómo abrir el proyecto

1. Descarga el repositorio: botón verde **Code → Download ZIP** y descomprímelo
   (o `git clone https://github.com/hombreprogramador94/SkateQuiltro.git`).
2. Instala **Unity 6000.6.5f1** desde Unity Hub (*Installs → Install Editor*).
   Con otra versión de Unity 6 también abre, pero puede pedir actualizar el proyecto.
3. En Unity Hub: **Add → Add project from disk** y elige la carpeta descomprimida
   (la que contiene `Assets/`, `Packages/` y `ProjectSettings/`).
4. La primera apertura tarda unos minutos: Unity regenera la carpeta `Library/`,
   que no va en el repositorio.
5. Abre la escena `Assets/Scenes/Menu.unity` y presiona **Play**.

El arte original (exportado desde Canva) está en `Arte_nuevo/`, fuera de
`Assets/` para que Unity no lo importe. Las versiones que usa el juego están en
`Assets/Art/`.

## Cómo ejecutarlo

El build compilado (`Builds/ULTIMA/SkateQuiltro.exe`, Windows 64 bits) no se
incluye en el repositorio. Se genera siguiendo los pasos de la sección
siguiente.

## Cómo compilar

Requiere **Unity 6000.6.5f1**.

Desde el editor, en el menú `Juego/`:

1. **Crear escenas base** — genera las tres escenas y enlaza todos los assets.
2. **Build Windows ULTIMA** — compila a `Builds/ULTIMA/`.

Por consola:

```bash
Unity.exe -batchmode -nographics -quit -projectPath . \
  -executeMethod SceneSetup.CreateAll

Unity.exe -batchmode -nographics -quit -projectPath . \
  -executeMethod BuildScript.BuildUltima
```

> Si editaste PNG fuera del editor, usa `BuildScript.ReimportarYBuild` en lugar
> de `BuildUltima`. En batchmode Unity no detecta cambios hechos por fuera y
> compila con la textura cacheada, dejando el arte viejo en el build.

### Herramientas del menú `Juego/`

- **Forzar reimport del arte** — reimporta todos los PNG de `Assets/Art`.
- **Verificar alpha de los perros** — diagnostica si un sprite se ve translúcido
  por el archivo o por caché de Unity.

## Estructura

```
Assets/
  Art/            Personajes, obstáculos, perros, efectos y UI
  Audio/          SFX/ y Music/
  Editor/         SceneSetup, SceneSetupUI, BuildScript
  Fonts/          Arial Narrow Bold
  Resources/      PersonajeData y ZonaData (ScriptableObjects)
  Scenes/         Menu, SeleccionPersonaje, Juego
  Scripts/        Lógica del juego
Builds/ULTIMA/    Ejecutable
```

Las escenas **se generan por código** desde `SceneSetup.CreateAll`. No se editan
a mano: cualquier cambio de layout, referencias o valores va en ese script y se
vuelve a ejecutar. Así el proyecto es reproducible desde cero y no depende del
estado guardado de un `.unity`.

## Detalles técnicos

**Colisiones por forma real.** Los obstáculos usan `PolygonCollider2D`
autogenerado desde la forma física del sprite, y el choque se resuelve con
`Physics2D.Distance().isOverlapped`. Con cajas AABB el jugador perdía al rozar
esquinas transparentes.

**Audio.** 17 efectos y 2 pistas, con variación de pitch de ±5% en los que se
repiten mucho para que no suenen mecánicos. La jauría se oye desde 30 unidades:
los gruñidos se aceleran de 4 s a 1.2 s y suben de volumen a medida que los
perros se acercan, y ladran cuando ya están encima. El sonido de las ruedas son
golpes discretos disparados **cada 1.35 unidades recorridas**, no un loop: así la
cadencia sale de la velocidad real.

**Dificultad progresiva.** La velocidad de la jauría y la frecuencia de
obstáculos escalan con el índice de zona.

## Créditos

Desarrollado por **Daniel Orellana** para INACAP.

Arte y audio generados con herramientas de IA y procesados para el proyecto.
Samples de perros adaptados de grabaciones de Audiopapkin.

Asistencia de desarrollo: Claude Code (Anthropic).

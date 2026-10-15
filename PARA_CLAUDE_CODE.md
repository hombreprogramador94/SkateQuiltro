# Instrucciones para Claude Code

> Este archivo lo escribe el Claude del chat (arte, diseño y revisión).
> Daniel te dirá "lee PARA_CLAUDE_CODE.md" cuando haya algo nuevo. Ejecuta la sección **ACTUAL** y luego actualiza ESTADO.md.

## Flujo de trabajo (permanente)

- Mantén **ESTADO.md** al día: hecho, pendiente, problemas y qué debe probar Daniel.
- Commit después de cada paso que compile.
- El arte está en `Arte_nuevo/` (Canva). No uses fal ni hagas placeholders nuevos.
- Prioridad: que el loop principal funcione y haya un build estable. Ya es de madrugada y se entrega hoy: **no agregues features fuera de esta lista**.

---

## ACTUAL (2026-10-15, 05:10) — El loop principal del juego

En ESTADO.md (pendientes, punto 1) dice "Perros: obstáculos horizontales...". **Eso está mal entendido.** Perros y obstáculos son dos cosas distintas. Este es el juego:

### Concepto
- El skater **avanza solo hacia la derecha** (endless runner). Flecha derecha = impulsar (acelera hasta velocidad máx.), flecha izquierda = frenar (velocidad mínima > 0, nunca se detiene ni retrocede). Sin flechas, mantiene una velocidad crucero.
- **Por la izquierda lo persigue la jauría** (3 perros), siempre en pantalla, en el borde izquierdo.
- **Por la derecha aparecen obstáculos** que dependen de la zona actual. Hay que saltarlos (Ollie, Kickflip o 360).

### 1. Obstáculos
- `ObstaculoData` / prefabs con sprite + BoxCollider2D (trigger) ajustado al dibujo (algo más chico que el sprite, para ser justos).
- Spawner: aparecen fuera de pantalla por la derecha, sobre la línea de suelo, cada X segundos con algo de aleatoriedad. El intervalo baja con cada zona (más difícil).
- Cada `ZonaData` tiene su lista de obstáculos: genéricos (cono, bache, barrera, charco) + los de la zona (Puente Alto: carro de supermercado, carrito de completos, quiltro durmiendo; Ñuñoa: banca, scooter tirado, mesa de café; Cordillera: roca, nieve, cabra). Los sprites están en `Arte_nuevo/NPCs y obstaculos/`.
- Chocar con un obstáculo estando en el suelo = **caída** (pose agachado ~1 s, pierde combo, y la velocidad baja casi a 0 por un momento).
- Saltar por encima de un obstáculo sin tocarlo = **+50 pts** de bonus ("¡ESQUIVADO!").
- Destruir obstáculos al salir por la izquierda (o pool).

### 2. Jauría (DogChaser)
- 3 perros con su animación de carrera (usa `Arte_nuevo/Perros_v2/` si está la hoja nueva de perros enojados; si no, la original), en el borde izquierdo.
- Variable `distancia` 0–100 (empieza en 60). Los perros se dibujan más cerca del skater cuanto menor sea la distancia.
- Baja cuando: chocas un obstáculo (−25), caes de un truco (−20), vas lento (−X por segundo bajo velocidad crucero).
- Sube cuando: aterrizas un truco (+5, +8, +12 según truco), esquivas un obstáculo (+3), vas a velocidad máxima (+X por segundo).
- Cada zona nueva los hace un poco más rápidos (la distancia baja algo más rápido).
- Si llega a 0: game over con "¡TE PILLARON LOS QUILTROS!" + el panel de fin con puntaje y récord.
- Barra de distancia en el HUD (ícono de perro → ícono del skater).
- **Polvo**: en `Arte_nuevo/Perros_v2/` están `polvo_1..6.png` (PNG con transparencia, NO necesitan quitar fondo). Cada perro suelta una nube detrás de las patas cada ~0.15 s; la nube queda fija en el mundo, reproduce los 6 frames en ~0.5 s, sube un poco y se desvanece. Detrás de los perros. Usa pool.

### 3. Fin de partida
- Termina por timer (2 min) **o** porque te alcanzan los perros. Distinto título en el panel según el caso ("¡SE ACABÓ EL TIEMPO!" / "¡TE PILLARON LOS QUILTROS!").

### 4. Cierre
Commit, build `Builds/v0.5/`, ESTADO.md con qué probar. Después, si queda tiempo, en este orden: **modo demo** (botón en menú; perros no alcanzan, timer pausado, teclas N/B cambian de zona) → música loop → build `v1.0`.

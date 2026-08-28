# Game Concept: Oasis

## 1. Overview

Hace 150 años el mundo ardió en una guerra nuclear — el Gran Fuego. La humanidad
sobrevivió escondida en un valle protegido llamado el Oasis, sellado por una
muralla de montañas conocida como el Járnviðr. Mientras afuera la radiación — el
Resplandor — lo devoraba todo, dentro la sociedad prosperó, se llenó, y ahora se
queda sin espacio.

La única esperanza está en cinco personas que pueden morir y volver: los
Renacidos. Creados por el Proyecto Jano de los Custodios — la orden que gobierna
el Oasis — estos cinco híbridos llevan en la sangre el linaje de los clanes
Adaptados que sobrevivieron en el exterior. Pueden cruzar El Paso, explorar las
Tierras Muertas, y revivir en la Cámara de Jano si mueren. Tú eres uno de ellos.

Pero el exterior no está vacío. Hay criaturas mutadas, ruinas del Mundo Antiguo,
y cinco clanes de humanos Adaptados que llevan 150 años esperando que alguien del
Oasis cruce la muralla. Y los Custodios — que causaron el Gran Fuego y lo
ocultaron — tienen sus propios planes para los Renacidos.

## 2. Player Fantasy

El jugador encarna a uno de los cinco Renacidos, cada uno con un linaje, una
bestia vinculada y habilidades únicas. La fantasía central es:

- **Ser el puente entre dos mundos**: El Oasis cerrado y protegido, y el exterior
  peligroso pero lleno de secretos. Solo los Renacidos pueden transitar entre ambos.
- **Explorar lo desconocido**: Ruinas del Mundo Antiguo, zonas radiactivas con
  criaturas únicas, asentamientos de clanes Adaptados que tienen su propia cultura
- **Morir y volver**: La muerte no es el final — es parte del ciclo. Pero cada
  muerte cuesta: equipo perdido, conocimiento olvidado, tiempo que el Oasis no tiene
- **Descubrir la verdad**: La historia que cuentan los Custodios es una mentira
  cuidadosamente construida. Cada expedición revela una pieza del rompecabezas
- **Forjar vínculos**: Con bestias mutadas del exterior, con personajes de ambos
  mundos, y con los otros cuatro Renacidos

## 3. Detailed Rules

### Reglas fundamentales del mundo

1. **El Resplandor**: La radiación persistente del exterior. Los Puros (habitantes
   del Oasis) no pueden sobrevivirla. Los Adaptados (clanes del exterior) la
   metabolizan. Los Renacidos son híbridos: pueden resistirla pero no son inmunes.

2. **Muerte y Resurrección**: Cuando un Renacido muere en el exterior, revive en
   la Cámara de Jano bajo El Corazón del Oasis. Pierde el equipo que llevaba y
   parte del conocimiento adquirido en esa expedición.

3. **Los Cinco Linajes**: Cada Renacido pertenece a un linaje de los clanes
   Adaptados, lo que define sus habilidades:
   - **Kael** (Los Errantes) — Rastreo, navegación, percepción del Resplandor
   - **Sira** (Los Sanadores) — Medicina, biología del Resplandor, soporte
   - **Davan** (Los Reconstructores) — Tecnología del Mundo Antiguo, ingeniería
   - **Vael** (Los Forjados) — Combate, caza de Devorados, resistencia física
   - **Ren** (Los Puentes) — Infiltración, sigilo, inteligencia

4. **Bestias del Vínculo**: Cada Renacido tiene una criatura afín que reconoce
   su firma biológica. No es domesticación — es compatibilidad radiactiva:
   - Kael → El Cristalino (leopardo con cristales de Resplandor)
   - Sira → El Ciervo Luminoso (campo protector pasivo)
   - Davan → El Lobo Ferroso (detector de tecnología antigua)
   - Vael → El Blindado (oso tanque de combate)
   - Ren → El Espectro (félido translúcido)

5. **Facciones**: Las decisiones del jugador afectan la relación con:
   - Los Custodios (Gobierno del Oasis, divididos en Guardianes y Reveladores)
   - Los Cimientos (facción civil leal al orden)
   - Los Umbrales (facción civil que cuestiona la historia oficial)
   - Los 5 clanes Adaptados del exterior (Pacto de las Cenizas + Sin Pacto)

### Ciclo de juego

1. **Preparación en el Oasis**: Recibir briefings de los Custodios, equiparse,
   hablar con personajes, descubrir pistas
2. **Expedición al exterior**: Cruzar El Paso, explorar zonas (La Cicatriz →
   Las Tierras Muertas → Las Torres Caídas → El Nido → El Mundo Roto)
3. **Exploración y combate**: Enfrentar Devorados, recolectar recursos y
   tecnología, encontrar clanes Adaptados
4. **Muerte o retorno**: Si mueres, revives en la Cámara con penalización.
   Si vuelves voluntariamente, entregas hallazgos y desbloqueas nuevas opciones
5. **Consecuencias narrativas**: Tus acciones y descubrimientos afectan las
   facciones, las relaciones y el destino del Oasis

## 4. Formulas

A alto nivel, las fórmulas principales del juego se definirán en sus GDDs
específicos. Las dependencias clave identificadas son:

- `supervivencia_resplandor = metabolismo_base * modificador_linaje - radiacion_zona - daño_acumulado`
- `penalizacion_muerte = equipo_perdido + (conocimiento_expedicion * factor_perdida)`
- `reputacion_faccion = Σ(decisiones * peso_decision) + historial_acciones`
- `progresion_linaje = experiencia_ganada * multiplicador_afinidad`

## 5. Edge Cases

- **Muerte en la Cámara de Jano**: Si un Renacido muere dentro del Oasis, ¿puede
  revivir? (Lore: la Cámara está diseñada para reconstruir desde el exterior)
- **Todos los Renacidos muertos simultáneamente**: ¿Qué pasa si los 5 mueren sin
  completar una expedición?
- **Traición del espía (Protocolo de Umbral)**: Uno de los 5 está condicionado.
  Si el jugador no lo descubre, el Acto 3 cambia radicalmente.
- **Elección de diáspora**: Si el jugador elige no volver al Oasis, ¿qué pasa
  con la Cámara de Jano y la capacidad de revivir?

## 6. Dependencies

El concepto de juego depende de los siguientes sistemas (a diseñar):

| Sistema | Tipo |
|---|---|
| Movimiento y Exploración | Foundation |
| Sistema de Muerte/Resurrección | Foundation |
| Metabolismo de Resplandor (Radiación) | Foundation |
| 5 Linajes (Clases/Habilidades) | Foundation |
| Bestias del Vínculo (Compañeros) | Core |
| Combate | Core |
| Inventario y Equipo | Core |
| Progresión de Personaje | Core |
| Facciones y Reputación | Feature |
| Narrativa Ramificada | Feature |
| Economía y Crafting | Feature |
| HUD e Interfaz | Presentation |
| Mapa y Navegación | Presentation |
| Diálogos | Feature |

## 7. Tuning Knobs

Valores configurables a nivel de diseño:

- Número de Renacidos: 5 (fijo por lore)
- Penalización por muerte: % de equipo perdido, % de conocimiento olvidado
- Intensidad de radiación por zona
- Número de clanes del exterior: 5
- Número de bestias del vínculo: 5 (una por linaje)
- Cantidad de finales: 4
- Relaciones románticas: 3

## 8. Acceptance Criteria

- [ ] El concepto define claramente el mundo, el conflicto central y la fantasía del jugador
- [ ] Los 3 pilares del juego están definidos y son verificables en cada sistema
- [ ] El core loop describe un ciclo completo de juego (Oasis → Exterior → Retorno)
- [ ] Los 5 linajes tienen identidad diferenciada con rol mecánico claro
- [ ] Las facciones principales (Oasis + Exterior) están identificadas con sus motivaciones
- [ ] El documento es coherente con el Lore Bible (Oasis-LoreBible.pdf)
- [ ] El concepto es viable en Unity 6000.4.7f1 con URP

---

**Referencia canónica:** `Assets/Oasis-LoreBible.pdf` — Documento maestro de narrativa y worldbuilding.
**Motor:** Unity 6000.4.7f1 (Universal Render Pipeline)
**Estado:** Borrador — primera extracción del Lore Bible

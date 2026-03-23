# Guia Senior - Roadmap Dofus 2.10 .NET

Fecha: 2026-03-23
Proyecto: Dofus 2.10 Private Server

## 1. Vision Del Proyecto

Construir un emulador Dofus 2.10 en .NET con arquitectura limpia, mantenible y escalable.
La meta no es solo "hacer que funcione", sino dejar una base profesional que permita crecer hacia:

- autenticacion estable
- seleccion de servidor
- lista y creacion de personajes
- entrada al mundo
- movimiento
- NPCs
- inventario
- hechizos
- combate

## 2. Estado Actual

Hoy el proyecto ya permite:

- levantar AuthServer y GameServer en .NET
- conectar el cliente privado local
- completar el login
- eliminar el bug del bootstrap que enviaba una cola falsa 1/1
- enviar ticket al GameServer
- responder handshake de juego
- devolver una CharactersList vacia

Esto significa que la fase de conexion base ya esta resuelta.

## 3. Principios De Diseno

Las reglas del proyecto seran estas:

1. La base de datos de runtime se diseña por nosotros.
2. El contenido del juego no se mete a mano si puede importarse desde el cliente.
3. Las capas deben estar separadas por responsabilidad.
4. El protocolo y la logica de negocio no deben mezclarse.
5. Cada fase debe cerrar un vertical slice jugable.
6. Nada de soluciones rapidas que despues bloqueen el crecimiento.

## 4. Arquitectura Objetivo

### 4.1 Capa Host

Responsable de:

- sockets
- auth protocol
- game protocol
- sessions
- tickets
- listeners
- logging de trafico

No debe contener logica de dominio compleja.

### 4.2 Capa Bll

Responsable de:

- casos de uso
- validaciones de negocio
- servicios de cuentas
- personajes
- servidores
- mundo

Debe ser la capa que orquesta el comportamiento del emulador.

### 4.3 Capa Data

Responsable de:

- EF Core
- AppDataContext
- AppDataContextHardcode
- repositories
- unit of work
- persistencia

Aqui viven tanto runtime data como import pipelines si necesitan acceso a almacenamiento.

### 4.4 Capa Helper

Responsable de:

- utilidades puras
- extensiones
- conversores
- helpers compartidos

## 5. Estrategia Senior Para La Base De Datos

La decision recomendada es esta:

- no usar una SQL vieja encontrada en internet como fuente principal
- si aparece una SQL antigua, usarla solo como referencia
- crear nuestra base de datos desde cero
- poblar contenido estatico mediante importadores desde el cliente

### 5.1 Por Que No Usar Una SQL Antigua Como Base Oficial

Riesgos:

- version inconsistente
- columnas sobrantes o faltantes
- datos corruptos
- ids que no coinciden con tu cliente
- contenido mezclado con custom de otro servidor
- dependencia tecnica de una estructura que no controlamos

### 5.2 Lo Correcto

Separar la informacion en dos bloques:

1. Runtime data
   Datos vivos del emulador.

2. Static data
   Datos del juego importados desde el cliente.

## 6. Modelo De Datos Recomendado

### 6.1 Runtime Data

Tablas iniciales:

- Accounts
- AccountSessions
- AuthTickets
- GameServers
- Characters
- CharacterStats
- CharacterPositions
- CharacterSpells
- CharacterInventory
- CharacterShortcuts

### 6.2 Static Data

Tablas iniciales:

- Breeds
- Maps
- MapCells
- SubAreas
- Worlds
- NpcTemplates
- MonsterTemplates
- ItemTemplates
- SpellTemplates

## 7. Estrategia De Contenido

No recomiendo crear razas, mapas y contenido grande a mano.

La ruta senior es:

1. extraer datos del cliente 2.10
2. crear importadores
3. cargar solo el vertical slice que necesitamos primero

Los targets de importacion inicial son:

- razas
- textos
- mapas base
- subareas
- metadatos minimos para entrada a juego

## 8. Vertical Slice Inicial

No vamos a intentar levantar todo Dofus de una vez.

El primer slice jugable debe ser:

- 1 servidor visible y funcional
- razas cargadas
- creacion de personaje
- lista de personajes persistida
- spawn en Incarnam
- 1 mapa funcional de inicio
- entrada al mundo
- movimiento basico

## 9. Roadmap Por Fases

### Fase 1 - Conexion Base

Objetivo:

- login
- seleccion de servidor
- ticket auth to game

Estado:

- completada

### Fase 2 - Personajes

Objetivo:

- CharactersList real desde DB
- CharacterCreationRequest
- CharacterSelectionRequest
- persistencia de raza, sexo, colores y nombre

Salida esperada:

- crear un personaje y verlo en la lista

### Fase 3 - Entrada Al Mundo

Objetivo:

- spawn inicial
- carga de mapa
- envio del contexto minimo
- entrada del personaje al mundo

Salida esperada:

- entrar con un personaje a Incarnam

### Fase 4 - Movimiento

Objetivo:

- movement request
- validacion de celdas
- actualizacion de posicion
- broadcast de movimiento

Salida esperada:

- mover el personaje en mapa

### Fase 5 - Contenido Base

Objetivo:

- NPCs base
- dialogos simples
- inventario inicial
- hechizos base

### Fase 6 - Combate

Objetivo:

- modelo de combate
- turnos
- lanzamiento de hechizos
- daños

## 10. Sprint Inmediato Recomendado

El siguiente sprint debe hacer solo esto:

1. crear entidad GameServer persistida
2. crear entidades Character, CharacterPosition y CharacterStats
3. crear catalogo Breeds
4. implementar CharactersList real
5. implementar creacion de personaje
6. dejar un spawn fijo en Incarnam

## 11. Definition Of Done Del Siguiente Sprint

El sprint se considera terminado cuando:

- el cliente lista personajes desde DB
- se puede crear personaje sin datos hardcodeados
- el personaje queda persistido
- al seleccionar personaje se asigna mapa inicial
- el servidor no desconecta durante el paso auth to game

## 12. Reglas De Trabajo Del Emulador

1. Cada feature nueva debe cerrar un flujo real del cliente.
2. Cada mensaje nuevo del protocolo debe dejar transcript entendible.
3. Toda persistencia nueva debe tener entidad, configuracion y migracion.
4. No meter contenido estatico grande a mano si existe import posible.
5. Mantener un backlog por vertical slices, no por ideas sueltas.

## 13. Decision Final

La base senior para este proyecto es:

- DB propia desde cero para runtime
- importadores para static data
- primer slice enfocado en personajes + Incarnam

Esa es la ruta mas estable, mantenible y profesional para seguir este emulador.

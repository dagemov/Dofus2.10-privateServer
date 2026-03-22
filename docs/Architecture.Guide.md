# Guia senior para montar el servidor Dofus 2.10 en .NET

## Objetivo

Montar un backend limpio, mantenible y escalable para un servidor privado de Dofus 2.10, con separacion clara entre conexiones, logica de negocio y persistencia.

## Decision principal

La base inicial queda en `.NET 8` por estabilidad LTS. Para un servidor de juego nos interesa mas una plataforma predecible que una version de .NET mas nueva.

## Arquitectura inicial

### 1. Host

Responsabilidad:
- levantar el proceso principal
- cargar configuracion
- abrir y administrar conexiones
- orquestar auth server y game server mas adelante
- observabilidad, logs y lifecycle del servidor

Regla:
- `Host` no contiene reglas de negocio ni SQL directo

### 2. BLL

Responsabilidad:
- casos de uso de negocio
- flujo de autenticacion
- seleccion de personaje
- inventario
- mapa
- combate
- validaciones funcionales

Regla:
- la `BLL` habla con `Data` mediante repositorios y `UnitOfWork`
- aqui van los servicios de aplicacion, no sockets ni detalles de EF

### 3. Data

Responsabilidad:
- `AppDataContext`
- `AppDataContextHardcode`
- entidades persistidas
- configuraciones EF
- repositorio generico
- `UnitOfWork`
- migraciones

Regla:
- `Data` es la unica capa que conoce EF Core y SQL Server

### 4. Helper

Responsabilidad:
- utilidades transversales
- constantes compartidas
- guards
- tipos pequeños reutilizables

Regla:
- `Helper` no debe depender de `Host`

## Enfoque senior para Dofus 2.10

### Fase 1. Fundacion

- cerrar arquitectura y contrato entre capas
- definir autenticacion y conexion del cliente
- modelar cuentas, personajes y sesiones
- preparar migraciones y seed minimo

### Fase 2. Auth server

- handshake
- login
- seleccion de servidor
- sesion temporal entre auth y game

### Fase 3. Game server

- entrada al mundo
- lista de personajes
- carga de mapa
- movimiento
- chat

### Fase 4. Sistemas de juego

- stats
- inventario
- hechizos
- NPC
- quests
- combate

### Fase 5. Operacion

- logs estructurados
- metricas
- herramientas GM
- importadores de data
- backups

## Nota especifica de Dofus 2.10

Dofus 2.10 pertenece a la linea clasica basada en cliente legacy. Eso implica:
- protocolo fijo y muy sensible a compatibilidad
- fuerte dependencia entre mensajes, mapas y data del cliente
- necesidad de separar claramente el framing/red del dominio

Por eso la recomendacion senior es esta:
- `Host` maneja sockets, sesiones, parseo bruto y despacho
- `BLL` decide reglas
- `Data` solo persiste estado

## Decision operativa para este repo

La capa principal que corre el proyecto sera `Dofus210.Host`.

## Siguiente evolucion recomendada

Cuando tengamos autenticacion estable, conviene dividir `Host` en dos procesos:
- `Dofus210.AuthHost`
- `Dofus210.GameHost`

Hoy arrancamos con un solo `Host` para reducir friccion y acelerar la base.


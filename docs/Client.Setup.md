# Cliente Dofus 2.10: estado, compatibilidad y configuracion

## Estado real en 2026

No he encontrado una descarga oficial actual y verificable de un cliente standalone de Dofus 2.10.

Lo que si pude verificar:
- Ankama indica que el soporte para las versiones `.zip` de DOFUS termino el 26/10/2021.
- Ankama indica que DOFUS 2.0 dio paso a DOFUS 3.0 el 03/12/2024.
- Existen servidores privados actuales que siguen distribuyendo clientes 2.10, pero son no oficiales y no estan aprobados por Ankama.

## Implicacion tecnica importante

Para un emulador 2.10 no basta con tener "cualquier" cliente antiguo.

Segun documentacion tecnica y experiencia de la comunidad:
- el cliente suele usar `App/Config.xml` o `config.xml` en la raiz
- ahi se modifican `connection.host`, `connection.port` y `lang.current`
- pero un cliente vanilla puede rechazar hosts no oficiales por validacion de firma en `connection.host.signature`

Eso significa que necesitamos uno de estos dos escenarios:
- un cliente 2.10 ya parcheado para emulador
- o un cliente 2.10 que podamos parchear mas adelante

## Estructura esperada del cliente

La estructura minima que espero encontrar es algo asi:

```text
Cliente2.10/
  Dofus.exe
  config.xml
  App/
    Config.xml
  data/
  ui/
```

## Script de configuracion incluido

He dejado un script para parchear el cliente:

```powershell
.\tools\Client\Set-Dofus210ClientConfig.ps1 -ClientRoot "C:\Ruta\Cliente2.10" -ServerHost "127.0.0.1" -AuthPorts "5555" -Language "es"
```

Que hace:
- valida que exista `App\Config.xml` o `config.xml`
- crea backup `.bak`
- actualiza `connection.host`
- actualiza `connection.port`
- actualiza `lang.current`
- avisa si detecta `connection.host.signature`

## Recomendacion senior

No recomiendo ejecutar a ciegas un cliente o launcher de terceros descargado de una web privada sin revisarlo primero.

Ruta recomendada:
1. conseguir un cliente 2.10 compatible o ya parcheado
2. copiarlo a una ruta controlada por nosotros
3. ejecutar el script de configuracion
4. probar handshake contra nuestro `AuthPort`

## Hallazgos utiles para nuestro backend

- El puerto clasico de entrada de Dofus 2 ronda `5555`.
- El `GamePort` no tiene por que ir en `Config.xml`; normalmente el cliente entra por auth y luego recibe el destino del world server.
- Por eso nuestro objetivo inmediato en .NET debe ser tener primero el `AuthServer` estable.

## Fuentes consultadas

- Ankama Support: Install the Ankama Launcher
- Ankama Support: [DOFUS] DOFUS 3.0
- RaGEZONE: [Release] Dofus 2
- J'ai changé: Dofus et le reverse-engineering (2/2)
- RetroFuz 2.10

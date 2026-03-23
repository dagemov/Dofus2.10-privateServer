# Herramientas cliente 2.10

Aqui van los scripts para adaptar un cliente Dofus 2.10 al backend del proyecto.

## Script principal

```powershell
.\tools\Client\Set-Dofus210ClientConfig.ps1 -ClientRoot "C:\Ruta\Cliente2.10"
```

## Script catalogo servidor

```powershell
.\tools\Client\Set-Dofus210ServerCatalog.ps1 -ClientRoot "C:\Ruta\Cliente2.10"
```

## Ejemplos utiles

Normalizar la ficha visual del servidor principal `Rushu` sin mover personajes:

```powershell
.\tools\Client\Set-Dofus210ServerCatalog.ps1 `
    -ClientRoot "C:\Ruta\Cliente2.10" `
    -ServerId 1 `
    -Language es `
    -PopulationId 1 `
    -GameTypeId 1 `
    -CommunityId 4 `
    -RestrictedToLanguagesCount 0 `
    -MonoAccount 1
```

Renombrar la entrada `4001` para que el selector local muestre `Henual`:

```powershell
.\tools\Client\Set-Dofus210ServerCatalog.ps1 `
    -ClientRoot "C:\Ruta\Cliente2.10" `
    -ServerId 4001 `
    -NameTranslationId 8693 `
    -CommentTranslationId 8693
```

Quitar una restriccion de idioma del catalogo local sin reescribir todo el `D2O`:

```powershell
.\tools\Client\Set-Dofus210ServerCatalog.ps1 `
    -ClientRoot "C:\Ruta\Cliente2.10" `
    -ServerId 4001 `
    -RestrictedToLanguagesCount 0 `
    -MonoAccount 0
```

Resetear la cache AIR/Berilia del cliente antes de una nueva prueba:

```powershell
.\tools\Client\Reset-Dofus210ClientState.ps1
```

## Nota

El script detecta `App\Config.xml` y `config.xml` en la raiz.

El script asume que el cliente ya es compatible con emulador o ya esta parcheado para aceptar hosts no oficiales.

El parcheador modifica `Servers.d2o` in-place y crea `Servers.d2o.bak` la primera vez.

No reescribe el layout binario del D2O. Por eso solo permite cambios seguros sobre campos del mismo tamano.

El reset de estado guarda backups en `runtime\client-state-backups` antes de borrar la cache local de `AppData\Roaming\Ankaline210`.

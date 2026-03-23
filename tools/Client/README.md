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

## Nota

El script detecta `App\Config.xml` y `config.xml` en la raiz.

El script asume que el cliente ya es compatible con emulador o ya esta parcheado para aceptar hosts no oficiales.

El parcheador modifica `Servers.d2o` in-place y crea `Servers.d2o.bak` la primera vez.

No reescribe el layout binario del D2O. Por eso solo permite cambios seguros sobre campos del mismo tamano.

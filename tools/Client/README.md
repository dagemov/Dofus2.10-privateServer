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

## Nota

El script detecta `App\Config.xml` y `config.xml` en la raiz.

El script asume que el cliente ya es compatible con emulador o ya esta parcheado para aceptar hosts no oficiales.

El parcheador del catalogo renombra la entrada espanola `4001` para que el selector local del cliente muestre `Henual`.

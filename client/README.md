# Cliente Dofus 2.10

Cuando tengamos un cliente compatible, puedes colocarlo aqui o en otra ruta de tu preferencia.

Ruta recomendada para trabajar junto al backend sin versionar binarios:

```text
C:\Users\Hombr\source\repos\Dofus2.10\client\Ankaline210
```

Luego aplica la configuracion con:

```powershell
.\tools\Client\Set-Dofus210ClientConfig.ps1 -ClientRoot "C:\Users\Hombr\source\repos\Dofus2.10\client\Ankaline210" -ServerHost "127.0.0.1" -AuthPorts "5555" -Language "es" -AlsoPatchRegConfig
```

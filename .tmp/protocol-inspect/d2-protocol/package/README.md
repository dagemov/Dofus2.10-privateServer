# D2.Protocol

## Description

D2.Protocol est une librairie Javascript offrant des outils de lecture et écriture de paquet pour le jeu Dofus 2.x.

Le protocole est construit par la librairie [D2.ProtocolBuilder](https://github.com/RBlanchet/D2.ProtocolBuilder) elle même basée sur l'excellente librairie [dofus-protocol-builder](https://github.com/Vicfou-dev/dofus-protocol-builder), un grand merci à lui.

## Installation

La version du paquet dépend de la version de Dofus, par exemple :

* Dofus est en version 2.61.7.8, le paquet sera alors en v2.61.7 (@rblanchet/d2-protocol@2.61.7)

```shell
# Dernière version
npm install @rblanchet/d2-protocol

# Une version en particulier
npm install @rblanchet/d2-protocol@2.61.7
```

## Utilisation

### Lire un Buffer provenant de Dofus

```javascript
const {Reader} = require('@rblanchet/d2-protocol');

/**
 * Provient de Dofus, peut-être fourni par un Socket ou un MITM.
 * 
 * @param {Buffer}
 */
buffer;

const message = Reader.readBuffer(buffer);
console.log(message);
```

### Récupérer la version du Protocol

```javascript
const {VERSION} = require('@rblanchet/d2-protocol');

console.log(VERSION); // output: 2.61.7.8
```

## Crédits

[Vicfou-dev](https://github.com/Vicfou-dev)

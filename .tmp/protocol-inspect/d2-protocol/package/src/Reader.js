const Protocol = require('../lib/protocol');

module.exports = {
    /**
     * Transforme un Buffer venant de Dofus 2.x en Objet compréhensible pour une application JS.
     * 
     * @param {Buffer} buffer
     *
     * @returns {Object}
     */
    readBuffer: (buffer) => {
        wrapper = new Protocol.CustomDataWrapper(buffer);
        
        return Protocol.MessageReceiver.parse(wrapper);
    },
};
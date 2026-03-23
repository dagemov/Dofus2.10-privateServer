const fs = require('fs');
const path = require('path');

const Reader = require('./src/Reader');
const Protocol = require('./lib/protocol');
const VERSION = fs.readFileSync(`${path.resolve(__dirname)}/VERSION`, 'utf-8');

module.exports = {
    Reader,
    Protocol,
    VERSION,
};
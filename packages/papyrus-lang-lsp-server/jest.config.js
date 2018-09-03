const path = require('path');
const { createConfig } = require('../../build/jest');

module.exports = createConfig(path.join(__dirname, 'tsconfig.json'));

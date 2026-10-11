const { getJestConfig } = require('@storybook/test-runner');
module.exports = {
  ...getJestConfig(),
  rootDir: __dirname,
  roots: ['<rootDir>/src'],
};

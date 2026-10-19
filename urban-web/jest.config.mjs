import presets from 'jest-preset-angular/presets/index.js';

const { createEsmPreset } = presets;

export default {
  ...createEsmPreset({ tsconfig: '<rootDir>/tsconfig.spec.json' }),
  setupFilesAfterEnv: ['<rootDir>/setup-jest.ts'],
  roots: ['<rootDir>/src'],
  collectCoverageFrom: ['src/app/**/*.ts', '!src/app/**/*.spec.ts', '!src/app/**/*.routes.ts'],
  coverageDirectory: 'coverage',
  coverageReporters: ['text-summary', 'cobertura', 'lcov'],
};

import presets from 'jest-preset-angular/presets/index.js';

const { createEsmPreset } = presets;

export default {
  ...createEsmPreset({ tsconfig: '<rootDir>/tsconfig.spec.json' }),
  setupFilesAfterEnv: ['<rootDir>/setup-jest.ts'],
  roots: ['<rootDir>/src'],
  // Com cobertura e muitos processos em paralelo, alguns testes (como os que percorrem 60 meses) passam
  // de 5 s num computador mais ocupado, embora passem sozinhos. 20 s evita a falha sem esconder travamentos.
  testTimeout: 20000,
  collectCoverageFrom: ['src/app/**/*.ts', '!src/app/**/*.spec.ts', '!src/app/**/*.routes.ts'],
  coverageDirectory: 'coverage',
  coverageReporters: ['text-summary', 'cobertura', 'lcov'],
};

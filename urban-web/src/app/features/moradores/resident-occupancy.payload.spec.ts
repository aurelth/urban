import { toEndPayload, toOccupancyPayload } from './resident-occupancy.payload';

describe('toOccupancyPayload', () => {
  it('manda a unidade, o tipo e a entrada em AAAA-MM-DD', () => {
    expect(
      toOccupancyPayload({
        unitId: '0199aaaa-0000-7000-8000-000000000001',
        kind: 'tenant',
        startDate: '01/03/2024',
      }),
    ).toEqual({
      unitId: '0199aaaa-0000-7000-8000-000000000001',
      kind: 'tenant',
      startDate: '2024-03-01',
    });
  });
});

describe('toEndPayload', () => {
  it('manda a data de saída em AAAA-MM-DD', () => {
    expect(toEndPayload('15/11/2026')).toEqual({ endDate: '2026-11-15' });
  });
});

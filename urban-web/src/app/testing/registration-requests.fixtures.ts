import { RegistrationRequest } from '../core/admin/registration-requests.models';

export const REGISTRATION_REQUESTS_URL = '/api/admin/registration-requests';

export function registrationRequest(
  overrides: Partial<RegistrationRequest> = {},
): RegistrationRequest {
  return {
    id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c4d01',
    email: 'marina@example.com',
    name: 'Marina',
    condominiumName: 'Residencial Aurora',
    requestedAt: '2026-11-13T04:23:00+00:00',
    ...overrides,
  };
}

export const SECOND_REQUEST: RegistrationRequest = registrationRequest({
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c4d02',
  email: 'carlos@example.com',
  name: 'Carlos',
  condominiumName: 'Edifício Bela Vista',
  requestedAt: '2026-11-13T15:05:00+00:00',
});

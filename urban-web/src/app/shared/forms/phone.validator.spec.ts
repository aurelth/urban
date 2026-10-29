import { FormControl } from '@angular/forms';
import { brazilianMobile, brazilianPhone } from './phone.validator';

function check(value: string) {
  return brazilianPhone(new FormControl(value, { nonNullable: true }));
}

describe('brazilianPhone', () => {
  it.each([
    '(47) 99123-4567',
    '47991234567',
    '(47) 3123-4567',
    '4731234567',
    '(11) 2123-4567',
    '(11) 5123-4567',
    '+55 47 99123-4567',
    '+55 (47) 3123-4567',
    '5547991234567',
    '554731234567',
    '(55) 99123-4567',
  ])('aceita "%s"', (value) => {
    expect(check(value)).toBeNull();
  });

  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(check('')).toBeNull();
  });

  it.each([
    '   ',
    '(10) 99123-4567',
    '(20) 99123-4567',
    '(00) 3123-4567',
    '(47) 89123-4567',
    '(47) 9123-4567',
    '(47) 1123-4567',
    '(47) 6123-4567',
    '(47) 99123-456',
    '(47) 99123-45678',
    '(47) 3123-456',
    '123',
    '(47) 9912a-4567',
    '+55 47 99123-45',
  ])('recusa "%s"', (value) => {
    expect(check(value)).toEqual({ phone: true });
  });

  it('só aceita DDD que existe', () => {
    expect(check('(23) 99123-4567')).toEqual({ phone: true });
    expect(check('(29) 99123-4567')).toEqual({ phone: true });
    expect(check('(36) 99123-4567')).toEqual({ phone: true });
    expect(check('(52) 99123-4567')).toEqual({ phone: true });
    expect(check('(72) 99123-4567')).toEqual({ phone: true });
    expect(check('(76) 99123-4567')).toEqual({ phone: true });
    expect(check('(78) 99123-4567')).toEqual({ phone: true });
    expect(check('(99) 99123-4567')).toBeNull();
    expect(check('(11) 99123-4567')).toBeNull();
  });
});

describe('brazilianMobile', () => {
  const mobile = (value: string) => brazilianMobile(new FormControl(value, { nonNullable: true }));

  it.each([
    '(47) 99123-4567',
    '47991234567',
    '+55 47 99123-4567',
    '5547991234567',
    '(55) 99123-4567',
  ])('aceita "%s"', (value) => {
    expect(mobile(value)).toBeNull();
  });

  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(mobile('')).toBeNull();
  });

  it.each([
    '   ',
    '(47) 3123-4567',
    '4731234567',
    '(47) 89123-4567',
    '(10) 99123-4567',
    '(47) 99123-456',
    '(47) 9912a-4567',
  ])('recusa "%s"', (value) => {
    expect(mobile(value)).toEqual({ mobile: true });
  });
});

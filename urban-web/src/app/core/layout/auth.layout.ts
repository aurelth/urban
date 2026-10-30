import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BrandLink } from '../../shared/ui/brand-link/brand.link';

/** Moldura das telas de entrada e cadastro: um cartão centralizado, sem a barra da área logada. */
@Component({
  selector: 'urban-auth-layout',
  imports: [RouterOutlet, BrandLink],
  templateUrl: './auth.layout.html',
})
export class AuthLayout {}

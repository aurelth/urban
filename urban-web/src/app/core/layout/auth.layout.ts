import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** Moldura das telas de entrada e cadastro: um cartão centralizado, sem a barra da área logada. */
@Component({
  selector: 'urban-auth-layout',
  imports: [RouterOutlet],
  templateUrl: './auth.layout.html',
})
export class AuthLayout {}

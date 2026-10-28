import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { RegistrationApi } from '../../../core/auth/registration.api';
import { LinkRequestCard } from '../link-request/link-request.card';

@Component({
  selector: 'urban-register-page',
  imports: [RouterLink, LinkRequestCard],
  templateUrl: './register.page.html',
})
export class RegisterPage {
  private readonly api = inject(RegistrationApi);

  protected readonly register = (email: string): Observable<void> => this.api.register(email);
}

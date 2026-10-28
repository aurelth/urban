import { Component, inject } from '@angular/core';
import { MatAnchor } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { PasswordResetApi } from '../../../core/auth/password-reset.api';
import { LinkRequestCard } from '../link-request/link-request.card';

@Component({
  selector: 'urban-forgot-password-page',
  imports: [MatAnchor, RouterLink, LinkRequestCard],
  templateUrl: './forgot-password.page.html',
})
export class ForgotPasswordPage {
  private readonly api = inject(PasswordResetApi);

  protected readonly requestLink = (email: string): Observable<void> => this.api.requestLink(email);
}

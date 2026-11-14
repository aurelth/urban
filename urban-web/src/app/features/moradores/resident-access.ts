import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { toApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { ResidentsApi } from '../../core/residents/residents.api';
import { Resident, ResidentAccessStatus } from '../../core/residents/residents.models';
import { ACCESS_MESSAGES } from './resident-access.messages';

interface AccessView {
  readonly label: string;
  readonly icon: string;
  /** Cor do ícone: laranja é "em andamento" (convite à espera da senha), verde é sucesso, cinza é neutro. */
  readonly tone: string;
}

const VIEWS: Readonly<Record<ResidentAccessStatus, AccessView>> = {
  none: { label: 'Sem acesso', icon: 'lock', tone: 'text-on-surface-variant' },
  invited: { label: 'Convite enviado', icon: 'mark_email_unread', tone: 'text-warning' },
  active: { label: 'Acesso ativo', icon: 'check_circle', tone: 'text-success' },
  inactive: { label: 'Acesso desativado', icon: 'pause_circle', tone: 'text-on-surface-variant' },
};

/**
 * A situação do acesso do morador ao sistema e a ação que cabe a ela: "Convidar" (sem acesso e com e-mail) ou "Reenviar convite"
 * (convite enviado). Serve à lista e à edição, então chama a API sozinho e só avisa o pai (`invited`) quando o convite saiu:
 * a lista troca a linha e a edição lê o morador de novo (o convite grava a pessoa, então a versão muda). `blockedReason` desliga
 * o botão com um motivo (a edição tem alterações por salvar).
 */
@Component({
  selector: 'urban-resident-access',
  imports: [MatButton, MatIcon],
  templateUrl: './resident-access.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResidentAccess {
  readonly resident = input.required<Resident>();
  readonly blockedReason = input<string | null>(null);
  readonly invited = output<void>();

  private readonly api = inject(ResidentsApi);

  protected readonly busy = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly view = computed(() => VIEWS[this.resident().accessStatus]);
  protected readonly hasEmail = computed(() => this.resident().email !== null);
  /** Quem já criou a senha não recebe convite; sem e-mail, não há para onde mandar. */
  protected readonly action = computed<'invite' | 'resend' | null>(() => {
    const { accessStatus, email } = this.resident();
    if (accessStatus === 'invited') {
      return 'resend';
    }
    return accessStatus === 'none' && email !== null ? 'invite' : null;
  });

  protected async send(): Promise<void> {
    const target = this.resident();
    if (this.busy() || this.blockedReason() !== null || target.email === null) {
      return;
    }
    const resend = target.accessStatus === 'invited';
    this.busy.set(true);
    this.notice.set(null);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.invite(target.id));
      this.notice.set(`${resend ? 'Convite reenviado' : 'Convite enviado'} para ${target.email}`);
      this.invited.emit();
    } catch (error) {
      this.error.set(describeProblem(toApiProblem(error), ACCESS_MESSAGES));
    } finally {
      this.busy.set(false);
    }
  }
}

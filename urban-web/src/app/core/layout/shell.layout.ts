import { Component, inject } from '@angular/core';
import { MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatToolbar } from '@angular/material/toolbar';
import { RouterOutlet } from '@angular/router';
import { ThemeStore } from '../services/theme.store';

@Component({
  selector: 'urban-shell',
  imports: [RouterOutlet, MatToolbar, MatIconButton, MatIcon],
  templateUrl: './shell.layout.html',
})
export class ShellLayout {
  protected readonly theme = inject(ThemeStore);
}

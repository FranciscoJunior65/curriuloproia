import { Injectable } from '@angular/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { Observable, of } from 'rxjs';
import { finalize, map, switchMap, take } from 'rxjs/operators';
import {
  CpfRequiredModalComponent,
  CpfRequiredModalData
} from '../components/cpf-required-modal/cpf-required-modal.component';
import { AuthService, User } from './auth.service';
import { getCpfDigits, isValidCpf } from '../utils/cpf.utils';

@Injectable({
  providedIn: 'root'
})
export class CpfEnforcementService {
  private dialogRef: MatDialogRef<CpfRequiredModalComponent> | null = null;

  constructor(
    private authService: AuthService,
    private dialog: MatDialog
  ) {}

  hasValidCpf(user?: User | null): boolean {
    const cpf = getCpfDigits(user?.cpf ?? this.authService.getCurrentUser()?.cpf ?? '');
    return cpf.length === 11 && isValidCpf(cpf);
  }

  refreshUserFromServer(): Observable<User | null> {
    return this.authService.verifyToken().pipe(
      take(1),
      map((response) => {
        if (response?.success && response.user) {
          this.authService.setUser(response.user);
          return response.user as User;
        }
        return this.authService.getCurrentUser();
      })
    );
  }

  ensureCpf(options: CpfRequiredModalData = {}): Observable<boolean> {
    if (this.hasValidCpf()) {
      return of(true);
    }

    if (this.dialogRef) {
      return this.dialogRef.afterClosed().pipe(
        take(1),
        switchMap((saved) => (saved ? this.refreshUserFromServer().pipe(map(() => this.hasValidCpf())) : of(false)))
      );
    }

    const mandatory = options.mandatory !== false;

    this.dialogRef = this.dialog.open(CpfRequiredModalComponent, {
      width: '100%',
      maxWidth: '440px',
      disableClose: mandatory,
      panelClass: 'cpf-required-modal-panel',
      data: {
        mandatory,
        context: options.context ?? 'login'
      } satisfies CpfRequiredModalData
    });

    return this.dialogRef.afterClosed().pipe(
      take(1),
      switchMap((saved) => {
        if (!saved) {
          return of(false);
        }
        return this.refreshUserFromServer().pipe(map(() => this.hasValidCpf()));
      }),
      finalize(() => {
        this.dialogRef = null;
      })
    );
  }
}

import { ChangeDetectionStrategy, Component, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { ConfirmationService, MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { DialogModule } from "primeng/dialog";
import { SelectModule } from "primeng/select";
import { TableModule } from "primeng/table";
import { TabsModule } from "primeng/tabs";
import { ToastModule } from "primeng/toast";
import { LanguageService } from "../src/app/core/i18n/language.service";

@Component({
  selector: "kalm-primeng-accessibility-fixture",
  standalone: true,
  imports: [
    FormsModule,
    ButtonModule,
    ConfirmDialogModule,
    DialogModule,
    SelectModule,
    TableModule,
    TabsModule,
    ToastModule
  ],
  providers: [ConfirmationService, MessageService],
  template: `
    <section class="fixture" aria-labelledby="fixture-title" data-testid="primeng-accessibility-fixture">
      <h2 id="fixture-title">PrimeNG accessibility fixture</h2>

      <div class="field">
        <label id="fixture-state-label" for="fixture-state">Fixture state</label>
        <p-select
          inputId="fixture-state"
          ariaLabelledBy="fixture-state-label"
          [options]="options"
          [(ngModel)]="selectedOption"
          optionLabel="label"
          optionValue="value" />
      </div>

      <p-tabs value="overview">
        <p-tablist [pt]="tabListPassThrough">
          <p-tab value="overview">Overview</p-tab>
          <p-tab value="details">Details</p-tab>
        </p-tablist>
        <p-tabpanels>
          <p-tabpanel value="overview">Overview panel</p-tabpanel>
          <p-tabpanel value="details">Details panel</p-tabpanel>
        </p-tabpanels>
      </p-tabs>

      <p-table [value]="rows" [pt]="tablePassThrough">
        <ng-template #header>
          <tr><th scope="col">Toolkit</th><th scope="col">Action</th></tr>
        </ng-template>
        <ng-template #body let-row>
          <tr>
            <td>{{ row.component }}</td>
            <td><p-button label="Inspect row" severity="secondary" /></td>
          </tr>
        </ng-template>
      </p-table>

      <div class="actions" aria-label="Overlay checks">
        <p-button label="Show toast" (onClick)="showToast()" />
        <button #dialogTrigger pButton type="button" (click)="dialogVisible.set(true)">Open dialog</button>
        <button pButton type="button" (click)="confirm($event)">Open confirmation</button>
      </div>

      <p-dialog
        header="Overlay verification"
        [modal]="true"
        [focusOnShow]="true"
        [focusTrap]="true"
        [closeOnEscape]="true"
        closeAriaLabel="Close overlay verification"
        [rtl]="direction() === 'rtl'"
        (onHide)="restoreFocus(dialogTrigger)"
        [(visible)]="dialogVisible">
        <p>{{ direction() === 'rtl' ? 'التحقق من النافذة' : 'Dialog keyboard verification' }}</p>
        <p-button label="Dialog action" [autofocus]="true" />
      </p-dialog>

      <p-toast [life]="10000" />
      <p-confirmdialog
        [defaultFocus]="'reject'"
        closeAriaLabel="Close confirmation"
        acceptAriaLabel="Accept confirmation"
        rejectAriaLabel="Reject confirmation" />
    </section>
  `,
  styles: [`
    .fixture { display: grid; gap: 1.5rem; margin-top: 2rem; }
    .field { display: grid; gap: 0.5rem; max-width: 20rem; }
    .actions { display: flex; flex-wrap: wrap; gap: 0.75rem; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PrimeNgAccessibilityFixtureComponent {
  private readonly confirmations = inject(ConfirmationService);
  private readonly messages = inject(MessageService);
  private readonly language = inject(LanguageService);

  protected readonly direction = this.language.direction;
  protected readonly dialogVisible = signal(false);
  protected selectedOption = "ready";
  protected readonly options = [
    { label: "Ready", value: "ready" },
    { label: "Pending", value: "pending" }
  ];
  protected readonly rows = [{ component: "PrimeNG" }];
  protected readonly tabListPassThrough = {
    content: { "aria-label": "Fixture sections" }
  };
  protected readonly tablePassThrough = {
    table: { "aria-label": "PrimeNG component status" }
  };

  protected showToast(): void {
    this.messages.add({ severity: "info", summary: "Accessibility", detail: "Toast announcement" });
  }

  protected confirm(event: Event): void {
    const trigger = event.currentTarget as HTMLElement;
    const restoreTriggerFocus = (): void => queueMicrotask(() => trigger.focus());

    this.confirmations.confirm({
      target: event.currentTarget as EventTarget,
      header: "Confirm accessibility action",
      message: "Choose accept or reject using the keyboard.",
      acceptLabel: "Accept",
      rejectLabel: "Reject",
      accept: restoreTriggerFocus,
      reject: restoreTriggerFocus
    });
  }

  protected restoreFocus(trigger: HTMLElement): void {
    trigger.focus();
  }
}

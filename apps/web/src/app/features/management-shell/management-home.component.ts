import { ChangeDetectionStrategy, Component, computed, inject } from "@angular/core";
import { LanguageService } from "../../core/i18n/language.service";

@Component({
  selector: "kalm-management-home",
  standalone: true,
  template: `
    <div class="empty-state" role="status">
      <span class="pi pi-compass" aria-hidden="true"></span>
      <h2>{{ copy().noModulesHeading }}</h2>
      <p>{{ copy().noModulesMessage }}</p>
    </div>
  `,
  styles: [`
    .empty-state { padding: clamp(24px, 4vw, 36px); border: 1px solid var(--kalm-warm-beige-deep); border-radius: var(--kalm-radius-large); background: var(--kalm-surface-raised); text-align: center; }
    .pi { margin-bottom: 14px; color: var(--kalm-coffee); font-size: 2rem; }
    h2 { margin: 0; }
    p { margin: 12px 0 0; color: var(--kalm-coffee); line-height: 1.6; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManagementHomeComponent {
  private readonly language = inject(LanguageService);
  protected readonly copy = computed(() => this.language.copy().managementShell);
}

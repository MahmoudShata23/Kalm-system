import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { BRANCHES_MANAGE_PERMISSION } from "../../core/auth/management-permissions";
import { LanguageService } from "../../core/i18n/language.service";
import { BranchesFacade } from "./branches.facade";
import { BRANCHES_COPY } from "./management-branches.copy";

@Component({
  selector: "kalm-branches-list",
  standalone: true,
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule],
  template: `
    <section class="panel" aria-labelledby="branches-heading">
      <header>
        <div><h2 id="branches-heading">{{ copy().heading }}</h2><p>{{ copy().support }}</p></div>
        @if (canManage()) { <a pButton routerLink="/management/branches/new"><span class="pi pi-plus" aria-hidden="true"></span>{{ copy().create }}</a> }
      </header>
      <form class="filters" (ngSubmit)="refresh(true)">
        <label>{{ copy().search }}<input pInputText name="search" [ngModel]="search()" (ngModelChange)="search.set($event)" /></label>
        <label>{{ copy().status }}
          <select name="status" [ngModel]="status()" (ngModelChange)="status.set($event)">
            <option value="all">{{ copy().all }}</option><option value="setup">{{ copy().setup }}</option>
            <option value="active">{{ copy().active }}</option><option value="suspended">{{ copy().suspended }}</option>
            <option value="archived">{{ copy().archived }}</option>
          </select>
        </label>
        <p-button type="submit" icon="pi pi-search" [label]="copy().search" />
      </form>
      @if (loading()) { <p aria-live="polite">{{ copy().loading }}</p> }
      @else if (error()) { <div role="alert"><p>{{ copy().error }}</p><p-button type="button" [label]="copy().retry" (onClick)="refresh()" /></div> }
      @else {
        <div class="table"><table>
          <thead><tr><th>{{ copy().code }}</th><th>{{ copy().name }}</th><th>{{ copy().status }}</th><th>{{ copy().timeZone }}</th><th></th></tr></thead>
          <tbody>
            @for (branch of list()?.items ?? []; track branch.id) {
              <tr><td><code>{{ branch.code }}</code></td><td>{{ branch.name }}</td><td>{{ statusLabel(branch.status) }}</td><td>{{ branch.timeZoneId }}</td>
                <td><a pButton [routerLink]="['/management/branches', branch.id]">{{ copy().edit }}</a></td></tr>
            } @empty { <tr><td colspan="5">{{ copy().empty }}</td></tr> }
          </tbody>
        </table></div>
        <nav class="paging" [attr.aria-label]="copy().page">
          <p-button type="button" [label]="copy().previous" [disabled]="(list()?.page ?? 1) <= 1" (onClick)="move(-1)" />
          <span>{{ copy().page }} {{ list()?.page ?? 1 }}</span>
          <p-button type="button" [label]="copy().next" [disabled]="!hasNext()" (onClick)="move(1)" />
        </nav>
      }
    </section>`,
  styles: [`
    :host { display:block } .panel { display:grid; gap:1rem } header,.filters,.paging { display:flex; gap:1rem; align-items:end; justify-content:space-between; flex-wrap:wrap }
    .filters label { display:grid; gap:.35rem } input,select,a { min-height:44px } .table { overflow:auto } table { width:100%; border-collapse:collapse }
    th,td { padding:.8rem; text-align:start; border-bottom:1px solid var(--p-surface-200) } .paging { justify-content:flex-end; align-items:center }
    a:focus-visible,input:focus-visible,select:focus-visible { outline:3px solid var(--p-primary-500); outline-offset:2px }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BranchesListComponent implements OnInit {
  private readonly facade = inject(BranchesFacade);
  private readonly language = inject(LanguageService);
  private readonly auth = inject(ManagementAuthService);
  protected readonly list = this.facade.list;
  protected readonly loading = this.facade.loading;
  protected readonly error = this.facade.errorCode;
  protected readonly search = signal("");
  protected readonly status = signal("all");
  protected readonly page = signal(1);
  protected readonly copy = computed(() => BRANCHES_COPY[this.language.language()]);
  protected readonly canManage = computed(() => this.auth.hasPermission(BRANCHES_MANAGE_PERMISSION));
  protected readonly hasNext = computed(() => {
    const value = this.list();
    return !!value && value.page * value.pageSize < value.totalCount;
  });

  ngOnInit(): void { void this.refresh(); }
  protected refresh(reset = false): Promise<void> { if (reset) this.page.set(1); return this.facade.loadList(this.status(), this.search(), this.page()); }
  protected move(delta: number): void { this.page.update(page => Math.max(1, page + delta)); void this.refresh(); }
  protected statusLabel(status: "setup" | "active" | "suspended" | "archived"): string { return this.copy()[status]; }
}

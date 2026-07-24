import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink, RouterLinkActive } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { CATALOG_MANAGE_PERMISSION } from "../../core/auth/management-permissions";
import { LanguageService } from "../../core/i18n/language.service";
import { CatalogFacade } from "./catalog.facade";
import { CATALOG_COPY } from "./management-catalog.copy";

@Component({
  selector: "kalm-categories-list",
  standalone: true,
  imports: [FormsModule, RouterLink, RouterLinkActive, ButtonModule, InputTextModule],
  template: `
    <section class="catalog-page" aria-labelledby="categories-heading">
      <nav class="tabs" [attr.aria-label]="copy().navigation">
        <a routerLink="/management/catalog/categories" routerLinkActive="active">{{ copy().categories }}</a>
        <a routerLink="/management/catalog/products" routerLinkActive="active">{{ copy().products }}</a>
      </nav>
      <header><div><p class="eyebrow">{{ copy().catalog }}</p><h2 id="categories-heading">{{ copy().categories }}</h2>
        <p>{{ copy().categoryIntro }}</p></div>
        @if (canManage()) { <a pButton routerLink="/management/catalog/categories/new"><span class="pi pi-plus" aria-hidden="true"></span>{{ copy().createCategory }}</a> }
      </header>
      <form class="filters" (ngSubmit)="refresh(true)">
        <label>{{ copy().search }}<input pInputText name="search" [ngModel]="search()" (ngModelChange)="search.set($event)" /></label>
        <label>{{ copy().status }}<select name="status" [ngModel]="status()" (ngModelChange)="status.set($event)">
          <option value="all">{{ copy().all }}</option><option value="active">{{ copy().active }}</option><option value="archived">{{ copy().archived }}</option>
        </select></label>
        <p-button type="submit" icon="pi pi-search" [label]="copy().search" />
      </form>
      @if (loading()) { <p aria-live="polite">{{ copy().loading }}</p> }
      @else if (error()) { <div role="alert"><p>{{ copy().error }}</p><p-button type="button" [label]="copy().retry" (onClick)="refresh()" /></div> }
      @else {
        <div class="table-wrap"><table>
          <thead><tr><th>{{ copy().order }}</th><th>{{ copy().arabicName }}</th><th>{{ copy().englishName }}</th><th>{{ copy().status }}</th><th>{{ copy().actions }}</th></tr></thead>
          <tbody>@for (category of list()?.items ?? []; track category.id; let index = $index) {
            <tr><td>{{ category.displayOrder }}</td><td lang="ar" dir="rtl">{{ category.arabicName }}</td><td lang="en" dir="ltr">{{ category.englishName }}</td>
              <td>{{ category.status === "active" ? copy().active : copy().archived }}</td><td class="row-actions">
                @if (canReorder()) {
                  <p-button type="button" icon="pi pi-arrow-up" [text]="true" [ariaLabel]="copy().moveUp" [disabled]="index === 0 || saving()" (onClick)="move(index, -1)" />
                  <p-button type="button" icon="pi pi-arrow-down" [text]="true" [ariaLabel]="copy().moveDown" [disabled]="index === (list()?.items?.length ?? 0) - 1 || saving()" (onClick)="move(index, 1)" />
                }
                <a pButton [routerLink]="['/management/catalog/categories', category.id]">{{ copy().open }}</a>
              </td></tr>
          } @empty { <tr><td colspan="5">{{ copy().empty }}</td></tr> }</tbody>
        </table></div>
        <nav class="paging" [attr.aria-label]="copy().page">
          <p-button type="button" [label]="copy().previous" [disabled]="page() <= 1" (onClick)="changePage(-1)" />
          <span>{{ copy().page }} {{ page() }}</span>
          <p-button type="button" [label]="copy().next" [disabled]="!hasNext()" (onClick)="changePage(1)" />
        </nav>
      }
    </section>`,
  styles: [`
    :host{display:block}.catalog-page{display:grid;gap:1.25rem}.tabs,header,.filters,.paging,.row-actions{display:flex;gap:.75rem;align-items:center;flex-wrap:wrap}.tabs{border-bottom:1px solid var(--p-content-border-color)}.tabs a{padding:.8rem;text-decoration:none}.tabs .active{border-block-end:3px solid var(--p-primary-color)}header{justify-content:space-between;align-items:end}.eyebrow{color:var(--p-primary-color);font-weight:700}.filters{padding:1rem;background:var(--p-content-background);border:1px solid var(--p-content-border-color);border-radius:var(--p-border-radius-md)}label{display:grid;gap:.35rem}input,select,a{min-height:44px}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse;min-width:720px}th,td{text-align:start;padding:.75rem;border-bottom:1px solid var(--p-content-border-color)}.paging{justify-content:flex-end}a:focus-visible,input:focus-visible,select:focus-visible{outline:3px solid var(--p-focus-ring-color);outline-offset:2px}
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CategoriesListComponent implements OnInit {
  private readonly facade = inject(CatalogFacade);
  private readonly language = inject(LanguageService);
  private readonly auth = inject(ManagementAuthService);
  protected readonly list = this.facade.categories;
  protected readonly loading = this.facade.loading;
  protected readonly saving = this.facade.saving;
  protected readonly error = this.facade.errorCode;
  protected readonly search = signal("");
  protected readonly status = signal("all");
  protected readonly page = signal(1);
  protected readonly copy = computed(() => CATALOG_COPY[this.language.language()]);
  protected readonly canManage = computed(() => this.auth.hasPermission(CATALOG_MANAGE_PERMISSION));
  protected readonly canReorder = computed(() => this.canManage()
    && this.status() === "all"
    && this.search().trim().length === 0
    && this.page() === 1
    && this.list()?.totalCount === this.list()?.items.length);
  protected readonly hasNext = computed(() => {
    const list = this.list();
    return !!list && list.page * list.pageSize < list.totalCount;
  });

  ngOnInit(): void { void this.refresh(); }
  protected refresh(reset = false): Promise<void> {
    if (reset) this.page.set(1);
    return this.facade.loadCategories(this.status(), this.search(), this.page());
  }
  protected changePage(delta: number): void { this.page.update(page => Math.max(1, page + delta)); void this.refresh(); }
  protected async move(index: number, delta: number): Promise<void> {
    if (await this.facade.moveCategory(index, delta)) await this.refresh();
  }
}

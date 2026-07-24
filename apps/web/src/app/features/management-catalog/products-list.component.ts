import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink, RouterLinkActive } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { CATALOG_MANAGE_PERMISSION } from "../../core/auth/management-permissions";
import { LanguageService } from "../../core/i18n/language.service";
import { CatalogFacade } from "./catalog.facade";
import { ProductType } from "./catalog.models";
import { CATALOG_COPY } from "./management-catalog.copy";

@Component({
  selector: "kalm-products-list",
  standalone: true,
  imports: [FormsModule, RouterLink, RouterLinkActive, ButtonModule, InputTextModule],
  template: `
    <section class="catalog-page" aria-labelledby="products-heading">
      <nav class="tabs" [attr.aria-label]="copy().navigation"><a routerLink="/management/catalog/categories" routerLinkActive="active">{{ copy().categories }}</a>
        <a routerLink="/management/catalog/products" routerLinkActive="active">{{ copy().products }}</a></nav>
      <header><div><p class="eyebrow">{{ copy().catalog }}</p><h2 id="products-heading">{{ copy().products }}</h2><p>{{ copy().productIntro }}</p></div>
        @if (canManage()) { <a pButton routerLink="/management/catalog/products/new"><span class="pi pi-plus" aria-hidden="true"></span>{{ copy().createProduct }}</a> }</header>
      <form class="filters" (ngSubmit)="refresh(true)">
        <label>{{ copy().search }}<input pInputText name="search" [ngModel]="search()" (ngModelChange)="search.set($event)" /></label>
        <label>{{ copy().status }}<select name="status" [ngModel]="status()" (ngModelChange)="status.set($event)"><option value="all">{{ copy().all }}</option><option value="active">{{ copy().active }}</option><option value="archived">{{ copy().archived }}</option></select></label>
        <label>{{ copy().category }}<select name="category" [ngModel]="categoryId()" (ngModelChange)="categoryId.set($event)"><option value="">{{ copy().all }}</option>@for (category of options()?.categories ?? []; track category.id) {<option [value]="category.id">{{ language.language() === "ar" ? category.arabicName : category.englishName }}</option>}</select></label>
        <label>{{ copy().productType }}<select name="type" [ngModel]="productType()" (ngModelChange)="productType.set($event)"><option value="">{{ copy().all }}</option>@for (type of options()?.productTypes ?? []; track type.code) {<option [value]="type.code">{{ language.language() === "ar" ? type.arabicLabel : type.englishLabel }}</option>}</select></label>
        <p-button type="submit" icon="pi pi-search" [label]="copy().search" />
      </form>
      @if (loading()) { <p aria-live="polite">{{ copy().loading }}</p> }
      @else if (error()) { <div role="alert"><p>{{ copy().error }}</p><p-button type="button" [label]="copy().retry" (onClick)="initialize()" /></div> }
      @else { <div class="table-wrap"><table><thead><tr><th>{{ copy().sku }}</th><th>{{ copy().name }}</th><th>{{ copy().category }}</th><th>{{ copy().productType }}</th><th>{{ copy().variants }}</th><th>{{ copy().status }}</th><th></th></tr></thead>
        <tbody>@for (product of list()?.items ?? []; track product.id) {<tr><td><code>{{ product.sku }}</code></td><td>{{ language.language() === "ar" ? product.arabicName : product.englishName }}</td>
          <td>{{ language.language() === "ar" ? product.categoryArabicName : product.categoryEnglishName }}</td><td>{{ typeLabel(product.productType) }}</td><td>{{ product.activeVariantCount }} / {{ product.variantCount }}</td>
          <td>{{ product.status === "active" ? copy().active : copy().archived }}</td><td><a pButton [routerLink]="['/management/catalog/products', product.id]">{{ copy().open }}</a></td></tr>
        } @empty {<tr><td colspan="7">{{ copy().empty }}</td></tr>}</tbody></table></div>
        <nav class="paging" [attr.aria-label]="copy().page"><p-button type="button" [label]="copy().previous" [disabled]="page() <= 1" (onClick)="changePage(-1)" /><span>{{ copy().page }} {{ page() }}</span>
          <p-button type="button" [label]="copy().next" [disabled]="!hasNext()" (onClick)="changePage(1)" /></nav>}
    </section>`,
  styles: [`
    :host{display:block}.catalog-page{display:grid;gap:1.25rem}.tabs,header,.filters,.paging{display:flex;gap:.75rem;align-items:center;flex-wrap:wrap}.tabs{border-bottom:1px solid var(--p-content-border-color)}.tabs a{padding:.8rem;text-decoration:none}.tabs .active{border-block-end:3px solid var(--p-primary-color)}header{justify-content:space-between;align-items:end}.eyebrow{color:var(--p-primary-color);font-weight:700}.filters{padding:1rem;border:1px solid var(--p-content-border-color);border-radius:var(--p-border-radius-md)}label{display:grid;gap:.35rem}input,select,a{min-height:44px}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse;min-width:900px}th,td{text-align:start;padding:.75rem;border-bottom:1px solid var(--p-content-border-color)}.paging{justify-content:flex-end}a:focus-visible,input:focus-visible,select:focus-visible{outline:3px solid var(--p-focus-ring-color);outline-offset:2px}
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductsListComponent implements OnInit {
  protected readonly facade = inject(CatalogFacade);
  protected readonly language = inject(LanguageService);
  private readonly auth = inject(ManagementAuthService);
  protected readonly list = this.facade.products;
  protected readonly options = this.facade.options;
  protected readonly loading = this.facade.loading;
  protected readonly error = this.facade.errorCode;
  protected readonly search = signal("");
  protected readonly status = signal("all");
  protected readonly categoryId = signal("");
  protected readonly productType = signal("");
  protected readonly page = signal(1);
  protected readonly copy = computed(() => CATALOG_COPY[this.language.language()]);
  protected readonly canManage = computed(() => this.auth.hasPermission(CATALOG_MANAGE_PERMISSION));
  protected readonly hasNext = computed(() => {
    const list = this.list();
    return !!list && list.page * list.pageSize < list.totalCount;
  });

  ngOnInit(): void { void this.initialize(); }
  protected async initialize(): Promise<void> { await this.facade.loadProductOptions(); await this.refresh(); }
  protected refresh(reset = false): Promise<void> {
    if (reset) this.page.set(1);
    return this.facade.loadProducts(this.status(), this.search(), this.categoryId(), this.productType(), this.page());
  }
  protected changePage(delta: number): void { this.page.update(page => Math.max(1, page + delta)); void this.refresh(); }
  protected typeLabel(type: ProductType): string { return this.copy()[type]; }
}

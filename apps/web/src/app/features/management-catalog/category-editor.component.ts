import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { CATALOG_MANAGE_PERMISSION } from "../../core/auth/management-permissions";
import { LanguageService } from "../../core/i18n/language.service";
import { CatalogFacade } from "./catalog.facade";
import { CATALOG_COPY } from "./management-catalog.copy";

@Component({
  selector: "kalm-category-editor",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ButtonModule, InputTextModule],
  template: `
    <section class="editor" aria-labelledby="category-heading">
      <a routerLink="/management/catalog/categories">← {{ copy().backCategories }}</a>
      <div><p class="eyebrow">{{ copy().catalog }}</p><h2 id="category-heading">{{ isNew ? copy().createCategory : copy().editCategory }}</h2></div>
      @if (loading()) { <p aria-live="polite">{{ copy().loading }}</p> }
      @else if (error() && !detail()) { <div role="alert"><p>{{ copy().error }}</p><p-button type="button" [label]="copy().retry" (onClick)="load()" /></div> }
      @else {
        <form [formGroup]="form" (ngSubmit)="save()">
          <label>{{ copy().arabicName }}<input pInputText lang="ar" dir="rtl" formControlName="arabicName" /></label>
          <label>{{ copy().englishName }}<input pInputText lang="en" dir="ltr" formControlName="englishName" /></label>
          <label>{{ copy().displayOrder }}<input pInputText type="number" min="0" formControlName="displayOrder" /></label>
          <label>{{ copy().color }}<select formControlName="posColorToken"><option value="">{{ copy().none }}</option>@for (color of colors; track color) {<option [value]="color">{{ color }}</option>}</select></label>
          <label>{{ copy().icon }}<select formControlName="iconCode"><option value="">{{ copy().none }}</option>@for (icon of icons; track icon) {<option [value]="icon">{{ icon }}</option>}</select></label>
          @if (canManage()) { <p-button type="submit" [label]="copy().save" [loading]="saving()" [disabled]="form.invalid" /> }
        </form>
        @if (form.invalid && form.touched) { <p role="alert">{{ copy().invalid }}</p> }
        @if (detail(); as current) { @if (canManage()) { <p-button type="button" [severity]="current.category.status === 'active' ? 'danger' : undefined"
          [label]="current.category.status === 'active' ? copy().archive : copy().activate"
          (onClick)="confirmation.set(current.category.status === 'active' ? 'archive' : 'activate')" /> } }
        @if (error() === "catalog.concurrency_conflict") { <aside role="alert"><p>{{ copy().conflict }}</p><p-button type="button" [label]="copy().refreshVersion" (onClick)="refreshVersion()" /></aside> }
        @else if (error() === "catalog.category_has_active_products") { <aside role="alert"><p>{{ copy().archiveBlocked }}</p><p>{{ copy().activeProducts }}: {{ activeProducts() ?? 0 }}</p></aside> }
        @else if (error()) { <p role="alert">{{ copy().error }}</p> }
        @if (confirmation(); as action) { <div class="dialog" role="dialog" aria-modal="true"><p>{{ action === "archive" ? copy().confirmArchive : copy().confirmActivate }}</p>
          <div class="actions"><p-button type="button" [severity]="action === 'archive' ? 'danger' : undefined" [label]="copy().confirm" (onClick)="changeStatus(action)" />
            <p-button type="button" severity="secondary" [label]="copy().cancel" (onClick)="confirmation.set(null)" /></div></div> }
      }
    </section>`,
  styles: [`
    :host{display:block}.editor,form{display:grid;gap:1rem;max-width:54rem}.eyebrow{color:var(--p-primary-color);font-weight:700}form{grid-template-columns:repeat(2,minmax(0,1fr));padding:1.25rem;border:1px solid var(--p-content-border-color);border-radius:var(--p-border-radius-lg)}label{display:grid;gap:.35rem}input,select,a{min-height:44px}.dialog,aside{padding:1rem;border:1px solid var(--p-primary-color);border-radius:var(--p-border-radius-md);background:var(--p-content-background)}.actions{display:flex;gap:.75rem}a:focus-visible,input:focus-visible,select:focus-visible{outline:3px solid var(--p-focus-ring-color);outline-offset:2px}@media(max-width:640px){form{grid-template-columns:1fr}}
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CategoryEditorComponent implements OnInit {
  private readonly facade = inject(CatalogFacade);
  private readonly language = inject(LanguageService);
  private readonly auth = inject(ManagementAuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly id = this.route.snapshot.paramMap.get("categoryId") ?? undefined;
  protected readonly isNew = !this.id;
  protected readonly detail = this.facade.category;
  protected readonly loading = this.facade.loading;
  protected readonly saving = this.facade.saving;
  protected readonly error = this.facade.errorCode;
  protected readonly activeProducts = this.facade.activeProductCount;
  protected readonly confirmation = signal<"activate" | "archive" | null>(null);
  protected readonly copy = computed(() => CATALOG_COPY[this.language.language()]);
  protected readonly canManage = computed(() => this.auth.hasPermission(CATALOG_MANAGE_PERMISSION));
  protected readonly colors = ["sand", "sage", "clay", "coffee", "cream"] as const;
  protected readonly icons = ["coffee", "cup", "bottle", "cake", "leaf", "sparkles"] as const;
  protected readonly form = new FormGroup({
    arabicName: new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
    englishName: new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
    displayOrder: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    posColorToken: new FormControl("", { nonNullable: true }),
    iconCode: new FormControl("", { nonNullable: true })
  });

  ngOnInit(): void { void this.load(); }
  protected async load(): Promise<void> {
    await this.facade.loadCategory(this.id);
    const category = this.detail()?.category;
    if (category) this.form.reset({
      arabicName: category.arabicName,
      englishName: category.englishName,
      displayOrder: category.displayOrder,
      posColorToken: category.posColorToken ?? "",
      iconCode: category.iconCode ?? ""
    });
    if (!this.canManage()) this.form.disable();
  }
  protected async save(): Promise<void> {
    if (!this.canManage() || this.form.invalid) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue();
    const result = await this.facade.saveCategory({
      ...value,
      posColorToken: value.posColorToken || null,
      iconCode: value.iconCode || null
    });
    if (result && this.isNew) await this.router.navigate(["/management/catalog/categories", result.category.id]);
  }
  protected refreshVersion(): Promise<void> { return this.facade.refreshCategoryVersionPreservingDraft(); }
  protected async changeStatus(action: "activate" | "archive"): Promise<void> {
    if (await this.facade.changeCategoryStatus(action)) this.confirmation.set(null);
  }
}

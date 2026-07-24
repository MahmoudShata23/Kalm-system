import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { CATALOG_MANAGE_PERMISSION } from "../../core/auth/management-permissions";
import { LanguageService } from "../../core/i18n/language.service";
import { CatalogFacade } from "./catalog.facade";
import { CatalogStatus, ProductType, ProductVariant, VariantWriteRequest } from "./catalog.models";
import { CATALOG_COPY } from "./management-catalog.copy";

type VariantForm = FormGroup<{
  id: FormControl<string | null>;
  arabicName: FormControl<string>;
  englishName: FormControl<string>;
  code: FormControl<string>;
  barcode: FormControl<string>;
  sizeCode: FormControl<string>;
  temperatureCode: FormControl<string>;
  servingFormatCode: FormControl<string>;
  displayOrder: FormControl<number>;
  status: FormControl<CatalogStatus>;
}>;

@Component({
  selector: "kalm-product-editor",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ButtonModule, InputTextModule],
  template: `
    <section class="editor" aria-labelledby="product-heading">
      <a routerLink="/management/catalog/products">← {{ copy().backProducts }}</a>
      <div><p class="eyebrow">{{ copy().catalog }}</p><h2 id="product-heading">{{ isNew ? copy().createProduct : copy().editProduct }}</h2></div>
      @if (loading()) { <p aria-live="polite">{{ copy().loading }}</p> }
      @else if (error() && !detail()) { <div role="alert"><p>{{ copy().error }}</p><p-button type="button" [label]="copy().retry" (onClick)="load()" /></div> }
      @else {
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="fields">
            <label>{{ copy().arabicName }}<input pInputText lang="ar" dir="rtl" formControlName="arabicName" /></label>
            <label>{{ copy().englishName }}<input pInputText lang="en" dir="ltr" formControlName="englishName" /></label>
            <label>{{ copy().arabicDescription }}<textarea lang="ar" dir="rtl" formControlName="arabicDescription"></textarea></label>
            <label>{{ copy().englishDescription }}<textarea lang="en" dir="ltr" formControlName="englishDescription"></textarea></label>
            <label>{{ copy().sku }}<input pInputText autocomplete="off" formControlName="sku" /></label>
            <label>{{ copy().category }}<select formControlName="categoryId">@for (category of options()?.categories ?? []; track category.id) {<option [value]="category.id">{{ language.language() === "ar" ? category.arabicName : category.englishName }}</option>}</select></label>
            <label>{{ copy().productType }}<select formControlName="productType">@for (type of options()?.productTypes ?? []; track type.code) {<option [value]="type.code">{{ language.language() === "ar" ? type.arabicLabel : type.englishLabel }}</option>}</select></label>
            <label>{{ copy().displayOrder }}<input type="number" min="0" formControlName="displayOrder" /></label>
          </div>
          <section class="variants" aria-labelledby="variants-heading"><header><h3 id="variants-heading">{{ copy().variants }}</h3>
            @if (canManage()) { <p-button type="button" icon="pi pi-plus" [label]="copy().addVariant" (onClick)="addVariant()" /> }</header>
            <div formArrayName="variants">@for (variant of variants.controls; track variant; let index = $index) {
              <fieldset [formGroupName]="index"><legend>{{ copy().variant }} {{ index + 1 }}</legend>
                <label>{{ copy().arabicName }}<input pInputText lang="ar" dir="rtl" formControlName="arabicName" /></label>
                <label>{{ copy().englishName }}<input pInputText lang="en" dir="ltr" formControlName="englishName" /></label>
                <label>{{ copy().code }}<input pInputText autocomplete="off" formControlName="code" /></label>
                <label>{{ copy().barcode }}<input pInputText inputmode="numeric" autocomplete="off" formControlName="barcode" /></label>
                <label>{{ copy().size }}<select formControlName="sizeCode"><option value="">{{ copy().none }}</option>@for (option of options()?.sizeCodes ?? []; track option.code) {<option [value]="option.code">{{ language.language() === "ar" ? option.arabicLabel : option.englishLabel }}</option>}</select></label>
                <label>{{ copy().temperature }}<select formControlName="temperatureCode"><option value="">{{ copy().none }}</option>@for (option of options()?.temperatureCodes ?? []; track option.code) {<option [value]="option.code">{{ language.language() === "ar" ? option.arabicLabel : option.englishLabel }}</option>}</select></label>
                <label>{{ copy().serving }}<select formControlName="servingFormatCode"><option value="">{{ copy().none }}</option>@for (option of options()?.servingFormatCodes ?? []; track option.code) {<option [value]="option.code">{{ language.language() === "ar" ? option.arabicLabel : option.englishLabel }}</option>}</select></label>
                <div class="variant-actions">@if (canManage()) {
                  <p-button type="button" icon="pi pi-arrow-up" [text]="true" [ariaLabel]="copy().moveUp" [disabled]="index === 0" (onClick)="moveVariant(index, -1)" />
                  <p-button type="button" icon="pi pi-arrow-down" [text]="true" [ariaLabel]="copy().moveDown" [disabled]="index === variants.length - 1" (onClick)="moveVariant(index, 1)" />
                  <p-button type="button" [severity]="variant.controls.status.value === 'active' ? 'danger' : undefined"
                    [label]="variant.controls.status.value === 'active' ? copy().archiveVariant : copy().activateVariant"
                    (onClick)="toggleVariant(index)" />
                }</div>
              </fieldset>
            }</div>
          </section>
          @if (canManage()) { <p-button type="submit" [label]="copy().save" [loading]="saving()" [disabled]="form.invalid || variants.length === 0" /> }
        </form>
        @if ((form.invalid || variants.length === 0) && form.touched) { <p role="alert">{{ copy().invalid }}</p> }
        @if (detail(); as current) { @if (canManage()) { <p-button type="button" [severity]="current.product.status === 'active' ? 'danger' : undefined"
          [label]="current.product.status === 'active' ? copy().archive : copy().activate"
          (onClick)="confirmation.set(current.product.status === 'active' ? 'archive' : 'activate')" /> } }
        @if (error() === "catalog.concurrency_conflict") { <aside role="alert"><p>{{ copy().conflict }}</p><p-button type="button" [label]="copy().refreshVersion" (onClick)="refreshVersion()" /></aside> }
        @else if (error() === "catalog.active_category_required" || error() === "catalog.active_variant_required") { <aside role="alert">{{ copy().lifecycleConflict }}</aside> }
        @else if (error()) { <p role="alert">{{ copy().error }}</p> }
        @if (confirmation(); as action) { <div class="dialog" role="dialog" aria-modal="true"><p>{{ action === "archive" ? copy().confirmArchive : copy().confirmActivate }}</p>
          <div class="actions"><p-button type="button" [severity]="action === 'archive' ? 'danger' : undefined" [label]="copy().confirm" (onClick)="changeStatus(action)" />
            <p-button type="button" severity="secondary" [label]="copy().cancel" (onClick)="confirmation.set(null)" /></div></div> }
      }
    </section>`,
  styles: [`
    :host{display:block}.editor,form,.variants{display:grid;gap:1rem}.editor{max-width:70rem}.eyebrow{color:var(--p-primary-color);font-weight:700}.fields{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:1rem;padding:1.25rem;border:1px solid var(--p-content-border-color);border-radius:var(--p-border-radius-lg)}label{display:grid;gap:.35rem}input,select,textarea,a{min-height:44px}textarea{min-height:7rem;padding:.65rem;border:1px solid var(--p-form-field-border-color);border-radius:var(--p-border-radius-sm)}.variants header,.variant-actions,.actions{display:flex;gap:.75rem;align-items:center;justify-content:space-between;flex-wrap:wrap}fieldset{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:.8rem;margin-block:1rem;padding:1rem;border:1px solid var(--p-content-border-color);border-radius:var(--p-border-radius-md)}.variant-actions{grid-column:1/-1;justify-content:flex-end}.dialog,aside{padding:1rem;border:1px solid var(--p-primary-color);border-radius:var(--p-border-radius-md);background:var(--p-content-background)}a:focus-visible,input:focus-visible,select:focus-visible,textarea:focus-visible{outline:3px solid var(--p-focus-ring-color);outline-offset:2px}@media(max-width:680px){.fields,fieldset{grid-template-columns:1fr}.variant-actions{grid-column:1}}
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductEditorComponent implements OnInit {
  private readonly facade = inject(CatalogFacade);
  protected readonly language = inject(LanguageService);
  private readonly auth = inject(ManagementAuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly id = this.route.snapshot.paramMap.get("productId") ?? undefined;
  protected readonly isNew = !this.id;
  protected readonly detail = this.facade.product;
  protected readonly options = this.facade.options;
  protected readonly loading = this.facade.loading;
  protected readonly saving = this.facade.saving;
  protected readonly error = this.facade.errorCode;
  protected readonly confirmation = signal<"activate" | "archive" | null>(null);
  protected readonly copy = computed(() => CATALOG_COPY[this.language.language()]);
  protected readonly canManage = computed(() => this.auth.hasPermission(CATALOG_MANAGE_PERMISSION));
  protected readonly form = new FormGroup({
    categoryId: new FormControl("", { nonNullable: true, validators: [Validators.required] }),
    arabicName: new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
    englishName: new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
    arabicDescription: new FormControl("", { nonNullable: true, validators: [Validators.maxLength(1000)] }),
    englishDescription: new FormControl("", { nonNullable: true, validators: [Validators.maxLength(1000)] }),
    sku: new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.maxLength(40)] }),
    productType: new FormControl<ProductType>("madeToOrder", { nonNullable: true, validators: [Validators.required] }),
    displayOrder: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    variants: new FormArray<VariantForm>([])
  });
  protected readonly variants = this.form.controls.variants;

  ngOnInit(): void { void this.load(); }
  protected async load(): Promise<void> {
    await this.facade.loadProduct(this.id);
    const product = this.detail()?.product;
    this.variants.clear();
    if (product) {
      this.form.patchValue({
        categoryId: product.categoryId,
        arabicName: product.arabicName,
        englishName: product.englishName,
        arabicDescription: product.arabicDescription ?? "",
        englishDescription: product.englishDescription ?? "",
        sku: product.sku,
        productType: product.productType,
        displayOrder: product.displayOrder
      });
      product.variants.forEach(variant => this.variants.push(this.variantForm(variant)));
    } else {
      const category = this.options()?.categories[0];
      if (category) this.form.controls.categoryId.setValue(category.id);
      this.addVariant();
    }
    if (!this.canManage()) this.form.disable();
  }

  protected addVariant(): void { this.variants.push(this.variantForm()); }
  protected toggleVariant(index: number): void {
    const control = this.variants.at(index).controls.status;
    control.setValue(control.value === "active" ? "archived" : "active");
  }
  protected moveVariant(index: number, delta: number): void {
    const destination = index + delta;
    if (destination < 0 || destination >= this.variants.length) return;
    const control = this.variants.at(index);
    this.variants.removeAt(index);
    this.variants.insert(destination, control);
    this.variants.controls.forEach((variant, order) => variant.controls.displayOrder.setValue(order));
  }
  protected async save(): Promise<void> {
    if (!this.canManage() || this.form.invalid || this.variants.length === 0) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue();
    const variants: VariantWriteRequest[] = value.variants.map((variant, displayOrder) => ({
      ...variant,
      barcode: variant.barcode || null,
      sizeCode: variant.sizeCode || null,
      temperatureCode: variant.temperatureCode || null,
      servingFormatCode: variant.servingFormatCode || null,
      displayOrder
    }));
    const ids = variants.map(variant => variant.id);
    const result = await this.facade.saveProduct({
      categoryId: value.categoryId,
      arabicName: value.arabicName,
      englishName: value.englishName,
      arabicDescription: value.arabicDescription || null,
      englishDescription: value.englishDescription || null,
      sku: value.sku,
      productType: value.productType,
      displayOrder: value.displayOrder,
      variants,
      variantOrder: ids.every((id): id is string => id !== null) ? ids : null
    });
    if (result && this.isNew) await this.router.navigate(["/management/catalog/products", result.product.id]);
  }
  protected refreshVersion(): Promise<void> { return this.facade.refreshProductVersionPreservingDraft(); }
  protected async changeStatus(action: "activate" | "archive"): Promise<void> {
    if (await this.facade.changeProductStatus(action)) this.confirmation.set(null);
  }

  private variantForm(variant?: ProductVariant): VariantForm {
    return new FormGroup({
      id: new FormControl(variant?.id ?? null),
      arabicName: new FormControl(variant?.arabicName ?? "", { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
      englishName: new FormControl(variant?.englishName ?? "", { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
      code: new FormControl(variant?.code ?? "", { nonNullable: true, validators: [Validators.required, Validators.maxLength(40)] }),
      barcode: new FormControl(variant?.barcode ?? "", { nonNullable: true, validators: [Validators.maxLength(64)] }),
      sizeCode: new FormControl(variant?.sizeCode ?? "", { nonNullable: true }),
      temperatureCode: new FormControl(variant?.temperatureCode ?? "", { nonNullable: true }),
      servingFormatCode: new FormControl(variant?.servingFormatCode ?? "", { nonNullable: true }),
      displayOrder: new FormControl(variant?.displayOrder ?? this.variants.length, { nonNullable: true, validators: [Validators.min(0)] }),
      status: new FormControl<CatalogStatus>(variant?.status ?? "active", { nonNullable: true })
    });
  }
}

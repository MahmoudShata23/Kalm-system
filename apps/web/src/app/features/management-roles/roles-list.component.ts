import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { LanguageService } from "../../core/i18n/language.service";
import { ROLES_COPY } from "./management-roles.copy";
import { RolesFacade } from "./roles.facade";

@Component({
  selector: "kalm-roles-list",
  standalone: true,
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule],
  templateUrl: "./roles-list.component.html",
  styleUrl: "./roles-list.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RolesListComponent implements OnInit {
  private readonly facade = inject(RolesFacade);
  private readonly language = inject(LanguageService);

  protected readonly copy = computed(() => ROLES_COPY[this.language.language()]);
  protected readonly list = this.facade.list;
  protected readonly loading = this.facade.loading;
  protected readonly errorCode = this.facade.errorCode;
  protected readonly search = signal("");
  protected readonly status = signal("active");
  protected readonly page = signal(1);
  protected readonly pageSize = 25;
  protected readonly canPrevious = computed(() => this.page() > 1);
  protected readonly canNext = computed(() => {
    const list = this.list();
    return !!list && list.page * list.pageSize < list.totalCount;
  });

  ngOnInit(): void {
    void this.refresh();
  }

  protected refresh(resetPage = false): Promise<void> {
    if (resetPage) this.page.set(1);
    return this.facade.loadList(this.status(), this.search(), this.page(), this.pageSize);
  }

  protected changeStatus(value: string): void {
    this.status.set(value);
    void this.refresh(true);
  }

  protected previous(): void {
    if (!this.canPrevious()) return;
    this.page.update(value => value - 1);
    void this.refresh();
  }

  protected next(): void {
    if (!this.canNext()) return;
    this.page.update(value => value + 1);
    void this.refresh();
  }
}

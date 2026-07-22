import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { USERS_MANAGE_PERMISSION } from "../../core/auth/management-permissions";
import { LanguageService } from "../../core/i18n/language.service";
import { USERS_COPY } from "./management-users.copy";
import { UsersFacade } from "./users.facade";

@Component({
  selector: "kalm-users-list",
  standalone: true,
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule],
  templateUrl: "./users-list.component.html",
  styleUrl: "./users-list.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UsersListComponent implements OnInit {
  private readonly facade = inject(UsersFacade);
  private readonly language = inject(LanguageService);
  private readonly auth = inject(ManagementAuthService);

  protected readonly copy = computed(() => USERS_COPY[this.language.language()]);
  protected readonly canManage = computed(() => this.auth.hasPermission(USERS_MANAGE_PERMISSION));
  protected readonly list = this.facade.list;
  protected readonly loading = this.facade.loading;
  protected readonly errorCode = this.facade.errorCode;
  protected readonly search = signal("");
  protected readonly status = signal("all");
  protected readonly page = signal(1);
  protected readonly pageSize = 25;
  protected readonly canPrevious = computed(() => this.page() > 1);
  protected readonly canNext = computed(() => {
    const result = this.list();
    return !!result && result.page * result.pageSize < result.totalCount;
  });

  ngOnInit(): void { void this.refresh(); }

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

  protected statusLabel(status: string): string {
    return this.copy()[status] ?? status;
  }
}

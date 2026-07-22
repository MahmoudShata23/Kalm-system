import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { LanguageService } from "../../core/i18n/language.service";
import { DevicesFacade } from "./devices.facade";

@Component({
  selector: "kalm-devices-list",
  standalone: true,
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="panel">
    <header>
      <div>
        <h2>{{ c().heading }}</h2>
        <p>{{ c().support }}</p>
      </div>
      <a pButton routerLink="/management/devices/new"
        ><span class="pi pi-plus"></span> {{ c().create }}</a
      >
    </header>
    <form (ngSubmit)="refresh(true)" class="filters">
      <label
        >{{ c().search
        }}<input
          pInputText
          name="search"
          [ngModel]="search()"
          (ngModelChange)="search.set($event)" /></label
      ><label
        >{{ c().status
        }}<select
          name="status"
          [ngModel]="status()"
          (ngModelChange)="status.set($event)"
        >
          <option value="all">{{ c().all }}</option>
          <option value="pendingPairing">{{ c().pending }}</option>
          <option value="active">{{ c().active }}</option>
          <option value="revoked">{{ c().revoked }}</option>
        </select></label
      ><label
        >{{ c().branch
        }}<select
          name="branch"
          [ngModel]="branch()"
          (ngModelChange)="branch.set($event)"
        >
          <option value="">{{ c().all }}</option>
          @for (b of options()?.branches ?? []; track b.id) {
            <option [value]="b.id">{{ b.name }}</option>
          }
        </select></label
      ><p-button type="submit" [label]="c().search" />
    </form>
    @if (loading()) {
      <p aria-live="polite">{{ c().loading }}</p>
    } @else if (error()) {
      <p role="alert">{{ c().error }}</p>
    } @else {
      <div class="table">
        <table>
          <thead>
            <tr>
              <th>{{ c().name }}</th>
              <th>{{ c().branch }}</th>
              <th>{{ c().type }}</th>
              <th>{{ c().status }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (d of list()?.items ?? []; track d.id) {
              <tr>
                <td>{{ d.name }}</td>
                <td>{{ d.branchName }}</td>
                <td>{{ d.type }}</td>
                <td>{{ d.status }}</td>
                <td>
                  <a pButton [routerLink]="['/management/devices', d.id]">{{
                    c().edit
                  }}</a>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="5">{{ c().empty }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  </section>`,
  styles: [
    `
      :host {
        display: block;
      }
      .panel {
        display: grid;
        gap: 1rem;
      }
      header,
      .filters {
        display: flex;
        gap: 1rem;
        justify-content: space-between;
        flex-wrap: wrap;
      }
      .filters label {
        display: grid;
        gap: 0.35rem;
      }
      input,
      select {
        min-height: 44px;
      }
      .table {
        overflow: auto;
      }
      table {
        width: 100%;
        border-collapse: collapse;
      }
      th,
      td {
        padding: 0.8rem;
        text-align: start;
        border-bottom: 1px solid var(--p-surface-200);
      }
    `,
  ],
})
export class DevicesListComponent implements OnInit {
  private readonly facade = inject(DevicesFacade);
  private readonly language = inject(LanguageService);
  protected readonly list = this.facade.list;
  protected readonly options = this.facade.options;
  protected readonly loading = this.facade.loading;
  protected readonly error = this.facade.errorCode;
  protected readonly search = signal("");
  protected readonly status = signal("all");
  protected readonly branch = signal("");
  protected readonly page = signal(1);
  protected readonly c = computed(() =>
    this.language.language() === "ar"
      ? {
          heading: "الأجهزة",
          support: "إدارة الأجهزة المقترنة بالفروع",
          create: "تسجيل جهاز",
          search: "بحث",
          status: "الحالة",
          branch: "الفرع",
          all: "الكل",
          pending: "بانتظار الاقتران",
          active: "نشط",
          revoked: "ملغي",
          loading: "جارٍ التحميل…",
          error: "تعذر تحميل الأجهزة.",
          name: "الاسم",
          type: "النوع",
          edit: "تعديل",
          empty: "لا توجد أجهزة.",
        }
      : {
          heading: "Devices",
          support: "Manage branch-bound paired devices",
          create: "Register device",
          search: "Search",
          status: "Status",
          branch: "Branch",
          all: "All",
          pending: "Pending pairing",
          active: "Active",
          revoked: "Revoked",
          loading: "Loading…",
          error: "Devices could not be loaded.",
          name: "Name",
          type: "Type",
          edit: "Edit",
          empty: "No devices found.",
        },
  );
  ngOnInit(): void {
    void this.refresh();
  }
  protected refresh(reset = false): Promise<void> {
    if (reset) this.page.set(1);
    return this.facade.loadList(
      this.status(),
      this.branch(),
      this.search(),
      this.page(),
    );
  }
}

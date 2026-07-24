import { Routes } from "@angular/router";
import { accessDeniedGuard, auditViewGuard, branchesManageGuard, branchesViewGuard, catalogManageGuard, catalogViewGuard, devicesManageGuard, loginGuard, managementGuard, rolesManageGuard, usersManageGuard, usersViewGuard } from "../src/app/core/auth/management-auth.guard";

export const routes: Routes = [
  { path: "", pathMatch: "full", redirectTo: "management/login" },
  {
    path: "management/login",
    canActivate: [loginGuard],
    loadComponent: () => import("../src/app/features/management-auth/management-login.component")
      .then(({ ManagementLoginComponent }) => ManagementLoginComponent)
  },
  {
    path: "management/access-denied",
    canActivate: [accessDeniedGuard],
    loadComponent: () => import("../src/app/features/management-auth/access-denied.component")
      .then(({ AccessDeniedComponent }) => AccessDeniedComponent)
  },
  { path: "device/pair", data: { mode: "pair" }, loadComponent: () => import("../src/app/features/device-auth/workstation.component").then(({ WorkstationComponent }) => WorkstationComponent) },
  { path: "workstation/login", data: { mode: "login" }, loadComponent: () => import("../src/app/features/device-auth/workstation.component").then(({ WorkstationComponent }) => WorkstationComponent) },
  { path: "workstation/locked", data: { mode: "locked" }, loadComponent: () => import("../src/app/features/device-auth/workstation.component").then(({ WorkstationComponent }) => WorkstationComponent) },
  { path: "workstation", data: { mode: "ready" }, loadComponent: () => import("../src/app/features/device-auth/workstation.component").then(({ WorkstationComponent }) => WorkstationComponent) },
  {
    path: "management",
    canActivate: [managementGuard],
    loadComponent: () => import("../src/app/features/management-shell/management-shell.component")
      .then(({ ManagementShellComponent }) => ManagementShellComponent),
    children: [
      {
        path: "",
        loadComponent: () => import("../src/app/features/management-shell/management-home.component")
          .then(({ ManagementHomeComponent }) => ManagementHomeComponent)
      },
      {
        path: "catalog/categories",
        canActivate: [catalogViewGuard],
        loadComponent: () => import("../src/app/features/management-catalog/categories-list.component")
          .then(({ CategoriesListComponent }) => CategoriesListComponent)
      },
      {
        path: "catalog/categories/new",
        canActivate: [catalogManageGuard],
        loadComponent: () => import("../src/app/features/management-catalog/category-editor.component")
          .then(({ CategoryEditorComponent }) => CategoryEditorComponent)
      },
      {
        path: "catalog/categories/:categoryId",
        canActivate: [catalogViewGuard],
        loadComponent: () => import("../src/app/features/management-catalog/category-editor.component")
          .then(({ CategoryEditorComponent }) => CategoryEditorComponent)
      },
      {
        path: "catalog/products",
        canActivate: [catalogViewGuard],
        loadComponent: () => import("../src/app/features/management-catalog/products-list.component")
          .then(({ ProductsListComponent }) => ProductsListComponent)
      },
      {
        path: "catalog/products/new",
        canActivate: [catalogManageGuard],
        loadComponent: () => import("../src/app/features/management-catalog/product-editor.component")
          .then(({ ProductEditorComponent }) => ProductEditorComponent)
      },
      {
        path: "catalog/products/:productId",
        canActivate: [catalogViewGuard],
        loadComponent: () => import("../src/app/features/management-catalog/product-editor.component")
          .then(({ ProductEditorComponent }) => ProductEditorComponent)
      },
      {
        path: "roles",
        canActivate: [rolesManageGuard],
        loadComponent: () => import("../src/app/features/management-roles/roles-list.component")
          .then(({ RolesListComponent }) => RolesListComponent)
      },
      {
        path: "roles/new",
        canActivate: [rolesManageGuard],
        loadComponent: () => import("../src/app/features/management-roles/role-editor.component")
          .then(({ RoleEditorComponent }) => RoleEditorComponent)
      },
      {
        path: "roles/:roleId",
        canActivate: [rolesManageGuard],
        loadComponent: () => import("../src/app/features/management-roles/role-editor.component")
          .then(({ RoleEditorComponent }) => RoleEditorComponent)
      },
      {
        path: "users",
        canActivate: [usersViewGuard],
        loadComponent: () => import("../src/app/features/management-users/users-list.component")
          .then(({ UsersListComponent }) => UsersListComponent)
      },
      {
        path: "users/new",
        canActivate: [usersManageGuard],
        loadComponent: () => import("../src/app/features/management-users/user-editor.component")
          .then(({ UserEditorComponent }) => UserEditorComponent)
      },
      {
        path: "users/:userId",
        canActivate: [usersViewGuard],
        loadComponent: () => import("../src/app/features/management-users/user-editor.component")
          .then(({ UserEditorComponent }) => UserEditorComponent)
      },
      {
        path: "branches",
        canActivate: [branchesViewGuard],
        loadComponent: () => import("../src/app/features/management-branches/branches-list.component")
          .then(({ BranchesListComponent }) => BranchesListComponent)
      },
      {
        path: "branches/new",
        canActivate: [branchesManageGuard],
        loadComponent: () => import("../src/app/features/management-branches/branch-editor.component")
          .then(({ BranchEditorComponent }) => BranchEditorComponent)
      },
      {
        path: "branches/:branchId",
        canActivate: [branchesViewGuard],
        loadComponent: () => import("../src/app/features/management-branches/branch-editor.component")
          .then(({ BranchEditorComponent }) => BranchEditorComponent)
      },
      {
        path: "devices",
        canActivate: [devicesManageGuard],
        loadComponent: () => import("../src/app/features/management-devices/devices-list.component")
          .then(({ DevicesListComponent }) => DevicesListComponent)
      },
      {
        path: "devices/new",
        canActivate: [devicesManageGuard],
        loadComponent: () => import("../src/app/features/management-devices/device-editor.component")
          .then(({ DeviceEditorComponent }) => DeviceEditorComponent)
      },
      {
        path: "devices/:deviceId",
        canActivate: [devicesManageGuard],
        loadComponent: () => import("../src/app/features/management-devices/device-editor.component")
          .then(({ DeviceEditorComponent }) => DeviceEditorComponent)
      },
      {
        path: "audit-logs",
        canActivate: [auditViewGuard],
        loadComponent: () => import("../src/app/features/management-audit-logs/audit-logs-list.component")
          .then(({ AuditLogsListComponent }) => AuditLogsListComponent)
      },
      {
        path: "audit-logs/:auditLogId",
        canActivate: [auditViewGuard],
        loadComponent: () => import("../src/app/features/management-audit-logs/audit-log-detail.component")
          .then(({ AuditLogDetailComponent }) => AuditLogDetailComponent)
      }
    ]
  },
  {
    path: "__e2e/primeng-accessibility",
    loadComponent: () => import("./primeng-accessibility-fixture.component")
      .then(({ PrimeNgAccessibilityFixtureComponent }) => PrimeNgAccessibilityFixtureComponent)
  },
  { path: "**", redirectTo: "management/login" }
];

import { Routes } from "@angular/router";
import { accessDeniedGuard, loginGuard, managementGuard, rolesManageGuard } from "../src/app/core/auth/management-auth.guard";

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

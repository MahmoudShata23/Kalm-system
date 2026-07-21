import { Routes } from "@angular/router";
import { accessDeniedGuard, loginGuard, managementGuard, rolesManageGuard } from "./core/auth/management-auth.guard";

export const routes: Routes = [
  { path: "", pathMatch: "full", redirectTo: "management/login" },
  {
    path: "management/login",
    canActivate: [loginGuard],
    loadComponent: () => import("./features/management-auth/management-login.component")
      .then(({ ManagementLoginComponent }) => ManagementLoginComponent)
  },
  {
    path: "management/access-denied",
    canActivate: [accessDeniedGuard],
    loadComponent: () => import("./features/management-auth/access-denied.component")
      .then(({ AccessDeniedComponent }) => AccessDeniedComponent)
  },
  {
    path: "management",
    canActivate: [managementGuard],
    loadComponent: () => import("./features/management-shell/management-shell.component")
      .then(({ ManagementShellComponent }) => ManagementShellComponent),
    children: [
      {
        path: "",
        loadComponent: () => import("./features/management-shell/management-home.component")
          .then(({ ManagementHomeComponent }) => ManagementHomeComponent)
      },
      {
        path: "roles",
        canActivate: [rolesManageGuard],
        loadComponent: () => import("./features/management-roles/roles-list.component")
          .then(({ RolesListComponent }) => RolesListComponent)
      },
      {
        path: "roles/new",
        canActivate: [rolesManageGuard],
        loadComponent: () => import("./features/management-roles/role-editor.component")
          .then(({ RoleEditorComponent }) => RoleEditorComponent)
      },
      {
        path: "roles/:roleId",
        canActivate: [rolesManageGuard],
        loadComponent: () => import("./features/management-roles/role-editor.component")
          .then(({ RoleEditorComponent }) => RoleEditorComponent)
      }
    ]
  },
  { path: "**", redirectTo: "management/login" }
];

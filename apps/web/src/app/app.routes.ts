import { Routes } from "@angular/router";
import { accessDeniedGuard, loginGuard, managementGuard } from "./core/auth/management-auth.guard";

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
      .then(({ ManagementShellComponent }) => ManagementShellComponent)
  },
  { path: "**", redirectTo: "management/login" }
];

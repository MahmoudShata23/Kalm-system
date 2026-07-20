import { Routes } from "@angular/router";

export const routes: Routes = [
  { path: "", pathMatch: "full", redirectTo: "management/login" },
  {
    path: "management/login",
    loadComponent: () => import("../src/app/features/management-auth/management-login.component")
      .then(({ ManagementLoginComponent }) => ManagementLoginComponent)
  },
  {
    path: "__e2e/primeng-accessibility",
    loadComponent: () => import("./primeng-accessibility-fixture.component")
      .then(({ PrimeNgAccessibilityFixtureComponent }) => PrimeNgAccessibilityFixtureComponent)
  },
  { path: "**", redirectTo: "management/login" }
];

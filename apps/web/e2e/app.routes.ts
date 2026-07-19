import { Routes } from "@angular/router";

export const routes: Routes = [
  {
    path: "__e2e/primeng-accessibility",
    loadComponent: () => import("./primeng-accessibility-fixture.component")
      .then(({ PrimeNgAccessibilityFixtureComponent }) => PrimeNgAccessibilityFixtureComponent)
  }
];

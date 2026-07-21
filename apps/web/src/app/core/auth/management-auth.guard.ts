import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { ManagementAuthService } from "./management-auth.service";
import { MANAGEMENT_ACCESS_PERMISSION, ROLES_MANAGE_PERMISSION } from "./management-permissions";

export const managementGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(ManagementAuthService);
  const router = inject(Router);
  await auth.ensureInitialized();
  if (!auth.user().isAuthenticated) {
    return router.createUrlTree(["/management/login"], { queryParams: { returnUrl: state.url } });
  }

  return auth.hasPermission(MANAGEMENT_ACCESS_PERMISSION)
    ? true
    : router.createUrlTree(["/management/access-denied"]);
};

export const loginGuard: CanActivateFn = async () => {
  const auth = inject(ManagementAuthService);
  const router = inject(Router);
  await auth.ensureInitialized();
  if (!auth.user().isAuthenticated) {
    return true;
  }

  return router.createUrlTree([
    auth.hasPermission(MANAGEMENT_ACCESS_PERMISSION) ? "/management" : "/management/access-denied"
  ]);
};

export const accessDeniedGuard: CanActivateFn = async () => {
  const auth = inject(ManagementAuthService);
  const router = inject(Router);
  await auth.ensureInitialized();
  return auth.user().isAuthenticated ? true : router.createUrlTree(["/management/login"]);
};

export const rolesManageGuard: CanActivateFn = async () => {
  const auth = inject(ManagementAuthService);
  const router = inject(Router);
  await auth.ensureInitialized();
  if (!auth.user().isAuthenticated) {
    return router.createUrlTree(["/management/login"]);
  }

  return auth.hasPermission(MANAGEMENT_ACCESS_PERMISSION) && auth.hasPermission(ROLES_MANAGE_PERMISSION)
    ? true
    : router.createUrlTree(["/management/access-denied"]);
};

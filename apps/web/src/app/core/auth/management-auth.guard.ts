import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { ManagementAuthService } from "./management-auth.service";
import { AUDIT_VIEW_PERMISSION, BRANCHES_MANAGE_PERMISSION, BRANCHES_VIEW_PERMISSION, CATALOG_MANAGE_PERMISSION, CATALOG_VIEW_PERMISSION, DEVICES_MANAGE_PERMISSION, MANAGEMENT_ACCESS_PERMISSION, ROLES_MANAGE_PERMISSION, USERS_MANAGE_PERMISSION, USERS_VIEW_PERMISSION } from "./management-permissions";

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

const userPermissionGuard = (permission: string): CanActivateFn => async () => {
  const auth = inject(ManagementAuthService);
  const router = inject(Router);
  await auth.ensureInitialized();
  if (!auth.user().isAuthenticated) return router.createUrlTree(["/management/login"]);
  return auth.hasPermission(MANAGEMENT_ACCESS_PERMISSION) && auth.hasPermission(permission)
    ? true
    : router.createUrlTree(["/management/access-denied"]);
};

export const usersViewGuard = userPermissionGuard(USERS_VIEW_PERMISSION);
export const usersManageGuard = userPermissionGuard(USERS_MANAGE_PERMISSION);
export const devicesManageGuard = userPermissionGuard(DEVICES_MANAGE_PERMISSION);
export const branchesViewGuard = userPermissionGuard(BRANCHES_VIEW_PERMISSION);
export const branchesManageGuard = userPermissionGuard(BRANCHES_MANAGE_PERMISSION);
export const auditViewGuard = userPermissionGuard(AUDIT_VIEW_PERMISSION);
export const catalogViewGuard = userPermissionGuard(CATALOG_VIEW_PERMISSION);
export const catalogManageGuard = userPermissionGuard(CATALOG_MANAGE_PERMISSION);

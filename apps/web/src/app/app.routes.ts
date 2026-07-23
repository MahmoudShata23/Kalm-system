import { Routes } from "@angular/router";
import { accessDeniedGuard, auditViewGuard, branchesManageGuard, branchesViewGuard, devicesManageGuard, loginGuard, managementGuard, rolesManageGuard, usersManageGuard, usersViewGuard } from "./core/auth/management-auth.guard";

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
  { path: "device/pair", data: { mode: "pair" }, loadComponent: () => import("./features/device-auth/workstation.component").then(({ WorkstationComponent }) => WorkstationComponent) },
  { path: "workstation/login", data: { mode: "login" }, loadComponent: () => import("./features/device-auth/workstation.component").then(({ WorkstationComponent }) => WorkstationComponent) },
  { path: "workstation/locked", data: { mode: "locked" }, loadComponent: () => import("./features/device-auth/workstation.component").then(({ WorkstationComponent }) => WorkstationComponent) },
  { path: "workstation", data: { mode: "ready" }, loadComponent: () => import("./features/device-auth/workstation.component").then(({ WorkstationComponent }) => WorkstationComponent) },
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
      },
      {
        path: "users",
        canActivate: [usersViewGuard],
        loadComponent: () => import("./features/management-users/users-list.component")
          .then(({ UsersListComponent }) => UsersListComponent)
      },
      {
        path: "users/new",
        canActivate: [usersManageGuard],
        loadComponent: () => import("./features/management-users/user-editor.component")
          .then(({ UserEditorComponent }) => UserEditorComponent)
      },
      {
        path: "users/:userId",
        canActivate: [usersViewGuard],
        loadComponent: () => import("./features/management-users/user-editor.component")
          .then(({ UserEditorComponent }) => UserEditorComponent)
      },
      {
        path: "branches",
        canActivate: [branchesViewGuard],
        loadComponent: () => import("./features/management-branches/branches-list.component").then(({ BranchesListComponent }) => BranchesListComponent)
      },
      {
        path: "branches/new",
        canActivate: [branchesManageGuard],
        loadComponent: () => import("./features/management-branches/branch-editor.component").then(({ BranchEditorComponent }) => BranchEditorComponent)
      },
      {
        path: "branches/:branchId",
        canActivate: [branchesViewGuard],
        loadComponent: () => import("./features/management-branches/branch-editor.component").then(({ BranchEditorComponent }) => BranchEditorComponent)
      },
      {
        path: "devices",
        canActivate: [devicesManageGuard],
        loadComponent: () => import("./features/management-devices/devices-list.component").then(({ DevicesListComponent }) => DevicesListComponent)
      },
      {
        path: "devices/new",
        canActivate: [devicesManageGuard],
        loadComponent: () => import("./features/management-devices/device-editor.component").then(({ DeviceEditorComponent }) => DeviceEditorComponent)
      },
      {
        path: "devices/:deviceId",
        canActivate: [devicesManageGuard],
        loadComponent: () => import("./features/management-devices/device-editor.component").then(({ DeviceEditorComponent }) => DeviceEditorComponent)
      },
      {
        path: "audit-logs",
        canActivate: [auditViewGuard],
        loadComponent: () => import("./features/management-audit-logs/audit-logs-list.component").then(({ AuditLogsListComponent }) => AuditLogsListComponent)
      },
      {
        path: "audit-logs/:auditLogId",
        canActivate: [auditViewGuard],
        loadComponent: () => import("./features/management-audit-logs/audit-log-detail.component").then(({ AuditLogDetailComponent }) => AuditLogDetailComponent)
      }
    ]
  },
  { path: "**", redirectTo: "management/login" }
];

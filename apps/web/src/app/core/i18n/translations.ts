import type { Language } from "./language.service";

export interface ManagementLoginCopy {
  managementAccess: string;
  heading: string;
  supporting: string;
  identifier: string;
  identifierRequired: string;
  password: string;
  submit: string;
  genericError: string;
  logoutError: string;
  welcome: string;
  signedIn: string;
  logout: string;
}

export interface ShellCopy {
  brand: string;
  sessionState: string;
  sessionAuthenticated: string;
  languageToggleLabel: string;
  managementLogin: ManagementLoginCopy;
}

export const TRANSLATIONS: Record<Language, ShellCopy> = {
  en: {
    brand: "Kalm Cafe",
    sessionState: "Not signed in",
    sessionAuthenticated: "Signed in",
    languageToggleLabel: "Language",
    managementLogin: {
      managementAccess: "Management access",
      heading: "Welcome back",
      supporting: "Sign in with your management username or email.",
      identifier: "Username or email",
      identifierRequired: "Enter your username or email.",
      password: "Password",
      submit: "Sign in",
      genericError: "Sign-in was unsuccessful. Check your details and try again.",
      logoutError: "Sign-out was unsuccessful. Please try again.",
      welcome: "Welcome",
      signedIn: "Your secure management session is active.",
      logout: "Sign out"
    }
  },
  ar: {
    brand: "كالم كافيه",
    sessionState: "لم يتم تسجيل الدخول",
    sessionAuthenticated: "تم تسجيل الدخول",
    languageToggleLabel: "اللغة",
    managementLogin: {
      managementAccess: "دخول الإدارة",
      heading: "مرحبًا بعودتك",
      supporting: "سجّل الدخول باسم مستخدم الإدارة أو البريد الإلكتروني.",
      identifier: "اسم المستخدم أو البريد الإلكتروني",
      identifierRequired: "أدخل اسم المستخدم أو البريد الإلكتروني.",
      password: "كلمة المرور",
      submit: "تسجيل الدخول",
      genericError: "تعذّر تسجيل الدخول. تحقّق من البيانات وحاول مرة أخرى.",
      logoutError: "تعذّر تسجيل الخروج. حاول مرة أخرى.",
      welcome: "مرحبًا",
      signedIn: "جلسة الإدارة الآمنة نشطة.",
      logout: "تسجيل الخروج"
    }
  }
};

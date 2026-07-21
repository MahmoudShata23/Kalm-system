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
  accessDenied: {
    heading: string;
    message: string;
    logout: string;
  };
  managementShell: {
    eyebrow: string;
    welcome: string;
    logout: string;
    branchAccess: string;
    assignedBranches: string;
    allBranches: string;
    authorizedBranches: string;
    operationalBranches: string;
    noModulesHeading: string;
    noModulesMessage: string;
  };
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
    },
    accessDenied: {
      heading: "Access denied",
      message: "Your account is signed in, but it does not currently have permission to use management tools.",
      logout: "Sign out"
    },
    managementShell: {
      eyebrow: "Management",
      welcome: "Welcome",
      logout: "Sign out",
      branchAccess: "Branch access",
      assignedBranches: "Assigned branches",
      allBranches: "All organization branches",
      authorizedBranches: "Branches in scope",
      operationalBranches: "Operational branches",
      noModulesHeading: "Management foundation ready",
      noModulesMessage: "No management modules are enabled in this slice. Future tools will appear here only when their server permissions are granted."
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
    },
    accessDenied: {
      heading: "غير مصرح بالدخول",
      message: "تم تسجيل دخولك، لكن حسابك لا يملك حالياً صلاحية استخدام أدوات الإدارة.",
      logout: "تسجيل الخروج"
    },
    managementShell: {
      eyebrow: "الإدارة",
      welcome: "مرحباً",
      logout: "تسجيل الخروج",
      branchAccess: "نطاق الفروع",
      assignedBranches: "الفروع المعيّنة",
      allBranches: "جميع فروع المؤسسة",
      authorizedBranches: "الفروع ضمن النطاق",
      operationalBranches: "الفروع التشغيلية",
      noModulesHeading: "أساس الإدارة جاهز",
      noModulesMessage: "لا توجد وحدات إدارة مفعّلة في هذه الشريحة. ستظهر الأدوات المستقبلية هنا فقط عند منح صلاحياتها من الخادم."
    }
  }
};

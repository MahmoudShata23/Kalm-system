import type { Language } from "../../core/i18n/language.service";

export const USERS_COPY: Record<Language, Record<string, string>> = {
  en: {
    nav: "Users", heading: "Users", supporting: "Provision employee access securely across roles and branches.",
    create: "Create user", search: "Search users", searchPlaceholder: "Username, name, or email", status: "Status",
    active: "Active", suspended: "Suspended", archived: "Archived", all: "All", username: "Username",
    displayName: "Display name", email: "Email (optional)", preferredLanguage: "Preferred language", english: "English", arabic: "Arabic",
    roles: "Roles", branches: "Branches", actions: "Actions", edit: "Open", previous: "Previous", next: "Next",
    empty: "No users match this view.", loadError: "Users could not be loaded.", retry: "Try again", loading: "Loading users…",
    newHeading: "Create employee user", editHeading: "User details", save: "Save user", saving: "Saving…", cancel: "Back to users",
    required: "Complete the required fields.", roleRequired: "Select at least one active role.", branchRequired: "Select at least one active branch.",
    assignedBranches: "Assigned branches", allBranches: "All organization branches", initialPassword: "Initial password (optional)",
    initialPasswordHelp: "Leave blank to keep the credential pending setup.", credential: "Password credential", credentialActive: "Active",
    credentialPending: "Pending setup", password: "Administrator-set password", passwordHelp: "15–128 characters. The value is never displayed again.",
    setPassword: "Set / reset password", activate: "Activate user", suspend: "Suspend user", suspendHeading: "Suspend this user?",
    suspendMessage: "The account will lose access immediately and all active sessions will be revoked.", confirmSuspend: "Suspend",
    dismiss: "Cancel", conflictHeading: "A newer version is available", conflictMessage: "Your draft is preserved. Reload only when you are ready to replace it.",
    reloadLatest: "Reload latest", saveError: "The user change could not be completed.", activationRequirements: "Set a password and valid roles and branch access before activation.",
    lastManagement: "This change would remove the final management-capable user.", reauthentication: "Sign in again before setting a password.",
    saved: "User saved.", activatedNotice: "User activated.", suspendedNotice: "User suspended.", passwordNotice: "Password updated and active sessions revoked.",
    readOnly: "Archived users are read-only."
  },
  ar: {
    nav: "المستخدمون", heading: "المستخدمون", supporting: "إعداد وصول الموظفين بأمان عبر الأدوار والفروع.",
    create: "إنشاء مستخدم", search: "البحث عن المستخدمين", searchPlaceholder: "اسم المستخدم أو الاسم أو البريد", status: "الحالة",
    active: "نشط", suspended: "موقوف", archived: "مؤرشف", all: "الكل", username: "اسم المستخدم",
    displayName: "الاسم المعروض", email: "البريد الإلكتروني (اختياري)", preferredLanguage: "اللغة المفضلة", english: "الإنجليزية", arabic: "العربية",
    roles: "الأدوار", branches: "الفروع", actions: "الإجراءات", edit: "فتح", previous: "السابق", next: "التالي",
    empty: "لا يوجد مستخدمون مطابقون.", loadError: "تعذر تحميل المستخدمين.", retry: "إعادة المحاولة", loading: "جارٍ تحميل المستخدمين…",
    newHeading: "إنشاء مستخدم موظف", editHeading: "تفاصيل المستخدم", save: "حفظ المستخدم", saving: "جارٍ الحفظ…", cancel: "العودة إلى المستخدمين",
    required: "أكمل الحقول المطلوبة.", roleRequired: "حدد دورًا نشطًا واحدًا على الأقل.", branchRequired: "حدد فرعًا نشطًا واحدًا على الأقل.",
    assignedBranches: "فروع محددة", allBranches: "كل فروع المؤسسة", initialPassword: "كلمة المرور الأولية (اختيارية)",
    initialPasswordHelp: "اتركها فارغة لإبقاء بيانات الاعتماد بانتظار الإعداد.", credential: "بيانات اعتماد كلمة المرور", credentialActive: "نشطة",
    credentialPending: "بانتظار الإعداد", password: "كلمة مرور يحددها المسؤول", passwordHelp: "من 15 إلى 128 حرفًا. لن تُعرض القيمة مرة أخرى.",
    setPassword: "تعيين / إعادة ضبط كلمة المرور", activate: "تنشيط المستخدم", suspend: "إيقاف المستخدم", suspendHeading: "إيقاف هذا المستخدم؟",
    suspendMessage: "سيفقد الحساب الوصول فورًا وستُلغى كل الجلسات النشطة.", confirmSuspend: "إيقاف",
    dismiss: "إلغاء", conflictHeading: "يتوفر إصدار أحدث", conflictMessage: "تم الاحتفاظ بمسودتك. حمّل الإصدار الأحدث عندما تكون مستعدًا لاستبدالها.",
    reloadLatest: "تحميل الأحدث", saveError: "تعذر إكمال تغيير المستخدم.", activationRequirements: "عيّن كلمة مرور وأدوارًا ووصولًا صالحًا للفروع قبل التنشيط.",
    lastManagement: "سيؤدي هذا التغيير إلى إزالة آخر مستخدم قادر على الإدارة.", reauthentication: "سجّل الدخول مرة أخرى قبل تعيين كلمة المرور.",
    saved: "تم حفظ المستخدم.", activatedNotice: "تم تنشيط المستخدم.", suspendedNotice: "تم إيقاف المستخدم.", passwordNotice: "تم تحديث كلمة المرور وإلغاء الجلسات النشطة.",
    readOnly: "المستخدمون المؤرشفون للقراءة فقط."
  }
};

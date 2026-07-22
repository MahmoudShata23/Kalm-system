export const BRANCHES_COPY = {
  en: {
    nav: "Branches", heading: "Branches", support: "Manage organization branches and operational status.", create: "Create branch",
    search: "Search", status: "Status", all: "All", setup: "Setup", active: "Active", suspended: "Inactive", archived: "Archived",
    name: "Branch name", code: "Branch code", locale: "Default language", timeZone: "Time zone", rollover: "Business-day rollover",
    edit: "Open", loading: "Loading…", empty: "No branches match these filters.", error: "Branches could not be loaded.", retry: "Try again",
    previous: "Previous", next: "Next", page: "Page", back: "Branches", createHeading: "Create branch", editHeading: "Branch details",
    english: "English", arabic: "Arabic", save: "Save branch", activate: "Activate branch", deactivate: "Deactivate branch",
    confirmActivate: "Activate this branch? Only the branch status will change.",
    confirmDeactivate: "Deactivate this branch? The operation is blocked while active dependencies remain.", cancel: "Cancel", confirm: "Confirm",
    conflict: "A newer branch version is available.", refreshVersion: "Load latest version and keep my draft", archivedReadOnly: "Archived branches are read-only.",
    dependencyTitle: "Deactivation is blocked", dependencyHelp: "Resolve these active dependencies before trying again.",
    registeredDevices: "Registered devices", activeDevices: "Active devices", activeCredentials: "Active credentials", activeSessions: "Active sessions", assignments: "Explicit user assignments",
    invalid: "Check the highlighted branch fields.", operationError: "The operation could not be completed."
  },
  ar: {
    nav: "الفروع", heading: "الفروع", support: "إدارة فروع المؤسسة وحالتها التشغيلية.", create: "إنشاء فرع",
    search: "بحث", status: "الحالة", all: "الكل", setup: "قيد الإعداد", active: "نشط", suspended: "غير نشط", archived: "مؤرشف",
    name: "اسم الفرع", code: "رمز الفرع", locale: "اللغة الافتراضية", timeZone: "المنطقة الزمنية", rollover: "وقت بداية يوم العمل",
    edit: "فتح", loading: "جارٍ التحميل…", empty: "لا توجد فروع مطابقة.", error: "تعذر تحميل الفروع.", retry: "إعادة المحاولة",
    previous: "السابق", next: "التالي", page: "صفحة", back: "الفروع", createHeading: "إنشاء فرع", editHeading: "تفاصيل الفرع",
    english: "الإنجليزية", arabic: "العربية", save: "حفظ الفرع", activate: "تفعيل الفرع", deactivate: "إلغاء تفعيل الفرع",
    confirmActivate: "هل تريد تفعيل هذا الفرع؟ ستتغير حالة الفرع فقط.",
    confirmDeactivate: "هل تريد إلغاء تفعيل هذا الفرع؟ ستُرفض العملية إذا بقيت تبعيات نشطة.", cancel: "إلغاء", confirm: "تأكيد",
    conflict: "توجد نسخة أحدث من بيانات الفرع.", refreshVersion: "تحميل النسخة الأحدث مع الاحتفاظ بمسودتي", archivedReadOnly: "الفروع المؤرشفة للقراءة فقط.",
    dependencyTitle: "تعذر إلغاء التفعيل", dependencyHelp: "عالج هذه التبعيات النشطة ثم حاول مرة أخرى.",
    registeredDevices: "أجهزة مسجلة", activeDevices: "أجهزة نشطة", activeCredentials: "بيانات اعتماد نشطة", activeSessions: "جلسات نشطة", assignments: "تعيينات مستخدمين صريحة",
    invalid: "تحقق من حقول الفرع المحددة.", operationError: "تعذر إكمال العملية."
  }
} as const;

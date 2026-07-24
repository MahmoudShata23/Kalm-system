export type AuditLanguage = "ar" | "en";

const ARABIC_ACTIONS: Record<string, string> = {
  organizationCreated: "إنشاء المؤسسة", organizationUpdated: "تحديث المؤسسة", organizationStatusChanged: "تغيير حالة المؤسسة",
  branchCreated: "إنشاء فرع", branchUpdated: "تحديث فرع", branchStatusChanged: "تغيير حالة الفرع", branchActivated: "تفعيل فرع",
  branchDeactivated: "إيقاف فرع", branchAdministrationRejected: "رفض إدارة الفرع", operationalBootstrapCompleted: "اكتمال التهيئة التشغيلية",
  passwordCredentialActivated: "تفعيل بيانات كلمة المرور", managementLoginSucceeded: "نجاح دخول الإدارة", managementLoginFailed: "فشل دخول الإدارة",
  managementAccountLocked: "قفل حساب الإدارة", managementLogoutSucceeded: "نجاح خروج الإدارة", managementSessionRevoked: "إلغاء جلسة الإدارة",
  systemRoleProvisioned: "تجهيز دور النظام", roleCreated: "إنشاء دور", roleRenamed: "إعادة تسمية دور",
  rolePermissionSetChanged: "تغيير صلاحيات الدور", roleArchived: "أرشفة دور", roleAdministrationRejected: "رفض إدارة الدور",
  lastManagementAccessProtectionTriggered: "تشغيل حماية آخر وصول إداري", userCreated: "إنشاء مستخدم", userProfileChanged: "تغيير ملف المستخدم",
  userActivated: "تفعيل مستخدم", userSuspended: "إيقاف مستخدم", userPasswordSet: "تعيين كلمة مرور", userPasswordReset: "إعادة تعيين كلمة مرور",
  userAdministrationRejected: "رفض إدارة المستخدم", userRoleAssigned: "إسناد دور للمستخدم", userRoleRevoked: "إلغاء دور المستخدم",
  userBranchAccessChanged: "تغيير وصول المستخدم للفروع", authorizationProvisioningCompleted: "اكتمال تجهيز التفويض",
  authorizationProvisioningFailed: "فشل تجهيز التفويض", managementAccessRevoked: "إلغاء الوصول الإداري",
  authorizationSessionsRevoked: "إلغاء جلسات التفويض", deviceRegistered: "تسجيل جهاز", deviceUpdated: "تحديث جهاز",
  devicePairingChallengeCreated: "إنشاء طلب إقران جهاز", devicePaired: "إقران جهاز", deviceCredentialRotated: "تدوير بيانات الجهاز",
  deviceRevoked: "إلغاء جهاز", userPinSet: "تعيين رمز الموظف", userPinReset: "إعادة تعيين رمز الموظف",
  pinLoginSucceeded: "نجاح الدخول بالرمز", pinLoginFailed: "فشل الدخول بالرمز", workstationLocked: "قفل محطة العمل",
  categoryCreated: "إنشاء تصنيف", categoryUpdated: "تحديث تصنيف", categoryActivated: "تفعيل تصنيف",
  categoryArchived: "أرشفة تصنيف", categoriesReordered: "إعادة ترتيب التصنيفات",
  productCreated: "إنشاء منتج", productUpdated: "تحديث منتج", productActivated: "تفعيل منتج",
  productArchived: "أرشفة منتج", variantCreated: "إنشاء متغير", variantUpdated: "تحديث متغير",
  variantActivated: "تفعيل متغير", variantArchived: "أرشفة متغير", variantsReordered: "إعادة ترتيب المتغيرات",
  catalogMutationRejected: "رفض تعديل الكتالوج"
};

function humanize(code: string): string {
  return code.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/^./, value => value.toUpperCase());
}

export function auditActionLabel(language: AuditLanguage, code: string): string {
  if (language === "ar") return ARABIC_ACTIONS[code] ?? `إجراء غير معروف: ${code}`;
  return code ? humanize(code) : "Unknown action";
}

export function auditResultLabel(language: AuditLanguage, code: string): string {
  const labels: Record<AuditLanguage, Record<string, string>> = {
    en: { succeeded: "Succeeded", failed: "Failed", denied: "Denied" },
    ar: { succeeded: "نجح", failed: "فشل", denied: "مرفوض" }
  };
  return labels[language][code] ?? (language === "ar" ? `نتيجة غير معروفة: ${code}` : `Unknown result: ${code}`);
}

export const AUDIT_COPY = {
  en: {
    heading: "Audit log", intro: "Review immutable operational events within your authorized branch scope.", filters: "Audit filters",
    from: "From (local time)", to: "To (local time)", action: "Action", result: "Result", branch: "Branch", actorId: "Actor ID",
    targetType: "Target type", targetId: "Target ID", correlationId: "Correlation ID", all: "All", apply: "Apply filters",
    loading: "Loading audit records…", empty: "No audit records match these filters.", error: "Audit records could not be loaded.", retry: "Retry",
    occurred: "Occurred at", actor: "Actor", target: "Target", summary: "Summary", open: "Open audit detail", previous: "Previous", next: "Next",
    detail: "Audit detail", back: "Back to audit log", copy: "Copy", copied: "Copied", reason: "Reason code", changedFields: "Changed fields",
    previousStatus: "Previous status", newStatus: "New status", safeMetadata: "Safe metadata", noMetadata: "No additional safe metadata is available.",
    pageSize: "Rows per page", unknownActor: "Unresolved actor", noBranch: "No accessible branch hint"
  },
  ar: {
    heading: "سجل التدقيق", intro: "راجع الأحداث التشغيلية غير القابلة للتغيير ضمن نطاق الفروع المصرح به.", filters: "مرشحات التدقيق",
    from: "من (الوقت المحلي)", to: "إلى (الوقت المحلي)", action: "الإجراء", result: "النتيجة", branch: "الفرع", actorId: "معرّف المنفذ",
    targetType: "نوع الهدف", targetId: "معرّف الهدف", correlationId: "معرّف الارتباط", all: "الكل", apply: "تطبيق المرشحات",
    loading: "جارٍ تحميل سجلات التدقيق…", empty: "لا توجد سجلات تطابق المرشحات.", error: "تعذر تحميل سجلات التدقيق.", retry: "إعادة المحاولة",
    occurred: "وقت الحدوث", actor: "المنفذ", target: "الهدف", summary: "الملخص", open: "فتح تفاصيل التدقيق", previous: "السابق", next: "التالي",
    detail: "تفاصيل التدقيق", back: "العودة إلى سجل التدقيق", copy: "نسخ", copied: "تم النسخ", reason: "رمز السبب", changedFields: "الحقول المتغيرة",
    previousStatus: "الحالة السابقة", newStatus: "الحالة الجديدة", safeMetadata: "بيانات وصفية آمنة", noMetadata: "لا توجد بيانات وصفية آمنة إضافية.",
    pageSize: "عدد الصفوف", unknownActor: "منفذ غير متاح", noBranch: "لا يوجد تلميح فرع متاح"
  }
} as const;

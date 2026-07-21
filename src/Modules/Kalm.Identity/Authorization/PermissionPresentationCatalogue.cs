using System.Collections.Frozen;

namespace Kalm.Identity.Authorization;

public sealed record PermissionPresentation(
    string Code,
    string GroupCode,
    int GroupOrder,
    int ItemOrder,
    string EnglishLabel,
    string EnglishDescription,
    string ArabicLabel,
    string ArabicDescription);

public static class PermissionPresentationCatalogue
{
    public const string Version = "2026.07.slice4.v1";

    private static readonly PermissionPresentation[] EntriesArray =
    [
        P(PermissionCodes.ManagementAccess, "management", 1, 1, "Management access", "Enter and use the protected management area.", "دخول الإدارة", "الدخول إلى منطقة الإدارة المحمية واستخدامها."),

        P(PermissionCodes.UsersView, "usersRoles", 2, 1, "View users", "View employee account information.", "عرض المستخدمين", "عرض معلومات حسابات الموظفين."),
        P(PermissionCodes.UsersManage, "usersRoles", 2, 2, "Manage users", "Create and maintain employee accounts.", "إدارة المستخدمين", "إنشاء حسابات الموظفين وصيانتها."),
        P(PermissionCodes.RolesManage, "usersRoles", 2, 3, "Manage roles", "Create roles and manage their permission sets.", "إدارة الأدوار", "إنشاء الأدوار وإدارة مجموعات صلاحياتها."),

        P(PermissionCodes.BranchesView, "branchesDevices", 3, 1, "View branches", "View branch configuration and status.", "عرض الفروع", "عرض إعدادات الفروع وحالتها."),
        P(PermissionCodes.BranchesManage, "branchesDevices", 3, 2, "Manage branches", "Create and maintain branch configuration.", "إدارة الفروع", "إنشاء إعدادات الفروع وصيانتها."),
        P(PermissionCodes.DevicesManage, "branchesDevices", 3, 3, "Manage devices", "Register and maintain operational devices.", "إدارة الأجهزة", "تسجيل أجهزة التشغيل وصيانتها."),
        P(PermissionCodes.PrintersManage, "branchesDevices", 3, 4, "Manage printers", "Configure printers and print destinations.", "إدارة الطابعات", "إعداد الطابعات ووجهات الطباعة."),

        P(PermissionCodes.CatalogView, "catalogPricing", 4, 1, "View catalog", "View products, variants, and availability.", "عرض الكتالوج", "عرض المنتجات والخيارات والتوفر."),
        P(PermissionCodes.CatalogManage, "catalogPricing", 4, 2, "Manage catalog", "Create and maintain catalog items.", "إدارة الكتالوج", "إنشاء عناصر الكتالوج وصيانتها."),
        P(PermissionCodes.PricesManage, "catalogPricing", 4, 3, "Manage prices", "Maintain price lists and effective prices.", "إدارة الأسعار", "صيانة قوائم الأسعار والأسعار الفعالة."),
        P(PermissionCodes.DiscountsConfigure, "catalogPricing", 4, 4, "Configure discounts", "Configure discount rules and thresholds.", "إعداد الخصومات", "إعداد قواعد الخصم وحدوده."),
        P(PermissionCodes.RecipesView, "catalogPricing", 4, 5, "View recipes", "View production recipes and versions.", "عرض الوصفات", "عرض وصفات الإنتاج وإصداراتها."),
        P(PermissionCodes.RecipesManage, "catalogPricing", 4, 6, "Manage recipes", "Create and version production recipes.", "إدارة الوصفات", "إنشاء وصفات الإنتاج وإدارة إصداراتها."),
        P(PermissionCodes.CostsView, "catalogPricing", 4, 7, "View costs", "View recipe and product cost information.", "عرض التكاليف", "عرض معلومات تكاليف الوصفات والمنتجات."),

        P(PermissionCodes.PosSell, "posOrders", 5, 1, "Sell at POS", "Create and complete point-of-sale orders.", "البيع في نقطة البيع", "إنشاء طلبات نقطة البيع وإكمالها."),
        P(PermissionCodes.PosDiscountBasic, "posOrders", 5, 2, "Apply basic discounts", "Apply discounts within the basic limit.", "تطبيق الخصم الأساسي", "تطبيق الخصومات ضمن الحد الأساسي."),
        P(PermissionCodes.PosDiscountOverride, "posOrders", 5, 3, "Override discounts", "Approve discounts above the basic limit.", "تجاوز حد الخصم", "اعتماد الخصومات التي تتجاوز الحد الأساسي."),
        P(PermissionCodes.PosVoid, "posOrders", 5, 4, "Void orders", "Void eligible orders with a reason.", "إلغاء الطلبات", "إلغاء الطلبات المؤهلة مع تسجيل السبب."),
        P(PermissionCodes.PosRefund, "posOrders", 5, 5, "Refund payments", "Create authorized payment refunds.", "رد المدفوعات", "إنشاء عمليات رد مدفوعات مصرح بها."),
        P(PermissionCodes.OrdersView, "posOrders", 5, 6, "View orders", "View order history and details.", "عرض الطلبات", "عرض سجل الطلبات وتفاصيلها."),
        P(PermissionCodes.OrdersReprint, "posOrders", 5, 7, "Reprint orders", "Reprint eligible order receipts.", "إعادة طباعة الطلبات", "إعادة طباعة إيصالات الطلبات المؤهلة."),
        P(PermissionCodes.OrdersEditSubmitted, "posOrders", 5, 8, "Edit submitted orders", "Change eligible orders after submission.", "تعديل الطلبات المرسلة", "تغيير الطلبات المؤهلة بعد إرسالها."),

        P(PermissionCodes.KdsView, "kds", 6, 1, "View KDS", "View production tickets on the KDS.", "عرض شاشة التحضير", "عرض تذاكر الإنتاج على شاشة التحضير."),
        P(PermissionCodes.KdsUpdate, "kds", 6, 2, "Update KDS", "Advance and recall production tickets.", "تحديث شاشة التحضير", "تحديث تذاكر الإنتاج واستدعاؤها."),

        P(PermissionCodes.ShiftsOpen, "shiftsCash", 7, 1, "Open shifts", "Open an operational cashier shift.", "فتح الورديات", "فتح وردية تشغيلية لأمين الصندوق."),
        P(PermissionCodes.ShiftsClose, "shiftsCash", 7, 2, "Close shifts", "Count and close an operational shift.", "إغلاق الورديات", "جرد وردية تشغيلية وإغلاقها."),
        P(PermissionCodes.ShiftsViewAll, "shiftsCash", 7, 3, "View all shifts", "View shifts belonging to other users.", "عرض كل الورديات", "عرض الورديات الخاصة بالمستخدمين الآخرين."),
        P(PermissionCodes.ShiftsReopen, "shiftsCash", 7, 4, "Reopen shifts", "Reopen a closed shift with authorization.", "إعادة فتح الورديات", "إعادة فتح وردية مغلقة بتصريح."),
        P(PermissionCodes.CashPayIn, "shiftsCash", 7, 5, "Record cash pay-in", "Record authorized cash added to a drawer.", "تسجيل إضافة نقدية", "تسجيل النقد المضاف إلى الدرج بتصريح."),
        P(PermissionCodes.CashPayOut, "shiftsCash", 7, 6, "Record cash pay-out", "Record authorized cash removed from a drawer.", "تسجيل صرف نقدي", "تسجيل النقد المصروف من الدرج بتصريح."),
        P(PermissionCodes.CashSafeDrop, "shiftsCash", 7, 7, "Record safe drops", "Move drawer cash to the safe.", "تسجيل إيداع الخزنة", "نقل النقد من الدرج إلى الخزنة."),
        P(PermissionCodes.CashViewExpected, "shiftsCash", 7, 8, "View expected cash", "View expected drawer cash after counting.", "عرض النقد المتوقع", "عرض النقد المتوقع في الدرج بعد الجرد."),

        P(PermissionCodes.InventoryView, "inventory", 8, 1, "View inventory", "View stock items, balances, and movements.", "عرض المخزون", "عرض أصناف المخزون وأرصدته وحركاته."),
        P(PermissionCodes.InventoryReceive, "inventory", 8, 2, "Receive inventory", "Post authorized inventory receipts.", "استلام المخزون", "ترحيل استلامات المخزون المصرح بها."),
        P(PermissionCodes.InventoryTransfer, "inventory", 8, 3, "Transfer inventory", "Transfer stock between locations.", "نقل المخزون", "نقل المخزون بين المواقع."),
        P(PermissionCodes.InventoryCount, "inventory", 8, 4, "Count inventory", "Create and post physical stock counts.", "جرد المخزون", "إنشاء جرد فعلي للمخزون وترحيله."),
        P(PermissionCodes.InventoryAdjust, "inventory", 8, 5, "Adjust inventory", "Post authorized stock adjustments.", "تسوية المخزون", "ترحيل تسويات المخزون المصرح بها."),
        P(PermissionCodes.InventoryWaste, "inventory", 8, 6, "Record waste", "Record inventory waste and reasons.", "تسجيل الهالك", "تسجيل هالك المخزون وأسبابه."),
        P(PermissionCodes.InventoryCostView, "inventory", 8, 7, "View inventory cost", "View inventory valuation and item costs.", "عرض تكلفة المخزون", "عرض تقييم المخزون وتكاليف الأصناف."),

        P(PermissionCodes.PurchasingView, "purchasingSuppliers", 9, 1, "View purchasing", "View purchasing documents and status.", "عرض المشتريات", "عرض مستندات المشتريات وحالتها."),
        P(PermissionCodes.PurchasingCreate, "purchasingSuppliers", 9, 2, "Create purchases", "Create purchasing documents.", "إنشاء المشتريات", "إنشاء مستندات المشتريات."),
        P(PermissionCodes.PurchasingApprove, "purchasingSuppliers", 9, 3, "Approve purchases", "Approve eligible purchasing documents.", "اعتماد المشتريات", "اعتماد مستندات المشتريات المؤهلة."),
        P(PermissionCodes.PurchasingReceive, "purchasingSuppliers", 9, 4, "Receive purchases", "Post goods received from suppliers.", "استلام المشتريات", "ترحيل البضائع المستلمة من الموردين."),
        P(PermissionCodes.SuppliersManage, "purchasingSuppliers", 9, 5, "Manage suppliers", "Create and maintain supplier records.", "إدارة الموردين", "إنشاء سجلات الموردين وصيانتها."),
        P(PermissionCodes.SupplierPaymentsManage, "purchasingSuppliers", 9, 6, "Manage supplier payments", "Record and maintain supplier payments.", "إدارة مدفوعات الموردين", "تسجيل مدفوعات الموردين وصيانتها."),

        P(PermissionCodes.ExpensesView, "expenses", 10, 1, "View expenses", "View expense records and attachments.", "عرض المصروفات", "عرض سجلات المصروفات ومرفقاتها."),
        P(PermissionCodes.ExpensesCreate, "expenses", 10, 2, "Create expenses", "Create operational expense records.", "إنشاء المصروفات", "إنشاء سجلات المصروفات التشغيلية."),
        P(PermissionCodes.ExpensesApprove, "expenses", 10, 3, "Approve expenses", "Approve eligible expense records.", "اعتماد المصروفات", "اعتماد سجلات المصروفات المؤهلة."),
        P(PermissionCodes.ExpensesManage, "expenses", 10, 4, "Manage expenses", "Maintain expense categories and records.", "إدارة المصروفات", "صيانة فئات المصروفات وسجلاتها."),

        P(PermissionCodes.ReportsSales, "reports", 11, 1, "View sales reports", "View sales and order reports.", "عرض تقارير المبيعات", "عرض تقارير المبيعات والطلبات."),
        P(PermissionCodes.ReportsCost, "reports", 11, 2, "View cost reports", "View cost and gross-profit reports.", "عرض تقارير التكلفة", "عرض تقارير التكلفة وإجمالي الربح."),
        P(PermissionCodes.ReportsInventory, "reports", 11, 3, "View inventory reports", "View inventory movement and variance reports.", "عرض تقارير المخزون", "عرض تقارير حركة المخزون وفروق الجرد."),
        P(PermissionCodes.ReportsFinance, "reports", 11, 4, "View finance reports", "View financial management reports.", "عرض التقارير المالية", "عرض تقارير الإدارة المالية."),
        P(PermissionCodes.ReportsExport, "reports", 11, 5, "Export reports", "Export authorized report data.", "تصدير التقارير", "تصدير بيانات التقارير المصرح بها."),

        P(PermissionCodes.AuditView, "auditSettingsBackups", 12, 1, "View audit log", "View authorized audit events.", "عرض سجل التدقيق", "عرض أحداث التدقيق المصرح بها."),
        P(PermissionCodes.SettingsManage, "auditSettingsBackups", 12, 2, "Manage settings", "Maintain organization operational settings.", "إدارة الإعدادات", "صيانة إعدادات تشغيل المؤسسة."),
        P(PermissionCodes.BackupsManage, "auditSettingsBackups", 12, 3, "Manage backups", "Run and verify authorized backup operations.", "إدارة النسخ الاحتياطية", "تشغيل عمليات النسخ الاحتياطي المصرح بها والتحقق منها.")
    ];

    private static readonly FrozenDictionary<string, PermissionPresentation> EntriesByCode =
        EntriesArray.ToFrozenDictionary(entry => entry.Code, StringComparer.Ordinal);

    public static IReadOnlyList<PermissionPresentation> All { get; } = Array.AsReadOnly(EntriesArray);

    public static bool TryGet(string code, out PermissionPresentation? presentation)
        => EntriesByCode.TryGetValue(code, out presentation);

    private static PermissionPresentation P(
        string code,
        string groupCode,
        int groupOrder,
        int itemOrder,
        string englishLabel,
        string englishDescription,
        string arabicLabel,
        string arabicDescription)
        => new(code, groupCode, groupOrder, itemOrder, englishLabel, englishDescription, arabicLabel, arabicDescription);
}

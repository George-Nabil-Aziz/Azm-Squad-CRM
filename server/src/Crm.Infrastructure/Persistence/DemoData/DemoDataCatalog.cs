namespace Crm.Infrastructure.Persistence.DemoData;

/// <summary>Static text and name lists for the Development demo data (Arabic and English).</summary>
internal static class DemoDataCatalog
{
    public const string CustomerEmailDomain = "@demo.crm.com";

    public sealed record StaffDef(string Email, string NameEn, string NameAr, string Role, int[] Departments, double Weight, double Speed, double ExtraBreach, double Quality);

    // Departments: 0 = Customer Support, 1 = Technical Support, 2 = Billing & Finance.
    public static readonly StaffDef[] Staff =
    [
        new("sara@crm.com", "Sara Al-Harbi", "سارة الحربي", "Agent", [0, 2], 3.0, 0.8, 0.00, 0.30),
        new("omar@crm.com", "Omar Khaled", "عمر خالد", "Agent", [0, 1], 2.5, 1.0, 0.02, 0.10),
        new("layla@crm.com", "Layla Hassan", "ليلى حسن", "Agent", [1], 2.0, 0.9, 0.00, 0.20),
        new("youssef@crm.com", "Youssef Mansour", "يوسف منصور", "Agent", [1, 2], 1.4, 1.2, 0.06, -0.10),
        new("noura@crm.com", "Noura Al-Qahtani", "نورة القحطاني", "Agent", [0], 1.0, 1.0, 0.03, 0.00),
        new("khaled@crm.com", "Khaled Ibrahim", "خالد إبراهيم", "Agent", [2, 0], 0.6, 1.3, 0.10, -0.30),
    ];

    public static readonly StaffDef Supervisor =
        new("supervisor@crm.com", "Hind Al-Otaibi", "هند العتيبي", "Supervisor", [0, 1, 2], 0, 1, 0, 0);

    /// <summary>The demo agent account some other change may have created: joins every department.</summary>
    public const string DemoAgentEmail = "agent@crm.com";

    public static readonly string[] DepartmentNames = ["Customer Support", "Technical Support", "Billing & Finance"];

    public static readonly string[] BranchNames = ["Riyadh Branch", "Cairo Branch"];

    public static readonly string[] CategoryNames = ["Billing", "Technical issue", "Orders & delivery", "Account", "Complaint"];

    // Template groups follow CategoryNames; the department of a group: Billing -> 2, Technical -> 1, others -> 0.
    public static readonly int[] GroupDepartment = [2, 1, 0, 1, 0];

    public static readonly double[] GroupWeights = [18, 28, 22, 14, 18];

    public static readonly (string Ar, string En)[] FirstNames =
    [
        ("أحمد", "Ahmed"), ("محمد", "Mohammed"), ("عبدالله", "Abdullah"), ("خالد", "Khaled"), ("فهد", "Fahad"),
        ("سلطان", "Sultan"), ("ماجد", "Majed"), ("تركي", "Turki"), ("نايف", "Nayef"), ("عمر", "Omar"),
        ("يوسف", "Youssef"), ("حسن", "Hassan"), ("إبراهيم", "Ibrahim"), ("مصطفى", "Mostafa"), ("طارق", "Tarek"),
        ("سارة", "Sara"), ("نورة", "Noura"), ("ريم", "Reem"), ("هند", "Hind"), ("منى", "Mona"),
        ("ليلى", "Layla"), ("فاطمة", "Fatima"), ("مريم", "Mariam"), ("دينا", "Dina"), ("هدى", "Huda"),
        ("لمى", "Lama"), ("أمل", "Amal"), ("رنا", "Rana"), ("سلمى", "Salma"), ("عائشة", "Aisha"),
    ];

    public static readonly (string Ar, string En)[] LastNames =
    [
        ("الحربي", "Al-Harbi"), ("القحطاني", "Al-Qahtani"), ("العتيبي", "Al-Otaibi"), ("الدوسري", "Al-Dosari"),
        ("الغامدي", "Al-Ghamdi"), ("الزهراني", "Al-Zahrani"), ("الشمري", "Al-Shammari"), ("المطيري", "Al-Mutairi"),
        ("السبيعي", "Al-Subaie"), ("البلوي", "Al-Balawi"), ("حسن", "Hassan"), ("عبدالرحمن", "Abdelrahman"),
        ("السيد", "El-Sayed"), ("منصور", "Mansour"), ("فتحي", "Fathy"), ("الشافعي", "El-Shafei"),
        ("بدوي", "Badawi"), ("عثمان", "Osman"), ("الجمال", "El-Gamal"), ("رضوان", "Radwan"),
    ];

    public static readonly (string Ar, string En)[] Companies =
    [
        ("شركة النور للتجارة", "Al Noor Trading Co."), ("مؤسسة الأفق للمقاولات", "Al Ofuq Contracting"),
        ("مجموعة الرياض الغذائية", "Riyadh Food Group"), ("شركة البحر الأحمر للشحن", "Red Sea Logistics"),
        ("مطاعم الديرة", "Al Deira Restaurants"), ("شركة الخليج للتقنية", "Gulf Tech Solutions"),
        ("صيدليات الشفاء", "Al Shifa Pharmacies"), ("شركة النيل للاستيراد", "Nile Import Company"),
        ("مكتب الدلتا للاستشارات", "Delta Consulting Office"), ("شركة الأهرام للأثاث", "Ahram Furniture"),
        ("مؤسسة الواحة للسياحة", "Al Waha Tourism"), ("شركة سما للإلكترونيات", "Sama Electronics"),
        ("مخابز الفجر", "Al Fajr Bakeries"), ("شركة المدينة للتطوير العقاري", "Madina Real Estate Development"),
        ("مركز الإسكندرية الطبي", "Alexandria Medical Center"),
    ];

    // Saudi cities map to the Riyadh branch, Egyptian cities to the Cairo branch.
    public static readonly (string Ar, string En)[] SaudiCities =
        [("الرياض", "Riyadh"), ("جدة", "Jeddah"), ("الدمام", "Dammam"), ("مكة المكرمة", "Makkah"), ("المدينة المنورة", "Madinah"), ("الخبر", "Khobar")];

    public static readonly (string Ar, string En)[] EgyptCities =
        [("القاهرة", "Cairo"), ("الإسكندرية", "Alexandria"), ("الجيزة", "Giza"), ("المنصورة", "Mansoura")];

    public static readonly string[] CustomerNotesEn =
    [
        "Prefers to be contacted on WhatsApp in the morning.",
        "VIP customer, handle with priority. Based in {city}.",
        "Asked for a monthly invoice summary by email.",
        "Works at {company}; the account is shared with a colleague.",
        "Called to confirm the delivery address in {city}.",
    ];

    public static readonly string[] CustomerNotesAr =
    [
        "يفضل التواصل عبر واتساب في الصباح.",
        "عميل مهم، يُعامل بأولوية. يقيم في {city}.",
        "طلب ملخص فواتير شهري عبر البريد الإلكتروني.",
        "يعمل في {company}، والحساب مشترك مع زميل.",
        "اتصل لتأكيد عنوان التسليم في {city}.",
    ];

    // Ticket templates per group (Billing, Technical issue, Orders & delivery, Account, Complaint): {ref} is a reference number.
    public static readonly (string Subject, string Body)[][] TicketsEn =
    [
        [
            ("Charged twice for invoice {ref}", "Hello, I was charged twice for invoice {ref} on my card this month. Please check and refund the extra amount."),
            ("Need a copy of invoice {ref}", "Could you please send me a copy of invoice {ref}? I need it for our accounting team."),
            ("Question about the subscription renewal price", "My subscription renewed at a higher price than last year (ref {ref}). Can you explain the difference?"),
        ],
        [
            ("The app crashes when I open my orders", "Since the last update the app closes as soon as I open the orders page. I tried reinstalling it. Ref {ref}."),
            ("Cannot connect to the service", "I get a connection error every time I try to sign in from the office network. It worked yesterday. Ref {ref}."),
            ("Notifications are not arriving", "I stopped receiving notifications on my phone about two days ago, although they are enabled. Ref {ref}."),
        ],
        [
            ("Order {ref} has not arrived", "I placed order {ref} a week ago and the tracking page has not changed. Where is my order?"),
            ("Wrong item delivered in order {ref}", "I received a different item than the one I ordered ({ref}). I would like an exchange."),
            ("Change the delivery address for order {ref}", "Please change the delivery address of order {ref} to my new office address before it ships."),
        ],
        [
            ("I forgot my password", "I cannot sign in and the reset email does not arrive. My reference is {ref}. Please help me recover my account."),
            ("Update the phone number on my account", "I changed my phone number and need to update it on my account. Ref {ref}."),
            ("Please close my account", "I would like to close my account (ref {ref}) and delete my personal data."),
        ],
        [
            ("Very disappointed with the service", "I have been waiting for an answer for days (ref {ref}) and nobody replied. This is not acceptable."),
            ("Rude behaviour from a delivery driver", "The driver delivering order {ref} was rude and left the package outside the door. I want to file a complaint."),
            ("Repeated problem not solved", "This is the third time I report the same problem (ref {ref}) and it is still not fixed."),
        ],
    ];

    public static readonly (string Subject, string Body)[][] TicketsAr =
    [
        [
            ("تم خصم مبلغ الفاتورة {ref} مرتين", "السلام عليكم، تم خصم قيمة الفاتورة {ref} مرتين من بطاقتي هذا الشهر. أرجو المراجعة وإرجاع المبلغ الزائد."),
            ("أحتاج نسخة من الفاتورة {ref}", "هل يمكن إرسال نسخة من الفاتورة {ref}؟ أحتاجها لقسم المحاسبة لدينا."),
            ("استفسار عن سعر تجديد الاشتراك", "تم تجديد اشتراكي بسعر أعلى من العام الماضي (مرجع {ref}). هل يمكن توضيح سبب الفرق؟"),
        ],
        [
            ("التطبيق يتوقف عند فتح الطلبات", "بعد آخر تحديث يغلق التطبيق فور فتح صفحة الطلبات. جربت إعادة التثبيت دون فائدة. مرجع {ref}."),
            ("لا أستطيع الاتصال بالخدمة", "تظهر لي رسالة خطأ في الاتصال عند تسجيل الدخول من شبكة المكتب. كان يعمل بالأمس. مرجع {ref}."),
            ("الإشعارات لا تصلني", "توقفت الإشعارات على هاتفي منذ يومين رغم أنها مفعّلة. مرجع {ref}."),
        ],
        [
            ("الطلب {ref} لم يصل حتى الآن", "قدمت الطلب {ref} قبل أسبوع وصفحة التتبع لم تتغير. أين طلبي؟"),
            ("وصلني منتج خاطئ في الطلب {ref}", "استلمت منتجاً مختلفاً عما طلبته ({ref}). أرغب في استبداله."),
            ("تغيير عنوان التسليم للطلب {ref}", "أرجو تغيير عنوان تسليم الطلب {ref} إلى عنوان مكتبي الجديد قبل الشحن."),
        ],
        [
            ("نسيت كلمة المرور", "لا أستطيع تسجيل الدخول ولا تصلني رسالة إعادة التعيين. مرجعي {ref}. أرجو مساعدتي في استعادة حسابي."),
            ("تحديث رقم الجوال في حسابي", "غيّرت رقم جوالي وأحتاج لتحديثه في الحساب. مرجع {ref}."),
            ("أرغب في إغلاق حسابي", "أرغب في إغلاق حسابي (مرجع {ref}) وحذف بياناتي الشخصية."),
        ],
        [
            ("غير راضٍ عن مستوى الخدمة", "أنتظر الرد منذ أيام (مرجع {ref}) ولم يرد عليّ أحد. هذا غير مقبول."),
            ("سلوك غير لائق من مندوب التوصيل", "مندوب توصيل الطلب {ref} كان فظاً وترك الطرد خارج الباب. أرغب في تقديم شكوى."),
            ("مشكلة متكررة لم تُحل", "هذه المرة الثالثة التي أبلغ فيها عن نفس المشكلة (مرجع {ref}) ولا تزال دون حل."),
        ],
    ];

    public static readonly string[] AgentRepliesEn =
    [
        "Hello, thank you for contacting us. I am looking into this now and will update you shortly.",
        "Thanks for the details. I have escalated this to the relevant team and will keep you posted.",
        "I am sorry for the inconvenience. Could you please confirm the reference number and the email on your account?",
        "We have checked your account and found the issue. We are working on a fix.",
    ];

    public static readonly string[] AgentRepliesAr =
    [
        "مرحباً، شكراً لتواصلك معنا. أراجع الموضوع الآن وسأوافيك بالتحديث قريباً.",
        "شكراً على التفاصيل. تم تحويل طلبك للفريق المختص وسأبقيك على اطلاع.",
        "نعتذر عن الإزعاج. هل يمكنك تأكيد رقم المرجع والبريد الإلكتروني المسجل في حسابك؟",
        "راجعنا حسابك وتبيّن لنا سبب المشكلة. نعمل حالياً على حلها.",
    ];

    public static readonly string[] CustomerFollowUpsEn =
    [
        "Thank you, I am waiting for your update.",
        "I sent the screenshot you asked for by email. Please check.",
        "Any news? It is quite urgent for us.",
    ];

    public static readonly string[] CustomerFollowUpsAr =
    [
        "شكراً، بانتظار تحديثكم.",
        "أرسلت لقطة الشاشة المطلوبة على البريد. أرجو المراجعة.",
        "هل من جديد؟ الموضوع عاجل بالنسبة لنا.",
    ];

    public static readonly string[] ResolutionRepliesEn =
    [
        "The issue is now resolved. Please let us know if you need anything else.",
        "We have applied the fix and refunded the difference. Thank you for your patience.",
        "Your request has been completed. We are closing this ticket; reply here if the problem comes back.",
    ];

    public static readonly string[] ResolutionRepliesAr =
    [
        "تم حل المشكلة. يسعدنا مساعدتك في أي استفسار آخر.",
        "تم تطبيق الحل وإرجاع الفرق. شكراً لصبرك.",
        "تم تنفيذ طلبك. سنغلق التذكرة، ويمكنك الرد هنا إذا عادت المشكلة.",
    ];

    public static readonly string[] InternalNotes =
    [
        "Checked the logs, the customer is right. Waiting for the payments team.",
        "Called the customer, confirmed the details by phone.",
        "تم التواصل مع قسم الشحن وبانتظار الرد.",
        "Known issue from the last release, fix scheduled this week.",
        "العميل مهم، يرجى المتابعة بأولوية.",
        "Refund approved by the supervisor.",
    ];

    public static readonly string[] CsatLowEn = ["Took too long to get an answer.", "The problem came back after a day.", "Not satisfied with the solution.", "Had to repeat myself several times."];

    public static readonly string[] CsatLowAr = ["تأخر الرد كثيراً.", "عادت المشكلة بعد يوم.", "لست راضياً عن الحل.", "اضطررت لتكرار المشكلة أكثر من مرة."];

    public static readonly string[] CsatHighEn = ["Great and fast support, thank you!", "Very helpful agent.", "Problem solved quickly.", "Professional service."];

    public static readonly string[] CsatHighAr = ["خدمة ممتازة وسريعة، شكراً لكم!", "موظف متعاون جداً.", "تم حل المشكلة بسرعة.", "خدمة احترافية."];

    // Knowledge base: category (en, ar) and articles (category index, title en/ar, body en/ar).
    public static readonly (string En, string Ar)[] KbCategories =
        [("Getting started", "البدء"), ("Billing and payments", "الفواتير والمدفوعات"), ("Orders and delivery", "الطلبات والتوصيل"), ("Troubleshooting", "حل المشكلات")];

    public static readonly (int Category, string TitleEn, string BodyEn, string TitleAr, string BodyAr)[] KbArticles =
    [
        (0, "How to create your account", "Open the sign-up page, enter your email and phone number, then confirm the code we send you.", "كيفية إنشاء حسابك", "افتح صفحة التسجيل، أدخل بريدك الإلكتروني ورقم جوالك، ثم أكد الرمز الذي نرسله لك."),
        (0, "Resetting your password", "Choose 'Forgot password' on the sign-in page and follow the link sent to your email. The link is valid for one hour.", "إعادة تعيين كلمة المرور", "اختر 'نسيت كلمة المرور' في صفحة الدخول واتبع الرابط المرسل إلى بريدك. الرابط صالح لمدة ساعة."),
        (1, "Understanding your invoice", "Each invoice lists the items, tax and any discount. You can download it as PDF from your account.", "فهم الفاتورة", "تعرض كل فاتورة البنود والضريبة وأي خصم. يمكنك تحميلها بصيغة PDF من حسابك."),
        (1, "Requesting a refund", "Refunds are processed within 7 working days to the original payment method after approval.", "طلب استرجاع المبلغ", "تتم معالجة الاسترجاع خلال 7 أيام عمل إلى وسيلة الدفع الأصلية بعد الموافقة."),
        (2, "Tracking your order", "Use the tracking number from your confirmation email on the tracking page to see where your order is.", "تتبع طلبك", "استخدم رقم التتبع الموجود في رسالة التأكيد على صفحة التتبع لمعرفة موقع طلبك."),
        (2, "Changing the delivery address", "You can change the address until the order is shipped. After that please contact support.", "تغيير عنوان التسليم", "يمكنك تغيير العنوان قبل شحن الطلب. بعد ذلك تواصل مع الدعم."),
        (3, "The app closes unexpectedly", "Update to the latest version, restart your device and clear the app cache. If it continues, contact support with your device model.", "التطبيق يُغلق بشكل مفاجئ", "حدّث التطبيق لآخر إصدار، أعد تشغيل جهازك وامسح ذاكرة التطبيق. إن استمرت المشكلة تواصل مع الدعم مع ذكر طراز جهازك."),
        (3, "Notifications are not arriving", "Check that notifications are allowed in your phone settings and that battery saving is not restricting the app.", "الإشعارات لا تصل", "تأكد من السماح بالإشعارات في إعدادات الهاتف وأن وضع توفير البطارية لا يقيّد التطبيق."),
    ];

    public static readonly (string QEn, string AEn, string QAr, string AAr)[] KbFaqs =
    [
        ("What are your support hours?", "Our team answers Sunday to Thursday, 8:00 to 17:00 (Riyadh time).", "ما هي ساعات الدعم؟", "يرد فريقنا من الأحد إلى الخميس من 8:00 صباحاً حتى 5:00 مساءً (بتوقيت الرياض)."),
        ("How can I contact support?", "You can reach us by email, WhatsApp, chat or the support portal.", "كيف أتواصل مع الدعم؟", "يمكنك التواصل عبر البريد الإلكتروني أو واتساب أو المحادثة أو بوابة الدعم."),
        ("How long does a refund take?", "Up to 7 working days after approval.", "كم يستغرق الاسترجاع؟", "حتى 7 أيام عمل بعد الموافقة."),
        ("Can I change my order after paying?", "Yes, until it is shipped. Contact us as soon as possible.", "هل يمكنني تعديل الطلب بعد الدفع؟", "نعم، حتى لحظة الشحن. تواصل معنا في أقرب وقت."),
        ("Which payment methods do you accept?", "Cards, Apple Pay, bank transfer and cash on delivery.", "ما وسائل الدفع المتاحة؟", "البطاقات وأبل باي والتحويل البنكي والدفع عند الاستلام."),
        ("How do I close my account?", "Send us a request from the portal and we will confirm within two working days.", "كيف أغلق حسابي؟", "أرسل لنا طلباً من البوابة وسنؤكد خلال يومي عمل."),
    ];

    public static readonly string[] TaskTitlesEn =
    [
        "Call the customer back about the refund", "Follow up with the logistics team", "Review tickets close to SLA breach",
        "Update the knowledge base article on password reset", "Send the invoice summary to the VIP customer",
    ];

    public static readonly string[] TaskTitlesAr =
    [
        "الاتصال بالعميل بخصوص الاسترجاع", "المتابعة مع فريق الشحن", "مراجعة التذاكر القريبة من خرق الاتفاقية",
        "تحديث مقال كلمة المرور في قاعدة المعرفة", "إرسال ملخص الفواتير للعميل المهم",
    ];
}

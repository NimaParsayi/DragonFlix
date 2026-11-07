namespace DragonFlix.Api.Services;

/// <summary>Detects the issuing bank from the first 6 digits (BIN) of an Iranian card number.</summary>
public static class BankInfo
{
    // BIN -> (logo file code, Persian name). Logos are optional: Assets/BankLogos/{code}.jpg
    private static readonly Dictionary<string, (string Code, string Name)> Bins = new()
    {
        ["603799"] = ("melli", "بانک ملی ایران"),
        ["589210"] = ("sepah", "بانک سپه"),
        ["627648"] = ("tosee-saderat", "بانک توسعه صادرات"),
        ["627961"] = ("sanat-madan", "بانک صنعت و معدن"),
        ["603770"] = ("keshavarzi", "بانک کشاورزی"),
        ["639217"] = ("keshavarzi", "بانک کشاورزی"),
        ["628023"] = ("maskan", "بانک مسکن"),
        ["627760"] = ("post", "پست بانک ایران"),
        ["502908"] = ("tosee-taavon", "بانک توسعه تعاون"),
        ["627412"] = ("eghtesad-novin", "بانک اقتصاد نوین"),
        ["622106"] = ("parsian", "بانک پارسیان"),
        ["639194"] = ("parsian", "بانک پارسیان"),
        ["502229"] = ("pasargad", "بانک پاسارگاد"),
        ["639347"] = ("pasargad", "بانک پاسارگاد"),
        ["627488"] = ("karafarin", "بانک کارآفرین"),
        ["621986"] = ("saman", "بانک سامان"),
        ["639346"] = ("sina", "بانک سینا"),
        ["639607"] = ("sarmayeh", "بانک سرمایه"),
        ["502806"] = ("shahr", "بانک شهر"),
        ["504706"] = ("shahr", "بانک شهر"),
        ["502938"] = ("dey", "بانک دی"),
        ["603769"] = ("saderat", "بانک صادرات ایران"),
        ["610433"] = ("mellat", "بانک ملت"),
        ["991975"] = ("mellat", "بانک ملت"),
        ["589463"] = ("refah", "بانک رفاه کارگران"),
        ["627353"] = ("tejarat", "بانک تجارت"),
        ["585983"] = ("tejarat", "بانک تجارت"),
        ["639370"] = ("mehr-eghtesad", "بانک مهر اقتصاد"),
        ["639599"] = ("ghavamin", "بانک قوامین"),
        ["504172"] = ("resalat", "بانک رسالت"),
        ["636949"] = ("hekmat", "بانک حکمت ایرانیان"),
        ["606373"] = ("mehr-iran", "بانک قرض‌الحسنه مهر ایران"),
        ["636214"] = ("ayandeh", "بانک آینده"),
        ["505785"] = ("iran-zamin", "بانک ایران زمین"),
        ["505416"] = ("gardeshgari", "بانک گردشگری"),
        ["505801"] = ("kosar", "موسسه اعتباری کوثر"),
        ["507677"] = ("noor", "موسسه اعتباری نور"),
        ["606256"] = ("melal", "موسسه اعتباری ملل"),
        ["585947"] = ("khavarmiane", "بانک خاورمیانه"),
        ["628157"] = ("tosee", "موسسه اعتباری توسعه")
    };

    public static (string Code, string Name)? Detect(string? cardNumber)
    {
        var digits = new string((cardNumber ?? "").Where(char.IsDigit).ToArray());
        return digits.Length >= 6 && Bins.TryGetValue(digits[..6], out var bank) ? bank : null;
    }

    /// <summary>"6104337412345678" -> "6104 3374 1234 5678"</summary>
    public static string Format(string? cardNumber)
    {
        var digits = new string((cardNumber ?? "").Where(char.IsDigit).ToArray());
        return digits.Length == 16
            ? string.Join(' ', Enumerable.Range(0, 4).Select(i => digits.Substring(i * 4, 4)))
            : (cardNumber ?? "").Trim();
    }

    /// <summary>Path of the bank's logo if the file was added under Assets/BankLogos, otherwise null.</summary>
    public static string? LogoPath(string? cardNumber)
    {
        var bank = Detect(cardNumber);
        if (bank is null) return null;
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "BankLogos", bank.Value.Code + ".jpg");
        return File.Exists(path) ? path : null;
    }

    /// <summary>نام بانک: اول از روی ۶ رقم اول کارت، وگرنه مقدار دستی (قدیمی) تنظیمات.</summary>
    public static string NameOf(string? cardNumber, string? fallback = null) =>
        Detect(cardNumber)?.Name ?? (fallback ?? "").Trim();

    /// <summary>آدرس نسبی لوگوی بانکِ تشخیص‌داده‌شده برای نمایش در مینی‌اپ (یا null).</summary>
    public static string? LogoUrl(string? cardNumber) => LogoPath(cardNumber) is null ? null : $"/api/bank-logo/{Detect(cardNumber)!.Value.Code}";
}

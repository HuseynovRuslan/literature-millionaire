using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Seed;

/// <summary>
/// Approved question bank for the book "Ölülər" (30 questions).
/// Source of truth: docs/content/Oluler_Sual_Banki.xlsx, sheet "Suallar", rows 7-36,
/// transcribed verbatim at implementation time. Review-only columns (№, Məclis,
/// Status, Qeyd) are intentionally not persisted.
/// </summary>
public static class OlulerQuestionSeed
{
    public const string BookTitle = "Ölülər";

    /// <summary>
    /// Inserts any approved question that is not yet present for the book.
    /// Idempotent: a row is skipped when the same BookId + exact Text already exists.
    /// Never touches rows it did not create (administrator or legacy questions).
    /// </summary>
    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var book = await db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Title == BookTitle, ct)
            ?? throw new InvalidOperationException($"Seed book '{BookTitle}' was not found. Seed books before questions.");

        var existing = (await db.Questions
                .Where(q => q.BookId == book.Id)
                .Select(q => q.Text)
                .ToListAsync(ct))
            .Select(t => t.Trim())
            .ToHashSet(StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        var missing = Questions()
            .Where(q => !existing.Contains(q.Text.Trim()))
            .Select(q => { q.BookId = book.Id; q.CreatedAt = now; return q; })
            .ToList();

        if (missing.Count > 0)
        {
            db.Questions.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }

        await ApplyMediaAsync(db, book.Id, ct);
    }

    /// <summary>
    /// Approved illustrations for six of the questions, keyed by the exact approved text.
    /// File names are deliberately neutral so the URL in the DOM never hints at the answer.
    /// Alt texts describe the picture without naming the correct option.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Url, string Alt)> Media =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["İskəndərin çağırdığı itin adı nədir?"] =
                ("/question-images/oluler-iskender-dog.webp",
                 "İskəndər həyətdə, əlində kitab, ona tərəf qaçan bir itə əl uzadır."),
            ["Kərbəlayı Fətullaha aid edilən məktubu camaata kim oxuyur?"] =
                ("/question-images/oluler-letter-reading.webp",
                 "Ağsaqqal bir kişi izdihamın qarşısında əlindəki məktubu ucadan oxuyur."),
            ["Kərbəlayı Fətullah haqqında gələn teleqraf hansı həqiqəti bildirir?"] =
                ("/question-images/oluler-telegram.webp",
                 "Teleqraf məntəqəsində məmur bir kağız uzadır; qarşısında dörd nəfər təəccüblə baxır."),
            ["Şeyx Nəsrullahın yeməklə bağlı davranışındakı ziddiyyət hansıdır?"] =
                ("/question-images/oluler-sheikh-table.webp",
                 "Şeyx Nəsrullah zəngin süfrə arxasında oturub; açıq qapıdan həyətdəki camaat görünür."),
            ["İskəndərin köhnə dərsliklə bağlı çıxışı təhsildə hansı nöqsanı göstərir?"] =
                ("/question-images/oluler-old-textbook.webp",
                 "İskəndər masa arxasında köhnə bir kitabı açıb fikirli halda oxuyur; ətrafda kitablar və mürəkkəbqabı."),
            ["Ölən qohumların adları çəkiləndə adamların bir-bir “fikirləşməyə” getməsi nəyi üzə çıxarır?"] =
                ("/question-images/oluler-cemetery-ledger.webp",
                 "Gecə qəbiristanlığında masa arxasında bir kişi dəftərə yazır; ətrafında bir neçə kişi dayanıb baxır."),
        };

    /// <summary>
    /// Fills ImageUrl/ImageAltText for the mapped questions of this book only.
    /// A row is touched only when both media fields are still empty, so anything an
    /// administrator has set or changed is never overwritten. Limitation: if an
    /// administrator deliberately clears an image, the next startup restores it.
    /// </summary>
    private static async Task ApplyMediaAsync(ApplicationDbContext db, int bookId, CancellationToken ct)
    {
        var texts = Media.Keys.ToList();
        var rows = await db.Questions
            .Where(q => q.BookId == bookId && texts.Contains(q.Text))
            .ToListAsync(ct);

        var changed = 0;
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row.ImageUrl) || !string.IsNullOrWhiteSpace(row.ImageAltText))
            {
                continue; // set by an administrator or an earlier run: leave as is
            }

            var (url, alt) = Media[row.Text];
            row.ImageUrl = url;
            row.ImageAltText = alt;
            changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>The 30 approved rows in workbook order. Wording is verbatim; do not edit here without updating the workbook.</summary>
    public static IEnumerable<Question> Questions()
    {
        // №1
        yield return new Question
        {
            Text = "Hacı Həsənin uzaqda təhsil almış böyük oğlu kimdir?",
            OptionA = "Cəlal",
            OptionB = "İskəndər",
            OptionC = "Heydər ağa",
            OptionD = "Mir Bağır ağa",
            CorrectOption = 'B',
            Difficulty = Difficulty.Easy,
            Category = "Ölülər",
            Explanation = "Hacı Həsən İskəndəri təhsil üçün uzağa göndərdiyini, lakin onun geri qayıdandan sonra iş tapa bilmədiyini söyləyir."
        };

        // №2
        yield return new Question
        {
            Text = "İskəndərin çağırdığı itin adı nədir?",
            OptionA = "Mars",
            OptionB = "Zeynal",
            OptionC = "Rüstəm",
            OptionD = "Karapet",
            CorrectOption = 'A',
            Difficulty = Difficulty.Easy,
            Category = "Ölülər",
            Explanation = "İskəndər otağa girərkən və sonrakı səhnələrdə iti Mars adı ilə çağırır."
        };

        // №3
        yield return new Question
        {
            Text = "Əsərin əvvəlində dirildiyi iddia edilən şəxs kimdir?",
            OptionA = "Hacı Rza",
            OptionB = "Hacı Mehdi",
            OptionC = "Kərbəlayı Fətullah",
            OptionD = "Məşədi Mustafa",
            CorrectOption = 'C',
            Difficulty = Difficulty.Easy,
            Category = "Ölülər",
            Explanation = "Şəhərə yayılan xəbərə görə Xorasanda ölən Kərbəlayı Fətullah guya dirilmişdi."
        };

        // №4
        yield return new Question
        {
            Text = "Kərbəlayı Fətullaha aid edilən məktubu camaata kim oxuyur?",
            OptionA = "Hacı Kazım",
            OptionB = "Şeyx Əhməd",
            OptionC = "İskəndər",
            OptionD = "Məşədi Oruc",
            CorrectOption = 'D',
            Difficulty = Difficulty.Easy,
            Category = "Ölülər",
            Explanation = "Məktub Məşədi Orucun əlində olur və o, camaatın qarşısında məktubu səsləndirir."
        };

        // №5
        yield return new Question
        {
            Text = "Şeyx Nəsrullah şəhərə gələndə kimin evində yerləşir?",
            OptionA = "Hacı Həsənin",
            OptionB = "Mir Bağır ağanın",
            OptionC = "Hacı Bəxşəlinin",
            OptionD = "Kərbəlayı Vəlinin",
            CorrectOption = 'A',
            Difficulty = Difficulty.Easy,
            Category = "Ölülər",
            Explanation = "Hacılar qonaqlıq üstündə mübahisə etsələr də, Şeyx Nəsrullah Hacı Həsənin evində qalır."
        };

        // №6
        yield return new Question
        {
            Text = "Şeyx Nəsrullahın yanında gəzən və onun tapşırıqlarını yerinə yetirən şəxs kimdir?",
            OptionA = "Məşədi Oruc",
            OptionB = "Şeyx Əhməd",
            OptionC = "Mirzə Hüseyn",
            OptionD = "Heydər ağa",
            CorrectOption = 'B',
            Difficulty = Difficulty.Easy,
            Category = "Ölülər",
            Explanation = "Şeyx Əhməd onun şagirdi və köməkçisidir; camaata izah verir və siyahıları yazır."
        };

        // №7
        yield return new Question
        {
            Text = "Üçüncü məclisin əsas hadisələri harada baş verir?",
            OptionA = "Məktəbdə",
            OptionB = "Bazarda",
            OptionC = "Qəbiristanlığın yanında",
            OptionD = "Teleqrafxanada",
            CorrectOption = 'C',
            Difficulty = Difficulty.Easy,
            Category = "Ölülər",
            Explanation = "Üçüncü məclis şəhərin kənarında, qəbiristanlığın yanında qurulub."
        };

        // №8
        yield return new Question
        {
            Text = "Hacı Həsən hansı qızını Şeyx Nəsrullaha verməyə razılaşır?",
            OptionA = "Sara",
            OptionB = "Püstə",
            OptionC = "Zeynəb",
            OptionD = "Nazlı",
            CorrectOption = 'D',
            Difficulty = Difficulty.Easy,
            Category = "Ölülər",
            Explanation = "Hacı Həsən azyaşlı qızı Nazlını Şeyx Nəsrullahın otağına köçürməyə hazırlaşır."
        };

        // №9
        yield return new Question
        {
            Text = "Kərbəlayı Fətullah haqqında gələn teleqraf hansı həqiqəti bildirir?",
            OptionA = "Onun dirilmədiyini",
            OptionB = "Şeyxin xəstə olduğunu",
            OptionC = "İskəndərin şəhərdən getdiyini",
            OptionD = "Hacı Rzanın sağ olduğunu",
            CorrectOption = 'A',
            Difficulty = Difficulty.Easy,
            Category = "Ölülər",
            Explanation = "Xorasandan gələn teleqraf Kərbəlayı Fətullahın dirilmədiyini və əvvəlki məktubun yalan olduğunu üzə çıxarır."
        };

        // №10
        yield return new Question
        {
            Text = "Şeyx Nəsrullah və Şeyx Əhməd qaçarkən qızlara hara getdiklərini deyirlər?",
            OptionA = "Nəcəfə",
            OptionB = "Hamama",
            OptionC = "Qəbiristanlığa",
            OptionD = "Təbrizə",
            CorrectOption = 'B',
            Difficulty = Difficulty.Easy,
            Category = "Ölülər",
            Explanation = "Onlar gecə əşyalarını yığıb qızlara hamama getdiklərini söyləyir, əslində isə qaçırlar."
        };

        // №11
        yield return new Question
        {
            Text = "İskəndər Cəlala hansı içkini içdiyini etiraf edir?",
            OptionA = "Şərab",
            OptionB = "Pivə",
            OptionC = "Araq",
            OptionD = "Su",
            CorrectOption = 'C',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "Cəlal onun çaxır içdiyini deyəndə İskəndər bunu düzəldib araq içdiyini söyləyir."
        };

        // №12
        yield return new Question
        {
            Text = "Şeyx Nəsrullahın yeməklə bağlı davranışındakı ziddiyyət hansıdır?",
            OptionA = "Cücə-plovu camaatla bölüşür",
            OptionB = "Yemək üçün bazara gedir",
            OptionC = "Səhərə qədər həqiqətən ac qalır",
            OptionD = "Hamının yanında yeməyi rədd edir, sonra gizlicə yemək istəyir",
            CorrectOption = 'D',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "O, camaat qarşısında gündə bir xurma yediyini göstərir, sonra isə Şeyx Əhməddən gizlicə çörək və yemək istəyir."
        };

        // №13
        yield return new Question
        {
            Text = "Məktubda Kərbəlayı Fətullahdan başqa neçə nəfərin dirildiyi iddia olunur?",
            OptionA = "114",
            OptionB = "40",
            OptionC = "12",
            OptionD = "7",
            CorrectOption = 'A',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "Məktubda Fətullah özündən başqa yüz on dörd nəfərin də qəbirdən qayıtdığını yazır."
        };

        // №14
        yield return new Question
        {
            Text = "Məktuba görə Şeyx Nəsrullah Məşhəddən sonra hansı istiqamətlə gedəcəkdi?",
            OptionA = "Bakı–Şamaxı–Gəncə",
            OptionB = "Culfa–Təbriz–Nəcəf",
            OptionC = "Tiflis–Qars–İstanbul",
            OptionD = "Ərdəbil–Zəncan–Qum",
            CorrectOption = 'B',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "Məktubda onun Culfa yolu ilə Təbrizdən keçərək Nəcəfə gedəcəyi bildirilir."
        };

        // №15
        yield return new Question
        {
            Text = "Mir Bağır ağa niyə Hacı Həsənin ölən qızı Saranın siyahıya yazılmasına etiraz edir?",
            OptionA = "Sara onun bacısıdır",
            OptionB = "Sara onun qızıdır",
            OptionC = "Sara onun həyat yoldaşıdır",
            OptionD = "Sara onun xalasıdır",
            CorrectOption = 'C',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "Mir Bağır ağa Saranın öz həyat yoldaşı olduğunu deyərək onun dirildilməsinə tərəddüd göstərir."
        };

        // №16
        yield return new Question
        {
            Text = "Hacı Bəxşəli ölənlərindən ilk növbədə kimlərin dirilməsini istəyir?",
            OptionA = "Valideynlərinin",
            OptionB = "Hürnisə ilə Saranın",
            OptionC = "Məşədi Mustafa ilə Zeynalın",
            OptionD = "Oğulları Cəlil ilə Xəlilin",
            CorrectOption = 'D',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "Hacı Bəxşəli qoca valideynlərini deyil, azyaşlı ölən oğulları Cəlil və Xəlili siyahıya yazdırır."
        };

        // №17
        yield return new Question
        {
            Text = "Hürnisə Kərbəlayı Vəlinin nəyi idi?",
            OptionA = "Bacısı",
            OptionB = "Qızı",
            OptionC = "Anası",
            OptionD = "Həyat yoldaşı",
            CorrectOption = 'A',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "Kərbəlayı Vəli Hürnisənin öz doğma bacısı və Hacı Bəxşəlinin ölən arvadı olduğunu bildirir."
        };

        // №18
        yield return new Question
        {
            Text = "Kərbəlayı Vəli anasını diriləcək şəxslər siyahısına yazdırmaqdan niyə çəkinir?",
            OptionA = "Anasının qəbrini bilmirdi",
            OptionB = "Anasını döyərək öldürməkdə günahlandırılır və şikayətdən qorxur",
            OptionC = "Anasının sağ olduğunu düşünürdü",
            OptionD = "Siyahıda yer qalmamışdı",
            CorrectOption = 'B',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "Hacı Bəxşəli onun anasını döyərək öldürdüyünü və dirilsə hökumətə şikayət edəcəyindən qorxduğunu üzünə vurur."
        };

        // №19
        yield return new Question
        {
            Text = "Şeyx Nəsrullah balaca qızı otaqda saxlamaq üçün əsasən hansı üsuldan istifadə edir?",
            OptionA = "Ona pul təklif edir",
            OptionB = "Atasını otaqdan qovur",
            OptionC = "Axirət əzabı ilə qorxudub savab vəd edir",
            OptionD = "Şəhərdən aparacağını gizlədir",
            CorrectOption = 'C',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "Şeyx qıza seçimin guya özündə olduğunu deyir, lakin getməyin dəhşətli axirət əzabına səbəb olacağını təsvir edərək onu qorxudur."
        };

        // №20
        yield return new Question
        {
            Text = "İskəndər Kərbəlayı Fətullahın dirildiyi xəbərini ilk eşidəndə necə reaksiya verir?",
            OptionA = "Dərhal inanır",
            OptionB = "Ağlamağa başlayır",
            OptionC = "Şeyxin qarşısına çıxır",
            OptionD = "Gülür",
            CorrectOption = 'D',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "İskəndər xəbərin məntiqsizliyini anlayır və gülərək otaqdan çıxır."
        };

        // №21
        yield return new Question
        {
            Text = "İskəndəri Şeyx Nəsrullahın qarşısına kim gətirir?",
            OptionA = "Hacı Həsən",
            OptionB = "Nazlı",
            OptionC = "Şeyx Əhməd",
            OptionD = "Məşədi Oruc",
            CorrectOption = 'A',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "Şeyxin tələbindən sonra Hacı Həsən oğlunun əlindən tutub onu Şeyx Nəsrullahın qarşısına gətirir."
        };

        // №22
        yield return new Question
        {
            Text = "Teleqraf gələndən sonra məsələni Şeyx Nəsrullahın özündən soruşmağı kim təklif edir?",
            OptionA = "İskəndər",
            OptionB = "Heydər ağa",
            OptionC = "Hacı Bəxşəli",
            OptionD = "Kərbəlayı Fatma xanım",
            CorrectOption = 'B',
            Difficulty = Difficulty.Medium,
            Category = "Ölülər",
            Explanation = "Teleqrafçı Heydər ağa teleqrafda səhv olmadığını deyib xəbəri Şeyxin özünə bildirməyi məsləhət görür."
        };

        // №23
        yield return new Question
        {
            Text = "Şeyx Nəsrullahın əvvəlki hiylələrini açıqlayan silahlı müsafirlər haradan gəlirlər?",
            OptionA = "Xorasandan",
            OptionB = "İsfahandan",
            OptionC = "İrəvan vilayətindən",
            OptionD = "Nəcəfdən",
            CorrectOption = 'C',
            Difficulty = Difficulty.Hard,
            Category = "Ölülər",
            Explanation = "Müsafirlər özlərini İrəvan vilayətindən gələnlər kimi təqdim edir və eyni dəstənin orada da insanları aldatdığını bildirirlər."
        };

        // №24
        yield return new Question
        {
            Text = "Müsafirlərin sözlərinə görə Şeyx Nəsrullahın dəstəsi əvvəlki şəhərdə hansı hiyləni təkrarlamışdı?",
            OptionA = "Ölülərin qəbirlərini dəyişmişdi",
            OptionB = "Camaatdan vergi toplamışdı",
            OptionC = "Məktəbi bağlayıb qaçmışdı",
            OptionD = "Hər gecə bir qızı alıb sonda qaçmışdı",
            CorrectOption = 'D',
            Difficulty = Difficulty.Hard,
            Category = "Ölülər",
            Explanation = "Müsafirlər dəstənin şəhərdə qaldığı müddətdə hər gecə bir qızı kəbinə alıb sonra qaçdığını açıqlayırlar."
        };

        // №25
        yield return new Question
        {
            Text = "İskəndərin köhnə dərsliklə bağlı çıxışı təhsildə hansı nöqsanı göstərir?",
            OptionA = "Elm öyrədilsə də, insanlıq və mənəvi məsuliyyətin öyrədilməməsini",
            OptionB = "Dərsliklərin çox baha olmasını",
            OptionC = "Müəllimlərin yalnız xarici dil bilməsini",
            OptionD = "Uşaqların məktəbə gec başlamasını",
            CorrectOption = 'A',
            Difficulty = Difficulty.Hard,
            Category = "Ölülər",
            Explanation = "İskəndər ona daim alim olmağın deyildiyini, amma heç kimin adam olmağı öyrətmədiyini vurğulayır."
        };

        // №26
        yield return new Question
        {
            Text = "Hacı Həsən ölən qardaşı Hacı Rzanı siyahıya yazdırmamaq üçün hansı səbəbi göstərir?",
            OptionA = "Onun başqa şəhərdə dəfn olunduğunu",
            OptionB = "Beş ildən çox keçdiyi üçün bədəninin çürüdüyünü",
            OptionC = "Onun əslində sağ olduğunu",
            OptionD = "Adını unutduğunu",
            CorrectOption = 'B',
            Difficulty = Difficulty.Hard,
            Category = "Ölülər",
            Explanation = "Hacı Həsən qardaşının çoxdan öldüyünü və bədəninin çürüdüyünü bəhanə edir, sonra düşünmək üçün vaxt istəyir."
        };

        // №27
        yield return new Question
        {
            Text = "Ölən qohumların adları çəkiləndə adamların bir-bir “fikirləşməyə” getməsi nəyi üzə çıxarır?",
            OptionA = "Onların adları xatırlamamasını",
            OptionB = "Şeyxin siyahısının çox uzun olmasını",
            OptionC = "Keçmiş günah və ailə münaqişələrinə görə bəzi ölülərin qayıtmasını istəməmələrini",
            OptionD = "Qəbiristanlığın uzaq olmasını",
            CorrectOption = 'C',
            Difficulty = Difficulty.Hard,
            Category = "Ölülər",
            Explanation = "Hər bir konkret ölünün dirilməsi mümkün sayılanda gizli zorakılıq, qorxu və ailə ziddiyyətləri ortaya çıxır."
        };

        // №28
        yield return new Question
        {
            Text = "Şeyx Nəsrullah İskəndərin məclisdən çıxarılmasını nə üçün tələb edir?",
            OptionA = "İskəndər siyahını cırdığı üçün",
            OptionB = "İskəndər qonaqlara yemək vermədiyi üçün",
            OptionC = "İskəndər Nazlını şəhərdən apardığı üçün",
            OptionD = "İskəndər onun iddialarını ələ salıb hiyləsini açıq göstərdiyi üçün",
            CorrectOption = 'D',
            Difficulty = Difficulty.Hard,
            Category = "Ölülər",
            Explanation = "İskəndər ölü diriltmək iddiasını istehza ilə qarşılayır; Şeyx bunu küfr kimi təqdim edib onu uzaqlaşdırır."
        };

        // №29
        yield return new Question
        {
            Text = "Şeyxin qaçmasından sonra pərdə arxasında dörd qızın tapılması ən çox nəyi sübut edir?",
            OptionA = "Dini və möcüzə görüntüsünün arxasında qızların istismarı olduğunu",
            OptionB = "Şeyxin həqiqətən hamama getdiyini",
            OptionC = "Qızların öz istəyi ilə gizləndiyini",
            OptionD = "Camaatın ölüləri diriltdiyini",
            CorrectOption = 'A',
            Difficulty = Difficulty.Hard,
            Category = "Ölülər",
            Explanation = "Şeyxin yoxa çıxması və ağlayan qızların qalması onun dini nüfuzdan şəxsi istismar üçün istifadə etdiyini açıq göstərir."
        };

        // №30
        yield return new Question
        {
            Text = "Final nitqində İskəndər “Ölülər” adını kimlərə verir?",
            OptionA = "Qəbiristanlıqdakı ölənlərə",
            OptionB = "Həqiqəti görməyən və uşaqları şeyxə təslim edən camaata",
            OptionC = "Yalnız Şeyx Nəsrullahla Şeyx Əhmədə",
            OptionD = "İrəvandan gələn müsafirlərə",
            CorrectOption = 'B',
            Difficulty = Difficulty.Hard,
            Category = "Ölülər",
            Explanation = "İskəndər mənəvi və düşüncə baxımından susqun qalan camaatı “Ölülər” adlandırır."
        };

    }
}

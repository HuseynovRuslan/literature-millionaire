// Hər kitab üçün bir quiz. Yeni kitab əlavə etmək üçün bu siyahıya
// eyni formada yeni obyekt yazın və üz qabığını public/covers/ qovluğuna qoyun.
// `cover` faylı yoxdursa, oyun avtomatik olaraq naxışlı müvəqqəti üzlük göstərir.
// `answer` — düzgün variantın `options` içindəki indeksi (0-dan başlayır).
// Oyun hər dəfə bankdan təsadüfi 10 sual seçir və variantları qarışdırır.

export const QUIZZES = [
  {
    id: 'oluler',
    title: 'Ölülər',
    author: 'Cəlil Məmmədquluzadə',
    tag: 'Komediya · 1909',
    cover: '/covers/oluler.png',
    questions: [
      {
        q: '"Ölülər" əsərinin müəllifi kimdir?',
        options: ['Cəlil Məmmədquluzadə', 'Mirzə Fətəli Axundzadə', 'Əbdürrəhim bəy Haqverdiyev', 'Nəriman Nərimanov'],
        answer: 0,
      },
      {
        q: '"Ölülər" hansı janrda yazılmış əsərdir?',
        options: ['Komediya', 'Roman', 'Poema', 'Qəzəl'],
        answer: 0,
      },
      {
        q: '"Ölülər" komediyası neçənci ildə yazılmışdır?',
        options: ['1909', '1889', '1925', '1937'],
        answer: 0,
      },
      {
        q: '"Ölülər" neçə məclisdən (pərdədən) ibarətdir?',
        options: ['4', '2', '6', '8'],
        answer: 0,
      },
      {
        q: 'Özünü ölüləri dirildə bilən adam kimi qələmə verən fırıldaqçı kimdir?',
        options: ['Şeyx Nəsrullah', 'Hacı Həsən ağa', 'Kefli İskəndər', 'Nazlı'],
        answer: 0,
      },
      {
        q: 'Şeyx Nəsrullahın fırıldaqçı olduğunu hamıdan əvvəl görən və ifşa edən obraz kimdir?',
        options: ['Kefli İskəndər', 'Hacı Həsən ağa', 'Nazlı', 'Şeyx Əhməd'],
        answer: 0,
      },
      {
        q: 'İskəndərə nə üçün "Kefli İskəndər" deyirlər?',
        options: ['İçki içdiyi üçün', 'Həmişə mahnı oxuduğu üçün', 'Çox varlı olduğu üçün', 'Tez-tez səyahət etdiyi üçün'],
        answer: 0,
      },
      {
        q: 'Kefli İskəndər Hacı Həsən ağanın nəyidir?',
        options: ['Oğlu', 'Qardaşı', 'Nökəri', 'Qonşusu'],
        answer: 0,
      },
      {
        q: 'Şeyx Nəsrullah kimlə evlənmək niyyətindədir?',
        options: ['İskəndərin bacısı Nazlı ilə', 'Hacı Həsən ağanın qonşusu ilə', 'Şəhər hakiminin qızı ilə', 'Heç kimlə'],
        answer: 0,
      },
      {
        q: 'Şeyx Nəsrullah kimin evində qonaq qalır?',
        options: ['Hacı Həsən ağanın', 'Kefli İskəndərin dostunun', 'Şəhər mollasının', 'Karvansarada, tək'],
        answer: 0,
      },
      {
        q: 'Əsərin adındakı "ölülər" sözü əslində kimlərə işarədir?',
        options: [
          'Cəhalət içində yaşayan, mənən ölü olan dirilərə',
          'Qəbiristanlıqda uyuyanlara',
          'Müharibədə həlak olanlara',
          'Şeyxin dirildə bilmədiyi adamlara',
        ],
        answer: 0,
      },
      {
        q: 'Əsərdə əsasən nə tənqid olunur?',
        options: ['Mövhumat, cəhalət və dini fırıldaqçılıq', 'Müharibə və zorakılıq', 'Kənd həyatının çətinlikləri', 'Şəhərdə tikinti işləri'],
        answer: 0,
      },
      {
        q: 'Şeyx Nəsrullahın əsl məqsədi nə idi?',
        options: ['Sadəlövh xalqın pulunu və malını ələ keçirmək', 'Uşaqlar üçün məktəb açmaq', 'Xəstələri müalicə etmək', 'Şəhərə məscid tikmək'],
        answer: 0,
      },
      {
        q: 'Ölülərinin dirilməsini istəyənlər sonra niyə fikirlərindən daşınırlar?',
        options: [
          'Ölülər qayıtsa, onların mal-mülkü və ailə işləri pozulacaqdı',
          'Şeyx Nəsrullah şəhəri tərk etmişdi',
          'Hökumət bunu qadağan etmişdi',
          'Qış gəldiyi üçün mərasim ləğv olundu',
        ],
        answer: 0,
      },
      {
        q: 'Cəlil Məmmədquluzadə hansı məşhur satirik jurnalın banisidir?',
        options: ['"Molla Nəsrəddin"', '"Əkinçi"', '"Füyuzat"', '"Kəşkül"'],
        answer: 0,
      },
      {
        q: '"Molla Nəsrəddin" jurnalı ilk dəfə hansı şəhərdə nəşr olunmuşdur?',
        options: ['Tiflisdə', 'Bakıda', 'Təbrizdə', 'Gəncədə'],
        answer: 0,
      },
      {
        q: '"Molla Nəsrəddin" jurnalının ilk sayı neçənci ildə çıxmışdır?',
        options: ['1906', '1875', '1918', '1922'],
        answer: 0,
      },
      {
        q: 'Cəlil Məmmədquluzadə harada anadan olmuşdur?',
        options: ['Naxçıvanda', 'Şuşada', 'Gəncədə', 'Şamaxıda'],
        answer: 0,
      },
      {
        q: 'Cəlil Məmmədquluzadə hansı şəhərdə vəfat etmişdir?',
        options: ['Bakıda', 'Tiflisdə', 'Naxçıvanda', 'Təbrizdə'],
        answer: 0,
      },
      {
        q: 'Hansı əsər Cəlil Məmmədquluzadəyə məxsusdur?',
        options: ['"Danabaş kəndinin əhvalatları"', '"Şeyx Sənan"', '"Vaqif"', '"Leyli və Məcnun"'],
        answer: 0,
      },
      {
        q: 'Aşağıdakı pyeslərdən hansı Cəlil Məmmədquluzadənindir?',
        options: ['"Anamın kitabı"', '"Hacı Qara"', '"Pəri cadu"', '"Almaz"'],
        answer: 0,
      },
      {
        q: '"Poçt qutusu" hekayəsinin müəllifi kimdir?',
        options: ['Cəlil Məmmədquluzadə', 'Abdulla Şaiq', 'Süleyman Sani Axundov', 'Mir Cəlal'],
        answer: 0,
      },
      {
        q: 'Cəlil Məmmədquluzadə jurnalda yazılarını əsasən hansı imza ilə çap etdirirdi?',
        options: ['Molla Nəsrəddin', 'Hophop', 'Nakam', 'Ümid'],
        answer: 0,
      },
      {
        q: '"Molla Nəsrəddin" jurnalında satirik şeirləri ilə tanınan böyük şair kimdir?',
        options: ['Mirzə Ələkbər Sabir', 'Hüseyn Cavid', 'Səməd Vurğun', 'Nizami Gəncəvi'],
        answer: 0,
      },
      {
        q: 'Cəlil Məmmədquluzadə hansı ədəbi cərəyanın görkəmli nümayəndəsidir?',
        options: ['Tənqidi realizm', 'Romantizm', 'Klassisizm', 'Simvolizm'],
        answer: 0,
      },
      {
        q: 'Cəlil Məmmədquluzadənin həyat yoldaşı kim idi?',
        options: ['Həmidə xanım Cavanşir', 'Xurşidbanu Natəvan', 'Sara xanım', 'Ümmügülsüm xanım'],
        answer: 0,
      },
    ],
  },

  {
    id: 'vaqif',
    title: 'Vaqif',
    author: 'Səməd Vurğun',
    tag: 'Mənzum dram · 1937',
    cover: '/covers/vaqif.png',
    questions: [
      {
        q: '"Vaqif" dramının müəllifi kimdir?',
        options: ['Səməd Vurğun', 'Hüseyn Cavid', 'Cəfər Cabbarlı', 'Süleyman Rüstəm'],
        answer: 0,
      },
      {
        q: '"Vaqif" əsəri hansı janrdadır?',
        options: ['Mənzum dram', 'Roman', 'Hekayə', 'Komediya'],
        answer: 0,
      },
      {
        q: '"Vaqif" dramı neçənci ildə yazılmışdır?',
        options: ['1937', '1909', '1957', '1920'],
        answer: 0,
      },
      {
        q: 'Əsərin baş qəhrəmanı Vaqifin əsl adı nədir?',
        options: ['Molla Pənah', 'Molla Vəli', 'Molla Cümə', 'Molla Qasım'],
        answer: 0,
      },
      {
        q: 'Vaqif Qarabağ xanlığında hansı vəzifəni tuturdu?',
        options: ['Vəzir', 'Sərkərdə', 'Tacir', 'Qazı'],
        answer: 0,
      },
      {
        q: 'Əsərdəki hadisələr əsasən harada cərəyan edir?',
        options: ['Qarabağda, Şuşa qalasında', 'Bakıda', 'Təbrizdə', 'Gəncədə'],
        answer: 0,
      },
      {
        q: 'Şuşaya hücum edən İran hökmdarı kimdir?',
        options: ['Ağa Məhəmməd şah Qacar', 'Nadir şah', 'Şah İsmayıl', 'Sultan Səlim'],
        answer: 0,
      },
      {
        q: 'Vaqifin şair dostu, məktublaşdığı müasiri kimdir?',
        options: ['Vidadi', 'Sabir', 'Nəbati', 'Zakir'],
        answer: 0,
      },
      {
        q: 'Əsərdə Vaqifin himayə etdiyi gənc aşiqlər kimlərdir?',
        options: ['Eldar və Xuraman', 'Leyli və Məcnun', 'Fərhad və Şirin', 'Aslan və Gülnar'],
        answer: 0,
      },
      {
        q: 'Vaqif Qacarın hədə-qorxusu qarşısında necə davranır?',
        options: ['Əyilmir, xalqın tərəfində qalır', 'Dərhal ona xidmətə keçir', 'Qaçıb gizlənir', 'Susub kənara çəkilir'],
        answer: 0,
      },
      {
        q: 'Qacar Vaqifə qarşı hansı addımı atır?',
        options: ['Onu həbs etdirir', 'Ona xələt bağışlayır', 'Onu vəzir təyin edir', 'Onu səfir göndərir'],
        answer: 0,
      },
      {
        q: 'Tarixi Molla Pənah Vaqif hansı əsrdə yaşamışdır?',
        options: ['XVIII əsrdə', 'XII əsrdə', 'XX əsrdə', 'XV əsrdə'],
        answer: 0,
      },
      {
        q: 'Vaqifin poeziyası nə ilə seçilir?',
        options: [
          'Sadə, aydın ana dilində yazılmış qoşmalarla',
          'Yalnız ərəbcə qəsidələrlə',
          'Dini rəvayətlərlə',
          'Elmi traktatlarla',
        ],
        answer: 0,
      },
      {
        q: 'Səməd Vurğun harada anadan olmuşdur?',
        options: ['Qazaxda', 'Şəkidə', 'Lənkəranda', 'Qubada'],
        answer: 0,
      },
      {
        q: 'Səməd Vurğuna verilmiş fəxri ad hansıdır?',
        options: ['Xalq şairi', 'Xalq artisti', 'Əməkdar müəllim', 'Xalq rəssamı'],
        answer: 0,
      },
      {
        q: 'Aşağıdakı əsərlərdən hansı Səməd Vurğunundur?',
        options: ['"Azərbaycan" şeiri', '"Poçt qutusu"', '"Dəli Kür"', '"İblis"'],
        answer: 0,
      },
      {
        q: 'Səməd Vurğunun məşhur poeması hansıdır?',
        options: ['"Komsomol poeması"', '"Qafqaz əsiri"', '"Sənsiz"', '"Xosrov və Şirin"'],
        answer: 0,
      },
      {
        q: '"Vaqif" dramında əsas ideya nədir?',
        options: [
          'Şair-vətəndaşın xalqa sədaqəti və zülmə boyun əyməməsi',
          'Var-dövlət toplamağın yolları',
          'Səyahət və kəşflər',
          'Ailə mübahisələri',
        ],
        answer: 0,
      },
      {
        q: 'Vaqif hansı xanlığın sarayında xidmət etmişdir?',
        options: ['Qarabağ xanlığında', 'Şirvan xanlığında', 'Naxçıvan xanlığında', 'Quba xanlığında'],
        answer: 0,
      },
      {
        q: 'Şuşa hansı bölgənin mərkəzi şəhəridir?',
        options: ['Qarabağın', 'Şirvanın', 'Muğanın', 'Naxçıvanın'],
        answer: 0,
      },
      {
        q: '"Vaqif" dramı ilk dəfə hansı növ səhnə əsəri kimi tamaşaya qoyulmuşdur?',
        options: ['Dram tamaşası', 'Opera', 'Balet', 'Kukla tamaşası'],
        answer: 0,
      },
      {
        q: 'Səməd Vurğun hansı dövrün şairidir?',
        options: ['XX əsrin', 'XII əsrin', 'XVI əsrin', 'XVIII əsrin'],
        answer: 0,
      },
    ],
  },

  {
    id: 'balaca-sahzade',
    title: 'Balaca Şahzadə',
    author: 'Antuan de Sent-Ekzüperi',
    tag: 'Fəlsəfi nağıl · 1943',
    cover: '/covers/balaca-sahzade.jpg',
    questions: [
      {
        q: '"Balaca Şahzadə" əsərinin müəllifi kimdir?',
        options: ['Antuan de Sent-Ekzüperi', 'Jül Vern', 'Viktor Hüqo', 'Mark Tven'],
        answer: 0,
      },
      {
        q: 'Müəllif əsas peşəsinə görə kim idi?',
        options: ['Təyyarəçi', 'Həkim', 'Dənizçi', 'Müəllim'],
        answer: 0,
      },
      {
        q: 'Əsər ilk dəfə neçənci ildə çap olunmuşdur?',
        options: ['1943', '1900', '1965', '1920'],
        answer: 0,
      },
      {
        q: 'Hekayəni danışan qəhrəman harada qəzaya uğrayır?',
        options: ['Sahara səhrasında', 'Okeanda', 'Dağlarda', 'Meşədə'],
        answer: 0,
      },
      {
        q: 'Balaca Şahzadə hansı planetdən gəlmişdir?',
        options: ['B-612 asteroidindən', 'Marsdan', 'Aydan', 'Veneradan'],
        answer: 0,
      },
      {
        q: 'Balaca Şahzadənin öz planetində qayğısına qaldığı çiçək hansıdır?',
        options: ['Qızılgül', 'Lalə', 'Nərgiz', 'Bənövşə'],
        answer: 0,
      },
      {
        q: 'Balaca Şahzadə planetində hansı təhlükəli ağacların cücərtilərini təmizləyirdi?',
        options: ['Baobabların', 'Palmaların', 'Şamların', 'Palıdların'],
        answer: 0,
      },
      {
        q: 'Təyyarəçi Balaca Şahzadənin xahişi ilə axırda nə çəkir?',
        options: ['İçində quzu olan qutu', 'Böyük bir gəmi', 'Uçan xalça', 'Qəsr'],
        answer: 0,
      },
      {
        q: 'Uşaqlıqda çəkdiyi şəkli böyüklər nə zənn edirdilər?',
        options: ['Şlyapa', 'Dağ', 'Bulud', 'Daş'],
        answer: 0,
      },
      {
        q: 'Əslində o şəkildə nə təsvir olunmuşdu?',
        options: ['Fil udmuş boa ilanı', 'Uçan quş', 'Batan gəmi', 'Yanan tonqal'],
        answer: 0,
      },
      {
        q: 'Tülkünün Balaca Şahzadəyə öyrətdiyi sirr hansıdır?',
        options: [
          'Ən vacib şeyləri yalnız ürəklə görmək olar',
          'Heç kimə inanmaq lazım deyil',
          'Var-dövlət hər şeydən üstündür',
          'Səyahət etmək faydasızdır',
        ],
        answer: 0,
      },
      {
        q: 'Tülkü Balaca Şahzadədən nə etməsini xahiş edir?',
        options: ['Onu əhliləşdirməsini', 'Ona yemək verməsini', 'Onu planetinə aparmasını', 'Onu rəsm çəkməyi öyrətməsini'],
        answer: 0,
      },
      {
        q: 'Balaca Şahzadə öz çiçəyi haqqında hansı nəticəyə gəlir?',
        options: [
          'Ona sərf etdiyi vaxt onu əvəzsiz edib',
          'Onun heç bir dəyəri yoxdur',
          'Bütün qızılgüllər eynidir',
          'Onu tamam unutmaq lazımdır',
        ],
        answer: 0,
      },
      {
        q: 'Balaca Şahzadə səyahətdə hansı planetdə fanarçı ilə görüşür?',
        options: ['Beşinci planetdə', 'Birinci planetdə', 'Doqquzuncu planetdə', 'Heç birində'],
        answer: 0,
      },
      {
        q: 'Ulduzları sayıb "onlar mənimdir" deyən obraz kimdir?',
        options: ['İşbaz (iş adamı)', 'Coğrafiyaçı', 'Tülkü', 'İlan'],
        answer: 0,
      },
      {
        q: 'Balaca Şahzadəyə "Mən hər şeyin hökmdarıyam" deyən obraz kimdir?',
        options: ['Padşah', 'Əyyaş', 'Fanarçı', 'Təyyarəçi'],
        answer: 0,
      },
      {
        q: 'Coğrafiyaçı Balaca Şahzadəyə hansı planeti ziyarət etməyi məsləhət görür?',
        options: ['Yer kürəsini', 'Marsı', 'Ayı', 'Yupiteri'],
        answer: 0,
      },
      {
        q: 'Balaca Şahzadə Yer üzündə ilk dəfə kiminlə rastlaşır?',
        options: ['İlanla', 'Tülkü ilə', 'Təyyarəçi ilə', 'Padşahla'],
        answer: 0,
      },
      {
        q: 'Balaca Şahzadə öz planetinə qayıtmaq üçün kimin köməyinə güvənir?',
        options: ['İlanın', 'Tülkünün', 'Coğrafiyaçının', 'Padşahın'],
        answer: 0,
      },
      {
        q: 'Əsərin əsas ideyası nədir?',
        options: [
          'Sevgi, dostluq və məsuliyyət insanı insan edir',
          'Güclü olan həmişə haqlıdır',
          'Pul olmadan xoşbəxtlik yoxdur',
          'Təklik ən yaxşı seçimdir',
        ],
        answer: 0,
      },
      {
        q: 'Əsər hansı dildə yazılmışdır?',
        options: ['Fransız dilində', 'İngilis dilində', 'Alman dilində', 'İspan dilində'],
        answer: 0,
      },
      {
        q: 'Balaca Şahzadənin planetindəki vulkanları o nə üçün təmizləyirdi?',
        options: ['Yaxşı yansınlar və partlamasınlar deyə', 'Orada yemək bişirmək üçün', 'Turistlər gəlsin deyə', 'Onları söndürmək üçün'],
        answer: 0,
      },
    ],
  },

  {
    id: 'aldanmis-kevakib',
    title: 'Aldanmış kəvakib',
    author: 'Mirzə Fətəli Axundzadə',
    tag: 'Povest · 1857',
    cover: '/covers/aldanmis-kevakib.jpg',
    questions: [
      {
        q: '"Aldanmış kəvakib" əsərinin müəllifi kimdir?',
        options: ['Mirzə Fətəli Axundzadə', 'Cəlil Məmmədquluzadə', 'Nəriman Nərimanov', 'Abbasqulu ağa Bakıxanov'],
        answer: 0,
      },
      {
        q: '"Aldanmış kəvakib" hansı janrda yazılmışdır?',
        options: ['Povest', 'Komediya', 'Poema', 'Qəzəl'],
        answer: 0,
      },
      {
        q: 'Əsərin ikinci adı (alt başlığı) nədir?',
        options: ['"Hekayəti-Yusif şah"', '"Hekayəti-Molla İbrahim"', '"Aldanmış ulduzlar"', '"Şah Abbasın səfəri"'],
        answer: 0,
      },
      {
        q: '"Kəvakib" sözü nə deməkdir?',
        options: ['Ulduzlar', 'Saraylar', 'Kitablar', 'Karvanlar'],
        answer: 0,
      },
      {
        q: 'Əsərdəki hadisələr hansı ölkədə cərəyan edir?',
        options: ['İranda', 'Osmanlı dövlətində', 'Hindistanda', 'Misirdə'],
        answer: 0,
      },
      {
        q: 'Əsərdə adı çəkilən hökmdar kimdir?',
        options: ['Şah Abbas', 'Nadir şah', 'Şah İsmayıl', 'Ağa Məhəmməd şah'],
        answer: 0,
      },
      {
        q: 'Şahı taxtdan müvəqqəti çəkilməyə vadar edən nə olur?',
        options: ['Münəccimlərin ulduzlara əsaslanan bəd xəbəri', 'Xarici ölkənin hücumu', 'Xəzinənin boşalması', 'Ağır xəstəlik'],
        answer: 0,
      },
      {
        q: 'Müvəqqəti şah seçilən Yusif əslində kim idi?',
        options: ['Sadə bir sənətkar (zərduz)', 'Sarayın vəziri', 'Ordu sərkərdəsi', 'Varlı tacir'],
        answer: 0,
      },
      {
        q: 'Yusif şah taxta çıxanda ilk növbədə nə etmək istəyir?',
        options: ['Xalqın vəziyyətini yaxşılaşdıran islahatlar', 'Yeni müharibəyə başlamaq', 'Özünə saray tikdirmək', 'Vergiləri artırmaq'],
        answer: 0,
      },
      {
        q: 'Yusif şahın islahatlarına kimlər müqavimət göstərir?',
        options: ['Saray əyanları və mövhumatçı ruhanilər', 'Kəndlilər', 'Uşaqlar', 'Tacirlər birliyi'],
        answer: 0,
      },
      {
        q: 'Yusif şahın hakimiyyəti necə başa çatır?',
        options: ['İslahatları baş tutmur, o, hakimiyyətdən salınır', 'Uzun illər ölkəni idarə edir', 'Könüllü olaraq başqa ölkəyə gedir', 'Şah Abbasla birgə idarə edir'],
        answer: 0,
      },
      {
        q: 'Əsərdə əsasən nə tənqid olunur?',
        options: ['Despotizm, mövhumat və cahil idarəçilik', 'Ticarətin inkişafı', 'Elmin öyrənilməsi', 'Kənd təsərrüfatı'],
        answer: 0,
      },
      {
        q: '"Aldanmış kəvakib" Azərbaycan ədəbiyyatında hansı yeri tutur?',
        options: ['İlk bədii nəsr (povest) nümunələrindən biridir', 'İlk qəzəl kitabıdır', 'İlk dastan toplusudur', 'İlk uşaq şeirləri kitabıdır'],
        answer: 0,
      },
      {
        q: 'Mirzə Fətəli Axundzadə hansı sahənin banisi sayılır?',
        options: ['Azərbaycan dramaturgiyasının', 'Azərbaycan jurnalistikasının', 'Azərbaycan kinosunun', 'Azərbaycan rəssamlığının'],
        answer: 0,
      },
      {
        q: 'Aşağıdakı komediyalardan hansı Axundzadənindir?',
        options: ['"Hacı Qara"', '"Ölülər"', '"Almaz"', '"Anamın kitabı"'],
        answer: 0,
      },
      {
        q: 'Axundzadənin fəlsəfi əsəri hansıdır?',
        options: ['"Kəmalüddövlə məktubları"', '"Hophopnamə"', '"Gülüstan"', '"Divan"'],
        answer: 0,
      },
      {
        q: 'Mirzə Fətəli Axundzadə harada anadan olmuşdur?',
        options: ['Nuxada (Şəkidə)', 'Bakıda', 'Gəncədə', 'Naxçıvanda'],
        answer: 0,
      },
      {
        q: 'Axundzadə ömrünün böyük hissəsini hansı şəhərdə yaşamışdır?',
        options: ['Tiflisdə', 'Təbrizdə', 'İstanbulda', 'Peterburqda'],
        answer: 0,
      },
      {
        q: 'Axundzadə hansı sahədə islahat layihəsi irəli sürmüşdür?',
        options: ['Əlifba islahatı', 'Hərbi islahat', 'Vergi islahatı', 'Torpaq islahatı'],
        answer: 0,
      },
      {
        q: 'Axundzadə neçə komediya yazmışdır?',
        options: ['6', '2', '12', '20'],
        answer: 0,
      },
      {
        q: 'Əsərin adı hansı mənanı verir?',
        options: ['"Aldanmış ulduzlar"', '"İtmiş xəzinə"', '"Yanılmış şah"', '"Səyyahın gündəliyi"'],
        answer: 0,
      },
    ],
  },

  {
    id: 'hophopname',
    title: 'Hophopnamə',
    author: 'Mirzə Ələkbər Sabir',
    tag: 'Satirik şeirlər · 1912',
    cover: '/covers/hophopname.jpg',
    questions: [
      {
        q: '"Hophopnamə" kimin şeirlər toplusudur?',
        options: ['Mirzə Ələkbər Sabirin', 'Məhəmməd Hadinin', 'Abbas Səhhətin', 'Hüseyn Cavidin'],
        answer: 0,
      },
      {
        q: 'Kitabın adı şairin hansı imzasından yaranıb?',
        options: ['Hophop', 'Molla Nəsrəddin', 'Lağlağı', 'Nakam'],
        answer: 0,
      },
      {
        q: 'Sabirin əsl adı nədir?',
        options: ['Ələkbər Tahirzadə', 'Ələkbər Sabirov', 'Əlibala Hacızadə', 'Ələsgər Talıbzadə'],
        answer: 0,
      },
      {
        q: 'Mirzə Ələkbər Sabir harada anadan olmuşdur?',
        options: ['Şamaxıda', 'Bakıda', 'Gəncədə', 'Şuşada'],
        answer: 0,
      },
      {
        q: 'Sabirin şeirləri əsasən hansı jurnalda çap olunurdu?',
        options: ['"Molla Nəsrəddin"', '"Əkinçi"', '"Kəşkül"', '"Ziya"'],
        answer: 0,
      },
      {
        q: '"Molla Nəsrəddin" jurnalının naşiri kim idi?',
        options: ['Cəlil Məmmədquluzadə', 'Həsən bəy Zərdabi', 'Əli bəy Hüseynzadə', 'Firidun bəy Köçərli'],
        answer: 0,
      },
      {
        q: 'Sabirin yaradıcılığının əsas istiqaməti nədir?',
        options: ['Satira', 'Qəhrəmanlıq dastanı', 'Elmi əsərlər', 'Səyahətnamə'],
        answer: 0,
      },
      {
        q: '"Oxutmuram, əl çəkin!" şeirində Sabir nəyi tənqid edir?',
        options: ['Uşaqları oxutmaq istəməyən cahil valideynləri', 'Müəllimlərin çoxluğunu', 'Kitabların bahalığını', 'Məktəb binalarını'],
        answer: 0,
      },
      {
        q: '"Fəhlə, özünü sən də bir insanmı sanırsan?" misrası kimin qələmindəndir?',
        options: ['Sabirin', 'Səməd Vurğunun', 'Nizaminin', 'Füzulinin'],
        answer: 0,
      },
      {
        q: 'Sabir satiralarında obrazları necə ifşa edir?',
        options: ['Obrazın öz dili ilə danışdıraraq', 'Yalnız müəllif təsviri ilə', 'Yalnız şəkillərlə', 'Dialoqsuz nəsrlə'],
        answer: 0,
      },
      {
        q: '"Hophopnamə" nə vaxt kitab halında nəşr olunmuşdur?',
        options: ['Şair vəfat etdikdən sonra', 'Şairin ilk gəncliyində', 'Şair uşaq ikən', 'Heç vaxt nəşr olunmayıb'],
        answer: 0,
      },
      {
        q: 'Sabir dolanışığını təmin etmək üçün hansı işlə məşğul olurdu?',
        options: ['Sabun bişirməklə', 'Dəmirçiliklə', 'Balıqçılıqla', 'Xalçaçılıqla'],
        answer: 0,
      },
      {
        q: 'Sabirin Şamaxıda açdığı məktəbin adı nə idi?',
        options: ['"Ümid"', '"Səadət"', '"Nicat"', '"İrşad"'],
        answer: 0,
      },
      {
        q: 'Sabir satiralarında hansı problemə xüsusi yer verirdi?',
        options: ['Qadın azadlığı və maarifə münasibət', 'Dəniz ticarəti', 'Memarlıq üslubları', 'Musiqi nəzəriyyəsi'],
        answer: 0,
      },
      {
        q: 'Sabir hansı ədəbi cərəyanın nümayəndəsidir?',
        options: ['Tənqidi realizm', 'Romantizm', 'Sentimentalizm', 'Klassisizm'],
        answer: 0,
      },
      {
        q: 'Sabir şeirlərini hansı dildə, necə yazırdı?',
        options: ['Xalqın başa düşdüyü sadə ana dilində', 'Yalnız ərəbcə', 'Yalnız farsca', 'Yalnız rusca'],
        answer: 0,
      },
      {
        q: '"Hophopnamə"yə hansı şeirlər daxildir?',
        options: ['Satiralar, lirik və uşaq şeirləri', 'Yalnız mərsiyələr', 'Yalnız tərcümələr', 'Yalnız poemalar'],
        answer: 0,
      },
      {
        q: 'Sabir hansı əsrin şairidir?',
        options: ['XIX–XX əsrin', 'XII əsrin', 'XVI əsrin', 'XVIII əsrin'],
        answer: 0,
      },
      {
        q: 'Sabirin satiraları kimlərə qarşı yönəlmişdi?',
        options: ['Cahillərə, mövhumatçılara və zülmkarlara', 'Müəllimlərə və həkimlərə', 'Uşaqlara', 'Fəhlələrə'],
        answer: 0,
      },
      {
        q: 'Sabir yaradıcılığı ilə hansı böyük yazıçı eyni jurnalda çalışırdı?',
        options: ['Cəlil Məmmədquluzadə', 'Mirzə Fətəli Axundzadə', 'Nizami Gəncəvi', 'İsmayıl Şıxlı'],
        answer: 0,
      },
      {
        q: 'Sabir hansı şəhərdə vəfat etmişdir?',
        options: ['Şamaxıda', 'Bakıda', 'Tiflisdə', 'Gəncədə'],
        answer: 0,
      },
    ],
  },
]

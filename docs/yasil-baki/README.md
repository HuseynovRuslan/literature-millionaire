# Yaşıl Bakı — bitki sual bankası

Bu qovluqda oyuna girməyə hazır bitki siyahısı və ona necə gəlindiyi var. Oyun bazasına hələ heç nə
yazılmayıb.

## Nə gəldi, nə qaldı

Başlanğıc: başqa bir modelin hazırladığı 216 bitki / 849 namizəd foto. Nə adlar, nə şəkillər
yoxlanmışdı — o modelin öz sandbox-u şəkil hostlarına çıxa bilmirdi, ona görə heç bir foto açılmamışdı.

| addım | qalan |
|---|---|
| başlanğıc kataloq | 216 |
| Wikidata-da elmi adı təsdiqlənən + mənbəli Azərbaycanca adı olan + şəkli olan | 124 |
| şəkillərə əl ilə baxıldıqdan sonra | **24** |

## Niyə 124-dən 24

Şəkillərin demək olar hamısı **düzgündür** — avtomatik yoxlama (fayl adı və Commons kateqoriyası növü
təsdiqləyirmi) 124-dən 119-unu keçirir. Problem düzgünlükdə deyil.

Sual belədir: *adi bir adam telefonda, 10 saniyə ərzində, bu şəklə baxıb bitkinin adını deyə bilərmi?*

Bu meyara görə:

- **63 bitki — ayırd edə bilmirəm.** Yetkin göyrüş, qarağac, qovaq, aylant bir-birindən fərqlənmir; üç
  sidr, iki sərv, iki ardıc, tuya və səlbi eynidir; beş palma eynidir; iki yukka, iki itburnu, iki
  qıfotu, iki birgöz bankın içində bir-birini yeyir. Bunları mən özüm seçə bilmirəmsə, oyunçu da
  bilməyəcək.
- **17 bitki — şəkil zəif.** Şəkil doğrudur, amma bitkini verən hissə görünmür: ginkqo uzaqdan yaşıl
  konusdur (məşhur yelpik yarpağı yoxdur), atşabalıdı çiçəksiz-meyvəsizdir, iydə çox qaranlıqdır,
  Laqerstroemiya şəkli **90° yan çevrilmiş** yüklənib.
- **16 bitki — ad tanınmır.** «Süpürgəvari kölreyteriya», «İran meşənovruzu», «Sərvəoxşar santolina» —
  şəkil yaxşı olsa da, bu cavab variantı oyunçuya heç nə demir. «Erməni ilansoğanı» ayrıca çıxarılıb.
- **4 bitki — şəkil foto deyil, köhnə botanika rəsmidir.** Lavanda, üzüm, gülümbaharı və mərsin üçün
  Wikidata-nın seçdiyi şəkil Köhlerin 1887-ci il dərman bitkiləri atlasından rəsmdir. Bunlar ən yaxşı
  namizədlər idi — **şəkil dəyişdirilsə, dördü də dərhal banka qayıda bilər.**

## Fayllar

- `yasil-baki-final.json` — 24 bitki: adı, elmi adı, çətinlik, şəkil (URL, lisenziya, müəllif), ad mənbəyi
- `verdicts.json` — 124-ün hamısı üçün qərar və səbəb
- `photos/` — yekun 24 şəkil (WebP)
- `baxis-01.webp`, `baxis-02.webp` — yekun bank bir vərəqdə
- `tools/` — kataloqu Wikidata-dan quran və qərarı tətbiq edən skriptlər

## Lisenziya

Şəkillərin hamısı sərbəst lisenziyalıdır (əsasən CC BY-SA, bir neçəsi ictimai mülkiyyət / CC0), lakin
müəllif göstərilməsi tələb olunur. Müəllif və lisenziya hər bitki üçün JSON-da saxlanılıb — oyuna
salınanda mövcud «Mənbələr» səhifəsinə əlavə olunmalıdır.

## Bunu kim təsdiqləməlidir

Bu seçimi aqronom yox, texnologiya etdi: elmi ad GBIF/Wikidata-dan, Azərbaycanca ad az.wikipedia-dan,
şəkil isə eyni Wikidata qeydindən gəlir — yəni ad da, şəkil də bir redaktə zəncirinə söykənir. Şübhə
olan hər şey atılıb. Buna baxmayaraq, 24 sual ictimaiyyətə çıxmadan əvvəl bitkiləri tanıyan bir nəfərin
gözdən keçirməsi yaxşı olardı; `baxis-01.webp` və `baxis-02.webp` məhz bunun üçündür — iki vərəq, beş
dəqiqəlik iş.

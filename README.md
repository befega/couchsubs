<p align="center"><img src="design/logo-1024.png" width="120" alt="CouchSubs"></p>

# CouchSubs

**PS5 oyunlarına Türkçe yama: Türkçe dil desteği olmayan oyunları Türkçe altyazıyla oynayın.**

Site, kurulum rehberi ve SSS: **[couchsubs.enmeraapps.com](https://couchsubs.enmeraapps.com)**

CouchSubs, oyundaki İngilizce altyazıyı anlık okur, Türkçeye çevirir ve TV'de oyun görüntüsünün üstünde
gösterir. Oyunu her zamanki gibi TV'de, kendi kolunuzla oynarsınız. Yazı tanıma ve varsayılan çeviri bilgisayarınızda
yapılır; oyun görüntüsü internete gönderilmez. Hazır paketlerde bulunmayan İngilizce altyazı satırları varsayılan olarak
açık olan ortak havuza gönderilebilir (ayarlardan kapatılabilir). Çevrimiçi çeviriyi seçerseniz metin ilgili hizmete de
gönderilir. Ayrıntılar: [Gizlilik](PRIVACY.md). Ücretsiz ve açık kaynaklıdır.

*English below.*

> **Yayıncılar için (Windows):** Capture card ve OBS ile yayın yapıyorsanız CouchSubs görüntüyü doğrudan OBS'den okuyabilir;
> ikinci PSN hesabı, PS5 eşleştirmesi ya da TV gerekmez. Birden çok çeviri motorunu (DeepL, yerel Gemma, ücretsiz
> OPUS-MT) aynı anda çalıştırıp en iyi çeviriyi seçen **seçici** de var. Adım adım kurulum:
> **[Yayıncılar için: capture card ve OBS ile kurulum](#yayıncılar-için-capture-card-ve-obs-ile-kurulum-windows)**.

## Oyunda nasıl görünüyor

Mafia: The Old Country, PS5'te İngilizce oynanıyor; Türkçe altyazı TV'de oyunun üstünde, CouchSubs bilgisayarda çalışıyor.

https://github.com/user-attachments/assets/bb8569d8-115d-49c7-a772-da2d9e171c17

## Ekran görüntüleri

**Mac** (uygulamanın kendisi, örnek verilerle):

| Ana ekran | Kurulum |
|---|---|
| <img src="docs/screenshots/mac/ana.png" width="420" alt="Ana ekran"> | <img src="docs/screenshots/mac/kurulum.png" width="420" alt="Kurulum: PS5'i eşle"> |
| **Ayarlar › Çeviri** | **Ayarlar › Oyun paketleri** |
| <img src="docs/screenshots/mac/ayarlar-ceviri.png" width="420" alt="Ayarlar: çeviri motoru"> | <img src="docs/screenshots/mac/ayarlar-paketler.png" width="420" alt="Ayarlar: oyun paketleri"> |

**Windows** (tasarım; uygulama aynı düzeni kullanır):

| Ana ekran | Kurulum |
|---|---|
| <img src="docs/screenshots/windows/ana.png" width="420" alt="Windows ana ekran tasarımı"> | <img src="docs/screenshots/windows/kurulum.png" width="420" alt="Windows kurulum tasarımı"> |
| **Ayarlar › Çeviri** | **Ayarlar › Oyun paketleri** |
| <img src="docs/screenshots/windows/ceviri.png" width="420" alt="Windows çeviri ayarları tasarımı"> | <img src="docs/screenshots/windows/paketler.png" width="420" alt="Windows oyun paketleri tasarımı"> |
| **Bilgisayar ekranındaki altyazı penceresi** | |
| <img src="docs/screenshots/windows/pencere.png" width="420" alt="Altyazı penceresi tasarımı"> | |

Windows'ta oyun paketlerinin kare rozetine tıklayarak kendi **PNG/JPEG oyun kapağınızı** seçebilirsiniz.
Görsel 128×128 PNG olarak yalnız bilgisayarınızdaki
`%LOCALAPPDATA%\CouchSubs\covers` klasörüne kaydedilir; sağ tıkla kaldırılabilir. Kapak yoksa
ACS/COD gibi kısaltma görünür. Uygulama telifli oyun görsellerini indirmez veya dağıtmaz.

**Telefon uygulaması** (Android: [Google Play](https://play.google.com/store/apps/details?id=com.couchsubs.mobile); iPhone yakında; tasarım; o da tamamen ücretsiz, hesap gerekmez; yatay altyazı ekranı:
[09-altyazi.png](docs/screenshots/telefon/09-altyazi.png)):

| | | | |
|---|---|---|---|
| <img src="docs/screenshots/telefon/01-karsilama-1.png" width="180" alt="Karşılama"> | <img src="docs/screenshots/telefon/02-karsilama-2.png" width="180" alt="Karşılama"> | <img src="docs/screenshots/telefon/03-psn.png" width="180" alt="PSN girişi"> | <img src="docs/screenshots/telefon/04-ps5.png" width="180" alt="PS5 eşleme"> |
| Karşılama | Karşılama | PSN girişi | PS5 eşleme |
| <img src="docs/screenshots/telefon/05-hedef.png" width="180" alt="Altyazı nerede"> | <img src="docs/screenshots/telefon/06-tur.png" width="180" alt="Kullanım turu"> | <img src="docs/screenshots/telefon/07-ana.png" width="180" alt="Ana ekran"> | <img src="docs/screenshots/telefon/08-calisiyor.png" width="180" alt="Çeviri sürüyor"> |
| Altyazı nerede | Kullanım turu | Ana ekran | Çeviri sürüyor |
| <img src="docs/screenshots/telefon/10-paketler.png" width="180" alt="Oyun paketleri"> | <img src="docs/screenshots/telefon/11-cihazlarim.png" width="180" alt="Cihazlarım"> | <img src="docs/screenshots/telefon/12-ayarlar.png" width="180" alt="Ayarlar"> | |
| Oyun paketleri | Cihazlarım | Ayarlar | |

## İndir

| Bilgisayarınız | Tek tıkla indirin |
|---|---|
| **Mac** (macOS 13+, Apple Silicon ve Intel) | **[CouchSubs-macos.dmg](https://github.com/yorulmazsinan/couchsubs/releases/latest/download/CouchSubs-macos.dmg)** |
| **Windows** 10 / 11 (64 bit) | **[CouchSubs-windows-Setup.exe](https://github.com/yorulmazsinan/couchsubs/releases/latest/download/CouchSubs-windows-Setup.exe)** |

**Android telefon** (Android 8+, 64 bit; bilgisayar gerekmez): **[Google Play'den indirin](https://play.google.com/store/apps/details?id=com.couchsubs.mobile)** (Play Store'da
"CouchSubs: Türkçe Yama"). Daha önce siteden APK kurduysanız Play sürümünden önce onu silin.

TV uygulamasını ayrıca indirmeniz gerekmez: bilgisayar uygulaması TV'nize kendisi kurar. Uygulama yeni sürümleri
kendisi bulur ve tek tıkla günceller. Kurulum istemeyenler için zip paketleri de duruyor:
[Mac](https://github.com/yorulmazsinan/couchsubs/releases/latest/download/CouchSubs-macos.zip) ·
[Windows](https://github.com/yorulmazsinan/couchsubs/releases/latest/download/CouchSubs-windows-x64.zip).

> **Beta.** Uygulama deneme aşamasındadır ve denemeniz bize çok yardımcı olur. Sorunları ve önerileri
> [Issues](https://github.com/yorulmazsinan/couchsubs/issues) sayfasından bildirebilirsiniz.

## Hangi TV'lerde çalışır

| TV | Durum |
|---|---|
| **LG** (webOS 4 ve üstü) | Gerçek TV'de denendi. Uygulamayı CouchSubs kendisi kurar |
| **Samsung** (Tizen, 2019 ve sonrası) | Gerçek TV'de denendi (75" Neo QLED). Uygulamayı CouchSubs kendisi kurar; bir kez Samsung hesabıyla giriş istenir |
| **Samsung** (Tizen 2.4–4.0, 2016–2018) | Deneysel: kullanıcılar UE43MU7000U'da (2017) ve 40KU7000'de (2016) elle kurup çalıştırdı; CouchSubs'ın kendi kurulumu bu TV'lerde henüz denenmedi ([ayrıntı](docs/tv-kurulum.md#20162018-samsung-tvler-deneysel)) |
| **Android TV / Google TV** (Vestel, Arçelik, Beko, Grundig, Philips ve diğerleri) | Gerçek TV'de denendi. Uygulamayı CouchSubs ağ üzerinden kendisi kurar |
| Vestel, Arçelik, Beko, Grundig'in Linux modelleri, Philips Saphi ve Titan OS | Desteklenmiyor: uygulama kurmanın bir yolunu bulamadık |

Bilgisayar tarafında Mac ve Windows gerçek cihazla denendi. Geri bildiriminiz çok değerli.

TV'de yapmanız gereken tek şey geliştirici modunu açmak. Adımları bilgisayar uygulaması ekranda gösterir ve TV'yi
kendisi bekler. Resimli rehber isterseniz: [docs/tv-kurulum.md](docs/tv-kurulum.md)

## Nasıl çalışır

```
PS5 ──HDMI──▶ TV (CouchSubs TV uygulaması: HDMI görüntüsü + altyazı katmanı)
 │                          ▲
 └─ Remote Play ──▶ CouchSubs (Mac / Windows) ── ev ağı
    (ikinci PSN hesabı)    okur → çevirir → TV'ye gönderir
```

Bilgisayar, PS5'e ikinci bir PlayStation Network hesabıyla Remote Play üzerinden bağlanır ve görüntüyü
yalnız altyazıyı okumak için kullanır. Siz oyunu HDMI'dan, gecikmesiz oynamaya devam edersiniz.

## Gerekenler

- **PS5** ve yukarıdaki listeden bir **TV**, bilgisayarla aynı ev ağında
- Ücretsiz **ikinci bir PlayStation Network hesabı** (kendi hesabınızla Remote Play kolunuzu
  kapatırdı; ikinci hesapla oyununuz kesilmez)
- Bilgisayar, şunlardan biri:
  - **Mac:** macOS 13 Ventura veya üstü, Apple Silicon (M1 ve sonrası) ya da Intel. Apple'ın ücretsiz çevirisi
    macOS 26 ister; daha eski macOS'ta oyun paketleri, Gemini (ücretsiz anahtar), DeepL ya da yapay zekâ çevirisi kullanılır
  - **Windows:** Windows 10 (2004) veya 11, 64 bit; ilk açılışta çeviri modeli için yaklaşık 475 MB indirme

## Kurulum

1. Yukarıdan bilgisayarınıza uygun dosyayı indirin.
   - **Mac:** DMG'yi açın, CouchSubs'ı **Uygulamalar** klasörüne sürükleyip açın. Uygulama Apple onaylıdır.
   - **Windows:** `CouchSubs-windows-Setup.exe`'yi açın; yönetici izni istemeden kurulur, Başlat menüsüne ve masaüstüne
     kısayol koyar. "Windows bilgisayarınızı korudu" uyarısı çıkarsa **Ek bilgi › Yine de çalıştır**'a basın. Güvenlik
     duvarı sorarsa **Özel ağlar**'a izin verin.
2. Kurulum sihirbazını izleyin: ikinci PSN hesabıyla giriş, PS5 eşleme, TV seçimi.
3. TV adımında uygulama, TV'nizin markasına göre geliştirici modunu nasıl açacağınızı gösterir. Açtığınızda TV
   uygulamasını kendisi kurar:
   - **LG:** TV ekranındaki 6 karakterli parolayı bir kez yazarsınız.
   - **Samsung:** tarayıcıda bir kez Samsung hesabınızla giriş yaparsınız (Samsung uygulamayı TV'nize özel imzalatır).
   - **Android TV:** TV'de "hata ayıklamaya izin verilsin mi?" sorusuna **İzin ver** dersiniz.

**Bilmeniz gerekenler**

- LG'de geliştirici modu en çok 1000 saat açık kalır. CouchSubs her kullanışınızda süreyi kendisi baştan başlatır;
  uygulamayı uzun süre kullanmazsanız TV'deki Developer Mode uygulamasından **EXTEND** ile uzatın.
- Samsung'da TV, CouchSubs TV uygulaması açıkken sürekli HDMI 1'e ya da başka bir kaynağa geçiyorsa TV'de
  **Ayarlar › Tüm Ayarlar › Bağlantı › Harici Cihaz Yöneticisi** altında **Anynet+ (HDMI-CEC)** ve **Otomatik Kaynak
  Değiştirme**'yi kapatın. Samsung TV'yi yeniden başlatmak için güç tuşunu yaklaşık 2 saniye basılı tutun; kısa basınca
  yalnız bekleme moduna geçer.
- Altyazı, oyunda altyazı açıksa okunabilir. Bölgeyi **Ayarlar › Altyazı alanı**'ndan oyuna göre ayarlayabilirsiniz.
- **Kısayollar** (yalnız çeviri sürerken, CouchSubs arka plandayken de çalışır): Windows'ta **Num 9** ya da
  **Ctrl+Shift+9** ekranı çevirir, **Num 1–3** / **Ctrl+Shift+1–3** ek bölgeleri, **Num 5** / **Ctrl+Shift+5** serbest
  alanı çevirir, **Num 0** / **Ctrl+Shift+0** canlı altyazıyı duraklatır, **Num 7** / **Ctrl+Shift+7** altyazı
  penceresinde tıklamaları geçirir, **Num 8** / **Ctrl+Shift+8** ayarlardaki görüntüyü yeniler. Sayı tuş takımı
  olmayan dizüstünde harflerin üstündeki rakamlar Ctrl+Shift ile kullanılır. Windows'ta tuşlar **Ayarlar ›
  Kısayollar**'dan değiştirilebilir. Mac'te ekranı çevir **⌥⌘T**,
  tıklamalar **⌥⌘L**.
- Çeviri makine çevirisidir; bazı cümleler kusurlu olabilir.

## Yerel yapay zekâ ile çeviri (Ollama, LM Studio)

Çeviriyi kendi bilgisayarınızda çalışan bir yapay zekâ modeline yaptırabilirsiniz: anahtar, kota ve ücret yoktur,
altyazı metni bilgisayardan çıkmaz. Hız bilgisayarınıza bağlıdır; ekran kartı ya da Apple Silicon olan bir bilgisayarda
7-8 milyar parametreli bir model (ör. Qwen 2.5 7B) bir altyazıyı genellikle bir iki saniyede çevirir. Model yanıt
vermezse o satır her zamanki ücretsiz çeviriyle gelir.

**Ollama ile**

1. [ollama.com/download](https://ollama.com/download)'dan Ollama'yı kurup açın.
2. Terminal'de (Windows'ta PowerShell) modeli indirin: `ollama pull qwen2.5:7b`
3. CouchSubs'ta **Ayarlar › Çeviri › Çeviri motoru › Yerel model**'i seçin. Sunucu adresi
   `http://127.0.0.1:11434/v1`, model adı `qwen2.5:7b` olarak gelir; **Bağlantıyı dene**'ye basın.

**LM Studio ile**

1. [lmstudio.ai](https://lmstudio.ai)'dan LM Studio'yu kurun, bir model indirip yükleyin (ör. Qwen 2.5 7B Instruct).
2. **Developer** bölümünden yerel sunucuyu başlatın (varsayılan adres `http://localhost:1234/v1`).
3. CouchSubs'ta **Yerel model**'i seçin, **LM Studio'yu kullan**'a basın, model adını LM Studio'da göründüğü gibi yazın
   ve **Bağlantıyı dene**'ye basın.

Başka bir bilgisayardaki ya da OpenAI uyumlu başka bir sunucuyu da adresini yazarak kullanabilirsiniz; sunucu anahtar
istiyorsa **API anahtarı (isteğe bağlı)** alanına yazın. İlk istek modeli belleğe yüklediği için uzun sürebilir;
Bağlantıyı dene bunu da bekler. Çeviriler sunucu adresi ve model adıyla ayrı saklanır; modeli değiştirince eski
modelin çevirileri karışmaz.

Yerel model sıcaklık 0 ile çalışır: aynı satır her seferinde aynı çevrilir. Ollama'ya giden her istek modeli 24 saat
bellekte tutar (`keep_alive`); oyunda uzun süre altyazı çıkmasa da ilk satır modelin yeniden yüklenmesini beklemez.
Modeli daha erken boşaltmak isterseniz `ollama stop <model>` yazın. LM Studio'da modelin bellekte kalma süresi LM
Studio'nun kendi ayarlarındadır.

**Ek talimat:** Yapay zekâ motorlarında (Gemini, OpenAI, Claude, yerel model) Ayarlar'daki çeviri motoru bölümünde bulunan
**Ek talimat** kutusuna yazdığınız metin çevirmenin talimatının sonuna eklenir (en çok 1000 karakter). Örneğin
"Küfürleri yumuşatmadan çevir." yazabilirsiniz. Boş bırakılırsa hiçbir şey değişmez; talimatı değiştirince önceki
talimatla yapılmış çeviriler yeniden kullanılmaz.

## Yayıncılar için: capture card ve OBS ile kurulum (Windows)

PS5'i capture card ile bilgisayara bağlayıp OBS'den (Kick, Twitch, YouTube) yayın yapıyorsanız CouchSubs oyun görüntüsünü
**OBS'deki capture card kaynağından** okur. Capture card'ı CouchSubs açmaz, OBS tutmaya devam eder: CouchSubs OBS'nin
kendi WebSocket sunucusundan yalnız o kaynağın anlık görüntüsünü ister (saniyede en çok 8 kare). Yayına, kayda ve
sahnelere dokunulmaz. Türkçe altyazı yayına OBS'deki bir tarayıcı kaynağıyla eklenir; CouchSubs sahneyi değil kaynağın
kendisini okuduğu için kendi altyazısını yeniden okumaz.

```
PS5 ──HDMI──▶ Capture card ──▶ OBS ──────────────────────────▶ Yayın / kayıt
                                │  ▲
             obs-websocket:     │  │  Tarayıcı kaynağı: http://localhost:8787/obs
             kaynağın görüntüsü ▼  │  (Türkçe altyazı katmanı)
                               CouchSubs ── okur → çevirir
```

Bu yolda **ikinci PSN hesabı, PS5 eşleştirmesi ve TV gerekmez**. Önerilen bilgisayar: ekran kartı NVIDIA olan bir Windows
10/11 (ör. RTX 3090). Yazı tanıma ve ücretsiz çeviri bilgisayarda çalışır; yerel yapay zekâ modeli için ekran kartı
belleği gerekir (aşağıda).

### 1. Bu sürümü edinin

Capture card / OBS kaynağı, kaynağa göre değişen kurulum sihirbazı ve seçici şimdilik bu depodaki
`feat/windows-obs-websocket` dalındadır; resmî sürümde (yukarıdaki İndir bağlantıları) henüz yoktur. İki yol var:

**A. Kendi bilgisayarınızda derleyin (önerilen, 10-15 dakika)**

1. PowerShell'i açıp gereken araçları bir kez kurun, sonra PowerShell'i kapatıp yeniden açın:
   ```powershell
   winget install Git.Git
   winget install Microsoft.DotNet.SDK.10
   winget install Python.Python.3.12
   winget install Microsoft.VCRedist.2015+.x64
   ```
2. Kodu indirin:
   ```powershell
   git clone -b feat/windows-obs-websocket https://github.com/befega/couchsubs.git
   cd couchsubs
   ```
3. Derleyin:
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts\build-windows-local.ps1
   ```
   Betik araçları denetler, uygulamayı derler, ücretsiz çeviri motorunu (CTranslate2) sabit sürüm ve özetle indirip
   dener. Önce testleri de koşmak için sona `-Test` ekleyin.
4. `publish\CouchSubs\CouchSubs.exe`'yi açın. Klasörü istediğiniz yere taşıyabilirsiniz; kurulum gerekmez. Yeni
   değişiklikleri almak için klasörde `git pull` yapıp 3. adımı yeniden çalıştırın.

Bu hafif derlemede PS5'e doğrudan bağlanma (ikinci PSN hesabıyla; MSYS2 ile derlenen yerel kütüphane ister) ve Türkçe
dublaj yoktur. OBS kaynağı, pencere kaynakları, bütün çeviri motorları, seçici ve OBS katmanı çalışır.

**B. GitHub Actions ile tam paket**

Depodaki **windows** iş akışı `feat/windows-*` dallarında kurulum dosyası dahil tam paketi derler (GitHub hesabınızda
Actions kullanılabilir olmalı):

1. Depoda **Actions** sekmesini açın. Fork'ta ilk kez açıyorsanız **I understand my workflows, go ahead and enable them**'a
   basın.
2. Soldan **windows** iş akışını seçin, **Run workflow** ile dalı `feat/windows-obs-websocket` seçip başlatın. Derleme
   uzun sürer (yarım saati bulabilir).
3. Biten çalışmanın altındaki **Artifacts** bölümünden `couchsubs-windows-installer`'ı (içinde
   `CouchSubs-windows-Setup.exe`) ya da kurulumsuz `couchsubs-windows` klasörünü indirin.

İki yolda da "Windows bilgisayarınızı korudu" uyarısında **Ek bilgi › Yine de çalıştır**'a basın (uygulama imzasızdır).
Güvenlik duvarı sorarsa **Özel ağlar**'a izin verin.

> **Güncelleme uyarısı:** Bu derlemenin sürümü `0.1.0-dev`'dir. Uygulama resmî sürüme güncellemeyi önerirse
> **kabul etmeyin**; resmî sürüm bu derlemenin yerine geçer ve OBS kaynağı ile seçici kaybolur.

### 2. OBS'yi hazırlayın

1. **OBS 28 veya sonrası** kullanın (WebSocket sunucusu OBS'nin içinde gelir).
2. Capture card'ı bir sahneye **Video Yakalama Aygıtı** olarak ekleyin ve kaynağın adını not edin (ör. `Elgato HD60 X`).
   Kaynak **bir sahnede görünür** olmalı; gizli ya da hiçbir sahnede olmayan kaynağın görüntüsü alınamaz.
3. **Araçlar › WebSocket Sunucu Ayarları**'nı açın, **WebSocket sunucusunu etkinleştir**'i işaretleyin (port `4455`).
4. **Bağlantı Bilgilerini Göster**'e basıp parolayı kopyalayın.

OBS başka bir bilgisayardaysa adres `IP:4455` olur (ör. `192.168.1.20:4455`); o bilgisayarın güvenlik duvarında 4455
portuna izin verin.

### 3. CouchSubs kurulum sihirbazı

1. **CouchSubs'a hoş geldiniz:** konsol olarak **PlayStation 5**'i, "CouchSubs oyun görüntüsünü nereden alsın?" sorusunda
   **OBS'deki capture card (obs-websocket)**'u seçin. Sihirbaz buna göre kısalır; PSN girişi ve PS5 eşleştirmesi hiç
   sorulmaz.
2. **OBS'ye bağlanın:** adres (aynı bilgisayarda `localhost:4455`) ve parolayı yazıp **Bağlan ve kaynakları listele**'ye
   basın. Listeden capture card kaynağını seçin; yakalama aygıtları listede en üsttedir. OBS o an kapalıysa **Şimdilik
   geç** ile devam edip kaynağı sonra **Ayarlar › Görüntü kaynağı**'ndan seçebilirsiniz.
3. **TV'nizi bağlayın:** yayında TV gerekmez; **TV'm yok, bilgisayar ekranında göster**'i seçin.
4. **Çeviri modelini indirin** (ücretsiz çeviri, yaklaşık 475 MB), test adımını geçin. "Daha iyi çeviri" adımı
   isteğe bağlıdır.

OBS kullanmadan bir pencereden okumak isterseniz (ör. OBS projektörü ya da capture card'ın kendi uygulaması) ilk adımda
**Capture card penceresi (OBS projektörü ya da yakalama uygulaması)**'nı seçin; sihirbaz sizden pencereyi seçmenizi ister.

### 4. Altyazıyı yayına ekleyin

1. CouchSubs'ta **Ayarlar › OBS katmanı**'ndaki adresi kopyalayın (aynı bilgisayarda `http://localhost:8787/obs`).
2. OBS'de sahneye **Tarayıcı** kaynağı ekleyin, adresi yapıştırın, genişliği `1920`, yüksekliği `1080` yapın ve
   kaynağı capture card'ın **üstüne** koyun.
3. **Ayarlar › Altyazı alanı**'nda oyunun İngilizce altyazısının olduğu bölgeyi ayarlayın (çoğu oyunda **Alt bölge**).
   Oyunda İngilizce altyazının açık olması gerekir.
4. Ana ekranda **Çeviriyi başlat**'a basın. Kutuda "OBS'den *kaynak adı* okunuyor" yazmalıdır.

### 5. Çeviri motorları

| Motor | Nerede çalışır | Ne gerekir | Not |
|---|---|---|---|
| **Ücretsiz (OPUS-MT)** | Bu bilgisayarda | Hiçbir şey (model sihirbazda iner) | En hızlısı. **Ayarlar › Çeviri › Ücretsiz çeviri aygıtı**'nda **GPU (Beta) · NVIDIA CUDA** seçilebilir |
| **DeepL** | İnternette | Kendi DeepL API anahtarınız | Ücretsiz anahtar ayda 500.000 karakter; doğal Türkçe |
| **Yerel model** (ör. Gemma) | Bu bilgisayarda (Ollama, LM Studio) | Ekran kartı belleği | Anahtar, kota, ücret yok; metin bilgisayardan çıkmaz |
| Gemini, OpenAI, Claude | İnternette | Kendi API anahtarınız | Ücretlendirme ilgili hesaba yapılır |

**DeepL**

1. [deepl.com/pro-api](https://www.deepl.com/pro-api)'den ücretsiz **DeepL API Free** anahtarı alın (`:fx` ile biter).
2. **Ayarlar › Çeviri › Çeviri motoru › DeepL**'i seçin, anahtarı yapıştırıp **Kaydet ve dene**'ye basın. Bu ayki
   kullanım görünür.

**Ollama ile yerel Gemma (RTX 3090 gibi ekran kartları için)**

1. [ollama.com/download](https://ollama.com/download)'dan Ollama'yı kurup açın.
2. PowerShell'de modeli indirin:
   ```powershell
   ollama pull gemma3:12b
   ```
   `gemma3:12b` ekran kartında yaklaşık 9-10 GB yer tutar; 24 GB'lık bir kartta OBS'nin kodlayıcısıyla birlikte rahat
   çalışır. `gemma3:27b` daha iyi çevirebilir ama yaklaşık 20 GB ister ve daha yavaştır; yayın sırasında kartı zorlayabilir.
   Daha hızlı bir seçenek: `qwen2.5:7b`.
3. Modelin bellekte kalması için (aradan sonra gelen ilk satır modelin yüklenmesini beklemesin) bir kez şunu çalıştırıp
   Ollama'yı yeniden başlatın:
   ```powershell
   setx OLLAMA_KEEP_ALIVE -1
   ```
4. CouchSubs'ta **Ayarlar › Çeviri › Çeviri motoru › Yerel model (Ollama, LM Studio)**'yu seçin, **Ollama'yı kullan**'a
   basın (adres `http://localhost:11434/v1`), **Model adı**'na `gemma3:12b` yazıp **Bağlantıyı dene**'ye basın. İlk
   deneme modeli belleğe yüklediği için uzun sürebilir.

LM Studio için aynı yerde **LM Studio'yu kullan**'a basın (adres `http://localhost:1234/v1`) ve model adını LM Studio'da
göründüğü gibi yazın. Ayrıntılar: [Yerel yapay zekâ ile çeviri](#yerel-yapay-zekâ-ile-çeviri-ollama-lm-studio).

### 6. Seçici: birden çok motorun en iyisi

Seçici her satırı seçtiğiniz motorların hepsine **aynı anda** gönderir ve en iyi çeviriyi saniyenin çok altında seçer.
Yapay zekâ hakemi kullanılmaz, karar kurallarla verilir:

- Elenir ya da kaybeder: hiç çevrilmeden dönen satır, çeviride kalan İngilizce kelimeler, kaybolan özel adlar
  (Franklin, Los Santos; Türkçe ek alabilir: *Franklin'e*), tuhaf uzunluk, modelin açıklama yazması ("Çeviri: …") ya da
  kendini tekrar etmesi.
- Kazanır: motorların üzerinde **uzlaştığı** çeviri (iki motorun benzer bulduğu çeviri genelde doğrudur).
- Eşitlikte öncelik: DeepL, Claude, OpenAI, Gemini, yerel model, ücretsiz.

Kurulum:

1. Kullanmak istediğiniz motorları önce tek tek kurun (yukarıda): DeepL anahtarı, yerel model (adres ve model adı).
   Yerel modeli kurduktan sonra **Çeviri motoru**'nu yeniden DeepL'e alabilirsiniz; seçici yerel modelin ayarını yine
   kullanır.
2. **Ayarlar › Çeviri › Seçici**'de **Birden çok motorun en iyisini seç (seçici)**'yi işaretleyin.
3. Yarışacak motorları seçin (varsayılan: DeepL, yerel model, ücretsiz). Kurulu olmayan motorlar soluk görünür ve
   "kurulu değil" yazar; altta şu an hangi motorların yarıştığı yazar. En az iki kurulu motor gerekir; seçici yalnız
   İngilizceden Türkçeye çeviride çalışır.

Gecikme ve kota:

- Altyazı yavaş motorları **en fazla 0,7 saniye** bekler; hepsi erken biterse beklenmez. Süre dolduğunda gelmeyen motor
  arka planda biter ve çevirisi önbelleğe girer; aynı satır yeniden geldiğinde hazırdır.
- DeepL'e bağlantı çeviri sürerken **açık tutulur**: bağlantı 5 dakika boşta kapanmaz ve 40 saniyede bir DeepL'in
  kullanım sorgusuyla yoklanır (bu sorgu kotadan düşmez). Diyalogsuz bir sahneden sonraki ilk satır bağlantı kurulmasını
  beklemez.
- Her satır DeepL'e de gittiği için **DeepL kotası daha hızlı biter**. Oyun paketlerinde ve önbellekte bulunan satırlar
  hiçbir motora gitmez.
- Hangi motorun neden seçildiği altyazı günlüğüne yazılır (`%LOCALAPPDATA%\CouchSubs\altyazi.log`), ör.
  `seçici: DeepL (DeepL 1.85, gemma3:12b 1.71, OPUS-MT 0.62; 240 ms)`.

### Sorun giderme

| Ana ekranda / ayarlarda | Ne yapmalı |
|---|---|
| *OBS'ye ulaşılamadı* | OBS açık mı, **Araçlar › WebSocket Sunucu Ayarları**'nda sunucu etkin mi, adres ve port doğru mu bakın. OBS başka bilgisayardaysa güvenlik duvarında 4455'e izin verin. |
| *OBS, WebSocket parolasını kabul etmedi* | Parolayı **Bağlantı Bilgilerini Göster**'den yeniden kopyalayın; **Ayarlar › Görüntü kaynağı**'nda eski parolayı **Kaldır**'ıp yenisini yazın. |
| *OBS'de … adında bir kaynak yok* | Kaynağın adı OBS'de değişmiş; **Bağlan ve kaynakları listele** ile yeniden seçin. |
| *OBS, … kaynağının görüntüsünü alamadı* | Kaynak hiçbir sahnede görünür değil; bir sahneye ekleyin ya da gizliyse gösterin. |
| *Bu OBS sürümü çok eski* | OBS 28 veya sonrasına geçin. |
| Altyazı okunmuyor | Oyunda İngilizce altyazı açık mı, **Ayarlar › Altyazı alanı** doğru bölgede mi bakın. |
| Yerel model uyarısı | Ollama açık mı, model indirildi mi (`ollama list`), adres ve model adı doğru mu bakın. Seçicide yerel model yanıt vermezse diğer motorlar çevirmeye devam eder. |

## Gizlilik

CouchSubs hesap şifrenizi görmez (giriş doğrudan Sony'nin sayfasında yapılır), altyazıları ve oyun
görüntüsünü hiçbir sunucuya göndermez. Ayrıntılar: [PRIVACY.md](PRIVACY.md).

## Derleme

**Mac**

```bash
brew install cmake ninja pkg-config protobuf openssl@3 xcodegen   # OpenSSL, json-c, libevent, miniupnpc kaynaktan derlenir
git submodule update --init third-party/chiaki-ng
./scripts/build-chiaki-macos.sh     # libchiaki (arm64 + x86_64, macOS 13+) → third-party/build/CChiaki.xcframework
./scripts/package-mac.sh            # → dist/CouchSubs-<sürüm>-macos.zip
```

Testler: `core/` ve `remoteplay/` içinde `swift test`; `mac/` içinde `xcodegen generate` sonrası
`xcodebuild test -project mac/CouchSubs.xcodeproj -scheme CouchSubs -destination 'platform=macOS'`.

**Windows**

Yerel kütüphane (libchiaki + FFmpeg) MSYS2 ile derlenir; adımlar `.github/workflows/windows.yml` içinde.
.NET 10 ile `dotnet test windows/CouchSubs.Windows.slnx` (uygulamanın kendisi yalnız Windows'ta çalışır).

**TV**

```bash
npm install -g @webos-tools/cli
./scripts/package-tv.sh             # LG → dist/tv-app/com.couchsubs.tv_<sürüm>_all.ipk
./scripts/package-tizen.sh          # Samsung → dist/tv-tizen/app (TIZEN_PROFILE ile imzalı .wgt)
(cd tv-android && gradle assembleRelease)   # Android TV (Gradle 8.11, JDK 17)
```

Mac ve Windows paketleri bu dosyayı içerir, bu yüzden Mac paketinden önce de bu araç kurulu olmalıdır.

Sürümler `v*` etiketiyle `.github/workflows/release.yml` tarafından derlenip yayınlanır.

**PR kontrolleri**

`.github/workflows/pr-tests.yml`, dış katkılar dahil pull request'lerde, yalnız ilgili klasör değiştiyse, üç ayrı kontrol çalıştırır:
Windows'ta .NET test projeleri, macOS'ta `core/` için `swift test`, Android telefonda JVM birim testleri.
Native kütüphane derlemez, çeviri/TTS modeli veya TV kurulum paketi indirmez; paketleme, imzalama ve yayın yapmaz.
Native/model dosyası ya da özel benchmark girdisi isteyen testler, Android TV paket testiyle birlikte,
bağımlılıkları yoksa atlanmış olarak raporlanır. Sonuçlar Actions kayıtlarında ve 7 gün saklanan
`pr-*-test-results` çıktılarındadır (Windows TRX, Swift test kaydı, Android JUnit XML/HTML).
Bu kontroller gerçek TV/PS5 bağlantısı, model kalitesi veya uçtan uca gecikme testinin yerine geçmez;
mevcut paketleme ve sürüm iş akışları ayrıca çalışmaya devam eder.

## Klasörler

- `mac/` – macOS uygulaması (SwiftUI, Vision, Translation)
- `windows/` – Windows uygulaması (WPF, Windows OCR, OPUS-MT çeviri modeli) ve yerel Remote Play kütüphanesi
- `core/` – platformdan bağımsız mantık (altyazı dengeleyici, yerel altyazı sunucusu, LG TV eşleme)
- `remoteplay/` – chiaki-ng'nin `libchiaki`'si üstünde Remote Play bağlantısı
- `tv-webos/` – LG webOS TV uygulaması ve TV arayüzünün ortak kodu
- `tv-tizen/` – Samsung TV uygulaması
- `tv-android/` – Android TV uygulaması
- `design/`, `docs/TASARIM.md` – logo ve ortak tasarım kuralları
- `prototip/` – ilk prototip (başvuru için saklanıyor)

## Lisans

Copyright (C) 2026 Sinan Yorulmaz. [GNU AGPL-3.0](LICENSE) ile lisanslanmıştır; çatallar ve değiştirilmiş
sürümler bu telif satırını korumalı ve [NOTICE](NOTICE) dosyasındaki koşullara uymalıdır. Kullanılan açık kaynak bileşenler ve lisansları:
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Remote Play bağlantısı
[chiaki-ng](https://github.com/chiaki-ng/chiaki-ng) sayesinde mümkün; geliştiricilerine teşekkürler.

Yayıncı: Enmera.

CouchSubs, Sony Interactive Entertainment ile bağlantılı değildir ve onun tarafından onaylanmamıştır.
"PlayStation" ve "PS5", Sony Interactive Entertainment Inc.'in ticari markalarıdır.

---

## English

CouchSubs reads the English subtitles of PS5 games in real time, translates them and shows the translation
on your TV on top of the game picture. You keep playing on the TV with your own controller; text recognition
and default translation run on your computer, and game frames are never uploaded. English subtitle lines not found
in game packs may be sent to the shared pool (enabled by default, opt-out in Settings). Choosing online translation
also sends subtitle text to the selected provider. See [Privacy](PRIVACY.md) for details.

**You need:** a PS5 and a supported TV on the same network as your computer, a free second
PlayStation Network account, and either a Mac on macOS 13 Ventura or later (Apple Silicon or Intel) or a 64-bit Windows 10 (2004)
or 11 PC. Apple's free on-device translation needs macOS 26; on older macOS the game packs, Gemini (free key), DeepL or
AI translation are used.

**TVs:** LG, Samsung (2019 and later; 2016–2018 models experimental) and Android TV / Google TV such as Vestel,
Arçelik, Beko, Grundig and Philips, all tested on real TVs. CouchSubs installs the TV app on all of them. Mac and
Windows are tested on real hardware.

**Download:** [Mac (DMG)](https://github.com/yorulmazsinan/couchsubs/releases/latest/download/CouchSubs-macos.dmg) · [Windows (installer)](https://github.com/yorulmazsinan/couchsubs/releases/latest/download/CouchSubs-windows-Setup.exe) · [Android phone (Google Play)](https://play.google.com/store/apps/details?id=com.couchsubs.mobile). The app finds new versions and updates itself.

**Screenshots:** the Mac app ([main screen](docs/screenshots/mac/ana.png),
[setup](docs/screenshots/mac/kurulum.png), [translation settings](docs/screenshots/mac/ayarlar-ceviri.png),
[game packs](docs/screenshots/mac/ayarlar-paketler.png)), the Windows design
([main screen](docs/screenshots/windows/ana.png), [setup](docs/screenshots/windows/kurulum.png),
[translation](docs/screenshots/windows/ceviri.png), [game packs](docs/screenshots/windows/paketler.png),
[subtitle window](docs/screenshots/windows/pencere.png)) and the design of the phone app
([docs/screenshots/telefon](docs/screenshots/telefon)). Everything is free, game packs included; no account is needed.

**Streamers (Windows, capture card + OBS):** on the `feat/windows-obs-websocket` branch CouchSubs can read the game picture
straight from the capture card source in OBS through OBS's built-in WebSocket server (no second PSN account, PS5 pairing
or TV needed); the first setup screen asks where the picture comes from and the rest of the wizard follows that choice.
A **conductor** sends each line to several engines at once (DeepL, a local model such as Gemma through Ollama, the free
OPUS-MT) and picks the best translation by rules in well under a second, waiting at most 0.7 s; the DeepL connection is
kept warm while translating. Step-by-step guide (Turkish): [Yayıncılar için](#yayıncılar-için-capture-card-ve-obs-ile-kurulum-windows).

**Setup:** open the app and follow the setup assistant. On the TV step it shows how to turn on developer mode for your
TV brand and then installs the TV app by itself (LG: type the on-screen passphrase once; Samsung: sign in with a
Samsung account once; Android TV: allow debugging on the TV). Guide: [docs/tv-setup.md](docs/tv-setup.md).
On Windows, if SmartScreen appears, choose **More info › Run anyway**.

**Local AI translation:** in **Settings › Translation** choose **Local model** to translate with a model running on your
own computer through [Ollama](https://ollama.com) (`ollama pull qwen2.5:7b`, address `http://127.0.0.1:11434/v1`) or
[LM Studio](https://lmstudio.ai) (local server, `http://localhost:1234/v1`). No key, quota or cost, and subtitle text
stays on your computer; **Test connection** checks the server and the model.

**Shortcuts** (while translating, even with CouchSubs in the background): on Windows **Num 9** or **Ctrl+Shift+9**
translates the screen; Num 1–3, 5, 0, 7 and 8 also work as Ctrl+Shift+1–3, 5, 0, 7 and 8, so laptops without a numeric
keypad can use them. Windows users can change the keys in **Settings › Keyboard shortcuts**. On a Mac: **⌥⌘T**
translates the screen, **⌥⌘L** toggles click-through.

Copyright (C) 2026 Sinan Yorulmaz. Licensed under the [GNU AGPL-3.0](LICENSE) (forks must keep this notice, see
[NOTICE](NOTICE)); third-party components are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). CouchSubs is not affiliated with or endorsed by Sony
Interactive Entertainment. "PlayStation" and "PS5" are trademarks of Sony Interactive Entertainment Inc.

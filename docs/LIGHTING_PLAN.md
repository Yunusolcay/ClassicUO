# Dinamik Işık Sistemi – Tasarım Planı (M1)

> Not: Teknik (D-001) tabandan bağımsızdır. Dosya yolları, kancalar ve görev ayrıntıları **MIG-4**
> görevinde yeni tabana göre güncellenecek (upstream'de ışık kodu evrildi: `ClassicUO.Renderer/Lights/Light.cs`
> atlas, `ClassicUO.Renderer/RenderTargets.cs`, `ClassicUO.Client/Game/Data/LightShaderData.cs` RGB + eğri).

Önkoşul okuma: `docs/arch/rendering.md` §3 (mevcut ışık sistemi) ve §6 (genişletme noktaları).

## 1. Hedefler

1. Renkli dinamik ışık kaynakları (meşale, mum, lamba, büyü, alan efektleri) – yumuşak radyal düşüş.
2. Yumuşak geçişler: ışık seviyesi değişimleri ve ışığın kendisi (yumuşak kenar, flicker).
3. Gece/gündüz: sunucunun `0x4F` seviyesine bağlı ambient **renk** (şafak turuncu, gece mavi), yumuşatılmış.
4. Protokol uyumu: `0x4F` (global), `0x4E` (kişisel), item `LightID` (0x1A/0xF3 içindeki light alanı) aynen kullanılır; sunucu her zaman otorite.
5. Her şey ayarlardan kapatılabilir; kapalıyken mevcut (upstream) yol birebir çalışır.
6. Performans: orta seviye GPU'da (GTX 1050 / RX 560 sınıfı), 1080p, 200 ışıkla **60+ FPS**; ışık sistemi kare başına ≤ 1.5 ms GPU.

Kapsam dışı (M2): ışıkların duvar arkasına geçmemesi için gerçek gölge/occlusion.

## 2. Önerilen teknik: Ayrı light map RT + shader ile kompozit

```
Dünya pass'i (değişmez) ──► viewport RT (sahne rengi)
Işık pass'i (yeni)       ──► light RT (ambient ile temizle, ışıkları additive çiz, opsiyonel blur)
Kompozit pass'i (yeni)   ──► ekran: sahne × (light × 2)   [overbright, tonemap/clamp]
```

Ayrıntılar:
- **Light RT** ayrı, ölçeklenebilir çözünürlükte (1/1, 1/2, 1/4). Düşük çözünürlük + bilinear örnekleme zaten yumuşak kenar verir; Yüksek kalitede ek 2-pass ayrık Gauss blur.
- **Kodlama:** `SurfaceFormat.Color` içinde ışık değeri `/2` saklanır, kompozitte `×2` → 0..2 aralığı = overbright (meşale yanındaki pikseller sahne renginden parlak olabilir). Yüksek kalite seçeneği: `HalfVector4` RT (FNA/GL destekliyorsa) – opsiyonel, varsayılan değil.
- **Işık sprite'ları:** yeni `Lighting.fx` içinde `LightSprite` tekniği: light.mul şekli (linear sampling) **veya** prosedürel radyal düşüş (`saturate(1 - d²)^k`) × renk × yoğunluk × flicker. Renk doğrudan RGB (vertex `Hue` alanı RGB olarak taşınır), hue tablosuna bağımlılık yok.
- **Ambient:** light RT `Clear` rengi = `AmbientLight.Current` (RGB). Sunucu seviyesi → hedef yoğunluk; tint eğrisi → renk; hedefe zamanla lerp.
- **Kompozit:** `WorldViewport.Draw` içinde, `_darknessBlend` çağrısı yerine `LightCompositor.Composite(...)` (yeni sınıf) tam ekran quad ile `Lighting.fx/Composite` tekniğini çalıştırır. Overhead/yazılar sonra çizildiği için etkilenmez.
- **Işık toplama:** `GameScene.AddLight` mantığı yeni `LightManager`'a taşınır; mevcut tile örtme testi korunur. Işık tanımları (renk, yarıçap, yoğunluk, flicker) veri dosyasından (`LightDefinitions`), yoksa mevcut `LightColors` / `ItemData.LightIndex` fallback.

### Neden bu?
| Seçenek | Artı | Eksi | Karar |
|---|---|---|---|
| **A. Light map RT + kompozit (önerilen)** | Mevcut mimarinin doğal uzantısı (zaten bir light RT var); sprite shader'ına dokunmaz; ışık sayısından bağımsız tek kompozit; çözünürlük/blur ile kalite ayarlanır; post-process zincirine (M5) doğal giriş | 2D ekran uzayı → ışık duvarlardan sızar (M2'de ele alınacak) | **Seçildi** |
| B. Forward: sprite pixel shader'ında ışık dizisi döngüsü | Piksel başına doğru, ek RT yok | ps_3_0 sabit limiti (~50 ışık), her sprite × her ışık maliyeti, tek global shader'a büyük müdahale, batch'ler state ile bölünür | Reddedildi |
| C. Deferred + normal map | Gerçekçi 3B ışık, gelecekte HD art ile güzel | UO art'ında normal yok; G-buffer için MRT + vertex format değişikliği; büyük refactor | M6 (HD art) sonrası yeniden değerlendirilecek; A'nın light RT'si buna evrilebilir |
| D. 2D ışık izleme / SDF GI | Gerçek yumuşak gölge | Orta GPU'da 60 FPS riskli, karmaşık | Reddedildi (M2'de sınırlı varyantı değerlendirilebilir) |

## 3. Bileşenler (yeni dosyalar)

`src/Game/Lighting/` (namespace `ClassicUO.Game.Lighting`):

| Sınıf | Sorumluluk |
|---|---|
| `LightingSettings` | Profile'dan okunan ayarlar, kalite önayarları (`Off/Low/Medium/High`) |
| `LightSource` (struct) | Ekran pozisyonu, RGB renk, yarıçap, yoğunluk, flicker seed, şekil (lightID / prosedürel) |
| `LightManager` | Kare başına ışık toplama (`Add`), örtme testi, tanım çözümleme, sayaç; limit ≥ 512 |
| `LightDefinitions` | `Data/Client/lights.json` benzeri veri dosyası: graphic → renk/yarıçap/yoğunluk/flicker; fallback mevcut tablolar |
| `AmbientLight` | Sunucu `World.Light` → hedef ambient RGB; gün/gece tint eğrisi; zaman tabanlı yumuşatma |
| `LightMapRenderer` | Light RT yönetimi (ölçek, yeniden oluşturma), ışıkları çizme, blur |
| `LightCompositor` | Kompozit pass (sahne × ışık), `WorldViewport`'tan çağrılır |
| `LightingEffect` (`Renderer/`) | `Lighting.fx` sarmalayıcısı (`MatrixEffect` türevi) |

`src/shaders/Lighting.fx` (+ `.fxc`): teknikler `LightSprite`, `BlurH`, `BlurV`, `Composite`.

Upstream dosyalara kancalar (tamamı “özellik kapalıysa eski yol” koşullu):
- `GameScene.AddLight` → `LightManager.Add` (veya her ikisi).
- `GameScene.DrawLights` → `LightMapRenderer.Draw`.
- `WorldViewport.Draw` ışık bloğu → `LightCompositor.Composite`.
- `Profile.cs` → yeni ayar property'leri. `OptionsGump.cs` → ayar arayüzü.

## 4. Ayarlar ve kalite seviyeleri

| Ayar | Varsayılan | Not |
|---|---|---|
| `EnhancedLighting` | açık | Kapalı = birebir upstream davranışı |
| `LightingQuality` | Medium | aşağıdaki tablo |
| `LightFlicker` | açık | ateş tabanlı ışıklar |
| `DayNightTint` | açık | ambient renk tonu; kapalıysa gri |
| `LightTransitionSeconds` | 2.0 | 0 = anlık (eski davranış) |
| `LightIntensity` | 1.0 | genel çarpan (0.5–2.0) |

Mevcut `UseColoredLights`, `UseCustomLightLevel`/`LightLevel`, `UseDarkNights`, `UseAlternativeLights` korunur ve yeni sistemde de anlamlı kalır.

| Kalite | Light RT ölçeği | Blur | Maks ışık | Prosedürel düşüş |
|---|---|---|---|---|
| Low | 1/4 | yok | 128 | hayır (light.mul) |
| Medium | 1/2 | yok | 256 | evet |
| High | 1/2 | 2-pass 9-tap | 512 | evet |

## 5. Görevler

Zorluk: K (kolay, <½ gün) · O (orta, ~1 gün) · Z (zor, 2+ gün). **[Codex]** = Codex'e verilebilecek kadar net.

### L1 – Işık limitini ID limitinden ayır **[Codex]** · K
- Dosyalar: `Game/Constants.cs` (yeni `MAX_LIGHTS_PER_FRAME`), `Game/Scenes/GameScene.cs` (`_lights` boyutu, `AddLight` limit kontrolü).
- Kabul: `MAX_LIGHTS_DATA_INDEX_COUNT` yalnızca light.mul ID'si için kullanılıyor; 300 ışıklı bir sahnede (ör. Britain bankası önü çok mum) hepsi çiziliyor; görsel fark başka yok.

### L2 – `LightingSettings` + Profile ayarları **[Codex]** · K
- Dosyalar: yeni `Game/Lighting/LightingSettings.cs`; `Configuration/Profile.cs` (§4'teki property'ler, `[JsonProperty]`).
- Kabul: Eski profile.json ile açılış hatasız, varsayılanlar §4'teki gibi; ayarlar kaydedilip yeniden yükleniyor.

### L3 – `LightManager`'a taşıma (davranış değişmeden) · O
- Dosyalar: yeni `Game/Lighting/LightManager.cs`, `LightSource.cs`; `GameScene.cs` (`AddLight`, `DrawLights` delegasyon).
- Kabul: `EnhancedLighting` açık/kapalı fark etmeksizin ekran görüntüleri değişiklik öncesiyle piksel piksel aynı (aynı konum, gece). `GameScene` diff'i < 30 satır.

### L4 – `Lighting.fx` + `LightingEffect` · O
- Dosyalar: yeni `shaders/Lighting.fx`, `.fxc`, `Renderer/LightingEffect.cs`; `ClassicUO.csproj` (EmbeddedResource); `Batcher2D.cs` `Resources` sınıfına getter.
- Kabul: Dört teknik derleniyor (fx_2_0), runtime'da yükleniyor; basit test: tek ışık sprite'ı + kompozit doğru çıktı veriyor (Windows D3D/GL).

### L5 – Shader derleme scripti **[Codex]** · K
- Dosyalar: `shaders/compile_shaders.bat` → fxc yolunu otomatik bulan `shaders/compile_shaders.ps1`; `docs/BUILD.md`'ye bölüm.
- Kabul: Tek komutla tüm `.fx` → `.fxc`; fxc bulunamazsa anlaşılır hata.

### L6 – `LightMapRenderer` + `LightCompositor` (çekirdek) · Z
- Dosyalar: yeni `Game/Lighting/LightMapRenderer.cs`, `LightCompositor.cs`; `GameSceneDrawingSorting.cs` (RT oluşturma kancası), `WorldViewport.cs` (kompozit kancası).
- Kabul: Enhanced açıkken gece sahnesi: ışıklar ölçekli RT'de, ×2 overbright ile birleşiyor; kapalıyken eski yol. Zoom 0.5–2.5 ve pencere boyutlandırmada RT doğru yeniden kuruluyor, sızıntı yok (RT'ler dispose).

### L7 – `AmbientLight`: yumuşak geçiş + gün/gece tonu · O
- Dosyalar: yeni `Game/Lighting/AmbientLight.cs`; `LightMapRenderer` clear rengi.
- Kabul: `0x4F` 0→30 değişiminde ambient `LightTransitionSeconds` içinde doğrusal/smoothstep geçiyor; tint kapalıyken gri ve eski `IsometricLevel` ile aynı son değer. `UseCustomLightLevel` hâlâ çalışıyor. Birim testi: verilen seviye ve zamana göre beklenen RGB.

### L8 – Işık tanım tablosu (`LightDefinitions`) **[Codex]** · O
- Dosyalar: yeni `Game/Lighting/LightDefinitions.cs`, varsayılan `lights.json` (çıktı klasörüne kopyalanır), `.csproj`.
- Kabul: JSON şeması dokümante (`graphic`, `color` "#RRGGBB", `radius`, `intensity`, `flicker`); dosya yok/bozuksa log + `LightColors` fallback; meşale/mum/lamba/brazier/ateş alanı/enerji alanı için en az 25 giriş.

### L9 – Prosedürel ışık + renk + flicker · O
- Dosyalar: `LightMapRenderer.cs`, `Lighting.fx` (`LightSprite`), `LightManager.cs`.
- Kabul: Ateş tabanlı ışıklar `Time.Ticks` tabanlı (kare hızından bağımsız) titriyor, seed konumdan → komşu meşaleler senkron değil; `LightFlicker` kapalıyken sabit.

### L10 – Yumuşak light map: blur + kalite seviyeleri · O
- Dosyalar: `LightMapRenderer.cs`, `Lighting.fx` (`BlurH/BlurV`), `LightingSettings.cs`.
- Kabul: Low/Medium/High §4 tablosuna uyuyor; High'da ışık kenarında 5-bit kademelenme görünmüyor.

### L11 – Kişisel ışık (0x4E) görselleştirmesi · O (opsiyonel)
- Dosyalar: `LightManager.cs` (oyuncu etrafında radyal ışık), `AmbientLight.cs`.
- Kabul: `0x4E` > 0 iken oyuncu etrafında yumuşak hale; global ambient'i değiştirme davranışı, eski sistemle aynı **görünürlüğü** sağlıyor (oyuncu karanlıkta eskisinden daha az görmüyor). Ölçek tutarsızlığı (bkz. rendering.md §3) doğrulanıp not edilecek.

### L12 – Ayarlar arayüzü **[Codex]** · O
- Dosyalar: `Game/UI/Gumps/OptionsGump.cs` (mevcut ışık ayarlarının yanına bölüm).
- Kabul: §4'teki tüm ayarlar UI'dan değişiyor ve oyun yeniden başlatmadan uygulanıyor.

### L13 – Debug overlay + performans ölçümü **[Codex]** · K
- Dosyalar: yeni `Game/Lighting/LightingDebug.cs`; bir komut (`CommandManager`, ör. `-lightdebug`).
- Kabul: Ekranda ışık sayısı, light RT boyutu, ışık pass süresi (ms); light map'i tek başına gösterme modu.

### L14 – Performans doğrulama ve kabul · O
- Sahip: Claude Code (ölçüm Codex ile).
- Kabul: §1/6'daki hedef; 3 senaryo (açık alan gece, şehir gece, zindan), Low/Medium/High için FPS tablosu `docs/PLAN.md`'ye.

**Sıra / bağımlılık:** L1, L2, L5 paralel → L3 → L4 → L6 → (L7, L8, L9, L10 paralel) → L11, L12, L13 → L14.

Toplam: **14 görev** (Codex'e uygun: L1, L2, L5, L8, L12, L13 = 6).

## 6. Riskler

- Taban sürüm (2020 snapshot) — upstream'e geçilirse L3/L6 kancaları yeniden yazılır. M1'e başlamadan karar (DECISIONS D-002).
- Işık sızıntısı (duvar/kat arkası) – 2D yaklaşımın doğası; M2.
- MojoShader/GL farkları – D3D ve OpenGL arka uçlarında test şart.
- Overbright kompozit, gündüz sahnelerini değiştirmemeli: gündüz ambient = 0.5 (×2 = 1.0) → nötr.

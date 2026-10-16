# Kararlar

Format: her karar için tarih, durum, bağlam, karar, gerekçe, sonuçlar.

## D-001 – Işık tekniği: ayrı light map RT + shader ile kompozit
- **Tarih:** 2026-10-16 · **Durum:** Kabul edildi (öneri, kullanıcı onayı bekliyor)
- **Bağlam:** Mevcut istemci zaten bir light RT kullanıyor (gri ambient + light.mul sprite'ları, çarpımsal blend). Hedef: renkli, yumuşak, overbright destekli dinamik ışık ve gece/gündüz tonu; orta GPU'da 60+ FPS; ayarlardan kapatılabilirlik; upstream'e minimum müdahale.
- **Karar:** Light map ayrı, ölçeklenebilir bir RT'de üretilir (ambient renk ile temizlenir, ışıklar additive çizilir, opsiyonel blur), sonra tam ekran kompozit shader'ı ile `sahne × ışık × 2` olarak birleştirilir. Yeni kod `src/Game/Lighting/` + `shaders/Lighting.fx`; upstream dosyalara yalnızca koşullu kancalar.
- **Gerekçe:** Mevcut mimarinin doğal uzantısı; sprite shader'ına ve vertex formatına dokunmaz; maliyet ışık sayısından büyük ölçüde bağımsız; kalite RT ölçeği ve blur ile ayarlanır; M5 post-process zincirine doğal giriş noktası. Forward (sprite shader içinde ışık döngüsü) ps_3_0 limitleri ve batch maliyeti nedeniyle; deferred/normal-map UO art'ında normal olmadığı için reddedildi (HD art sonrası tekrar değerlendirilebilir).
- **Sonuçlar:** Işıklar 2D ekran uzayında; duvar/kat arkasına sızma M2'de (gölge/occlusion) ele alınacak. Ayrıntı: `docs/LIGHTING_PLAN.md`.

## D-002 – Taban sürüm: 2020 snapshot mı, güncel upstream mı? (AÇIK)
- **Tarih:** 2026-10-16 · **Durum:** Açık – kullanıcı kararı gerekli
- **Bağlam:** Fork `master` = ClassicUO `211d4b0b` (2020-05, .NET Framework 4.7, C# 7.3). Upstream o tarihten beri .NET sürümü, FNA/FNA3D, render ve asset katmanında büyük değişiklikler yaptı. AGENTS.md “fork'u upstream ile senkron tut” diyor.
- **Seçenekler:** (a) Mevcut snapshot üzerinde devam (senkron fiilen imkânsız, upstream düzeltmeleri gelmez); (b) M1'e başlamadan güncel upstream'e geç, M0 haritalarını onun için tazele.
- **Ek bilgi (2026-10-16):** Shard'ın mevcut istemci klasörü `C:\UOSoft\Spherenet` zaten **modern** bir ClassicUO türevi çalıştırıyor (`cuo.dll` 1.0.0+dad7942e, FNA3D, SDL2/SDL3, FontStashSharp, Discord SDK — muhtemelen TazUO benzeri bir fork). Veri seti ağırlıklı MUL (Map0/1, Art.mul, light.mul) + birkaç UOP (AnimationFrame, Tileart, MainMisc). Kullanıcı 7.x üzeri herhangi bir client veri sürümüne geçebileceğini belirtti → veri tarafında kısıt yok.
- **Öneri:** (b). M0 dokümanları ucuz yenilenir; M1 kodu eski tabana yazıldıktan sonra taşımak pahalı.

# Kararlar

Format: her karar için tarih, durum, bağlam, karar, gerekçe, sonuçlar.

## D-001 – Işık tekniği: ayrı light map RT + shader ile kompozit
- **Tarih:** 2026-10-16 · **Durum:** Kabul edildi (öneri, kullanıcı onayı bekliyor)
- **Bağlam:** Mevcut istemci zaten bir light RT kullanıyor (gri ambient + light.mul sprite'ları, çarpımsal blend). Hedef: renkli, yumuşak, overbright destekli dinamik ışık ve gece/gündüz tonu; orta GPU'da 60+ FPS; ayarlardan kapatılabilirlik; upstream'e minimum müdahale.
- **Karar:** Light map ayrı, ölçeklenebilir bir RT'de üretilir (ambient renk ile temizlenir, ışıklar additive çizilir, opsiyonel blur), sonra tam ekran kompozit shader'ı ile `sahne × ışık × 2` olarak birleştirilir. Yeni kod `src/Game/Lighting/` + `shaders/Lighting.fx`; upstream dosyalara yalnızca koşullu kancalar.
- **Gerekçe:** Mevcut mimarinin doğal uzantısı; sprite shader'ına ve vertex formatına dokunmaz; maliyet ışık sayısından büyük ölçüde bağımsız; kalite RT ölçeği ve blur ile ayarlanır; M5 post-process zincirine doğal giriş noktası. Forward (sprite shader içinde ışık döngüsü) ps_3_0 limitleri ve batch maliyeti nedeniyle; deferred/normal-map UO art'ında normal olmadığı için reddedildi (HD art sonrası tekrar değerlendirilebilir).
- **Sonuçlar:** Işıklar 2D ekran uzayında; duvar/kat arkasına sızma M2'de (gölge/occlusion) ele alınacak. Ayrıntı: `docs/LIGHTING_PLAN.md`.

## D-002 – Taban sürüm: güncel upstream ClassicUO `main`
- **Tarih:** 2026-10-16 · **Durum:** Kabul edildi (kullanıcı kararı Claude Code'a bıraktı)
- **Karar:** Taban = upstream `ClassicUO/ClassicUO` `main`, `ee79d7eb` (2025-11-26; .NET 10, çok projeli yapı, BSD-2-Clause lisans). Yeni `main` dalı bunun üzerine kurulur; eski `master` `legacy/2020` dalında saklanır. TazUO vb. türevler taban olarak **seçilmedi**: upstream'den çok sapmışlar, kendi özellik setlerini taşıyorlar; amacımız ince bir görsel katman ve düzenli upstream senkronu.
- **Gerekçe:** 2693 commit'lik fark; .NET 10/NativeAOT, güncel FNA, UOP ve yeni client sürümü desteği; upstream ışık sistemi zaten evrilmiş (atlas, RGB ışık verisi) → M1 işinin bir kısmı hazır. M0 dokümanlarını yenilemek ucuz, M1 kodunu sonradan taşımak pahalı.
- **Sonuçlar:** `rendering.md` ve `LIGHTING_PLAN.md` dosya yolları yenilenecek; Codex'in build/ağ/asset dokümanları yeni tabana göre yazılacak. Plan: `docs/MIGRATION.md`.
- **Bağlam:** Fork `master` = ClassicUO `211d4b0b` (2020-05, .NET Framework 4.7, C# 7.3). Upstream o tarihten beri .NET sürümü, FNA/FNA3D, render ve asset katmanında büyük değişiklikler yaptı. AGENTS.md “fork'u upstream ile senkron tut” diyor.
- **Seçenekler:** (a) Mevcut snapshot üzerinde devam (senkron fiilen imkânsız, upstream düzeltmeleri gelmez); (b) M1'e başlamadan güncel upstream'e geç, M0 haritalarını onun için tazele.
- **Ek bilgi (2026-10-16):** Shard'ın mevcut istemci klasörü `C:\UOSoft\Spherenet` zaten **modern** bir ClassicUO türevi çalıştırıyor (`cuo.dll` 1.0.0+dad7942e, FNA3D, SDL2/SDL3, FontStashSharp, Discord SDK — muhtemelen TazUO benzeri bir fork). Veri seti ağırlıklı MUL (Map0/1, Art.mul, light.mul) + birkaç UOP (AnimationFrame, Tileart, MainMisc). Kullanıcı 7.x üzeri herhangi bir client veri sürümüne geçebileceğini belirtti → veri tarafında kısıt yok.
  Referans orijinal kurulum: `D:\Games\Electronic Arts\Ultima Online Classic`, client **7.0.112.75**, art/gump/map/sound UOP (`*LegacyMUL.uop`, `tileart.uop`, `AnimationFrame1-6.uop`). Işık verisi her iki kurulumda da `light.mul` + `lightidx.mul` (MUL) → LIGHTING_PLAN'deki `LightsLoader` varsayımı geçerli. Güncel veri setiyle test için modern upstream tabanı daha güvenli (UOP/yeni client sürümü desteği).
- **Seçilen:** (b).

## D-003 – Client verisi 7.0.112, protokol 7.0.20
- **Tarih:** 2026-10-16 · **Durum:** Kabul edildi (MIG-5 doğrulamasına bağlı)
- **Bağlam:** ClassicUO'da veri dosyaları ile sunucuya bildirilen protokol sürümü (`settings.json` → `clientversion`) ayrı. Kullanıcı 7.x üzeri her sürüme geçebiliyor; Nimloth Sphere/Source-X, ileride SphereNet.
- **Karar:**
  - **Veri:** 7.0.112.75 (diskte mevcut referans kurulum). Eski veriyi kapsayan en güncel set; UOP desteği upstream'de olgun; ileride özel art paketi istemci katmanında olacağı için MUL/UOP farkı önemsiz.
  - **Protokol:** `clientversion = 7.0.20.0`. Time of Legends öncesi, Sphere dünyasında yaygın ve oturmuş paket seti; SphereNet'in desteklemesi gereken protokol yüzeyini küçük tutar. Stygian Abyss + High Seas paketleri dahil.
- **Doğrulama (MIG-5):** 7.0.112 verisi + 7.0.20 protokolü ile Nimloth'a giriş, yürüme, eşya/gump, harita geçişi, büyü efektleri test edilir. Sorun çıkarsa geri dönüş: protokolü sunucunun şu an çalışan istemciyle kullandığı sürüme eşitle ve bu kararı güncelle.
- **Sonuç:** Görsel proje protokol sürümünden bağımsızdır; bu karar render/ışık işlerini bloklamaz.

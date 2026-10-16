# Proje: ClassicUO tabanlı geliştirilmiş UO istemcisi

## Amaç
ClassicUO fork'u üzerinde, kendi shard'ım Nimloth (Sphere/Source-X ve ileride
kendi emülatörüm SphereNet) için çok daha kaliteli görüntü sunan bir UO istemcisi.
Oynanış ve protokol uyumluluğu korunur; değişiklikler görsel katmanda yoğunlaşır.

## Taban
- Kod: upstream ClassicUO `main` (`ee79d7eb`, 2025-11-26), .NET 10, submodule'ler `external/` altında.
  Eski 2020 kopyası `legacy/2020` dalında, yalnızca referans.
- Referans UO verisi: `D:\Games\Electronic Arts\Ultima Online Classic` (7.0.112.75).
  Protokol hedefi: 7.0.20 (bkz. docs/DECISIONS.md D-003).

## Görsel hedefler (öncelik sırasıyla)
1. Dinamik ışık + gece/gündüz döngüsü
2. Yumuşak gölgeler
3. Shader efektleri (su, sis, hava durumu, bitki salınımı)
4. Akıcılık (yüksek FPS, yumuşak kamera, kademesiz zoom)
5. Post-processing (renk düzenleme, bloom)
6. İleride: yüksek çözünürlüklü/özel art paketleri (asset katmanı değiştirilebilir olmalı)

## Kurallar
- Mevcut oynanışı ve ağ protokolünü bozma. Her görsel özellik ayarlardan kapatılabilir olmalı.
- Değişiklikleri mümkün olduğunca yeni dosya/sınıflarda topla, upstream kodu az değiştir
  (fork'u upstream ile senkron tutabilmek için).
- UO data dosyaları (.mul, .uop vb.) asla repoya eklenmez.
- Dokümanlar Türkçe, kod ve kod yorumları İngilizce.
- Kod tabanını rastgele baştan sona okuma; önce docs/ altındaki haritalara bak.
- Her görev sonunda docs/PLAN.md güncellenir.

## Dokümanlar
- docs/arch/rendering.md   – render pipeline haritası
- docs/arch/network-assets.md – ağ, paket işleme, asset yükleme haritası
- docs/BUILD.md            – derleme ve çalıştırma
- docs/LIGHTING_PLAN.md    – ışık sistemi tasarımı
- docs/PLAN.md             – kilometre taşları ve görev durumu
- docs/DECISIONS.md        – alınan kararlar ve nedenleri

## İş bölümü
- Claude Code: mimari, render/shader işleri, çok dosyalı refactor'lar
- Codex: net tanımlı görevler, build/config, testler, küçük düzeltmeler
- Her iş diğer araç tarafından incelenir (çapraz review).

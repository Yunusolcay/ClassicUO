# Render Pipeline Haritası

> Kod tabanı: ClassicUO 2020-05 snapshot (`211d4b0b`), .NET Framework 4.7, C# 7.3, FNA,
> shader'lar `fx_2_0` (vs_3_0 / ps_3_0). Tüm yollar `src/` altına göredir.
> Satır numaraları bu commit için geçerlidir; kod değiştikçe kayabilir, metot adları esastır.

## 1. Bir karenin yolu (özet)

```mermaid
flowchart TD
    A[GameController.Update] -->|"_totalElapsed > 1000/FPS"| B[Scene.FixedUpdate]
    A -->|aksi halde| S[SuppressDraw + Sleep 1ms]
    B --> C[GameScene.FillGameObjectList<br/>görünür nesneleri _renderList'e diz]
    C --> D[GameController.Draw]
    D --> E[GameScene.Draw → DrawWorld]
    E --> E1[SetRenderTarget _viewportRenderTarget]
    E1 --> E2[CircleOfTransparency stencil]
    E2 --> E3["_renderList sırayla obj.Draw()<br/>(Land/Static/Item/Mobile/Effect View'ları)<br/>→ ışık kaynakları AddLight ile toplanır"]
    E3 --> E4[Weather.Draw]
    E4 --> F[GameScene.DrawLights<br/>SetRenderTarget _lightRenderTarget<br/>clear = ambient, additive light sprite'ları]
    F --> G[SetRenderTarget GameController._buffer<br/>UIManager.Draw]
    G --> H[WorldViewportGump → WorldViewport.Draw]
    H --> H1[viewport RT'yi çiz (opsiyonel xBR)]
    H1 --> H2[light RT'yi çarpımsal blend ile üstüne çiz]
    H2 --> H3[seçim kutusu, overhead yazılar, sağlık çubukları]
    G --> I[diğer gump'lar + imleç]
    I --> J[SetRenderTarget null → _buffer'ı backbuffer'a kopyala]
```

Önemli: **Çizim yalnızca FixedUpdate çalıştığı karede yapılır** (`GameController.Update`,
satır ~333–355). FPS limiti `Settings.GlobalSettings.FPS` (15–250), pasif pencerede ~5 FPS.
Render listesi her çizilen karede baştan kurulur.

## 2. Kritik sınıflar

### Ana döngü
| Sınıf / dosya | Sorumluluk | Önemli üyeler |
|---|---|---|
| `GameController` — `GameController.cs` | FNA `Game`; döngü, sahne, hue doku yükleme, final kopya | `LoadContent` (hue sampler'ları `Textures[1]`,`[2]`'ye bağlar), `Update`, `Draw`, `SetRefreshRate`, `_buffer` (tüm ekranın RT'si), `_uoSpriteBatch` |
| `Scene` — `Game/Scenes/Scene.cs` | Sahne tabanı | `Update`, `FixedUpdate`, `Draw(batcher)` |
| `GameScene` — `Game/Scenes/GameScene.cs` | Oyun dünyası sahnesi | `FixedUpdate→FillGameObjectList`, `Draw→DrawWorld`, `DrawLights`, `AddLight`, `UseLights`, `UseAltLights`, `ViewportTexture`, `LightRenderTarget`, `Scale` (zoom) |
| `GameScene` (partial) — `Game/Scenes/GameSceneDrawingSorting.cs` | Görünürlük, sıralama, RT boyutu | `GetViewPort` (offset, tile/pixel sınırları, **RT'leri oluşturur**), `AddTileToRenderList`, `AddOffsetCharacterTileToRenderList`, `UpdateMaxDrawZ` (çatı/kat kesme), foliage şeffaflığı |
| `WorldViewportGump` — `Game/UI/Gumps/WorldViewportGump.cs` | Oyun penceresi çerçevesi, boyutlandırma | `ResizeGameWindow`, `UpdateGameWindowPos` |
| `WorldViewport` — `Game/UI/Controls/WorldViewport.cs` | Dünya RT'si + ışık RT'sini ekrana birleştirir | `Draw`, `_darknessBlend`, `_altLightsBlend`, `XBREffect` |
| `UIManager` — `Game/Managers/UIManager.cs` | Tüm gump'ları tek batch'te çizer | `Draw` |

### Sahne nesneleri ve View'lar
Her `GameObject` kendi `Draw(batcher, posX, posY)`'ını uygular (`Game/GameObjects/Views/`):

| View | Notlar |
|---|---|
| `View.cs` (GameObject partial) | Varsayılan `Draw`: `DrawSprite`, alfa (`AlphaHue`), circle-of-transparency stencil, `Select` (piksel bazlı seçim). Statik `HueVector` paylaşılır. |
| `TileView.cs` (Land) | Düz land → `DrawSprite`; esnetilmiş land → `DrawSpriteLand` + 4 köşe **normal** (`Land.Normal0..3`, `Land.cs` içinde komşu Z'lerden hesaplanır) + `SHADER_LAND(_HUED)` |
| `StaticView.cs`, `ItemView.cs`, `MultiView.cs` | Art texture; `ItemData.IsLight` ise `AddLight(this,this,posX+22,posY+22)` |
| `MobileView.cs` | Katman katman animasyon kareleri; gölge `DrawSpriteShadow` (mode 12); elde ışık tutan eşya için `AddLight` |
| `AnimatedEffectView`, `MovingEffectView`, `DragEffect` (`GameObjects/`), `LightningEffectView` | Efektler; light flag'li efektler de `AddLight` çağırır |

### Batcher ve shader
| Sınıf / dosya | Sorumluluk | Önemli üyeler |
|---|---|---|
| `UltimaBatcher2D` — `Renderer/Batcher2D.cs` | Tek sprite batcher (UI dahil her şey) | `Begin(effect?)`, `End`, `DrawSprite`, `DrawSpriteLand`, `DrawSpriteShadow`, `DrawSpriteRotated`, `DrawCharacterSitted`, `Draw2D*`, `SetBlendState`, `SetStencil`, `EnableScissorTest`, `Flush`, `DefaultEffect` (`IsometricEffect`) |
| `MatrixEffect` — `Renderer/MatrixEffect.cs` | Effect tabanı; ortografik projeksiyonu viewport boyutundan kurar | `ApplyStates` |
| `IsometricWorld.fx` — `shaders/` | Tek pixel shader, `Hue.y` ile mod seçer | `PixelShader_Hue`, `get_rgb`, `get_light` |
| `xBR.fx` — `shaders/` | Opsiyonel upscale filtresi (viewport kopyasında) | |
| `ShaderHuesTraslator` — `Renderer/ShaderHuesTraslator.cs` | Hue → `Vector3(hueIndex-1, mode, alpha)` | `GetHueVector`, `SHADER_*` sabitleri |
| `UOTexture` ailesi — `Renderer/UOTexture.cs` | `UOTexture16` (Bgra5551: art, anim, light), `UOTexture32`; `Ticks` ile cache yaşlandırma | |

**Batcher davranışı:** `MAX_SPRITES = 2048`; sprite başına 4 vertex
(`Position, Normal, TexCoord, Hue` — hepsi `Vector3`). Flush; texture değişince yeni draw call,
blend/stencil/scissor değişiminde ve buffer dolunca. **Depth buffer kapalı** (`Stencil` state:
`DepthBufferEnable=false`), sıralama tamamen CPU'da (painter's algorithm).
Sampler'lar `PointClamp` (s0 = sprite, s1/s2 = hue tabloları).
`Begin(customEffect)` verildiğinde önce `DefaultEffect.ApplyStates()`, sonra custom effect uygulanır.

### Hue / renk
- `HuesLoader.CreateShaderColors` → 32×2048 iki texture (`_hues_sampler[0..1]`), her satır bir hue'nun 32 tonu.
- Vertex'teki `Hue = (hueIndex, mode, alpha)`. `alpha` aslında **şeffaflık** (`1 - Hue.z`).
- Pixel shader modları (`IsometricWorld.fx`):

| mode | Sabit | Etki |
|---|---|---|
| 0 | NONE | texture olduğu gibi |
| 1 | HUED | `get_rgb(color.r, hue)` (gri tonu hue paletinden) |
| 2 | PARTIAL_HUED | sadece gri pikseller hue'lanır |
| 3/4/5 | TEXT_HUE* | metin renklendirme |
| 6/7 | LAND / LAND_HUED | normal ile sabit yönlü (`LIGHT_DIRECTION(-1,-1,.5)`) Lambert benzeri gölgeleme |
| 10 | SPECTRAL | siyah + kırmızı kanaldan alfa |
| 12 | SHADOW | yarı saydam siyah (mobil gölgesi) |
| 13 | LIGHTS | `color.rgb *= get_rgb(color.r, hue)` (renkli ışık) |
| +20 | GUMP | gump hue varyantı |

### Asset → ışık texture'ı
- `LightsLoader` — `IO/Resources/LightsLoader.cs`: `light.mul/lightidx.mul`, en fazla 100 ID.
  8-bit yoğunluk → gri `Bgra5551` (`UOTexture16`). Alfa 1-bit.
- `LightColors.GetHue(graphic)` — `Game/Data/LightColors.cs`: graphic → hue sabit tablosu.
- `TileDataLoader.StaticData[g].IsLight / LightIndex` — tiledata'dan ışık bayrağı ve şekli.

## 3. Mevcut ışık sistemi

**Durum:** `World.Light` (`IsometricLight` — `Game/GameObjects/IsometricLight.cs`)
- `Overall` / `Personal` (efektif), `RealOverall` / `RealPersonal` (sunucudan gelen).
- UO ölçeği: 0 = tam aydınlık, 0x1E (30) = en karanlık.
- `IsometricLevel = max(Personal, 32 - Overall) / 32` → ambient gri seviyesi.
- Paketler (`Network/PacketHandlers.cs`): `0x4F LightLevel` → `RealOverall/Overall`,
  `0x4E PersonalLightLevel` → `RealPersonal/Personal` (yalnız oyuncu serial'ı). Her ikisi 0x1E ile sınırlanır.
  `Profile.UseCustomLightLevel` açıksa efektif değer `Profile.LightLevel`'dan gelir.

**Akış:**
1. `obj.Draw` sırasında ışık bayraklı nesneler `GameScene.AddLight(obj, lightObject, x, y)` çağırır.
   - Kaba **örtme testi**: `(X+1, Y+1)` tile'ında `Z+5 ≤ z < _maxZ` aralığında opak statik/multi varsa ışık eklenmez (çatı altı).
   - Işık ID'si: özel aralıklar → 2; `Item.LightID` (paket ile gelen); `ItemData.LightIndex`; mobil → 1.
   - Renk: `Profile.UseColoredLights` ise `LightColors.GetHue(graphic)+1`.
   - `_lights[]` dizisi: **kare başına en fazla 100 ışık** (`MAX_LIGHTS_DATA_INDEX_COUNT`, ID sayısıyla aynı sabit).
2. `DrawLights`: `_lightRenderTarget`'ı ambient (`IsometricLevel`, `UseDarkNights` ise −0.02) ile temizle,
   `BlendState.Additive` + mode 13 ile light.mul sprite'larını çiz.
3. `WorldViewport.Draw`: viewport RT çizildikten sonra light RT'yi
   - normal mod: `_darknessBlend` (src×dst) → **çarpımsal** karartma,
   - alternatif mod (`UseAlternativeLights`): `dst + src×dst`, alfa 0.5 → aydınlatma.
4. `UseLights` yalnızca `Personal < Overall` iken true → gündüz ışık geçişi hiç çalışmaz.

**Bilinen zayıflıklar:** sabit 8-bit LDR (overbright yok), sert kenarlı 5-bit ışık sprite'ları,
renk paleti hardcoded ve küçük, 100 ışık limiti, geçişler anlık (yumuşatma yok),
gün/gece tonu yok (sadece gri), `Personal` ile `32-Overall`'ın aynı ölçekte karşılaştırılması şüpheli.
`SetBrightlight` → shader'daki `Brightlight` parametresi **kullanılmıyor** (no-op).

## 4. Render target'lar

| RT | Sahip | Boyut | Format | Not |
|---|---|---|---|---|
| `_buffer` | `GameController` | pencere | Color | Tüm ekran; sonra backbuffer'a kopyalanır |
| `_viewportRenderTarget` | `GameScene` | `GameWindowSize × Scale` | Color + D24S8 | Dünya; zoom = RT'yi büyük/küçük üretip pencereye ölçekleyerek çizmek |
| `_lightRenderTarget` | `GameScene` | viewport RT ile aynı | Color + D24S8 | Işık haritası; depth gereksiz |

RT'ler `GetViewPort` içinde boyut değişince yeniden oluşturulur, `Unload`'da dispose edilir.
Usage `DiscardContents`.

## 5. Derinlik / çizim sırası

- `Chunk.AddGameObject` (`Game/Map/Chunk.cs`) her tile'ın bağlı listesini `PriorityZ`'ye göre sıralı tutar
  (land, statik, item, mobil için farklı Z düzeltmeleri).
- `FillGameObjectList` tile'ları **izometrik çaprazlar** boyunca (sol-üst → sağ-alt) gezer;
  `AddTileToRenderList` ekran dışını, `_maxZ` üstünü (çatı/üst kat), kış yapraklarını, iç (`IsInternal`) nesneleri eler.
- Hareketli/ofsetli nesneler (mobil, ceset, kayan item, moving effect) `AddOffsetCharacterTileToRenderList`
  ile komşu 8 tile'ı önce ekleyerek doğru örtüşmeyi sağlar.
- Çizim sırası = `_renderList` sırası. GPU depth kullanılmaz.

## 6. Genişletme noktaları (yeni render pass'leri)

| # | Nokta | Ne için uygun |
|---|---|---|
| E1 | `GameScene.DrawWorld` – `batcher.End()` ile `DrawLights` arası | Dünya üstü efektler (sis katmanı, bulut gölgesi) viewport RT'ye |
| E2 | `GameScene.DrawLights` (tamamen değiştirilebilir) | Yeni ışık haritası üretimi |
| E3 | `WorldViewport.Draw` – viewport RT kopyası + light composite | **Kompozit / post-process zinciri** (ışık birleştirme, bloom, color grading). Overhead'ler bundan sonra çizildiği için UI etkilenmez. |
| E4 | `GameSceneDrawingSorting.GetViewPort` RT oluşturma bloğu | Ek RT'ler (yarım çözünürlüklü light map, bloom zinciri) |
| E5 | `AddLight` | Işık kaynağı toplama; tanım tablosu, flicker, yarıçap |
| E6 | `IsometricWorld.fx` yeni mod (ör. 14+) | Sprite başına özel efekt (su dalgası, bitki salınımı) — **dikkat:** tek shader, herkes etkilenir |
| E7 | `UltimaBatcher2D.Begin(customEffect)` | Ayrı shader ile tam ekran pass (xBR örneği gibi) |
| E8 | `IsometricLight` | Ambient hesaplaması, gün/gece eğrisi |

Tavsiye: yeni kod `src/Game/Lighting/` (veya `src/Renderer/Lighting/`) altında yeni sınıflarda;
upstream dosyalarda yalnızca birkaç satırlık “kanca” çağrıları.

## 7. Kısıtlar ve riskler

- **Shader derleme:** `.fx` → `.fxc` `fxc.exe /T fx_2_0` ile (Windows SDK, `shaders/compile_shaders.bat`,
  yol hardcoded). `.fxc` dosyaları repoda ve `EmbeddedResource`. `.fx` değişip `.fxc` güncellenmezse değişiklik görünmez.
  FNA bunları MojoShader ile GL'e çevirir: **SM3 sınırları** (ps_3_0: ~224 sabit, döngü/dallanma kısıtlı, MRT sınırlı).
- **Depth yok:** Işık ve gölgeler 2D ekran uzayında; ışık duvar/üst kat arkasına sızabilir. Sadece kaba tile örtme testi var.
- **Tek batcher, tek effect:** Her `SetBlendState`/`SetStencil` flush tetikler; sprite başına state değişimi pahalı.
- **Vertex formatı dolu:** `Normal` yalnızca land'de anlamlı; yeni per-sprite veri için `Normal` veya `TexCoord.z` boş alanları kullanılabilir, format değişikliği tüm çizimi etkiler.
- **Zoom = RT ölçekleme:** Light RT dünya RT ile aynı boyutta; zoom out'ta (Scale 2.5) RT 2.5× büyür → fill-rate maliyeti artar.
- **Çizim FixedUpdate'e bağlı:** Zaman tabanlı animasyonlar `Time.Ticks` kullanmalı, kare sayısı değil.
- **`MAX_LIGHTS_DATA_INDEX_COUNT` çift anlamlı:** hem light.mul ID limiti hem kare başına ışık limiti.
- **Eski taban:** Bu snapshot 2020'den; upstream ClassicUO o tarihten beri render katmanını ciddi biçimde değiştirdi. Upstream senkronu hedefleniyorsa önce taban sürüm kararı verilmeli (bkz. `DECISIONS.md` D-002).
